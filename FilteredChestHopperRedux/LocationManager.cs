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
    public IInputGroup InputGroup { get; set; }
    public Chest Output { get; set; }
    private readonly Context ctx;

    public HopperIOGroup(Context ctx, Chest hopper, IInputGroup inputGroup, Chest output) {
      this.ctx = ctx;
      this.Hopper = hopper;
      this.InputGroup = inputGroup;
      this.Output = output;
    }

    public bool ContainsChest(Chest chest) {
      return this.InputGroup?.Contains(chest) ?? false;
    }

    public bool IsStartChest(Chest chest) {
      return this.InputGroup?.StartChest == chest;
    }

    /// <summary>
    /// 处理单个 hopper 组的输入宝箱
    /// </summary>
    public void ProcessInputChest() {
      var inputItems = this.InputGroup.GetItems();

      for (int i = inputItems.Count - 1; i >= 0; i--) {
        var filterItems = this.Hopper.GetItemsForPlayer(this.InputGroup.StartChest.owner.Value);
        Item item = inputItems[i];
        if (!this.shouldTransfer(item, filterItems)) {
          this.ctx.Trace($"Skipping item {item.Name} - filter mismatch");
          continue;
        }
        this.transferItem(item, i);
        this.ctx.Trace($"Transferring item {item.Name} x{item.Stack}");
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
    private bool transferItem(Item item, int itemIndex) {
      string processedItemID = Utill.GetItemsFlavourID(item);
      Item newItem;

      if (!string.IsNullOrEmpty(processedItemID)) {
        StardewValley.Object processedItem = new(processedItemID, 1);
        newItem = Utill.GetFlavoredObjectVariant(item as StardewValley.Object, processedItem).CreateItem();
        newItem.Stack = item.Stack;
        newItem.Quality = item.Quality;
      }
      else {
        newItem = ItemRegistry.Create(item.QualifiedItemId, item.Stack, item.Quality);
      }

      if (this.Output.addItem(newItem) != null) {
        return false;
      }

      this.InputGroup.RemoveItem(item, item.Stack);
      return true;
    }
  }

  internal class LocationManager {
    public List<HopperIOGroup> IOGroups { get; }
    private readonly Context ctx;

    public LocationManager(Context ctx) {
      this.ctx = ctx;
      this.IOGroups = new List<HopperIOGroup>();
    }

    public void Add(Chest hopper) {
      if (!this.IsHopperConnected(hopper)) {
        return;
      }

      var chests = this.GetInputOutputChests(hopper);

      IInputGroup inputGroup;
      if (this.ctx.Config.GrabAutomateChestGroup) {
        inputGroup = new AutomateChestGroup(this.ctx, chests.input, hopper.Location);
      } else {
        inputGroup = new ChestWrap(chests.input);
      }

      hopper.modData[ModEntry.ModDataFlag] = "1";
      this.IOGroups.Add(new HopperIOGroup(this.ctx, hopper, inputGroup, chests.output));
    }

    private bool IsHopperConnected(Chest hopper) {
      var chests = this.GetInputOutputChests(hopper);
      return chests.input != null && chests.output != null;
    }

    private (Chest input, Chest output) GetInputOutputChests(Chest hopper) {
      Chest inputChest = Utill.GetChestAt(hopper.Location, hopper.TileLocation - new Vector2(0, 1));
      Chest outputChest = Utill.GetChestAt(hopper.Location, hopper.TileLocation + new Vector2(0, 1));
      return (inputChest, outputChest);
    }

    public void AttemptTransfer() {
      foreach (var group in this.IOGroups) {
        group.ProcessInputChest();
      }
    }

    public void RemoveGroupByHopper(Chest hopper) {
      for (int i = this.IOGroups.Count - 1; i >= 0; i--) {
        if (this.IOGroups[i].Hopper == hopper) {
          this.IOGroups[i].Hopper.modData.Remove(ModEntry.ModDataFlag);
          this.IOGroups.RemoveAt(i);
          return;
        }
      }
    }
  }

}
