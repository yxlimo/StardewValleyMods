using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Objects;
using StardewValley.Inventories;
using xTile.Dimensions;

namespace SmartFilteredHopper.LocationManager {

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
      var filterItems = this.Hopper.Items;
      string inputNames = string.Join(", ", inputItems.Select(i => i.Name));
      string filterNames = string.Join(", ", filterItems.Where(i => i != null).Select(i => i.Name));
      this.ctx.Trace($"ProcessInputChest: hopper={this.Hopper.TileLocation}, input=[{inputNames}], filter=[{filterNames}], output={this.Output.TileLocation}");

      for (int i = inputItems.Count - 1; i >= 0; i--) {
        Item item = inputItems[i];
        if (!this.shouldTransfer(item, filterItems)) {
          this.ctx.Trace($"Skip {item.Name}: not in filter");
          continue;
        }
        this.transferItem(item);
        this.ctx.Info($"Transferred {item.Name} to {this.Output.TileLocation}");
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
    private bool transferItem(Item item) {
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

      this.InputGroup.RemoveItem(item, item.Stack);
      return true;
    }

    /// <summary>
    /// 重建 InputGroup
    /// </summary>
    public void RebuildInput(Context ctx, Chest inputChest) {
      IInputGroup inputGroup;
      if (ctx.AutomateEnabled()) {
        inputGroup = new AutomateChestGroup(ctx, inputChest, this.Hopper.Location);
      }
      else {
        inputGroup = new ChestWrap(inputChest);
      }
      this.InputGroup = inputGroup;
    }
  }

  internal class Manager {
    //Applying this flag gets automate to ignore the hopper, so I hijack it
    private const string modDataFlag = "spacechase0.SuperHopper";
    public List<HopperIOGroup> IOGroups { get; }
    private readonly Context ctx;
    private GameLocation location;

    public Manager(Context ctx, GameLocation location) {
      this.ctx = ctx;
      this.location = location;
      this.IOGroups = new List<HopperIOGroup>();
    }

    public void Add(Chest hopper) {
      var (input, output) = this.findHopperConnector(hopper);
      if (input == null || output == null) {
        this.ctx.Trace($"Hopper({hopper.TileLocation}) in {this.location.Name} does not have valid input/output chests, skipping");
        return;
      }


      IInputGroup inputGroup;
      if (this.ctx.AutomateEnabled()) {
        inputGroup = new AutomateChestGroup(this.ctx, input, hopper.Location);
        this.ctx.Info($"Hopper({hopper.TileLocation}) in {this.location.Name} with Automate Mode added");
      }
      else {
        inputGroup = new ChestWrap(input);
        this.ctx.Info($"Hopper({hopper.TileLocation}) in {this.location.Name} with Normal Mode added");
      }

      hopper.modData[modDataFlag] = "1";
      this.IOGroups.Add(new HopperIOGroup(this.ctx, hopper, inputGroup, output));
      
    }

    private (Chest input, Chest output) findHopperConnector(Chest hopper) {
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
          this.ctx.Info($"Hopper({hopper.TileLocation}) in {this.location.Name} removed");
          this.IOGroups[i].Hopper.modData.Remove(modDataFlag);
          this.IOGroups.RemoveAt(i);
          return;
        }
      }
    }

    /// <summary>
    /// 重建所有 IOGroup 的 Input
    /// </summary>
    public void RebuildIOGroups() {
      for (int i = this.IOGroups.Count - 1; i >= 0; i--) {
        var group = this.IOGroups[i];
        var (input, output) = this.findHopperConnector(group.Hopper);
        if (input == null || output == null) {
          // input 或 output 不存在，移除该 Group
          group.Hopper.modData.Remove(modDataFlag);
          this.IOGroups.RemoveAt(i);
          continue;
        }
        group.RebuildInput(this.ctx, input);
      }
    }
  }

}
