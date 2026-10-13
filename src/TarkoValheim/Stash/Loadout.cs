using System.Collections.Generic;
using TarkoValheim.Content;
using UnityEngine;

namespace TarkoValheim.Stash
{
    internal enum SlotId
    {
        Head,
        Body,
        Legs,
        Cape,
        Belt,
        Trinket,
        Rig,
        Backpack,
        Secure,
        // Hand slots, on the hotbar. The names are kept from older versions
        // (they're in saved item data); what they're for is in the comments.
        Primary,    // weapon, key 1
        Secondary,  // weapon, key 2
        Holster,    // tool, key 4
        Melee,      // off-hand: shield or torch, key 3
        Tool2,      // tool, key 5
        Tool3       // tool, key 6
    }

    /// <summary>
    /// The player's Tetris layout, rebuilt from the items' stored locations.
    /// Valheim's own inventory still holds every carried item (so crafting,
    /// building and saving work unchanged); this decides which of them the
    /// player actually has room for and where they are drawn.
    /// </summary>
    internal sealed class Loadout
    {
        public const string Pockets = "pk";
        public const string RigGrid = "rig";
        public const string PackGrid = "bp";
        public const string SecureGrid = "sec";
        public const string ContainerGrid = "c";

        // Hotbar: hand slots on keys 1-6, quick-use bindings on 7-8.
        public const int QuickFirst = 6;
        public const int QuickLast = 7;

        public static readonly SlotId[] HandSlots =
            { SlotId.Primary, SlotId.Secondary, SlotId.Melee, SlotId.Holster, SlotId.Tool2, SlotId.Tool3 };

        public readonly Player Player;
        public readonly Inventory Inventory;
        public readonly Dictionary<SlotId, ItemDrop.ItemData> Slots = new Dictionary<SlotId, ItemDrop.ItemData>();
        public readonly Dictionary<string, TGrid> Grids = new Dictionary<string, TGrid>();
        public readonly List<ItemDrop.ItemData> Unplaced = new List<ItemDrop.ItemData>();

        private Loadout(Player player)
        {
            Player = player;
            Inventory = player.GetInventory();
        }

        public static Loadout Build(Player player)
        {
            var lo = new Loadout(player);
            var pending = new List<KeyValuePair<ItemDrop.ItemData, Loc>>();

            foreach (var item in lo.Inventory.GetAllItems())
            {
                if (!Loc.TryRead(item, out var loc))
                {
                    lo.Unplaced.Add(item);
                }
                else if (loc.IsSlot(out var slot))
                {
                    if (Accepts(slot, item) && !lo.Slots.ContainsKey(slot))
                    {
                        lo.Slots[slot] = item;
                    }
                    else
                    {
                        lo.Unplaced.Add(item);
                    }
                }
                else
                {
                    pending.Add(new KeyValuePair<ItemDrop.ItemData, Loc>(item, loc));
                }
            }

            lo.CreateGrids();

            foreach (var entry in pending)
            {
                var loc = entry.Value;
                var size = ItemSizes.Of(entry.Key, loc.Rot);
                if (lo.Grids.TryGetValue(loc.Grid, out var grid) && grid.IsFree(loc.X, loc.Y, size))
                {
                    grid.Put(entry.Key, loc.X, loc.Y, size);
                }
                else
                {
                    lo.Unplaced.Add(entry.Key);
                }
            }

            return lo;
        }

        private void CreateGrids()
        {
            Grids[Pockets] = new TGrid(Pockets, Mathf.Clamp(Plugin.PocketWidth.Value, 1, 8), Mathf.Clamp(Plugin.PocketRows.Value, 1, 4))
            {
                SingleCellsOnly = true
            };
            AddGearGrid(SlotId.Rig, RigGrid);
            AddGearGrid(SlotId.Backpack, PackGrid);
            AddGearGrid(SlotId.Secure, SecureGrid);
        }

        private void AddGearGrid(SlotId slot, string id)
        {
            if (Slots.TryGetValue(slot, out var item))
            {
                var gear = Gear.Find(item);
                if (gear != null)
                {
                    Grids[id] = new TGrid(id, gear.GridW, gear.GridH);
                }
            }
        }

