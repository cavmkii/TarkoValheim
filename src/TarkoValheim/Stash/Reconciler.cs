using System.Collections.Generic;
using System;
using TarkoValheim.Content;
using TarkoValheim.Util;
using UnityEngine;

namespace TarkoValheim.Stash
{
    /// <summary>
    /// Brings the Tetris layout and Valheim's inventory back in line after
    /// anything changes: gives new items a place, keeps worn armor in its
    /// slot, keeps the hotbar row in step with weapon and quick slots, and
    /// hands out the one-time starter kit.
    /// </summary>
    internal static class Reconciler
    {
        private const string StarterKey = "tv_starter_kit";

        private static bool _busy;
        private static bool _loggedError;

        // Set in Player.OnSpawned. Before that the player is still loading
        // (its saved custom data may not be read yet), so nothing runs.
        private static Player _spawned;

        public static void OnSpawned(Player player)
        {
            _spawned = player;
        }

        private static bool Ready(Player player)
        {
            return player != null && player == _spawned && player == Player.m_localPlayer && !player.IsDead();
        }

        public static Loadout Current { get; private set; }

        /// <summary>Bumped after every run; the UI redraws when it changes.</summary>
        public static int Version { get; private set; }

        public static bool Active => Plugin.TetrisInventory.Value;

        public static bool Busy => _busy;

        public static void Run(Player player)
        {
            if (!Active || _busy || !Ready(player))
            {
                return;
            }

            _busy = true;
            try
            {
                GiveStarterKit(player);
                foreach (var item in player.GetInventory().GetAllItems())
                {
                    Modding.WeaponMods.EnsureBuilt(item);
                }

                var lo = Loadout.Build(player);
                bool changed = PlaceUnplaced(lo);

                lo.SyncEquipment();
                bool moved = lo.NormalizeInternal() || changed;

                Current = lo;
                Version++;

                if (moved)
                {
                    GameAccess.Changed(player.GetInventory());
                }
            }
            catch (Exception e)
            {
                if (!_loggedError)
                {
                    _loggedError = true;
                    Plugin.Log.LogError("Inventory reconcile failed: " + e);
                }
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>
        /// Finds a place for everything without one: gear and hand items first
        /// (they take slots, not grid space), then the biggest. What still
        /// doesn't fit tops up existing stacks, and failing that the grids are
        /// repacked. Only what's left after all that is UNSORTED.
        /// </summary>
        private static bool PlaceUnplaced(Loadout lo)
        {
            if (lo.Unplaced.Count == 0)
            {
                return false;
            }

            var order = new List<ItemDrop.ItemData>(lo.Unplaced);
            order.Sort((a, b) =>
            {
                int c = Loadout.PlacementRank(a).CompareTo(Loadout.PlacementRank(b));
                return c != 0 ? c : Loadout.Area(b).CompareTo(Loadout.Area(a));
            });

            bool changed = false;
            foreach (var item in order)
            {
                if (lo.FindAutoLoc(item, out var loc))
                {
                    lo.Apply(item, loc);
                    changed = true;
                }
            }

            if (lo.Unplaced.Count > 0)
            {
                changed |= lo.MergeStacks();
            }
            if (lo.Unplaced.Count > 0)
            {
                changed |= lo.Repack(false);
            }
            foreach (var item in lo.Unplaced)
            {
                Loc.Clear(item);
            }
            return changed;
        }

        /// <summary>The SORT button: lays the rig, backpack and pockets out again.</summary>
        public static bool Sort(Player player)
        {
            bool ok = false;
            Batch(player, () =>
            {
                var lo = Loadout.Build(player);
                ok = lo.Repack(true);
                if (ok)
                {
                    GameAccess.Changed(player.GetInventory());
                }
            });
            return ok;
        }

        /// <summary>Runs <paramref name="action"/> without intermediate reconciles,
        /// then reconciles once.</summary>
        public static void Batch(Player player, Action action)
        {
            bool outer = !_busy;
            _busy = true;
            try
            {
                action();
            }
            finally
            {
                if (outer)
                {
                    _busy = false;
                    Run(player);
                }
            }
        }

        public static bool CanFit(Player player, ItemDrop.ItemData item, int stack)
        {
            if (!Active || item == null || !Ready(player))
            {
                return true;
            }
            return Loadout.Build(player).CanFit(item, stack);
        }

        private static void GiveStarterKit(Player player)
        {
            var saved = GameAccess.CustomData(player);
            if (saved == null || saved.ContainsKey(StarterKey))
            {
                // No saved store means no way to remember the kit was given:
                // better no kit than a new one every login.
                return;
            }

            var inventory = player.GetInventory();
            var given = new List<KeyValuePair<string, SlotId>>
            {
                new KeyValuePair<string, SlotId>(Gear.StarterSecure, SlotId.Secure),
                new KeyValuePair<string, SlotId>(Gear.StarterRig, SlotId.Rig),
                new KeyValuePair<string, SlotId>(Gear.StarterPack, SlotId.Backpack),
            };

            var lo = Loadout.Build(player);
            foreach (var entry in given)
            {
                if (lo.Slots.ContainsKey(entry.Value))
                {
                    continue;
                }

                var item = CreateItem(entry.Key, 1);
                if (item == null)
                {
                    // Items not registered yet (main menu); try again later.
                    return;
                }

                item.m_gridPos = Loadout.FreeInternalCell(inventory, 1, new Vector2i(0, 1));
                Loc.Write(item, Loc.Slot(entry.Value));
                GameAccess.AddItemAt(inventory, item, item.m_gridPos);
                lo = Loadout.Build(player);
            }

            saved[StarterKey] = "1";
        }

        public static ItemDrop.ItemData CreateItem(string prefabName, int stack)
        {
            var prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefabName) : null;
            var drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop == null)
            {
                return null;
            }

            var item = drop.m_itemData.Clone();
            item.m_customData = new Dictionary<string, string>(drop.m_itemData.m_customData ?? new Dictionary<string, string>());
            item.m_dropPrefab = prefab;
            item.m_stack = stack;
            item.m_durability = item.GetMaxDurability();
            return item;
        }
    }
}
