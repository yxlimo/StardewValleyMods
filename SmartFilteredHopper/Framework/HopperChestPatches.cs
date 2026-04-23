using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;
using SmartFilteredHopper.LocationManager;

namespace SmartFilteredHopper.Framework;

internal static class HopperChestPatches {
	public const string CapacityModDataKey = "SmartFilteredHopper/Capacity";

	private static IMonitor _monitor = null;

	public static void Apply(Harmony harmony, IMonitor monitor) {
		_monitor = monitor;
		_monitor.Log("Applying HopperChestPatches");
		// 1. GetActualCapacity - modify return value from modData
		harmony.Patch(
			original: AccessTools.Method(typeof(Chest), nameof(Chest.GetActualCapacity)),
			postfix: new HarmonyMethod(typeof(HopperChestPatches), nameof(GetActualCapacity_Postfix))
		);

		// 2. ItemGrabMenu constructor - suppress capacity during construction
		var ctor = AccessTools.GetDeclaredConstructors(typeof(ItemGrabMenu)).FirstOrDefault(c => c.GetParameters().Length >= 18);

		if (ctor != null) {
			harmony.Patch(
				original: ctor,
				prefix: new HarmonyMethod(typeof(HopperChestPatches), nameof(ItemGrabMenuCtor_Prefix)),
				finalizer: new HarmonyMethod(typeof(HopperChestPatches), nameof(ItemGrabMenuCtor_Finalizer))
			);
		}

		// 3. Prevent grabItemFromChest from rebuilding the entire ItemGrabMenu
		harmony.Patch(
			original: AccessTools.Method(typeof(Chest), nameof(Chest.grabItemFromChest)),
			prefix: new HarmonyMethod(typeof(HopperChestPatches), nameof(GrabItemFromChest_Prefix))
		);

		// 4. Prevent grabItemFromInventory from rebuilding the entire ItemGrabMenu
		harmony.Patch(
			original: AccessTools.Method(typeof(Chest), nameof(Chest.grabItemFromInventory)),
			prefix: new HarmonyMethod(typeof(HopperChestPatches), nameof(GrabItemFromInventory_Prefix))
	);
	}

	// ── ItemGrabMenu constructor prefix / finalizer ─────────────────

	private static void ItemGrabMenuCtor_Prefix(Item sourceItem) {
		bool isOurs = sourceItem is Chest c && c.modData.ContainsKey(CapacityModDataKey);
		_monitor?.Log($"[ItemGrabMenuCtor_Prefix] sourceItem={sourceItem?.GetType().Name}, isOurs={isOurs}");
		// Don't suppress - let the menu build with actual capacity.
		// SetupBorderNeighbors should handle non-standard layouts.
	}

	private static Exception ItemGrabMenuCtor_Finalizer(
		ItemGrabMenu __instance, Exception __exception, Item sourceItem) {
		if (__exception != null) {
			return __exception;
		}
		return null;
	}

	// ── GetActualCapacity postfix ───────────────────────────────────

	private static void GetActualCapacity_Postfix(Chest __instance, ref int __result) {
		if (
			__instance.modData.TryGetValue(CapacityModDataKey, out string val)
			&& int.TryParse(val, out int cap)
			&& cap > 0
		) {
			_monitor?.Log($"[GetActualCapacity] BEFORE: chest at {__instance.TileLocation}: modData={cap}, __result={__result}");
			__result = cap;
			_monitor?.Log($"[GetActualCapacity] AFTER: chest at {__instance.TileLocation}: modData={cap}, __result={__result}");
		}
	}

	// ── grabItemFromChest / grabItemFromInventory prefixes ──────────

	/// <summary>
	/// Replaces Chest.grabItemFromChest for hopper chests.
	/// Does the same inventory work but skips ShowMenu() rebuild.
	/// </summary>
	private static bool GrabItemFromChest_Prefix(Chest __instance, Item item, Farmer who) {
		if (Game1.activeClickableMenu is not ItemGrabMenu igm) {
			return true;
		}

		var foundChest = FindHopperChest(igm);
		if (foundChest != __instance) {
			return true;
		}

		if (who.couldInventoryAcceptThisItem(item)) {
			__instance.GetItemsForPlayer().Remove(item);
			__instance.clearNulls();
		}

		return false;
	}

	/// <summary>
	/// Replaces Chest.grabItemFromInventory for hopper chests.
	/// Adds the item to the chest without calling ShowMenu().
	/// </summary>
	private static bool GrabItemFromInventory_Prefix(Chest __instance, Item item, Farmer who) {
		if (Game1.activeClickableMenu is not ItemGrabMenu igm) {
			return true;
		}

		var foundChest = FindHopperChest(igm);
		if (foundChest != __instance) {
			return true;
		}

		// Preserve snapped component across the (non-)rebuild.
		int snappedId = igm.currentlySnappedComponent?.myID ?? -1;

		// Replicate vanilla logic without the ShowMenu() call.
		if (item.Stack == 0) {
			item.Stack = 1;
		}

		Item remainder = __instance.addItem(item);
		if (remainder == null) {
			who.removeItemFromInventory(item);
		}
		else {
			remainder = who.addItemToInventory(remainder);
		}

		__instance.clearNulls();

		// Update heldItem on the existing menu.
		igm.heldItem = remainder;

		if (snappedId != -1) {
			igm.currentlySnappedComponent = igm.getComponentWithID(snappedId);
			igm.snapCursorToCurrentSnappedComponent();
		}

		return false;
	}

	// ── Helper methods ─────────────────────────────────────────────

	/// <summary>
	/// Finds the hopper chest backing the given menu, if any.
	/// </summary>
	private static Chest FindHopperChest(ItemGrabMenu menu) {
		_monitor?.Log($"[FindHopperChest] menu.sourceItem={menu.sourceItem?.GetType().Name}, menu.context={menu.context?.GetType().Name}");

		// 1. Check if sourceItem is a marked chest
		if (menu.sourceItem is Chest srcChest
			&& srcChest.modData.ContainsKey(CapacityModDataKey)) {
			_monitor?.Log($"[FindHopperChest] Found by sourceItem: {srcChest.TileLocation}");
			return srcChest;
		}

		// 2. Check if menu.context is a hopper
		if (menu.context is StardewValley.Object obj
			&& Utill.TryExtractHopper(obj, out var hopper)) {
			_monitor?.Log($"[FindHopperChest] Found by context: {hopper.TileLocation}");
			return hopper;
		}

		// 3. Fallback: search current location for a hopper whose held chest matches
		if (Game1.currentLocation == null) {
			_monitor?.Log($"[FindHopperChest] currentLocation is null");
			return null;
		}

		foreach (var locObj in Game1.currentLocation.Objects.Values) {
			if (Utill.TryExtractHopper(locObj, out var h)
				&& h.heldObject.Value is Chest chest
				&& ReferenceEquals(chest.Items, menu.ItemsToGrabMenu?.actualInventory)) {
				_monitor?.Log($"[FindHopperChest] Found by heldObject match: {h.TileLocation}");
				return chest;
			}
		}

		_monitor?.Log($"[FindHopperChest] Not found");
		return null;
	}
}
