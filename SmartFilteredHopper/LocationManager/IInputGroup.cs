using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Objects;

namespace SmartFilteredHopper.LocationManager {
  /// <summary>
  /// 输入组接口，支持单个 ChestWrap 或 AutomateChestGroup
  /// </summary>
  internal interface IInputGroup {
    /// <summary>
    /// 起点箱子（输入组的起始点）
    /// </summary>
    Chest StartChest { get; }

    /// <summary>
    /// 判断某个 Chest 是否在输入组中
    /// </summary>
    bool Contains(Chest chest);

    /// <summary>
    /// 从输入组中移除物品
    /// </summary>
    void RemoveItem(Item item, int count);

    /// <summary>
    /// 获取输入组中的所有物品
    /// </summary>
    List<Item> GetItems();
  }

  /// <summary>
  /// 包装单个 Chest，实现 IInputGroup 接口
  /// </summary>
  internal class ChestWrap : IInputGroup {
    private Chest chest;

    public ChestWrap(Chest chest) {
      this.chest = chest;
    }

    public Chest StartChest => this.chest;

    public bool Contains(Chest chest) {
      return this.chest == chest;
    }

    public List<Item> GetItems() {
      return this.chest.GetItemsForPlayer(this.chest.owner.Value).ToList();
    }

    public void RemoveItem(Item item, int count) {
      var items = this.chest.GetItemsForPlayer(this.chest.owner.Value);
      items.Remove(item);
    }
  }

  /// <summary>
  /// Automate 连接的一组 Chest，实现 IInputGroup 接口
  /// </summary>
  internal class AutomateChestGroup : IInputGroup {
    private readonly Context ctx;
    private readonly Chest startChest;
    private readonly GameLocation location;
    private readonly List<Chest> chests;
    private readonly List<Vector2> machines;

    public AutomateChestGroup(Context ctx, Chest startChest, GameLocation location) {
      this.ctx = ctx;
      this.startChest = startChest;
      this.location = location;
      var (foundChests, foundMachines) = this.floodFillChests(startChest, location);
      this.chests = foundChests;
      this.machines = foundMachines;

      this.logCollectionResult();
    }

    private void logCollectionResult() {
      this.ctx.Trace($"AutomateChestGroup at {this.startChest.TileLocation}: collected {this.chests.Count} chests, {this.machines.Count} machines");
      foreach (var chest in this.chests) {
        this.ctx.Trace($"  Chest at {chest.TileLocation}");
      }
      foreach (var machine in this.machines) {
        this.ctx.Trace($"  Machine at {machine}");
      }
    }

    public Chest StartChest => this.startChest;

    public List<Chest> Chests => this.chests;

    public bool Contains(Chest chest) {
      return this.chests.Contains(chest);
    }

    public List<Item> GetItems() {
      var items = new List<Item>();
      foreach (var chest in this.chests) {
        items.AddRange(chest.GetItemsForPlayer(chest.owner.Value));
      }
      return items;
    }

    public void RemoveItem(Item item, int count) {
      foreach (var chest in this.chests) {
        var chestItems = chest.GetItemsForPlayer(chest.owner.Value);
        if (chestItems.Contains(item)) {
          chestItems.Remove(item);
          return;
        }
      }
    }

    /// <summary>
    /// Flood fill 算法查找所有连接的 Chest 和 Machine
    /// </summary>
    private (List<Chest> chests, List<Vector2> machines) floodFillChests(Chest startChest, GameLocation location) {
      var visited = new HashSet<Vector2>();
      var chests = new List<Chest>();
      var machines = new List<Vector2>();
      var queue = new Queue<Vector2>();

      queue.Enqueue(startChest.TileLocation);
      visited.Add(startChest.TileLocation);

      var machineStates = this.ctx.GetAutomateMachineStates(location);

      while (queue.Count > 0) {
        var current = queue.Dequeue();

        // 检查是否是 Chest
        if (location.objects.TryGetValue(current, out var obj) && obj is Chest chest) {
          chests.Add(chest);
        }

        // 检查是否是 Machine
        if (machineStates.ContainsKey(current)) {
          machines.Add(current);
        }

        // 遍历四向邻居
        foreach (var neighbor in this.getCardinalNeighbors(current)) {
          if (visited.Contains(neighbor)) {
            continue;
          }
          visited.Add(neighbor);

          if (this.isConnectableTile(neighbor, location)) {
            queue.Enqueue(neighbor);
          }
        }
      }

      return (chests, machines);
    }

    private IEnumerable<Vector2> getCardinalNeighbors(Vector2 pos) {
      yield return new Vector2(pos.X - 1, pos.Y);
      yield return new Vector2(pos.X + 1, pos.Y);
      yield return new Vector2(pos.X, pos.Y - 1);
      yield return new Vector2(pos.X, pos.Y + 1);
    }

    private bool isConnectableTile(Vector2 tile, GameLocation location) {
      // 检查是否是 Chest (排除 hopper)
      if (location.objects.TryGetValue(tile, out var obj) && obj is Chest chest) {
        if (Utill.IsHopper(chest)) {
          return false;
        }
        return true;
      }

      // 检查是否是 Machine (使用 Automate API)
      var machineStates = this.ctx.GetAutomateMachineStates(location);
      if (machineStates.ContainsKey(tile)) {
        return true;
      }

      // 检查是否是 Flooring/Connector
      if (location.terrainFeatures.TryGetValue(tile, out var feature)
          && feature is StardewValley.TerrainFeatures.Flooring) {
        return true;
      }

      return false;
    }
  }
}
