using TarkoValheim.Content;
using TarkoValheim.Util;
using UnityEngine;

namespace TarkoValheim.Stash
{
    /// <summary>A drop destination: an inventory plus a Tetris location in it.</summary>
    internal struct Dest
    {
        public Inventory Inventory;
        public Loc Loc;
    }

    /// <summary>
    /// Every item move the Tetris UI can make. Moves inside one inventory only
    /// rewrite the stored location; moves between inventories go through
    /// Inventory.MoveItemToThis so containers sync over the network as usual.
    /// </summary>
    internal static class StashOps
    {
        public static string LastError;

        public static bool IsPlayerInventory(Inventory inventory)
        {
            return Player.m_localPlayer != null && inventory == Player.m_localPlayer.GetInventory();
        }

        /// <summary>Checks a move without performing it.</summary>
        public static bool CanMove(ItemDrop.ItemData item, Inventory from, Dest dest, int amount)
        {
            return Resolve(item, from, dest, amount, out _, out _);
        }

        public static bool Move(ItemDrop.ItemData item, Inventory from, Dest dest, int amount)
        {
            var player = Player.m_localPlayer;
            if (player == null || !Resolve(item, from, dest, amount, out var stackTarget, out var swapOut))
            {
                return false;
            }

            bool ok = false;
            Reconciler.Batch(player, () => ok = Perform(player, item, from, dest, amount, stackTarget, swapOut));
            return ok;
        }

        private static bool Resolve(ItemDrop.ItemData item, Inventory from, Dest dest, int amount,
            out ItemDrop.ItemData stackTarget, out ItemDrop.ItemData swapOut)
        {
            stackTarget = null;
            swapOut = null;
            LastError = null;
            var player = Player.m_localPlayer;
            if (player == null || item == null || dest.Inventory == null || amount < 1 || amount > item.m_stack)
            {
                return false;
            }

            bool split = amount < item.m_stack;
            bool fromPlayer = IsPlayerInventory(from);
            bool toPlayer = IsPlayerInventory(dest.Inventory);

            // Leaving an equipment slot.
            var current = fromPlayer ? Loadout.Build(player) : null;
            if (current != null && current.TryGetSlotOf(item, out var fromSlot)
                && !(dest.Inventory == from && dest.Loc.Grid == Loc.Slot(fromSlot).Grid))
            {
                var ownGrid = Loadout.GridOf(fromSlot);
                if (ownGrid != null)
                {
                    if (fromSlot == SlotId.Secure)
                    {
                        return Fail("$tv_err_secure_fixed");
                    }
                    if (current.Grids.TryGetValue(ownGrid, out var g) && !g.IsEmpty())
                    {
                        return Fail("$tv_err_not_empty");
                    }
                    if (toPlayer && dest.Loc.Grid == ownGrid)
                    {
                        return Fail(null);
                    }
                }
            }

            if (toPlayer)
            {
                var lo = Loadout.Build(player);
                if (dest.Loc.IsSlot(out var slot))
                {
                    if (!Loadout.Accepts(slot, item))
                    {
                        return Fail(null);
                    }
                    if (split && item.m_shared.m_maxStackSize > 1)
                    {
                        return Fail(null);
                    }
                    if (lo.Slots.TryGetValue(slot, out var occupant) && occupant != item)
                    {
                        // A secure container can be swapped for another one
                        // while it's empty: that's how you upgrade it.
                        if (slot == SlotId.Secure && lo.Grids.TryGetValue(Loadout.SecureGrid, out var sg) && sg.IsEmpty())
                        {
                            swapOut = occupant;
                            return true;
                        }
                        return Fail(null);
                    }
                    return true;
                }

                if (!lo.Grids.TryGetValue(dest.Loc.Grid, out var grid))
                {
                    return Fail(null);
                }
                return CheckGrid(grid, item, from == dest.Inventory, dest.Loc, split, out stackTarget);
            }

            var cl = ContainerLayout.Build(dest.Inventory);
            if (dest.Loc.Grid != Loadout.ContainerGrid)
            {
                return Fail(null);
            }
            if (!CheckGrid(cl.Grid, item, from == dest.Inventory, dest.Loc, split, out stackTarget))
            {
                return false;
            }

            // Valheim's own grid has to have a free cell too.
            if (stackTarget == null && (from != dest.Inventory || split) && !HasFreeInternalCell(dest.Inventory))
            {
                return Fail("$tv_err_full");
            }
            return true;
        }

