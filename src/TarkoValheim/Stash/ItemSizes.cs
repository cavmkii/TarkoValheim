using System.Collections.Generic;
using System;
using TarkoValheim.Content;
using UnityEngine;

namespace TarkoValheim.Stash
{
    /// <summary>
    /// How many cells an item covers, unrotated (width x height). Mod items
    /// carry their own size; everything else goes by Valheim item type, with
    /// a few prefab-name exceptions and a config override on top.
    /// </summary>
    internal static class ItemSizes
    {
        private static readonly Dictionary<string, Vector2Int> Named = new Dictionary<string, Vector2Int>
        {
            { "Hammer", new Vector2Int(1, 3) },
            { "Hoe", new Vector2Int(1, 3) },
            { "Cultivator", new Vector2Int(1, 3) },
            { "FishingRod", new Vector2Int(1, 4) },
            { "PickaxeAntler", new Vector2Int(2, 3) },
            { "PickaxeBronze", new Vector2Int(2, 3) },
            { "PickaxeIron", new Vector2Int(2, 3) },
            { "PickaxeBlackMetal", new Vector2Int(2, 3) },
        };

        private static string _overrideSource;
        private static Dictionary<string, Vector2Int> _overrides = new Dictionary<string, Vector2Int>();

        public static Vector2Int Of(ItemDrop.ItemData item)
        {
            var name = Gear.PrefabName(item);
            if (name != null)
            {
                var overrides = Overrides();
                if (overrides.TryGetValue(name, out var o))
                {
                    return o;
                }

                var gun = Firearms.FindGun(name);
                if (gun != null)
                {
                    return new Vector2Int(gun.SizeW, gun.SizeH);
                }

                var gear = Gear.Find(item);
                if (gear != null)
                {
                    return new Vector2Int(gear.SizeW, gear.SizeH);
                }

                var part = GunParts.Find(name);
                if (part != null)
                {
                    return new Vector2Int(part.SizeW, part.SizeH);
                }

                if (Named.TryGetValue(name, out var n))
                {
                    return n;
                }
            }

            switch (item.m_shared.m_itemType)
            {
                case ItemDrop.ItemData.ItemType.Helmet:
                case ItemDrop.ItemData.ItemType.Shoulder:
                    return new Vector2Int(2, 2);
                case ItemDrop.ItemData.ItemType.Chest:
                    return new Vector2Int(3, 3);
                case ItemDrop.ItemData.ItemType.Legs:
                    return new Vector2Int(2, 3);
                case ItemDrop.ItemData.ItemType.Utility:
                case ItemDrop.ItemData.ItemType.Torch:
                    return new Vector2Int(1, 2);
                case ItemDrop.ItemData.ItemType.Shield:
                    return new Vector2Int(2, 3);
                case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                    return new Vector2Int(1, 3);
                case ItemDrop.ItemData.ItemType.Bow:
                    return new Vector2Int(1, 4);
                case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
                    return new Vector2Int(2, 4);
                case ItemDrop.ItemData.ItemType.Tool:
                    return new Vector2Int(2, 3);
                default:
                    return Vector2Int.one;
            }
        }

        public static Vector2Int Of(ItemDrop.ItemData item, bool rotated)
        {
            var s = Of(item);
            return rotated ? new Vector2Int(s.y, s.x) : s;
        }

        // Format: "Wood=1x1, SwordIron=1x4". Re-parsed when the config changes.
        private static Dictionary<string, Vector2Int> Overrides()
        {
            var source = Plugin.SizeOverrides?.Value ?? "";
            if (source == _overrideSource)
            {
                return _overrides;
            }

            var parsed = new Dictionary<string, Vector2Int>();
            foreach (var part in source.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=');
                if (kv.Length != 2)
                {
                    continue;
                }

                var dims = kv[1].Trim().ToLowerInvariant().Split('x');
                if (dims.Length == 2 && int.TryParse(dims[0], out int w) && int.TryParse(dims[1], out int h)
                    && w > 0 && h > 0 && w <= 10 && h <= 10)
                {
                    parsed[kv[0].Trim()] = new Vector2Int(w, h);
                }
            }

            _overrideSource = source;
            _overrides = parsed;
            return parsed;
        }
    }
}
