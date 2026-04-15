using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Objects;
using StardewValley.Inventories;
using xTile.Dimensions;

namespace SmartFilteredHopper.LocationManager {

  /// <summary>
  /// 表示 hopper 输入连接件的类型
  /// </summary>
  internal enum ConnectorType {
    None,
    Chest,
    Machine,
    Flooring
  }

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

    /// <summary>
    /// 处理单个 hopper 组的输入宝箱
    /// </summary>
    public void ProcessInputChest() {
      var inputItems = this.InputGroup.GetItems();
      var filterItems = this.Hopper.Items;
      string inputNames = string.Join(", ", inputItems.Select(i => $"{i.Name}:{i.QualifiedItemId}"));
      string filterNames = string.Join(", ", filterItems.Where(i => i != null).Select(i => $"{i.Name}:{i.QualifiedItemId}"));
      // this.ctx.Trace($"ProcessInputChest: hopper={this.Hopper.TileLocation}, input=[{inputNames}], filter=[{filterNames}], output={this.Output.TileLocation}");

      for (int i = inputItems.Count - 1; i >= 0; i--) {
        Item item = inputItems[i];
        if (!this.shouldTransfer(item, filterItems)) {
          // this.ctx.Trace($"Skip {item.Name}:{item.QualifiedItemId}: not passed in filter");
          continue;
        }
        this.transferItem(item);
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
        // 只有当物品和过滤器都是工匠物品时才比较来源
        if (this.ctx.Config.CompareArtifactSource && Utill.HasPreserveSource(item) && Utill.HasPreserveSource(filterItem)) {
          string itemSource = Utill.GetPreserveTypeID(item);
          string filterSource = Utill.GetPreserveTypeID(filterItem);
          this.ctx.Trace($"shouldTransfer: {item.Name}:{item.QualifiedItemId} CompareArtifactSource enabled, itemSource={itemSource}, filterSource={filterSource}");
          if (itemSource != filterSource) {
            continue;
          }
        }
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
      var newItem = item.getOne();
      newItem.Stack = item.Stack;
      Item remaining = this.Output.addItem(newItem);

      int transferred = remaining != null ? item.Stack - remaining.Stack : item.Stack;
      this.ctx.Info($"transferItem: {item.Name}:{item.QualifiedItemId} to output({this.Output.TileLocation}), transferred={transferred}/{item.Stack}");

      this.InputGroup.RemoveItem(item, transferred);
      return transferred > 0;
    }

    /// <summary>
    /// 重建 InputGroup
    /// </summary>
    public void RebuildInput(Context ctx, Vector2 inputPos) {
      IInputGroup inputGroup;
      if (ctx.AutomateEnabled()) {
        inputGroup = new AutomateChestGroup(ctx, inputPos, this.Hopper.Location);
      }
      else {
        Chest inputChest = Utill.GetChestAt(this.Hopper.Location, inputPos);
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
      // Skip if already added
      if (this.IOGroups.Any(g => g.Hopper == hopper)) {
        return;
      }

      // Mark hopper with modData flags immediately when scanning
      hopper.modData[modDataFlag] = "1";
      hopper.modData[Framework.HopperChestPatches.CapacityModDataKey] = this.ctx.Config.HopperCapacity.ToString();
      this.ctx.Trace($"Stamped hopper at {hopper.TileLocation} with capacity {this.ctx.Config.HopperCapacity}");

      var (inputPos, output) = this.findHopperConnector(hopper);
      if (inputPos == null || output == null) {
        this.ctx.Trace($"Hopper({hopper.TileLocation}) in {this.location.Name} does not have valid input/output chests, skipping");
        return;
      }

      IInputGroup inputGroup;
      if (this.ctx.AutomateEnabled()) {
        inputGroup = new AutomateChestGroup(this.ctx, inputPos.Value, hopper.Location);
        this.ctx.Info($"Hopper({hopper.TileLocation}) in {this.location.Name} with Automate Mode added");
      } else {
        inputGroup = new ChestWrap(Utill.GetChestAt(this.location, inputPos.Value));
        this.ctx.Info($"Hopper({hopper.TileLocation}) in {this.location.Name} with Normal Mode added");
      }

      this.IOGroups.Add(new HopperIOGroup(this.ctx, hopper, inputGroup, output));

    }

    private (Vector2? inputPos, Chest output) findHopperConnector(Chest hopper) {
      var inputTile = hopper.TileLocation - new Vector2(0, 1);
      var outputTile = hopper.TileLocation + new Vector2(0, 1);

      Chest output = Utill.GetChestAt(hopper.Location, outputTile);

      var inputType = this.getConnectorType(inputTile, hopper.Location);

      if (inputType == ConnectorType.Chest){
        return (inputTile, output);
      }

      if (this.ctx.AutomateEnabled() && (inputType == ConnectorType.Machine || inputType == ConnectorType.Flooring)) {
        return (inputTile, output);
      }

      return (null, output);
    }

    private ConnectorType getConnectorType(Vector2 tile, GameLocation location) {
      // Chest
      if (location.objects.TryGetValue(tile, out var obj) && obj is Chest chest) {
        if (Utill.IsHopper(chest)) return ConnectorType.None;
        return ConnectorType.Chest;
      }

      // Machine via Automate API
      var machineStates = this.ctx.GetAutomateMachineStates(location);
      if (machineStates.ContainsKey(tile)) return ConnectorType.Machine;

      // Flooring/Connector (only if user enabled it)
      if (this.ctx.Config.FlooringAsInput
          && location.terrainFeatures.TryGetValue(tile, out var feature)
          && feature is StardewValley.TerrainFeatures.Flooring) {
        return ConnectorType.Flooring;
      }

      return ConnectorType.None;
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
      if (this.IOGroups.Count <= 0) {
        return;
      }
      this.ctx.Info($"[Manager.RebuildIOGroups] Rebuilding {this.IOGroups.Count} groups with capacity {this.ctx.Config.HopperCapacity} in {this.location.Name}");
      for (int i = this.IOGroups.Count - 1; i >= 0; i--) {
        var group = this.IOGroups[i];
        var (inputPos, output) = this.findHopperConnector(group.Hopper);
        if (inputPos == null || output == null) {
          // input 或 output 不存在，移除该 Group
          group.Hopper.modData.Remove(modDataFlag);
          this.IOGroups.RemoveAt(i);
          continue;
        }
        // Re-stamp capacity in case config changed
        group.Hopper.modData[Framework.HopperChestPatches.CapacityModDataKey] = this.ctx.Config.HopperCapacity.ToString();
        this.ctx.Info($"[Manager.RebuildIOGroups] Re-stamped hopper at {group.Hopper.TileLocation} with capacity {this.ctx.Config.HopperCapacity}");
        group.RebuildInput(this.ctx, inputPos.Value);
      }
    }
  }

}
