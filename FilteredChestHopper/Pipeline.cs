using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Objects;
using System.Linq;
using System;
using StardewValley.ItemTypeDefinitions;
using StardewValley.GameData.FishPonds;
using StardewValley.Inventories;

namespace FilteredChestHopper {

  internal class HopperIOGroup {
    public Chest Hopper { get; set; }
    public List<Chest> Inputs { get; set; }
    public Chest Output { get; set; }

    public HopperIOGroup(Chest hopper, List<Chest> inputs, Chest output) {
      Hopper = hopper;
      Inputs = inputs;
      Output = output;
    }
  }
  
  internal class Pipeline {
    public List<Chest> Hoppers = [];
    internal GameLocation Location;

    public Pipeline(Chest originHopper) {
      Location = originHopper.Location;

      originHopper.modData[Mod.ModDataFlag] = "1";

      Hoppers.Add(originHopper);

      CheckSideHoppers(new Vector2(1, 0), originHopper);
      CheckSideHoppers(new Vector2(-1, 0), originHopper);

      Hoppers.Sort(new ChestLeftToRight());
    }

    //Checks adjacent hoppers for expansion
    private void CheckSideHoppers(Vector2 direction, Chest hopper) {
      //check for hopper in direction
      Chest chest = Mod.GetChestAt(Location, hopper.TileLocation + direction);
      if (chest == null || !Mod.TryGetHopper(chest, out hopper)) {
        return;
      }

      ExpandPipeline(hopper);

      CheckSideHoppers(direction, hopper);
    }

    internal void ExpandPipeline(Chest hopper) {
      //Expand Pipeline
      Hoppers.Add(hopper);
      hopper.modData[Mod.ModDataFlag] = "1";
    }

    //Attempt to output with this hopper as a filter
    public void AttemptTransfer(Mod mod) {
      // 步骤1: 收集所有输入输出宝箱
      var groups = CollectInputAndOutputChests();

      // 步骤2: 处理每个 hopper 组
      foreach (var group in groups) {
        // 步骤3: 检查过滤器和执行转移
        ProcessInputChest(mod, group);
      }
    }

    /// <summary>
    /// 收集所有输入输出宝箱，按 hopper 分组（需同时存在输入和输出）
    /// </summary>
    private List<HopperIOGroup> CollectInputAndOutputChests() {
      List<HopperIOGroup> groups = [];

      foreach (Chest hopper in Hoppers) {
        List<Chest> inputs = [];
        Chest output = null;

        // 获取上方的输入宝箱
        Chest inputChest = Mod.GetChestAt(Location, hopper.TileLocation - new Vector2(0, 1));
        if (inputChest != null) {
          inputs.Add(inputChest);
        }

        // 获取下方的输出宝箱
        Chest outputChest = Mod.GetChestAt(Location, hopper.TileLocation + new Vector2(0, 1));
        if (outputChest != null) {
          output = outputChest;
        }

        // 只有当同时存在输入和输出时才添加
        if (inputs.Count > 0 && output != null) {
          groups.Add(new HopperIOGroup(hopper, inputs, output));
        }
      }

      return groups;
    }

    /// <summary>
    /// 步骤2: 处理单个 hopper 组的输入宝箱
    /// </summary>
    private void ProcessInputChest(Mod mod, HopperIOGroup group) {
      foreach (Chest inputChest in group.Inputs) {
        inputChest.clearNulls();
        var inputChestItems = inputChest.GetItemsForPlayer(inputChest.owner.Value);

        for (int i = inputChestItems.Count - 1; i >= 0; i--) {
          var filterItems = group.Output.GetItemsForPlayer(inputChest.owner.Value);
          Item item = inputChestItems[i];
          // 步骤3: 检查过滤器并获取转移数量
          if (!ShouldTransfer(mod, item, filterItems))
            continue;
          TransferItem(mod, group, inputChestItems, i);
        }
      }
    }

    /// <summary>
    /// 步骤3: 检查过滤器并返回转移数量（匹配则返回物品栈叠数量）
    /// </summary>
    private bool ShouldTransfer(Mod mod, Item item, IInventory filterItems) {
      if (item == null)
        return false;

      filterItems.RemoveEmptySlots();

      // 检查输入物品是否匹配过滤器中的任意物品
      for (int j = filterItems.Count - 1; j >= 0; j--) {
        Item filterItem = filterItems[j];
        if (filterItem == null)
          continue;
        if (filterItem.QualifiedItemId != item.QualifiedItemId)
          continue;
        if (GetItemsFlavourID(filterItem) != GetItemsFlavourID(item))
          continue;
        if (mod.Config.CompareQuality && filterItem.Quality != item.Quality)
          continue;

        return true;
      }

      return false;
    }

