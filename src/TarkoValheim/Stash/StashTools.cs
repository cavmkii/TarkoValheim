using System.Collections.Generic;
using TarkoValheim.Content;
using TarkoValheim.Util;
using UnityEngine;

namespace TarkoValheim.Stash
{
    /// <summary>
    /// Hideout stash buttons. SORT STASH merges part stacks and lays each
    /// chest out again by kind; TRANSFER ALL moves everything you carry loose
    /// (pockets, rig, backpack, unsorted) into the stash, onto matching
    /// stacks first. Worn and held gear, the pouch, locked items and items
    /// bound to a quick-use key stay with you.
    /// </summary>
    internal static class StashTools
    {
        // ------------------------------------------------------------------
        // Sort

        public static int SortStash(Player player, List<Container> containers)
        {
            HideoutStash.Claim(containers);
            int sorted = 0;
            foreach (var c in containers)
            {
                var inventory = c.GetInventory();
                if (inventory == null)
                {
                    continue;
                }
                MergeStacks(inventory);
                if (Relayout(inventory))
                {
                    sorted++;
                }
            }
            return sorted;
        }

        // Fills part stacks from later ones of the same kind.
        private static void MergeStacks(Inventory inventory)
        {
            var items = new List<ItemDrop.ItemData>(inventory.GetAllItems());
            bool changed = false;
            for (int i = 0; i < items.Count; i++)
            {
                var a = items[i];
                if (a.m_stack <= 0 || a.m_shared.m_maxStackSize <= 1 || Loc.IsLocked(a))
                {
                    continue;
                }
                for (int j = i + 1; j < items.Count && a.m_stack < a.m_shared.m_maxStackSize; j++)
                {
                    var b = items[j];
                    if (b.m_stack <= 0 || Loc.IsLocked(b) || !StashOps.CanStack(a, b))
                    {
                        continue;
                    }
                    int n = Mathf.Min(b.m_stack, a.m_shared.m_maxStackSize - a.m_stack);
                    a.m_stack += n;
                    b.m_stack -= n;
                    changed = true;
                }
            }
            foreach (var item in items)
            {
                if (item.m_stack <= 0)
                {
                    inventory.RemoveItem(item);
                }
            }
            if (changed)
            {
                GameAccess.Changed(inventory);
            }
        }

        // Kind, then name, then biggest first; locked items keep their place.
        private static bool Relayout(Inventory inventory)
        {
            var layout = ContainerLayout.Build(inventory);
            var grid = new TGrid(layout.Grid.Id, layout.Grid.W, layout.Grid.H);
            var items = new List<ItemDrop.ItemData>();
            foreach (var item in inventory.GetAllItems())
            {
                if (Loc.IsLocked(item) && Loc.TryRead(item, out var keep) && keep.Grid == Loadout.ContainerGrid)
                {
                    grid.Put(item, keep.X, keep.Y, ItemSizes.Of(item, keep.Rot));
                }
                else
                {
                    items.Add(item);
                }
            }

            items.Sort((a, b) =>
            {
                int c = Kind(a).CompareTo(Kind(b));
                if (c != 0) return c;
                c = string.CompareOrdinal(a.m_shared.m_name, b.m_shared.m_name);
                if (c != 0) return c;
                c = b.m_quality.CompareTo(a.m_quality);
                return c != 0 ? c : b.m_stack.CompareTo(a.m_stack);
            });

            var locs = new Dictionary<ItemDrop.ItemData, Loc>();
            foreach (var item in items)
            {
                if (!grid.FindSpot(item, out int x, out int y, out bool rot))
                {
                    return false; // Wouldn't all fit this way: leave it as it is.
                }
                grid.Put(item, x, y, ItemSizes.Of(item, rot));
                locs[item] = new Loc { Grid = Loadout.ContainerGrid, X = x, Y = y, Rot = rot };
            }
            foreach (var kv in locs)
            {
                Loc.Write(kv.Key, kv.Value);
            }
            GameAccess.Changed(inventory);
            return true;
        }

        private static int Kind(ItemDrop.ItemData item)
        {
            var type = item.m_shared.m_itemType;
            if (Loadout.IsHandItem(item)) return 0;
            if (Loadout.ArmorSlotFor(item, out _)) return 1;
            if (Gear.Find(item) != null) return 2;
            if (WeaponParts.Find(Gear.PrefabName(item)) != null) return 3;
            if (type == ItemDrop.ItemData.ItemType.Ammo || type == ItemDrop.ItemData.ItemType.AmmoNonEquipable) return 4;
            if (type == ItemDrop.ItemData.ItemType.Consumable) return 5;
            if (type == ItemDrop.ItemData.ItemType.Trophy) return 7;
            return 6;
        }

        // ------------------------------------------------------------------
        // Transfer all

        /// <summary>Moves loose carried items into the stash. Returns (moved,
        /// left behind for lack of room).</summary>
        public static KeyValuePair<int, int> TransferAll(Player player, List<Container> containers)
        {
            int moved = 0, left = 0;
            if (containers.Count == 0)
            {
                return new KeyValuePair<int, int>(0, 0);
            }
            HideoutStash.Claim(containers);
            var inventory = player.GetInventory();

            Reconciler.Batch(player, () =>
            {
                var lo = Loadout.Build(player);
                var loose = new List<ItemDrop.ItemData>();
                foreach (var item in inventory.GetAllItems())
                {
                    if (Loc.IsLocked(item) || item.m_equipped || Loc.QuickSlot(item) >= 0 || lo.TryGetSlotOf(item, out _))
                    {
                        continue;
                    }
                    // The pouch keeps its contents.
                    if (Loc.TryRead(item, out var loc) && loc.Grid == Loadout.SecureGrid)
                    {
                        continue;
                    }
                    loose.Add(item);
                }

                foreach (var item in loose)
                {
                    if (Send(item, inventory, containers))
                    {
                        moved++;
                    }
                    else
                    {
                        left++;
                    }
                }
            });
            return new KeyValuePair<int, int>(moved, left);
        }

        // Onto matching stacks first, anywhere they'll fit after that.
        private static bool Send(ItemDrop.ItemData item, Inventory from, List<Container> containers)
        {
            if (item.m_shared.m_maxStackSize > 1)
            {
                foreach (var c in containers)
                {
                    var to = c.GetInventory();
                    foreach (var other in new List<ItemDrop.ItemData>(to.GetAllItems()))
                    {
                        if (!from.GetAllItems().Contains(item) || item.m_stack <= 0)
                        {
                            return true;
                        }
                        if (other == item || !StashOps.CanStack(other, item) || !Loc.TryRead(other, out var at))
                        {
                            continue;
                        }
                        StashOps.Move(item, from, new Dest { Inventory = to, Loc = at }, item.m_stack);
                    }
                }
                if (!from.GetAllItems().Contains(item))
                {
                    return true;
                }
            }

            foreach (var c in containers)
            {
                var to = c.GetInventory();
                var layout = ContainerLayout.Build(to);
                if (layout.Grid.FindSpot(item, out int x, out int y, out bool rot))
                {
                    var dest = new Dest { Inventory = to, Loc = new Loc { Grid = Loadout.ContainerGrid, X = x, Y = y, Rot = rot } };
                    if (StashOps.Move(item, from, dest, item.m_stack))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
