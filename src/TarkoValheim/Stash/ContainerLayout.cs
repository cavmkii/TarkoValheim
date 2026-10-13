using System.Collections.Generic;
using TarkoValheim.Util;

namespace TarkoValheim.Stash
{
    /// <summary>
    /// Tetris layout for a chest, stash or grave: the same size as its real
    /// inventory, which HideoutStash.Enlarge has already grown to the chest's
    /// rows x ContainerHeightFactor. One real cell per Tetris cell means a
    /// chest is never "full" while its grid still shows space.
    /// </summary>
    internal sealed class ContainerLayout
    {
        public readonly Inventory Inventory;
        public readonly TGrid Grid;
        public readonly List<ItemDrop.ItemData> Unplaced = new List<ItemDrop.ItemData>();

        private ContainerLayout(Inventory inventory)
        {
            Inventory = inventory;
            Grid = new TGrid(Loadout.ContainerGrid, inventory.GetWidth(), inventory.GetHeight());
        }

        /// <summary>Builds the layout and gives unplaced items a spot.
        /// Persists any new locations through Inventory.Changed.</summary>
        public static ContainerLayout Build(Inventory inventory)
        {
            var cl = new ContainerLayout(inventory);
            bool changed = false;
            foreach (var item in inventory.GetAllItems())
            {
                changed |= Modding.WeaponMods.EnsureBuilt(item);
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
