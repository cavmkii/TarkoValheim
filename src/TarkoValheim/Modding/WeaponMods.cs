using System.Collections.Generic;
using System.Globalization;
using TarkoValheim.Content;
using TarkoValheim.Stash;
using TarkoValheim.Util;
using UnityEngine;

namespace TarkoValheim.Modding
{
    /// <summary>An installed part and the level it was at when installed.</summary>
    internal struct Installed
    {
        public PartSpec Part;
        public int Level;
    }

    /// <summary>What a weapon's parts add up to. Fractions: +0.2 Damage is 20% more.</summary>
    internal sealed class PartTotals
    {
        public float Damage;
        public float Durability;
        public float Stamina;
        public float Block;
        public float Parry;
        public float Spirit;
        public float Fire;

        /// <summary>Parts fitted.</summary>
        public int Count;

        /// <summary>Anything to apply: the weapon is built from parts.</summary>
        public bool Active;

        /// <summary>Differs from its standard build.</summary>
        public bool Modified;

        /// <summary>Its head (blade, limbs...) is missing: it barely works.</summary>
        public bool MissingHead;
    }

    /// <summary>
    /// Installing and removing parts on vanilla weapons. Every weapon with a
    /// standard build (see <see cref="WeaponRecipes"/>) is made of parts:
    /// crafted from them, or, if it came from somewhere else (loot, a save
    /// from before this mod), given its standard parts at level 1 the first
    /// time it's seen. Stats count against the standard build.
    /// An installed part
    /// stops being an item: "id:level" is stored on the weapon
    /// (m_customData "tv_part_Head" etc.), so it travels with the weapon into
    /// chests and graves. Taking it off gives it back at the same level.
    /// A weapon with no parts is exactly the vanilla weapon.
    /// </summary>
    internal static class WeaponMods
    {
        private const string Prefix = "tv_part_";
        private const string BuiltKey = "tv_part_built";

        // Missing parts: no head and the weapon barely works; no handle and
        // it's awkward and fragile.
        private const float NoHeadDamage = -0.75f;
        private const float NoHandleStamina = 0.3f;
        private const float NoHandleDurability = -0.3f;

        /// <summary>Fits standard parts to a weapon seen for the first time.
        /// Returns true if it changed anything.</summary>
        public static bool EnsureBuilt(ItemDrop.ItemData item)
        {
            if (item?.m_customData == null || item.m_customData.ContainsKey(BuiltKey) || !IsModular(item))
            {
                return false;
            }
            var stock = WeaponRecipes.StockFor(Gear.PrefabName(item));
            if (stock == null)
            {
                return false;
            }

            Loc.Own(item);
            foreach (var kv in stock)
            {
                if (!item.m_customData.ContainsKey(Prefix + kv.Key))
                {
                    item.m_customData[Prefix + kv.Key] = kv.Value.Id + ":1";
                }
            }
            item.m_customData[BuiltKey] = "1";
            return true;
        }

        /// <summary>A weapon just crafted: the parts it was made from.</summary>
        public static void SetBuilt(ItemDrop.ItemData item, List<Installed> parts)
        {
            Loc.Own(item);
            foreach (var p in parts)
            {
                item.m_customData[Prefix + p.Part.Slot] = p.Part.Id + ":" + p.Level.ToString(CultureInfo.InvariantCulture);
            }
            item.m_customData[BuiltKey] = "1";
        }

        public static WeaponFamily FamilyOf(ItemDrop.ItemData item)
        {
            if (item?.m_shared == null || WeaponParts.Find(Gear.PrefabName(item)) != null)
            {
                return WeaponFamily.None;
            }
            if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield)
            {
                return WeaponFamily.Shield;
            }
            if (!item.IsWeapon())
            {
                return WeaponFamily.None;
            }
            switch (item.m_shared.m_skillType)
            {
                case Skills.SkillType.Swords: return WeaponFamily.Sword;
                case Skills.SkillType.Knives: return WeaponFamily.Knife;
                case Skills.SkillType.Axes: return WeaponFamily.Axe;
                case Skills.SkillType.Clubs: return WeaponFamily.Club;
                case Skills.SkillType.Spears: return WeaponFamily.Spear;
                case Skills.SkillType.Polearms: return WeaponFamily.Polearm;
                case Skills.SkillType.Bows: return WeaponFamily.Bow;
                case Skills.SkillType.Crossbows: return WeaponFamily.Crossbow;
                default: return WeaponFamily.None;
            }
        }

        public static bool IsModular(ItemDrop.ItemData item) => FamilyOf(item) != WeaponFamily.None;

