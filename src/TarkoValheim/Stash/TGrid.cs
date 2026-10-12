using UnityEngine;

namespace TarkoValheim.Stash
{
    /// <summary>
    /// Occupancy for one Tetris grid. Holds references only; where an item
    /// lives is persisted in its m_customData (see <see cref="Loc"/>).
    /// </summary>
    internal sealed class TGrid
    {
        public readonly string Id;
        public readonly int W;
        public readonly int H;

        // Pockets: every cell is its own 1x1 pocket.
        public bool SingleCellsOnly;

        private readonly ItemDrop.ItemData[,] _cells;

        public TGrid(string id, int w, int h)
        {
            Id = id;
            W = w;
            H = h;
            _cells = new ItemDrop.ItemData[w, h];
        }

        public ItemDrop.ItemData At(int x, int y)
        {
            return x >= 0 && y >= 0 && x < W && y < H ? _cells[x, y] : null;
        }

        public bool Fits(Vector2Int size)
        {
            if (SingleCellsOnly && (size.x != 1 || size.y != 1))
            {
                return false;
            }
            return size.x <= W && size.y <= H;
        }

        public bool IsFree(int x, int y, Vector2Int size, ItemDrop.ItemData ignore = null)
        {
            if (!Fits(size) || x < 0 || y < 0 || x + size.x > W || y + size.y > H)
            {
                return false;
            }

            for (int i = x; i < x + size.x; i++)
            {
                for (int j = y; j < y + size.y; j++)
                {
                    var occupant = _cells[i, j];
                    if (occupant != null && occupant != ignore)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public void Put(ItemDrop.ItemData item, int x, int y, Vector2Int size)
        {
            for (int i = x; i < x + size.x; i++)
            {
                for (int j = y; j < y + size.y; j++)
                {
                    _cells[i, j] = item;
                }
            }
        }

        public void Remove(ItemDrop.ItemData item)
        {
            for (int i = 0; i < W; i++)
            {
                for (int j = 0; j < H; j++)
                {
                    if (_cells[i, j] == item)
                    {
                        _cells[i, j] = null;
                    }
                }
            }
        }

        public bool IsEmpty()
        {
            foreach (var cell in _cells)
            {
                if (cell != null)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>First free spot scanning rows top to bottom, trying the
        /// item's natural orientation before the rotated one.</summary>
        public bool FindSpot(ItemDrop.ItemData item, out int x, out int y, out bool rotated)
        {
            var size = ItemSizes.Of(item);
            for (int pass = 0; pass < 2; pass++)
            {
                rotated = pass == 1;
                if (rotated && size.x == size.y)
                {
                    break;
                }

                var s = rotated ? new Vector2Int(size.y, size.x) : size;
                for (y = 0; y < H; y++)
                {
                    for (x = 0; x < W; x++)
                    {
                        if (IsFree(x, y, s))
                        {
                            return true;
                        }
                    }
                }
            }

            x = y = -1;
            rotated = false;
            return false;
        }
    }
}