        private static bool CheckGrid(TGrid grid, ItemDrop.ItemData item, bool sameInventory, Loc loc, bool split,
            out ItemDrop.ItemData stackTarget)
        {
            stackTarget = null;
            var under = grid.At(loc.X, loc.Y);
            if (under != null && under != item && CanStack(under, item))
            {
                stackTarget = under;
                return true;
            }

            var ignore = sameInventory && !split ? item : null;
            return grid.IsFree(loc.X, loc.Y, ItemSizes.Of(item, loc.Rot), ignore) || Fail(null);
        }

        public static bool CanStack(ItemDrop.ItemData target, ItemDrop.ItemData item)
        {
            return target.m_shared.m_name == item.m_shared.m_name
                && target.m_quality == item.m_quality
                && target.m_shared.m_maxStackSize > 1
                && target.m_stack < target.m_shared.m_maxStackSize;
        }

        private static bool Perform(Player player, ItemDrop.ItemData item, Inventory from, Dest dest, int amount,
            ItemDrop.ItemData stackTarget, ItemDrop.ItemData swapOut)
        {
            var to = dest.Inventory;
            bool toSlot = dest.Loc.IsSlot(out _);

            // Anything leaving the player, or going from a slot into a grid,
            // comes out of the player's hands or off their body.
            if (item.m_equipped && (to != from || !toSlot))
            {
                player.UnequipItem(item, false);
            }

            if (swapOut != null)
            {
                // Secure container upgrade: the old one goes where the new one was.
                Loc.TryRead(item, out var oldLoc);
                if (from == to)
                {
                    Loc.Write(swapOut, oldLoc);
                }
                else
                {
                    MoveAcross(player.GetInventory(), swapOut, from, oldLoc, 1);
                }
            }

            if (stackTarget != null)
            {
                int n = Mathf.Min(amount, stackTarget.m_shared.m_maxStackSize - stackTarget.m_stack);
                if (from == to)
                {
                    stackTarget.m_stack += n;
                    item.m_stack -= n;
                    if (item.m_stack <= 0)
                    {
                        from.RemoveItem(item);
                    }
                    GameAccess.Changed(from);
                }
                else
                {
                    to.MoveItemToThis(from, item, n, stackTarget.m_gridPos.x, stackTarget.m_gridPos.y);
                    GameAccess.Changed(to);
                }
                return true;
            }

            if (from == to && amount == item.m_stack)
            {
                Loc.Write(item, dest.Loc);
                GameAccess.Changed(from);
                return true;
            }

            if (from == to)
            {
                var pos = Loadout.FreeInternalCell(to, IsPlayerInventory(to) ? 1 : 0, new Vector2i(-1, -1));
                if (pos.x < 0)
                {
                    return Fail("$tv_err_full");
                }

                var piece = item.Clone();
                piece.m_customData = new System.Collections.Generic.Dictionary<string, string>(item.m_customData);
                piece.m_stack = amount;
                piece.m_customData.Remove("tv_quick");
                Loc.Write(piece, dest.Loc);
                if (!GameAccess.AddItemAt(to, piece, pos))
                {
                    return Fail("$tv_err_full");
                }
                item.m_stack -= amount;
                GameAccess.Changed(to);
                return true;
            }

            return MoveAcross(from, item, to, dest.Loc, amount);
        }