        public static string GridOf(SlotId slot)
        {
            switch (slot)
            {
                case SlotId.Rig: return RigGrid;
                case SlotId.Backpack: return PackGrid;
                case SlotId.Secure: return SecureGrid;
                default: return null;
            }
        }

        public static bool Accepts(SlotId slot, ItemDrop.ItemData item)
        {
            var type = item.m_shared.m_itemType;
            var gear = Gear.Find(item);
            switch (slot)
            {
                case SlotId.Head: return type == ItemDrop.ItemData.ItemType.Helmet;
                case SlotId.Body: return type == ItemDrop.ItemData.ItemType.Chest;
                case SlotId.Legs: return type == ItemDrop.ItemData.ItemType.Legs;
                case SlotId.Cape: return type == ItemDrop.ItemData.ItemType.Shoulder;
                case SlotId.Belt: return type == ItemDrop.ItemData.ItemType.Utility;
                case SlotId.Trinket: return type == ItemDrop.ItemData.ItemType.Trinket;
                case SlotId.Rig: return gear != null && gear.Kind == GearKind.Rig;
                case SlotId.Backpack: return gear != null && gear.Kind == GearKind.Backpack;
                case SlotId.Secure: return gear != null && gear.Kind == GearKind.Secure;
                default: return IsHandItem(item);
            }
        }

        public static bool IsHandItem(ItemDrop.ItemData item)
        {
            var type = item.m_shared.m_itemType;
            return item.IsWeapon()
                || type == ItemDrop.ItemData.ItemType.Shield
                || type == ItemDrop.ItemData.ItemType.Tool
                || type == ItemDrop.ItemData.ItemType.Torch;
        }

        public static bool IsArmorSlot(SlotId slot)
        {
            return slot <= SlotId.Trinket;
        }

