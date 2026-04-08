using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Objects;

namespace SmartFilteredHopper.LocationManager {
  internal static class Utill {

    /// <summary>
    /// 从 chest 中移除物品，返回未成功移除的数量
    /// </summary>
    public static int RemoveItemFromChest(Chest chest, Item item, int count) {
      var items = chest.GetItemsForPlayer(chest.owner.Value);
      int removeRemaining = count;

      while (removeRemaining > 0) {
        int idx = items.IndexOf(item);
        if (idx < 0)
          return removeRemaining;
        if (removeRemaining < items[idx].Stack) {
          items[idx].Stack -= removeRemaining;
          return 0;
        }
        removeRemaining -= items[idx].Stack;
        items.RemoveAt(idx);
      }

      return removeRemaining;
    }

    /// <summary>
    /// 判断是否为有风味来源的工匠物品（酒、果酱、果汁、腌菜、蜂蜜、鱼子等）
    /// </summary>
    public static bool HasPreserveSource(Item item) {
      if (item is not StardewValley.Object obj)
        return false;
      return obj.itemId.Value switch {
        "348" => true,  // wine
        "344" => true,  // jelly
        "350" => true,  // juice
        "342" => true,  // pickle
        "340" => true,  // honey
        "812" => true,  // roe
        "447" => true,  // aged roe
        _ => false
      };
    }

    /// <summary>
    /// 获取物品的加工类型 ID（如 344=果酱，348=葡萄酒，350=果汁）
    /// </summary>
    public static string GetPreserveTypeID(Item item) {
      foreach (string contextTag in item.GetContextTags()) {
        if (contextTag.Contains("preserve_sheet_index_")) {
          return contextTag.Replace("preserve_sheet_index_", "");
        }
      }
      return null;
    }

    /// <summary>
    /// 获取物品的风味来源 ID（如 88=葡萄，613=苹果，304=草莓）
    /// 用于区分同类型不同风味的加工品（如葡萄果酱 vs 苹果果酱）
    /// </summary>
    public static string GetFlavorSourceID(Item item) {
      if (item is StardewValley.Object obj && obj.preservedParentSheetIndex.Value != null) {
        return obj.preservedParentSheetIndex.Value.ToString();
      }
      return null;
    }

    public static bool IsHopper(StardewValley.Object obj) {
      return obj is Chest { SpecialChestType: Chest.SpecialChestTypes.AutoLoader };
    }

    public static Chest ExtractHopper(StardewValley.Object obj) {
      return obj as Chest;
    }

    public static bool TryExtractHopper(StardewValley.Object obj, out Chest hopper) {
      if (IsHopper(obj)) {
        hopper = ExtractHopper(obj);

      }
      else {
        hopper = null;
      }
      return hopper != null;
    }

    public static Chest GetChestAt(GameLocation location, Vector2 position) {
      if (location.objects.TryGetValue(position, out StardewValley.Object obj) && obj != null && obj is Chest chest) {
        return chest;
      }
      return null;
    }

    public static FilteredItem GetFlavoredObjectVariant(StardewValley.Object newItem, StardewValley.Object processedItem) {
      string id = processedItem.ItemId;
      ObjectDataDefinition objectDataDefinition = (ObjectDataDefinition)ItemRegistry.GetTypeDefinition(ItemRegistry.type_object);
      IItemDataDefinition itemType = ItemRegistry.ItemTypes.Find(t => t.Identifier == newItem.TypeDefinitionId);

      switch (newItem.itemId.Value) {
        case "348":
          return tryCreateItem(itemType.Identifier, $"348/{id}", _ => objectDataDefinition.CreateFlavoredWine(processedItem));
        case "344":
          return tryCreateItem(itemType.Identifier, $"344/{id}", _ => objectDataDefinition.CreateFlavoredJelly(processedItem));
        case "350":
          return tryCreateItem(itemType.Identifier, $"350/{id}", _ => objectDataDefinition.CreateFlavoredJuice(processedItem));
        case "342":
          return tryCreateItem(itemType.Identifier, $"342/{id}", _ => objectDataDefinition.CreateFlavoredPickle(processedItem));
        case "340":
          return tryCreateItem(itemType.Identifier, $"340/{id}", _ => objectDataDefinition.CreateFlavoredHoney(processedItem));
        case "812":
          return tryCreateItem(itemType.Identifier, $"812/{id}", _ => objectDataDefinition.CreateFlavoredRoe(processedItem));
        case "447":
          return tryCreateItem(itemType.Identifier, $"447/{id}", _ => objectDataDefinition.CreateFlavoredAgedRoe(processedItem));
      }
      return null;
    }

    private static FilteredItem tryCreateItem(string type, string key, Func<FilteredItem, Item> createItem) {
      try {
        FilteredItem item = new(type, key, createItem);
        item.Item.getDescription();

        if (item.Item.Name is null or "Error Item")
          return null;

        return item;
      }
      catch {
        return null;
      }
    }
  }
}