        private static bool MoveAcross(Inventory from, ItemDrop.ItemData item, Inventory to, Loc loc, int amount)
        {
            var pos = Loadout.FreeInternalCell(to, IsPlayerInventory(to) ? 1 : 0, new Vector2i(-1, -1));
            if (pos.x < 0)
            {
                return Fail("$tv_err_full");
            }

            to.MoveItemToThis(from, item, amount, pos.x, pos.y);
            var moved = to.GetItemAt(pos.x, pos.y);
            if (moved == null)
            {
                return Fail("$tv_err_full");
            }

            // A split leaves two items; make sure they don't share one
            // custom-data dictionary, or moving one would move both.
            if (moved != item && ReferenceEquals(moved.m_customData, item.m_customData))
            {
                moved.m_customData = new System.Collections.Generic.Dictionary<string, string>(item.m_customData);
            }

            Loc.Write(moved, loc);
            Loc.SetQuickSlot(moved, -1);
            GameAccess.Changed(to);
            GameAccess.Changed(from);
            return true;
        }

        private static bool HasFreeInternalCell(Inventory inventory)
        {
            return Loadout.FreeInternalCell(inventory, 0, new Vector2i(-1, -1)).x >= 0;
        }

        // ------------------------------------------------------------------
        // Shortcuts

        /// <summary>Ctrl+click: send to the open container, or back to the player.</summary>
        public static bool QuickMove(ItemDrop.ItemData item, Inventory from, Inventory container)
        {
            var player = Player.m_localPlayer;
            if (player == null)
            {
                return false;
            }

            if (IsPlayerInventory(from))
            {
                if (container == null)
                {
                    return false;
                }

                var cl = ContainerLayout.Build(container);
                if (!cl.Grid.FindSpot(item, out int x, out int y, out bool rot))
                {
                    return Fail("$tv_err_full");
                }
                var loc = new Loc { Grid = Loadout.ContainerGrid, X = x, Y = y, Rot = rot };
                return Move(item, from, new Dest { Inventory = container, Loc = loc }, item.m_stack);
            }

            var lo = Loadout.Build(player);
            if (!lo.FindAutoLoc(item, out var dest))
            {
                return Fail("$tv_err_no_room");
            }
            return Move(item, from, new Dest { Inventory = player.GetInventory(), Loc = dest }, item.m_stack);
        }

        public static void TakeAll(Inventory container)
        {
            string error = null;
            foreach (var item in new System.Collections.Generic.List<ItemDrop.ItemData>(container.GetAllItems()))
            {
                if (!QuickMove(item, container, null))
                {
                    error = LastError ?? error;
                }
            }
            LastError = error;
        }

        public static void Drop(ItemDrop.ItemData item, Inventory from, int amount)
        {
            var player = Player.m_localPlayer;
            if (player == null)
            {
                return;
            }

            if (IsPlayerInventory(from) && Reconciler.Current != null
                && Reconciler.Current.TryGetSlotOf(item, out var slot) && Loadout.GridOf(slot) != null)
            {
                if (slot == SlotId.Secure)
                {
                    Fail("$tv_err_secure_fixed");
                    return;
                }
                if (Reconciler.Current.Grids.TryGetValue(Loadout.GridOf(slot), out var g) && !g.IsEmpty())
                {
                    Fail("$tv_err_not_empty");
                    return;
                }
            }

            Loc.Clear(item);
            Loc.SetQuickSlot(item, -1);
            player.DropItem(from, item, amount);
        }

        public static void BindQuickSlot(ItemDrop.ItemData item, int index)
        {
            var player = Player.m_localPlayer;
            if (player == null)
            {
                return;
            }

            foreach (var other in player.GetInventory().GetAllItems())
            {
                if (Loc.QuickSlot(other) == index)
                {
                    Loc.SetQuickSlot(other, -1);
                }
            }
            Loc.SetQuickSlot(item, index);
            Reconciler.Run(player);
        }

        private static bool Fail(string token)
        {
            if (token != null)
            {
                LastError = token;
            }
            return false;
        }
    }
}