    /// <summary>
    /// 步骤3a: 转移物品到目标箱
    /// </summary>
    private bool TransferItem(Mod cfg, HopperIOGroup group, IInventory chestAboveItems, int itemIndex) {
      Item item = chestAboveItems[itemIndex];
      // 创建加工产物（如葡萄酒）
      StardewValley.Object processedItem = null;
      string processedItemID = GetItemsFlavourID(item);
      if (!string.IsNullOrEmpty(processedItemID)) {
        processedItem = new StardewValley.Object(processedItemID, 1);
      }

      // 创建新物品
      Item newItem;
      if (processedItem != null) {
        ObjectDataDefinition objectDataDefinition = (ObjectDataDefinition)ItemRegistry.GetTypeDefinition(ItemRegistry.type_object);
        newItem = GetFlavoredObjectVariant(objectDataDefinition, item as StardewValley.Object, processedItem,
            ItemRegistry.ItemTypes.Find(t => t.Identifier == item.TypeDefinitionId)).CreateItem();
        newItem.Stack = item.Stack;
        newItem.Quality = item.Quality;
      }
      else {
        newItem = ItemRegistry.Create(item.QualifiedItemId, item.Stack, item.Quality);
      }

      // 尝试放入目标箱
      if (group.Output.addItem(newItem) == null) {
        chestAboveItems.RemoveAt(itemIndex);
        return true;
      }

      return false;
    }

    public class ChestLeftToRight : Comparer<Chest> {
      public override int Compare(Chest x, Chest y) {
        return x.TileLocation.X.CompareTo(y.TileLocation.X);
      }
    }

    public static string GetItemsFlavourID(Item item) {
      foreach (string contextTag in item.GetContextTags()) {
        if (contextTag.Contains("preserve_sheet_index_")) {
          return contextTag.Replace("preserve_sheet_index_", "");
        }
      }
      return null;
    }

    //Everything past this I adapted (stole) from CJB Item Spawner, thanks CJB

    /// <summary>Get flavored variants of a base item (like Blueberry Wine for Blueberry), if any.</summary>
    private FilteredItem GetFlavoredObjectVariant(ObjectDataDefinition objectDataDefinition, StardewValley.Object newItem, StardewValley.Object processedItem, IItemDataDefinition itemType) {
      string id = processedItem.ItemId;

      switch (newItem.itemId.Value) {
        case "348":
          return TryCreate(itemType.Identifier, $"348/{id}", _ => objectDataDefinition.CreateFlavoredWine(processedItem));
        case "344":
          return TryCreate(itemType.Identifier, $"344/{id}", _ => objectDataDefinition.CreateFlavoredJelly(processedItem));
        case "350":
          return TryCreate(itemType.Identifier, $"350/{id}", _ => objectDataDefinition.CreateFlavoredJuice(processedItem));
        case "342":
          return TryCreate(itemType.Identifier, $"342/{id}", _ => objectDataDefinition.CreateFlavoredPickle(processedItem));
        case "340":
          return TryCreate(itemType.Identifier, $"340/{id}", _ => objectDataDefinition.CreateFlavoredHoney(processedItem));
        case "812":
          return TryCreate(itemType.Identifier, $"812/{id}", _ => objectDataDefinition.CreateFlavoredRoe(processedItem));
        case "447":
          return TryCreate(itemType.Identifier, $"447/{id}", _ => objectDataDefinition.CreateFlavoredAgedRoe(processedItem));
      }
      return null;
    }


    /// <summary>Create a searchable item if valid.</summary>
    /// <param name="type">The item type.</param>
    /// <param name="key">The locally unique item key.</param>
    /// <param name="createItem">Create an item instance.</param>
    private static FilteredItem TryCreate(string type, string key, Func<FilteredItem, Item> createItem) {
      try {
        FilteredItem item = new FilteredItem(type, key, createItem);
        item.Item.getDescription(); // force-load item data, so it crashes here if it's invalid

        if (item.Item.Name is null or "Error Item")
          return null;

        return item;
      }
      catch {
        return null; // if some item data is invalid, just don't include it
      }
    }
  }

}
