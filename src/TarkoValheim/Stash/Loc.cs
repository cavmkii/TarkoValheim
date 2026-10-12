using System.Globalization;

namespace TarkoValheim.Stash
{
    /// <summary>
    /// Where an item sits in the Tetris layer, stored in the item's own
    /// m_customData so it survives saving, chests, graves and other players.
    /// Format: "grid,x,y,r". Grid is pk/rig/bp/sec for the player, c for any
    /// container, or slot:Name for an equipment slot.
    /// </summary>
    internal struct Loc
    {
        private const string Key = "tv_loc";
        private const string QuickKey = "tv_quick";

        public string Grid;
        public int X;
        public int Y;
        public bool Rot;

        public static Loc Slot(SlotId slot)
        {
            return new Loc { Grid = "slot:" + slot };
        }

        public bool IsSlot(out SlotId slot)
        {
            slot = default(SlotId);
            return Grid != null && Grid.StartsWith("slot:")
                && System.Enum.TryParse(Grid.Substring(5), out slot);
        }

        public static bool TryRead(ItemDrop.ItemData item, out Loc loc)
        {
            loc = default(Loc);
            if (item?.m_customData == null || !item.m_customData.TryGetValue(Key, out var raw))
            {
                return false;
            }

            var parts = raw.Split(',');
            if (parts.Length != 4
                || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out loc.X)
                || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out loc.Y))
            {
                return false;
            }

            loc.Grid = parts[0];
            loc.Rot = parts[3] == "1";
            return true;
        }

        /// <summary>
        /// Gives the item its own m_customData before it is written to. A clone
        /// made from a prefab could otherwise share the prefab's dictionary, and
        /// every later item of that kind would inherit this one's data.
        /// </summary>
        public static void Own(ItemDrop.ItemData item)
        {
            if (item.m_customData == null)
            {
                item.m_customData = new System.Collections.Generic.Dictionary<string, string>();
                return;
            }

            var prefab = item.m_dropPrefab != null ? item.m_dropPrefab.GetComponent<ItemDrop>() : null;
            if (prefab != null && prefab.m_itemData != item && ReferenceEquals(prefab.m_itemData.m_customData, item.m_customData))
            {
                item.m_customData = new System.Collections.Generic.Dictionary<string, string>(item.m_customData);
            }
        }

        public static void Write(ItemDrop.ItemData item, Loc loc)
        {
            Own(item);
            item.m_customData[Key] = string.Join(",",
                loc.Grid,
                loc.X.ToString(CultureInfo.InvariantCulture),
                loc.Y.ToString(CultureInfo.InvariantCulture),
                loc.Rot ? "1" : "0");
        }

        public static void Clear(ItemDrop.ItemData item)
        {
            Own(item);
            item.m_customData.Remove(Key);
        }

        /// <summary>Hotbar index 4..7 (keys 5..8) the player bound this item to, or -1.</summary>
        public static int QuickSlot(ItemDrop.ItemData item)
        {
            return item.m_customData != null && item.m_customData.TryGetValue(QuickKey, out var raw)
                && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)
                ? i : -1;
        }

        public static void SetQuickSlot(ItemDrop.ItemData item, int index)
        {
            Own(item);
            if (index < 0)
            {
                item.m_customData.Remove(QuickKey);
            }
            else
            {
                item.m_customData[QuickKey] = index.ToString(CultureInfo.InvariantCulture);
            }
        }
    }
}
