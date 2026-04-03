using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Objects;
using System.Linq;
using System;
using StardewValley.ItemTypeDefinitions;
using StardewValley.GameData.FishPonds;
using StardewValley.Inventories;

namespace FilteredChestHopper
{
    internal class Pipeline
    {
        public List<Chest> Hoppers = new List<Chest>();
        //location
        internal GameLocation Location;

        public Pipeline(Chest originHopper)
        {
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
            var (inputChests, outputChests) = CollectInputAndOutputChests();

            // 步骤2: 处理每个输入宝箱
            foreach (var inputChest in inputChests) {
                // 步骤3: 检查过滤器和执行转移
                ProcessInputChest(mod, inputChest, outputChests);
            }
        }

        /// <summary>
        /// 步骤1: 收集所有输入输出宝箱
        /// </summary>
        private (List<Chest> inputChests, List<Chest[]> outputChests) CollectInputAndOutputChests() {
            List<Chest> inputChests = new List<Chest>();
            List<Chest[]> outputChests = new List<Chest[]>();

            for (int i = 0; i < Hoppers.Count; i++) {
                // 获取上方的输入宝箱
                Chest inputChest = Mod.GetChestAt(Location, Hoppers[i].TileLocation - new Vector2(0, 1));
                if (inputChest != null) {
                    inputChests.Add(inputChest);
                }

                // 获取下方的输出宝箱
                Chest outputChest = Mod.GetChestAt(Location, Hoppers[i].TileLocation + new Vector2(0, 1));
                if (outputChest != null) {
                    outputChests.Add(new Chest[] { Hoppers[i], outputChest });
                }
            }

            return (inputChests, outputChests);
        }

        /// <summary>
        /// 步骤2: 处理单个输入宝箱
        /// </summary>
        private void ProcessInputChest(Mod mod, Chest inputChest, List<Chest[]> outputChests) {
            inputChest.clearNulls();
            var chestAboveItems = inputChest.GetItemsForPlayer(inputChest.owner.Value);

            foreach (var outputChest in outputChests) {
                for (int i = chestAboveItems.Count - 1; i >= 0; i--) {
                    var filterItems = outputChest[1].GetItemsForPlayer(inputChest.owner.Value);
                    Item item = chestAboveItems[i];
                    // 步骤3: 检查过滤器并获取转移数量
                    int transferAmount = GetTransferAmount(mod, item, filterItems);
                    if (transferAmount == 0) {
                        // 未匹配过滤器，继续下一个
                        continue;
                    }

                    if (transferAmount == -1) {
                        // 全部转移模式
                        TransferAllItems(mod, inputChest, chestAboveItems, i, outputChest);
                    } else {
                        // 精确数量模式
                        TransferWithQuantityLimit(mod, inputChest, chestAboveItems, i, outputChest, item, transferAmount);
                    }
                }
            }
        }

        /// <summary>
        /// 步骤3: 检查过滤器并返回转移数量
        /// </summary>
        /// <returns>0=未转移, -1=全部转移, >0=精确数量</returns>
        private int GetTransferAmount(Mod mod, Item item, IInventory filterItems) {
            
            if (item == null)
                return 0;

            bool match = false;
            int filterCount = 0;
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

                // 匹配成功
                match = true;
                // 启用数量比较时，记录过滤器中的物品数量
                if (mod.Config.CompareQuantity) {
                    filterCount = filterItem.Stack == 1 ? 0 : filterItem.Stack;
                }
                break;
            }

            if (!match)
                return 0;

            // 返回转移数量
            if (mod.Config.CompareQuantity && filterCount > 0) {
                return filterCount;
            } else {
                return -1;
            }
        }

        /// <summary>
        /// 步骤3a: 精确数量限制的转移
        /// </summary>
        private bool TransferWithQuantityLimit(Mod mod, Chest inputChest, IInventory chestAboveItems, int itemIndex,
            Chest[] outputChest, Item item, int amountToMove) {
            int calculatedAmount = amountToMove;

            // 计算目标箱子中已存在的同类物品数量
            foreach (var existingItem in outputChest[1].GetItemsForPlayer(inputChest.owner.Value)) {
                if (existingItem != null && existingItem.canStackWith(item)) {
                    if (existingItem.Stack == 0) {
                        calculatedAmount--;
                    }
                    calculatedAmount -= existingItem.Stack;
                }
            }

            // 如果目标箱已满，跳过
            if (calculatedAmount < 1)
                return false;

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
            } else {
                newItem = ItemRegistry.Create(item.QualifiedItemId, item.Stack, item.Quality);
            }

