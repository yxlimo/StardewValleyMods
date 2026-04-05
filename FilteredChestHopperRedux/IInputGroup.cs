using System.Collections.Generic;
using StardewValley;
using StardewValley.Objects;

namespace FilteredChestHopperRedux {
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
    private readonly Chest chest;

    public ChestWrap(Chest chest) {
      this.chest = chest;
    }

    public Chest StartChest => this.chest;

    public bool Contains(Chest chest) {
      throw new System.NotImplementedException();
    }

    public List<Item> GetItems() {
      throw new System.NotImplementedException();
    }

    public void RemoveItem(Item item, int count) {
      throw new System.NotImplementedException();
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

    public AutomateChestGroup(Context ctx, Chest startChest, GameLocation location) {
      this.ctx = ctx;
      this.startChest = startChest;
      this.location = location;
      this.chests = new List<Chest>();
      // TODO: Flood fill to find all connected chests
    }

    public Chest StartChest => this.startChest;

    public List<Chest> Chests => this.chests;

    public bool Contains(Chest chest) {
      throw new System.NotImplementedException();
    }

    public List<Item> GetItems() {
      throw new System.NotImplementedException();
    }

    public void RemoveItem(Item item, int count) {
      throw new System.NotImplementedException();
    }
  }
}