        public static bool ArmorSlotFor(ItemDrop.ItemData item, out SlotId slot)
        {
            for (slot = SlotId.Head; slot <= SlotId.Trinket; slot++)
            {
                if (Accepts(slot, item))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Hotbar index (0-based) a weapon slot is bound to, or -1.</summary>
        public static int HotbarIndex(SlotId slot)
        {
            switch (slot)
            {
                case SlotId.Primary: return 0;
                case SlotId.Secondary: return 1;
                case SlotId.Melee: return 2;
                case SlotId.Holster: return 3;
                case SlotId.Tool2: return 4;
                case SlotId.Tool3: return 5;
                default: return -1;
            }
        }

        public bool TryGetSlotOf(ItemDrop.ItemData item, out SlotId slot)
        {
            foreach (var kv in Slots)
            {
                if (kv.Value == item)
                {
                    slot = kv.Key;
                    return true;
                }
            }
            slot = default(SlotId);
            return false;
        }

        public bool IsPlaced(ItemDrop.ItemData item)
        {
            return !Unplaced.Contains(item);
        }

        // ------------------------------------------------------------------
        // Automatic placement (pickups, crafting results, first load)

        /// <summary>Finds where a new item would go without changing anything.</summary>
        public bool FindAutoLoc(ItemDrop.ItemData item, out Loc loc)
        {
            if (item.m_equipped && ArmorSlotFor(item, out var armor) && !Slots.ContainsKey(armor))
            {
                loc = Loc.Slot(armor);
                return true;
            }

            var gear = Gear.Find(item);
            if (gear != null)
            {
                var gearSlot = gear.Kind == GearKind.Rig ? SlotId.Rig
                    : gear.Kind == GearKind.Backpack ? SlotId.Backpack : SlotId.Secure;
                if (!Slots.ContainsKey(gearSlot))
                {
                    loc = Loc.Slot(gearSlot);
                    return true;
                }
            }

            foreach (var slot in PreferredWeaponSlots(item))
            {
                if (!Slots.ContainsKey(slot))
                {
                    loc = Loc.Slot(slot);
                    return true;
                }
            }

            foreach (var id in new[] { RigGrid, Pockets, PackGrid })
            {
                if (Grids.TryGetValue(id, out var grid) && grid.FindSpot(item, out int x, out int y, out bool rot))
                {
                    loc = new Loc { Grid = id, X = x, Y = y, Rot = rot };
                    return true;
                }
            }

            loc = default(Loc);
            return false;
        }

        /// <summary>Hammer, pickaxe, hoe, cultivator, fishing rod...</summary>
        public static bool IsTool(ItemDrop.ItemData item)
        {
            return item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Tool
                || item.m_shared.m_skillType == Skills.SkillType.Pickaxes
                || Gear.PrefabName(item) == "FishingRod";
        }

        public static bool IsOffHand(ItemDrop.ItemData item)
        {
            var type = item.m_shared.m_itemType;
            return type == ItemDrop.ItemData.ItemType.Shield || type == ItemDrop.ItemData.ItemType.Torch;
        }

        // Shields and torches go to the off-hand, tools to the tool slots,
        // weapons to the weapon slots; each spills over to the next group.
        private static IEnumerable<SlotId> PreferredWeaponSlots(ItemDrop.ItemData item)
        {
            if (!IsHandItem(item))
            {
                yield break;
            }
            if (IsOffHand(item))
            {
                yield return SlotId.Melee;
                yield return SlotId.Holster;
                yield return SlotId.Tool2;
                yield return SlotId.Tool3;
            }
            else if (IsTool(item))
            {
                yield return SlotId.Holster;
                yield return SlotId.Tool2;
                yield return SlotId.Tool3;
            }
            else
            {
                yield return SlotId.Primary;
                yield return SlotId.Secondary;
            }
        }

        /// <summary>The order in which unplaced items are given a place: gear
        /// first (it brings its own grid), then hand items (they take a slot
        /// rather than grid space), then the biggest.</summary>
        public static int PlacementRank(ItemDrop.ItemData item)
        {
            if (Gear.Find(item) != null)
            {
                return 0;
            }
            if (IsHandItem(item))
            {
                return 1;
            }
            return 2;
        }

        public static int Area(ItemDrop.ItemData item)
        {
            var s = ItemSizes.Of(item);
            return s.x * s.y;
        }

        // ------------------------------------------------------------------
        // Making room

        /// <summary>Tops up existing stacks with unplaced items of the same
        /// kind. Returns true if anything changed.</summary>
        public bool MergeStacks()
        {
            bool changed = false;
            foreach (var item in Unplaced.ToArray())
            {
                if (item.m_shared.m_maxStackSize <= 1)
                {
                    continue;
                }
                foreach (var other in Inventory.GetAllItems())
                {
                    if (other == item || !IsPlaced(other) || other.m_shared.m_name != item.m_shared.m_name
                        || other.m_quality != item.m_quality || other.m_stack >= other.m_shared.m_maxStackSize)
                    {
                        continue;
                    }
                    int n = Mathf.Min(item.m_stack, other.m_shared.m_maxStackSize - other.m_stack);
                    other.m_stack += n;
                    item.m_stack -= n;
                    changed = true;
                    if (item.m_stack <= 0)
                    {
                        break;
                    }
                }
                if (item.m_stack <= 0)
                {
                    Unplaced.Remove(item);
                    Inventory.RemoveItem(item);
                }
            }
            return changed;
        }

        /// <summary>
        /// Lays the rig, backpack and pockets out again from scratch, biggest
        /// items first, so gaps left by moving things around can be used.
        /// Only applied if everything already placed still fits, and either
        /// <paramref name="force"/> (the SORT button) or it found room for at
        /// least one unplaced item. The pouch is left alone.
        /// </summary>
        public bool Repack(bool force)
        {
            if (!force && Unplaced.Count == 0)
            {
                return false;
            }

            var ids = new List<string>();
            foreach (var id in new[] { RigGrid, PackGrid, Pockets })
            {
                if (Grids.ContainsKey(id))
                {
                    ids.Add(id);
                }
            }

            var placed = new List<ItemDrop.ItemData>();
            foreach (var id in ids)
            {
                var grid = Grids[id];
                for (int x = 0; x < grid.W; x++)
                {
                    for (int y = 0; y < grid.H; y++)
                    {
                        var item = grid.At(x, y);
                        if (item != null && !placed.Contains(item))
                        {
                            placed.Add(item);
                        }
                    }
                }
            }

            var candidates = new List<ItemDrop.ItemData>(placed);
            foreach (var item in Unplaced)
            {
                if (!Loc.IsLocked(item))
                {
                    candidates.Add(item);
                }
            }
            // Locked items keep their place: leave them out of a repack.
            foreach (var item in placed)
            {
                if (Loc.IsLocked(item))
                {
                    return false;
                }
            }

            candidates.Sort((a, b) =>
            {
                int c = Area(b).CompareTo(Area(a));
                if (c != 0) return c;
                c = string.CompareOrdinal(a.m_shared.m_name, b.m_shared.m_name);
                return c != 0 ? c : b.m_stack.CompareTo(a.m_stack);
            });

            var fresh = new Dictionary<string, TGrid>();
            foreach (var id in ids)
            {
                var g = Grids[id];
                fresh[id] = new TGrid(id, g.W, g.H) { SingleCellsOnly = g.SingleCellsOnly };
            }

            var locs = new Dictionary<ItemDrop.ItemData, Loc>();
            foreach (var item in candidates)
            {
                // Small things: pockets, rig, pack. Big things: pack, rig.
                var order = Area(item) == 1 ? new[] { Pockets, RigGrid, PackGrid } : new[] { PackGrid, RigGrid };
                foreach (var id in order)
                {
                    if (fresh.TryGetValue(id, out var grid) && grid.FindSpot(item, out int x, out int y, out bool rot))
                    {
                        grid.Put(item, x, y, ItemSizes.Of(item, rot));
                        locs[item] = new Loc { Grid = id, X = x, Y = y, Rot = rot };
                        break;
                    }
                }
            }

            int gained = 0;
            foreach (var item in Unplaced)
            {
                if (locs.ContainsKey(item))
                {
                    gained++;
                }
            }
            foreach (var item in placed)
            {
                if (!locs.ContainsKey(item))
                {
                    return false;
                }
            }
            if (!force && gained == 0)
            {
                return false;
            }

            foreach (var id in ids)
            {
                var g = Grids[id];
                Grids[id] = new TGrid(id, g.W, g.H) { SingleCellsOnly = g.SingleCellsOnly };
            }
            foreach (var kv in locs)
            {
                Apply(kv.Key, kv.Value);
            }
            return true;
        }

        /// <summary>Records a location in the model and on the item.</summary>
        public void Apply(ItemDrop.ItemData item, Loc loc)
        {
            Detach(item);
            Loc.Write(item, loc);

            if (loc.IsSlot(out var slot))
            {
                Slots[slot] = item;
                var gridId = GridOf(slot);
                var gear = Gear.Find(item);
                if (gridId != null && gear != null && !Grids.ContainsKey(gridId))
                {
                    Grids[gridId] = new TGrid(gridId, gear.GridW, gear.GridH);
                }
            }
            else if (Grids.TryGetValue(loc.Grid, out var grid))
            {
                grid.Put(item, loc.X, loc.Y, ItemSizes.Of(item, loc.Rot));
            }

            Unplaced.Remove(item);
        }

        public void Detach(ItemDrop.ItemData item)
        {
            foreach (var grid in Grids.Values)
            {
                grid.Remove(item);
            }

            if (TryGetSlotOf(item, out var slot))
            {
                Slots.Remove(slot);
            }
        }

        public bool CanFit(ItemDrop.ItemData item, int stack)
        {
            if (item.m_shared.m_maxStackSize > 1)
            {
                int room = 0;
                foreach (var other in Inventory.GetAllItems())
                {
                    if (other.m_shared.m_name == item.m_shared.m_name && other.m_quality == item.m_quality
                        && IsPlaced(other))
                    {
                        room += other.m_shared.m_maxStackSize - other.m_stack;
                    }
                }
                if (room >= stack)
                {
                    return true;
                }
            }

            return FindAutoLoc(item, out _);
        }

        // ------------------------------------------------------------------
        // Keeping Valheim's state in line with the layout

        /// <summary>
        /// Armor in an armor slot is worn; armor worn from anywhere else (for
        /// example equipped through Valheim's own "use") is moved into its slot,
        /// and whatever was there takes its place.
        /// </summary>
        public void SyncEquipment()
        {
            for (var slot = SlotId.Head; slot <= SlotId.Trinket; slot++)
            {
                Slots.TryGetValue(slot, out var slotted);

                ItemDrop.ItemData worn = null;
                foreach (var item in Inventory.GetAllItems())
                {
                    if (item.m_equipped && Accepts(slot, item))
                    {
                        worn = item;
                        break;
                    }
                }

                if (worn == slotted)
                {
                    continue;
                }

                if (worn != null)
                {
                    bool hadLoc = Loc.TryRead(worn, out var previous) && !previous.IsSlot(out _);
                    Apply(worn, Loc.Slot(slot));

                    if (slotted != null)
                    {
                        Detach(slotted);
                        var size = ItemSizes.Of(slotted, previous.Rot);
                        if (hadLoc && Grids.TryGetValue(previous.Grid, out var grid)
                            && grid.IsFree(previous.X, previous.Y, size))
                        {
                            Apply(slotted, previous);
                        }
                        else if (FindAutoLoc(slotted, out var loc))
                        {
                            Apply(slotted, loc);
                        }
                        else
                        {
                            Loc.Clear(slotted);
                            Unplaced.Add(slotted);
                        }
                    }
                }
                else if (slotted != null)
                {
                    Player.EquipItem(slotted, false);
                }
            }

            // Armor that is worn but has no slot left (the slot holds something
            // else that is also worn is impossible, so this is leftovers).
            foreach (var item in new List<ItemDrop.ItemData>(Inventory.GetAllItems()))
            {
                if (item.m_equipped && ArmorSlotFor(item, out var slot)
                    && (!Slots.TryGetValue(slot, out var s) || s != item))
                {
                    Player.UnequipItem(item, false);
                }
            }
        }

        /// <summary>
        /// Valheim's hotbar shows row 0 of the inventory. Weapon slots own
        /// cells 0-3 (keys 1-4); quick-slot bindings own 4-7 (keys 5-8);
        /// everything else is kept out of row 0. Returns true if anything moved.
        /// </summary>
        public bool NormalizeInternal()
        {
            var desired = new Dictionary<ItemDrop.ItemData, int>();
            var taken = new HashSet<int>();

            foreach (var kv in Slots)
            {
                int index = HotbarIndex(kv.Key);
                if (index >= 0)
                {
                    desired[kv.Value] = index;
                    taken.Add(index);
                }
            }

            foreach (var item in Inventory.GetAllItems())
            {
                int quick = Loc.QuickSlot(item);
                if (quick < 0)
                {
                    continue;
                }

                if (quick < QuickFirst || quick > QuickLast || taken.Contains(quick)
                    || !IsPlaced(item) || desired.ContainsKey(item))
                {
                    Loc.SetQuickSlot(item, -1);
                    continue;
                }

                desired[item] = quick;
                taken.Add(quick);
            }

            bool moved = false;
            foreach (var kv in desired)
            {
                moved |= MoveInternal(kv.Key, new Vector2i(kv.Value, 0));
            }

            foreach (var item in new List<ItemDrop.ItemData>(Inventory.GetAllItems()))
            {
                if (item.m_gridPos.y == 0 && !desired.ContainsKey(item))
                {
                    moved |= MoveInternal(item, FreeInternalCell(Inventory, 1, item.m_gridPos));
                }
            }

            return moved;
        }

        private bool MoveInternal(ItemDrop.ItemData item, Vector2i pos)
        {
            if (item.m_gridPos.x == pos.x && item.m_gridPos.y == pos.y)
            {
                return false;
            }

            var occupant = Inventory.GetItemAt(pos.x, pos.y);
            if (occupant != null && occupant != item)
            {
                occupant.m_gridPos = FreeInternalCell(Inventory, 1, occupant.m_gridPos);
            }

            item.m_gridPos = pos;
            return true;
        }

        public static Vector2i FreeInternalCell(Inventory inventory, int minRow, Vector2i fallback)
        {
            for (int y = minRow; y < inventory.GetHeight(); y++)
            {
                for (int x = 0; x < inventory.GetWidth(); x++)
                {
                    if (inventory.GetItemAt(x, y) == null)
                    {
                        return new Vector2i(x, y);
                    }
                }
            }
            return fallback;
        }
    }
}
