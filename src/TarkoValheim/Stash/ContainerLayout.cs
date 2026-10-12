using System.Collections.Generic;
using TarkoValheim.Util;

namespace TarkoValheim.Stash
{
    /// <summary>
    /// Tetris layout for a chest, stash or grave. Valheim's grid is W x H
    /// single cells; the Tetris grid is W wide and H * factor tall, since most
    /// items now cover more than one cell.
    /// </summary>
    internal sealed class ContainerLayout
    {
        public readonly Inventory Inventory;
        public readonly TGrid Grid;
        public readonly List<ItemDrop.ItemData> Unplaced = new List<ItemDrop.ItemData>();

        private ContainerLayout(Inventory inventory)
        {
            Inventory = inventory;
            int factor = System.Math.Max(1, Plugin.ContainerHeightFactor.Value);
            Grid = new TGrid(Loadout.ContainerGrid, inventory.GetWidth(), inventory.GetHeight() * factor);
        }

        /// <summary>Builds the layout and gives unplaced items a spot.
        /// Persists any new locations through Inventory.Changed.</summary>
        public static ContainerLayout Build(Inventory inventory)
        {
            var cl = new ContainerLayout(inventory);
            bool changed = false;
            foreach (var item in inventory.GetAllItems())
            {
                changed |= Guns.GunMods.EnsureDefaultMag(item);
            }
            foreach (var item in inventory.GetAllItems())
            {
                var size = Loc.TryRead(item, out var loc) ? ItemSizes.Of(item, loc.Rot) : default(UnityEngine.Vector2Int);
                if (loc.Grid == Loadout.ContainerGrid && cl.Grid.IsFree(loc.X, loc.Y, size))
                {
                    cl.Grid.Put(item, loc.X, loc.Y, size);
                }
                else
                {
                    cl.Unplaced.Add(item);
                }
            }

            foreach (var item in cl.Unplaced.ToArray())
            {
                if (cl.Grid.FindSpot(item, out int x, out int y, out bool rot))
                {
                    var loc = new Loc { Grid = Loadout.ContainerGrid, X = x, Y = y, Rot = rot };
                    Loc.Write(item, loc);
                    cl.Grid.Put(item, x, y, ItemSizes.Of(item, rot));
                    cl.Unplaced.Remove(item);
                    changed = true;
                }
            }

            if (changed)
            {
                GameAccess.Changed(inventory);
            }
            return cl;
        }
    }
}
