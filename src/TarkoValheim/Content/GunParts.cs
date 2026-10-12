using System.Collections.Generic;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;

namespace TarkoValheim.Content
{
    internal enum PartSlot
    {
        Barrel,
        Handguard,
        Stock,
        Muzzle,
        Sight,
        Magazine
    }

    /// <summary>
    /// A weapon part. Modifiers are fractions applied to the gun's base
    /// stats: +0.08 Damage is 8% more damage, -0.12 Recoil is 12% less kick.
    /// </summary>
    internal sealed class PartSpec
    {
        public string Id;
        public string Base;
        public PartSlot Slot;
        public int Tier;
        public string Label;
        public int SizeW = 1;
        public int SizeH = 1;
        public float Weight = 0.5f;

        public float Damage;
        public float Velocity;
        public float Spread;
        public float AdsSpread;
        public float Recoil;
        public float Reload;
        public float MagSize;

        // Sights only: replace the gun's sight picture and zoom.
        public SightType? Sight;
        public float AdsFov;

        // Null fits every class the slot is open on.
        public GunClass[] Fits;

        public RequirementConfig[] Requirements;

        public bool FitsGun(GunSpec gun)
        {
            if (!GunParts.SlotOpen(gun, Slot))
            {
                return false;
            }
            if (Fits == null)
            {
                return true;
            }
            foreach (var c in Fits)
            {
                if (c == gun.Class)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// The parts catalogue. Barrels, handguards and stocks come in five
    /// materials (bronze, iron, silver, black metal, flametal); each tier up
    /// improves the same stats further. Muzzles, sights and magazines are
    /// single items with trade-offs.
    /// </summary>
    internal static class GunParts
    {
        private static readonly string[] Metals = { "Bronze", "Iron", "Silver", "BlackMetal", "FlametalNew" };
        private static readonly string[] MetalNames = { "bronze", "iron", "silver", "blackmetal", "flametal" };

        internal static readonly List<PartSpec> All = Build();

        public static bool SlotOpen(GunSpec gun, PartSlot slot)
        {
            switch (slot)
            {
                case PartSlot.Stock:
                case PartSlot.Sight:
                    return gun.Class != GunClass.Handgun;
                case PartSlot.Magazine:
                    return !gun.PerRoundReload;
                case PartSlot.Handguard:
                    return gun.Class != GunClass.Handgun;
                default:
                    return true;
            }
        }

        public static IEnumerable<PartSlot> OpenSlots(GunSpec gun)
        {
            foreach (PartSlot slot in System.Enum.GetValues(typeof(PartSlot)))
            {
                if (SlotOpen(gun, slot))
                {
                    yield return slot;
                }
            }
        }

        public static PartSpec Find(string prefabName)
        {
            if (prefabName == null)
            {
                return null;
            }
            foreach (var part in All)
            {
                if (part.Id == prefabName)
                {
                    return part;
                }
            }
            return null;
        }

        private static List<PartSpec> Build()
        {
            var list = new List<PartSpec>();
            for (int i = 0; i < Metals.Length; i++)
            {
                int t = i + 1;
                string metal = Metals[i];
                string name = MetalNames[i];

                list.Add(new PartSpec
                {
                    Id = "TV_Barrel_" + name, Base = metal, Slot = PartSlot.Barrel, Tier = t, Label = "BRL",
                    SizeW = 2, Damage = 0.04f * t, Velocity = 0.05f * t, Spread = -0.05f * t, Weight = 1f,
                    Requirements = new[] { R(metal, 6), R("Coal", 4) }
                });
                list.Add(new PartSpec
                {
                    Id = "TV_Handguard_" + name, Base = "FineWood", Slot = PartSlot.Handguard, Tier = t, Label = "HG",
                    SizeW = 2, Recoil = -0.04f * t, Spread = -0.03f * t, Weight = 0.4f,
                    Requirements = new[] { R(metal, 3), R("FineWood", 4) }
                });
                list.Add(new PartSpec
                {
                    Id = "TV_Stock_" + name, Base = "RoundLog", Slot = PartSlot.Stock, Tier = t, Label = "STK",
                    SizeW = 2, Recoil = -0.06f * t, AdsSpread = -0.03f * t, Weight = 0.6f,
                    Requirements = new[] { R(metal, 2), R("FineWood", 6) }
                });
            }

            list.Add(new PartSpec
            {
                Id = "TV_Muzzle_FlashHider", Base = "BronzeNails", Slot = PartSlot.Muzzle, Label = "MZL",
                Recoil = -0.08f, Weight = 0.1f,
                Requirements = new[] { R("Bronze", 3) }
            });
            list.Add(new PartSpec
            {
                Id = "TV_Muzzle_Compensator", Base = "IronNails", Slot = PartSlot.Muzzle, Label = "MZL",
                Recoil = -0.18f, Spread = 0.05f, Weight = 0.15f,
                Requirements = new[] { R("Iron", 4) }
            });
            list.Add(new PartSpec
            {
                Id = "TV_Muzzle_Suppressor", Base = "Chain", Slot = PartSlot.Muzzle, Label = "SUP",
                SizeW = 2, Recoil = -0.12f, Damage = -0.05f, Velocity = -0.08f, Weight = 0.4f,
                Requirements = new[] { R("BlackMetal", 4), R("LinenThread", 4) }
            });

            list.Add(new PartSpec
            {
                Id = "TV_Sight_Reflex", Base = "Amber", Slot = PartSlot.Sight, Label = "RDS",
                Sight = SightType.Reflex, AdsFov = 52f, AdsSpread = -0.15f, Weight = 0.1f,
                Requirements = new[] { R("Bronze", 2), R("Amber", 1) }
            });
            list.Add(new PartSpec
            {
                Id = "TV_Sight_Scope4x", Base = "Crystal", Slot = PartSlot.Sight, Label = "4x",
                SizeW = 2, Sight = SightType.Scope, AdsFov = 16f, AdsSpread = -0.2f, Weight = 0.4f,
                Fits = new[] { GunClass.AssaultRifle, GunClass.Marksman },
                Requirements = new[] { R("Iron", 4), R("Crystal", 2) }
            });
            list.Add(new PartSpec
            {
                Id = "TV_Sight_Scope8x", Base = "Crystal", Slot = PartSlot.Sight, Label = "8x",
                SizeW = 2, Sight = SightType.Scope, AdsFov = 8f, AdsSpread = -0.35f, Weight = 0.6f,
                Fits = new[] { GunClass.Marksman },
                Requirements = new[] { R("BlackMetal", 4), R("Crystal", 4) }
            });

            list.Add(new PartSpec
            {
                Id = "TV_Mag_Extended", Base = "IronScrap", Slot = PartSlot.Magazine, Label = "MAG",
                SizeH = 2, MagSize = 0.5f, Reload = 0.15f, Weight = 0.3f,
                Requirements = new[] { R("Iron", 4) }
            });
            list.Add(new PartSpec
            {
                Id = "TV_Mag_Drum", Base = "IronScrap", Slot = PartSlot.Magazine, Label = "DRUM",
                SizeW = 2, SizeH = 2, MagSize = 1.5f, Reload = 0.45f, Recoil = 0.05f, Weight = 0.9f,
                Fits = new[] { GunClass.Smg, GunClass.AssaultRifle },
                Requirements = new[] { R("BlackMetal", 6) }
            });

            return list;
        }

        public static void Register()
        {
            foreach (var spec in All)
            {
                var token = "$" + spec.Id.ToLowerInvariant();
                var item = new CustomItem(spec.Id, spec.Base, new ItemConfig
                {
                    Name = token,
                    Description = token + "_desc",
                    CraftingStation = Hideout.PrefabName,
                    Requirements = spec.Requirements
                });
                if (!item.IsValid())
                {
                    Plugin.Log.LogError($"Could not clone {spec.Base} for {spec.Id}.");
                    continue;
                }

                var shared = item.ItemDrop.m_itemData.m_shared;
                shared.m_itemType = ItemDrop.ItemData.ItemType.Material;
                shared.m_maxStackSize = 1;
                shared.m_maxQuality = 1;
                shared.m_weight = spec.Weight;
                shared.m_teleportable = true;

                ItemManager.Instance.AddItem(item);
            }
        }

        private static RequirementConfig R(string item, int amount)
        {
            return new RequirementConfig(item, amount, 0, true);
        }
    }
}
