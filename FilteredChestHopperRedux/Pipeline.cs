using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Objects;
using System.Linq;
using System;
using StardewValley.ItemTypeDefinitions;
using StardewValley.GameData.FishPonds;
using StardewValley.Inventories;

namespace FilteredChestHopperRedux {

  internal class HopperIOGroup {
    public Chest Hopper { get; set; }
    public List<Chest> Inputs { get; set; }
    public Chest Output { get; set; }
    private readonly Context ctx;

    public HopperIOGroup(Context ctx, Chest hopper, List<Chest> inputs, Chest output) {
      this.ctx = ctx;
      this.Hopper = hopper;
      this.Inputs = inputs;
      this.Output = output;
    }

    /// <summary>
    /// 处理单个 hopper 组的输入宝箱
    /// </summary>
    public void ProcessInputChest() {
      foreach (Chest inputChest in this.Inputs) {
        inputChest.clearNulls();
        var inputItems = inputChest.GetItemsForPlayer(inputChest.owner.Value);

        for (int i = inputItems.Count - 1; i >= 0; i--) {
          var filterItems = this.Hopper.GetItemsForPlayer(inputChest.owner.Value);
          Item item = inputItems[i];
          if (!this.shouldTransfer(item, filterItems)) {
            this.ctx.Trace($"Skipping item {item.Name} - filter mismatch");
            continue;
          }
          this.transferItem(inputItems, i);
          this.ctx.Trace($"Transferring item {item.Name} x{item.Stack}");
        }
      }
    }

    /// <summary>
    /// 检查过滤器
    /// </summary>
    private bool shouldTransfer(Item item, IInventory filterItems) {
      if (item == null)
        return false;

      filterItems.RemoveEmptySlots();

      for (int j = filterItems.Count - 1; j >= 0; j--) {
        Item filterItem = filterItems[j];
        if (filterItem == null)
          continue;
        if (filterItem.QualifiedItemId != item.QualifiedItemId)
          continue;
        if (Utill.GetItemsFlavourID(filterItem) != Utill.GetItemsFlavourID(item))
          continue;
        if (this.ctx.Config.CompareQuality && filterItem.Quality != item.Quality)
          continue;

        return true;
      }

      return false;
    }

    /// <summary>
    /// 转移物品到目标箱
    /// </summary>
    private bool transferItem(IInventory chestAboveItems, int itemIndex) {
      Item item = chestAboveItems[itemIndex];
      string processedItemID = Utill.GetItemsFlavourID(item);
      Item newItem;

      if (!string.IsNullOrEmpty(processedItemID)) {
        StardewValley.Object processedItem = new(processedItemID, 1);
        newItem = Utill.GetFlavoredObjectVariant(item as StardewValley.Object, processedItem).CreateItem();
        newItem.Stack = item.Stack;
        newItem.Quality = item.Quality;
      } else {
        newItem = ItemRegistry.Create(item.QualifiedItemId, item.Stack, item.Quality);
      }

      if (this.Output.addItem(newItem) != null) {
        return false;
      }

      chestAboveItems.RemoveAt(itemIndex);
      return true;
    }
  }

  internal class Pipeline {
    public List<Chest> Hoppers = new List<Chest>();
    internal GameLocation Location;
    private readonly Context ctx;

    public Pipeline(Context ctx, Chest originHopper) {
      this.Location = originHopper.Location;
      this.ctx = ctx;

      originHopper.modData[Mod.ModDataFlag] = "1";

      this.Hoppers.Add(originHopper);

      this.checkSideHoppers(new Vector2(1, 0), originHopper);
      this.checkSideHoppers(new Vector2(-1, 0), originHopper);

      this.Hoppers.Sort(new ChestLeftToRight());

      this.ctx.Trace($"Pipeline created with {this.Hoppers.Count} hoppers at {this.Location.Name}");
    }

    private void checkSideHoppers(Vector2 direction, Chest hopper) {
      Chest chest = Mod.GetChestAt(this.Location, hopper.TileLocation + direction);
      if (chest == null || !Mod.TryGetHopper(chest, out hopper)) {
        return;
      }

      this.ExpandPipeline(hopper);

      this.checkSideHoppers(direction, hopper);
    }

    internal void ExpandPipeline(Chest hopper) {
      this.Hoppers.Add(hopper);
      hopper.modData[Mod.ModDataFlag] = "1";
    }

    public void AttemptTransfer() {
      var groups = this.collectInputAndOutputChests();
      this.ctx.Trace($"Attempting transfer for {groups.Count} groups");

      foreach (var group in groups) {
        group.ProcessInputChest();
      }
    }

    private List<HopperIOGroup> collectInputAndOutputChests() {
      List<HopperIOGroup> groups = new List<HopperIOGroup>();

      foreach (Chest hopper in this.Hoppers) {
        List<Chest> inputs = new List<Chest>();
        Chest output = null;

        Chest inputChest = Mod.GetChestAt(this.Location, hopper.TileLocation - new Vector2(0, 1));
        if (inputChest != null) {
          inputs.Add(inputChest);
        }

        Chest outputChest = Mod.GetChestAt(this.Location, hopper.TileLocation + new Vector2(0, 1));
        if (outputChest != null) {
          output = outputChest;
        }

        if (inputs.Count > 0 && output != null) {
          this.ctx.Trace($"Found group: hopper={hopper.TileLocation}, inputs={inputs.Count}, output={output.TileLocation}");
          groups.Add(new HopperIOGroup(this.ctx, hopper, inputs, output));
        }
      }

      return groups;
    }

    public class ChestLeftToRight : Comparer<Chest> {
      public override int Compare(Chest x, Chest y) {
        return x.TileLocation.X.CompareTo(y.TileLocation.X);
      }
    }

  }

}
