using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using TarkoValheim.Util;

namespace TarkoValheim.Content
{
    /// <summary>
    /// Guns are clones of the Arbalest and ammo is cloned from iron bolts.
    /// Each gun takes a caliber of its own, so ammo is not interchangeable.
    /// The game adds ammo damage to weapon damage on every projectile.
    /// </summary>
    internal static class Firearms
    {
        private const string GunBase = "CrossbowArbalest";
        private const string AmmoBase = "BoltIron";

        internal sealed class AmmoSpec
        {
            public string Id;
            public string Caliber;
            public float Pierce;
            public int CraftAmount;
            public RequirementConfig[] Requirements;
        }

        internal sealed class GunSpec
        {
            public string Id;
            public string Caliber;
            public float Pierce;
            public float ReloadTime;
            public int Pellets = 1;
            public float Spread;
            public float Velocity;
            public int Burst = 1;
            public float BurstInterval;
            public float Durability;
            public float Weight;
            public RequirementConfig[] Requirements;
        }

        internal static readonly AmmoSpec[] Ammo =
        {
            new AmmoSpec
            {
                Id = "TV_Ammo_9x18", Caliber = "$tv_cal_9x18", Pierce = 12, CraftAmount = 20,
                Requirements = new[] { R("Copper", 1), R("Coal", 2) }
            },
            new AmmoSpec
            {
                Id = "TV_Ammo_12ga", Caliber = "$tv_cal_12ga", Pierce = 4, CraftAmount = 10,
                Requirements = new[] { R("Bronze", 1), R("Coal", 2), R("Resin", 2) }
            },
            new AmmoSpec
            {
                Id = "TV_Ammo_762x54R", Caliber = "$tv_cal_762x54r", Pierce = 40, CraftAmount = 10,
                Requirements = new[] { R("Iron", 1), R("Coal", 3) }
            },
            new AmmoSpec
            {
                Id = "TV_Ammo_545x39", Caliber = "$tv_cal_545x39", Pierce = 22, CraftAmount = 30,
                Requirements = new[] { R("BlackMetal", 1), R("Coal", 4) }
            },
        };

        // Rough progression: pistol and shotgun in the bronze age, the Mosin
        // after iron, the AK after black metal.
        internal static readonly GunSpec[] Guns =
        {
            new GunSpec
            {
                Id = "TV_PM", Caliber = "$tv_cal_9x18",
                Pierce = 28, ReloadTime = 0.8f, Spread = 2f, Velocity = 160f,
                Durability = 200, Weight = 1.5f,
                Requirements = new[] { R("Bronze", 8), R("FineWood", 2), R("LeatherScraps", 2) }
            },
            new GunSpec
            {
                Id = "TV_MP133", Caliber = "$tv_cal_12ga",
                Pierce = 10, ReloadTime = 2.2f, Pellets = 8, Spread = 9f, Velocity = 120f,
                Durability = 250, Weight = 3.5f,
                Requirements = new[] { R("Bronze", 14), R("FineWood", 8), R("DeerHide", 2) }
            },
            new GunSpec
            {
                Id = "TV_Mosin", Caliber = "$tv_cal_762x54r",
                Pierce = 110, ReloadTime = 3.0f, Spread = 0.3f, Velocity = 260f,
                Durability = 300, Weight = 4f,
                Requirements = new[] { R("Iron", 16), R("FineWood", 12), R("LeatherScraps", 4) }
            },
            new GunSpec
            {
                Id = "TV_AK74", Caliber = "$tv_cal_545x39",
                Pierce = 34, ReloadTime = 0.6f, Spread = 3f, Velocity = 220f,
                Burst = 3, BurstInterval = 0.09f,
                Durability = 400, Weight = 3.5f,
                Requirements = new[] { R("BlackMetal", 14), R("FineWood", 6), R("Iron", 6) }
            },
        };

        public static void Register()
        {
            foreach (var spec in Ammo)
            {
                RegisterAmmo(spec);
            }

            foreach (var spec in Guns)
            {
                RegisterGun(spec);
            }
        }

        private static void RegisterAmmo(AmmoSpec spec)
        {
            var item = new CustomItem(spec.Id, AmmoBase, new ItemConfig
            {
                Name = "$" + spec.Id.ToLowerInvariant(),
                Description = "$" + spec.Id.ToLowerInvariant() + "_desc",
                CraftingStation = Hideout.PrefabName,
                Amount = spec.CraftAmount,
                Requirements = spec.Requirements
            });
            if (!item.IsValid())
            {
                Plugin.Log.LogError($"Could not clone {AmmoBase} for {spec.Id}.");
                return;
            }

            var shared = item.ItemDrop.m_itemData.m_shared;
            shared.m_ammoType = spec.Caliber;
            shared.m_damages = new HitData.DamageTypes { m_pierce = spec.Pierce };
            shared.m_weight = 0.02f;
            shared.m_maxStackSize = 120;

            ItemManager.Instance.AddItem(item);
        }

        private static void RegisterGun(GunSpec spec)
        {
            var item = new CustomItem(spec.Id, GunBase, new ItemConfig
            {
                Name = "$" + spec.Id.ToLowerInvariant(),
                Description = "$" + spec.Id.ToLowerInvariant() + "_desc",
                CraftingStation = Hideout.PrefabName,
                RepairStation = Hideout.PrefabName,
                Requirements = spec.Requirements
            });
            if (!item.IsValid())
            {
                Plugin.Log.LogError($"Could not clone {GunBase} for {spec.Id}.");
                return;
            }

            var shared = item.ItemDrop.m_itemData.m_shared;
            shared.m_ammoType = spec.Caliber;
            shared.m_damages = new HitData.DamageTypes { m_pierce = spec.Pierce };
            shared.m_maxDurability = spec.Durability;
            shared.m_weight = spec.Weight;
            shared.m_maxQuality = 1;

            var attack = shared.m_attack;
            Reflect.TrySet(attack, "m_requiresReload", true);
            Reflect.TrySet(attack, "m_reloadTime", spec.ReloadTime);
            Reflect.TrySet(attack, "m_projectiles", spec.Pellets);
            Reflect.TrySet(attack, "m_projectileAccuracy", spec.Spread);
            Reflect.TrySet(attack, "m_projectileAccuracyMin", spec.Spread);
            Reflect.TrySet(attack, "m_projectileVel", spec.Velocity);
            Reflect.TrySet(attack, "m_projectileVelMin", spec.Velocity);
            if (spec.Burst > 1)
            {
                Reflect.TrySet(attack, "m_projectileBursts", spec.Burst);
                Reflect.TrySet(attack, "m_burstInterval", spec.BurstInterval);
            }

            ItemManager.Instance.AddItem(item);
        }

        private static RequirementConfig R(string item, int amount)
        {
            return new RequirementConfig(item, amount, 0, true);
        }
    }
}
