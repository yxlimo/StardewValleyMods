using System;
using StardewModdingAPI;
using StardewValley;
using StardewValley.ItemTypeDefinitions;

namespace FilteredChestHopperRedux {
  internal static class Utill {

    public static string GetItemsFlavourID(Item item) {
      foreach (string contextTag in item.GetContextTags()) {
        if (contextTag.Contains("preserve_sheet_index_")) {
          return contextTag.Replace("preserve_sheet_index_", "");
        }
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
