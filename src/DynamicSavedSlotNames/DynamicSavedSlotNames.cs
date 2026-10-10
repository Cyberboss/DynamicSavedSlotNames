using System;
using System.Reflection;

using FrooxEngine;

using HarmonyLib;

using ResoniteModLoader;

using static FrooxEngine.ItemHelper;

namespace DynamicSavedSlotNames
{
	public sealed class DynamicSavedSlotNames : ResoniteMod
	{
		internal const string NameConstant = nameof(DynamicSavedSlotNames);

		internal const string Description = "A resonite mod that allows the name of saved slots that are changed using the `OnSaving` Protoflux node to persist to the inventory rather than using their old name.";

		internal const string VersionConstant = "1.0.0";

		public override string Name => NameConstant;

		public override string Author => "Dominion";

		public override string Version => VersionConstant;

		public override string Link => $"https://github.com/Cyberboss/{NameConstant}";

		[AutoRegisterConfigKey]
		private static readonly ModConfigurationKey<bool> Enabled = new ModConfigurationKey<bool>("Enabled", "Mod Enabled", () => true);

		public override void OnEngineInit()
		{
			GetConfiguration()!.Save(true);

			Debug("Running harmony patches");
			Harmony harmony = new($"net.dextraspace.{NameConstant}");
			harmony.PatchAll();
		}

		[HarmonyPatch]
		class ItemHelper_SaveItemInternal_Patch
		{
			public static MethodBase TargetMethod()
			{
				var originalMethod = AccessTools.Method(typeof(ItemHelper), "SaveItemInternal");

				return AccessTools.AsyncMoveNext(originalMethod);
			}

			public static void Postfix(object __instance)
			{
				if (!Enabled.Value)
					return;

				// Use reflection on '__instance' if you need to read local state variables
				var asyncStateMachineType = __instance.GetType();

				var savedItemField = AccessTools.Field(asyncStateMachineType, "<item>5__2");
				if (savedItemField?.FieldType != typeof(SavedItem))
				{
					Error($"Could not find {nameof(SavedItem)} field in state machine. The method has likely changed and this mod needs a patch!");
					return;
				}

				var savedItem = (SavedItem?)savedItemField.GetValue(__instance);
				var savedItemName = savedItem?.Name;
				if (savedItemName == null)
					// not populated yet
					return;

				var itemSlotParameterField = AccessTools.Field(asyncStateMachineType, "itemSlot");
				if (itemSlotParameterField?.FieldType != typeof(Slot))
				{
					Error($"Could not find input {nameof(Slot)} (itemSlot) field in state machine. The method has likely changed and this mod needs a patch!");
					return;
				}

				var saveRootParameterField = AccessTools.Field(asyncStateMachineType, "saveRoot");
				if (saveRootParameterField?.FieldType != typeof(bool))
				{
					Error($"Could not find input {nameof(Boolean)} (saveRoot) field in state machine. The method has likely changed and this mod needs a patch!");
					return;
				}

				var itemSlot = (Slot)itemSlotParameterField.GetValue(__instance)!;
				var saveRoot = (bool)saveRootParameterField.GetValue(__instance)!;

				var namedSlot = (!saveRoot && itemSlot.ChildrenCount != 0) ? itemSlot[0] : itemSlot;

				var currentSlotName = namedSlot.Name;
				if (savedItemName != currentSlotName)
				{
					Msg($"Slot being saved ({namedSlot.ReferenceID}) has has its' name changed from \"{savedItemName}\" to \"{currentSlotName}\" on save. Updating {nameof(SavedItem)}.{nameof(SavedItem.Name)}");
					savedItem!.Name = currentSlotName;
				}
			}
		}
	}
};