            // 限制转移数量
            if (newItem.Stack > calculatedAmount) {
                newItem.Stack = calculatedAmount;
            }

            // 尝试放入目标箱
            if (outputChest[1].addItem(newItem) == null) {
                // 清理原箱子中的物品
                if (item.Stack == newItem.Stack) {
                    chestAboveItems.RemoveAt(itemIndex);
                } else if (newItem.Stack == 0) {
                    item.Stack--;
                } else {
                    item.Stack -= newItem.Stack;
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// 步骤3b: 全部转移模式
        /// </summary>
        private bool TransferAllItems(Mod mod, Chest inputChest, IInventory chestAboveItems, int itemIndex, Chest[] outputChest) {
            Item item = chestAboveItems[itemIndex];
            if (item == null)
                return false;
            return TransferWithQuantityLimit(mod, inputChest, chestAboveItems, itemIndex, outputChest, item, item.Stack);
        }

        public class ChestLeftToRight : Comparer<Chest> {
            public override int Compare(Chest x, Chest y) {
                return x.TileLocation.X.CompareTo(y.TileLocation.X);
            }
        }

        public string GetItemsFlavourID(Item item) {
            foreach (string contextTag in item.GetContextTags())
            {
                if(contextTag.Contains("preserve_sheet_index_"))
                {
                    return contextTag.Replace("preserve_sheet_index_", ""); 
                }
            }
            return null;
        }

        //Everything past this I adapted (stole) from CJB Item Spawner, thanks CJB

        /// <summary>Get flavored variants of a base item (like Blueberry Wine for Blueberry), if any.</summary>
        private FilteredItem GetFlavoredObjectVariant(ObjectDataDefinition objectDataDefinition, StardewValley.Object newItem, StardewValley.Object processedItem, IItemDataDefinition itemType) {
            string id = processedItem.ItemId;

            switch(newItem.itemId.Value) {
                case "348":
                    return this.TryCreate(itemType.Identifier, $"348/{id}", _ => objectDataDefinition.CreateFlavoredWine(processedItem));
                case "344":
                    return this.TryCreate(itemType.Identifier, $"344/{id}", _ => objectDataDefinition.CreateFlavoredJelly(processedItem));
                case "350":
                    return this.TryCreate(itemType.Identifier, $"350/{id}", _ => objectDataDefinition.CreateFlavoredJuice(processedItem));
                case "342":
                    return this.TryCreate(itemType.Identifier, $"342/{id}", _ => objectDataDefinition.CreateFlavoredPickle(processedItem));
                case "340":
                    return this.TryCreate(itemType.Identifier, $"340/{id}", _ => objectDataDefinition.CreateFlavoredHoney(processedItem));
                case "812":
                    return this.TryCreate(itemType.Identifier, $"812/{id}", _ => objectDataDefinition.CreateFlavoredRoe(processedItem));
                case "447":
                    return this.TryCreate(itemType.Identifier, $"447/{id}", _ => objectDataDefinition.CreateFlavoredAgedRoe(processedItem));
            }
            return null;
        }


        /// <summary>Create a searchable item if valid.</summary>
        /// <param name="type">The item type.</param>
        /// <param name="key">The locally unique item key.</param>
        /// <param name="createItem">Create an item instance.</param>
        private FilteredItem TryCreate(string type, string key, Func<FilteredItem, Item> createItem) {
            try {
                FilteredItem item = new FilteredItem(type, key, createItem);
                item.Item.getDescription(); // force-load item data, so it crashes here if it's invalid

                if (item.Item.Name is null or "Error Item")
                    return null;

                return item;
            } catch {
                return null; // if some item data is invalid, just don't include it
            }
        }
    }
}