        public static bool TryGet(ItemDrop.ItemData item, PartSlot slot, out Installed installed)
        {
            installed = default(Installed);
            if (item?.m_customData == null || !item.m_customData.TryGetValue(Prefix + slot, out var raw))
            {
                return false;
            }
            var bits = raw.Split(':');
            var part = WeaponParts.Find(bits[0]);
            if (part == null)
            {
                return false;
            }
            int level = 1;
            if (bits.Length > 1)
            {
                int.TryParse(bits[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out level);
            }
            installed = new Installed { Part = part, Level = Mathf.Clamp(level, 1, WeaponParts.MaxLevel) };
            return true;
        }

        public static List<Installed> AllInstalled(ItemDrop.ItemData item)
        {
            var list = new List<Installed>();
            foreach (PartSlot slot in System.Enum.GetValues(typeof(PartSlot)))
            {
                if (TryGet(item, slot, out var installed))
                {
                    list.Add(installed);
                }
            }
            return list;
        }

        // Cached per weapon by its custom-data contents: the stat patches run
        // every frame for every equipped weapon.
        private static readonly Dictionary<ItemDrop.ItemData, KeyValuePair<int, PartTotals>> Cache =
            new Dictionary<ItemDrop.ItemData, KeyValuePair<int, PartTotals>>();

        private static readonly PartTotals None = new PartTotals();

        public static PartTotals Totals(ItemDrop.ItemData item)
        {
            if (item?.m_customData == null || item.m_customData.Count == 0)
            {
                return None;
            }

            int sig = 17;
            bool any = false;
            foreach (var kv in item.m_customData)
            {
                if (kv.Key.StartsWith(Prefix))
                {
                    sig = sig * 31 + kv.Key.GetHashCode() * 7 + (kv.Value ?? "").GetHashCode();
                    any = true;
                }
            }
            if (!any)
            {
                return None;
            }
            if (Cache.TryGetValue(item, out var cached) && cached.Key == sig)
            {
                return cached.Value;
            }

            var family = FamilyOf(item);
            var stock = WeaponRecipes.StockFor(Gear.PrefabName(item));
            var t = new PartTotals { Active = true };
            foreach (var slot in WeaponParts.SlotsOf(family))
            {
                bool has = TryGet(item, slot, out var inst) && WeaponParts.Fits(inst.Part, family);
                PartSpec standard = null;
                stock?.TryGetValue(slot, out standard);

                if (has)
                {
                    Add(t, inst.Part, inst.Level, 1f);
                    t.Count++;
                }
                if (standard != null)
                {
                    // Count against the standard part at level 1.
                    Add(t, standard, 1, -1f);
                    if (!has || inst.Part != standard || inst.Level > 1)
                    {
                        t.Modified = true;
                    }
                    if (!has && slot == PartSlot.Head)
                    {
                        t.Damage += NoHeadDamage;
                        t.MissingHead = true;
                    }
                    if (!has && slot == PartSlot.Handle)
                    {
                        t.Stamina += NoHandleStamina;
                        t.Durability += NoHandleDurability;
                    }
                }
                else if (has)
                {
                    t.Modified = true;
                }
            }

            if (Cache.Count > 512)
            {
                Cache.Clear();
            }
            Cache[item] = new KeyValuePair<int, PartTotals>(sig, t);
            return t;
        }

        private static void Add(PartTotals t, PartSpec p, int l, float sign)
        {
            t.Damage += sign * p.At(p.Damage, p.DamageStep, l);
            t.Durability += sign * p.At(p.Durability, p.DurabilityStep, l);
            t.Stamina += sign * p.At(p.Stamina, p.StaminaStep, l);
            t.Block += sign * p.At(p.Block, p.BlockStep, l);
            t.Parry += sign * p.At(p.Parry, p.ParryStep, l);
            t.Spirit += sign * (p.Spirit > 0f ? p.At(p.Spirit, p.SpiritStep, l) : 0f);
            t.Fire += sign * (p.Fire > 0f ? p.At(p.Fire, p.FireStep, l) : 0f);
        }

        /// <summary>Puts a part from <paramref name="from"/> onto the weapon,
        /// giving back whatever was in that slot.</summary>
        public static bool Install(Player player, ItemDrop.ItemData weapon, Inventory weaponInventory,
            ItemDrop.ItemData partItem, Inventory from, out string error)
        {
            error = null;
            var family = FamilyOf(weapon);
            var part = WeaponParts.Find(Gear.PrefabName(partItem));
            if (family == WeaponFamily.None || part == null)
            {
                return false;
            }
            if (!WeaponParts.Fits(part, family))
            {
                error = "$tv_err_part_doesnt_fit";
                return false;
            }
            if (Loc.IsLocked(weapon))
            {
                error = "$tv_err_locked";
                return false;
            }

            int level = Mathf.Clamp(partItem.m_quality, 1, WeaponParts.MaxLevel);
            bool hadOld = TryGet(weapon, part.Slot, out var old);

            from.RemoveItem(partItem, 1);
            Loc.Own(weapon);
            weapon.m_customData[Prefix + part.Slot] = part.Id + ":" + level.ToString(CultureInfo.InvariantCulture);

            if (hadOld)
            {
                GiveBack(player, old);
            }

            GameAccess.Changed(from);
            if (weaponInventory != from)
            {
                GameAccess.Changed(weaponInventory);
            }
            return true;
        }

        public static void Remove(Player player, ItemDrop.ItemData weapon, Inventory weaponInventory, PartSlot slot)
        {
            if (!TryGet(weapon, slot, out var installed))
            {
                return;
            }
            Loc.Own(weapon);
            weapon.m_customData.Remove(Prefix + slot);
            GiveBack(player, installed);
            GameAccess.Changed(weaponInventory);
        }

        private static void GiveBack(Player player, Installed installed)
        {
            var item = Reconciler.CreateItem(installed.Part.Id, 1);
            if (item == null || player == null)
            {
                return;
            }
            item.m_quality = installed.Level;

            if (Reconciler.CanFit(player, item, 1))
            {
                player.GetInventory().AddItem(item);
            }
            else
            {
                var pos = player.transform.position + player.transform.forward * 0.6f + Vector3.up;
                ItemDrop.DropItem(item, 1, pos, Quaternion.identity);
            }
        }
    }
}
