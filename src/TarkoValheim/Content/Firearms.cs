using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;

namespace TarkoValheim.Content
{
    internal enum SightType
    {
        Irons,
        Reflex,
        Scope
    }

    /// <summary>Which weapon skill a gun trains and benefits from.</summary>
    internal enum GunClass
    {
        Handgun,
        Smg,
        Shotgun,
        AssaultRifle,
        Marksman
    }

    internal enum FireMode
    {
        Single,
        Burst,
        Auto
    }

    /// <summary>A round. Ammo decides damage and pellet count, as in Tarkov.</summary>
    internal sealed class AmmoSpec
    {
        public string Id;
        public string Caliber;
        public float Damage;
        public int Pellets = 1;
        public float PelletSpread;
        public float VelocityFactor = 1f;
        public int CraftAmount;
        public RequirementConfig[] Requirements;
    }

    /// <summary>A gun. The gun decides handling: magazine, fire rate, recoil, accuracy.</summary>
    internal sealed class GunSpec
    {
        public string Id;
        public string Caliber;
        public GunClass Class;
        public int SizeW;
        public int SizeH;
        public bool Holster;
        public int MagSize;
        public bool PerRoundReload;
        public float ReloadTime;
        public int Rpm;
        public FireMode[] Modes;
        public float RecoilUp;
        public float RecoilSide;
        public float HipSpread;
        public float AdsSpread;
        public float AdsFov;
        public SightType Sight;
        public float Velocity;
        public float Durability;
        public float Weight;
        public RequirementConfig[] Requirements;
    }

    /// <summary>
    /// Guns are Arbalest clones and rounds are iron-bolt clones, for their
    /// models, icons and sounds only. Firing, reloading and damage are done
    /// by <see cref="Guns.GunController"/>; Valheim's crossbow attack never runs.
    /// </summary>
    internal static class Firearms
    {
        private const string GunBase = "CrossbowArbalest";
        private const string AmmoBase = "BoltIron";

        // Prefab names of the first ammo of each caliber are unchanged from
        // 0.1.0, so existing stacks stay valid.
        internal static readonly AmmoSpec[] Ammo =
        {
            new AmmoSpec
            {
                Id = "TV_Ammo_9x18", Caliber = "$tv_cal_9x18", Damage = 22, CraftAmount = 20,
                Requirements = new[] { R("Copper", 1), R("Coal", 2) }
            },
            new AmmoSpec
            {
                Id = "TV_Ammo_9x18_PBM", Caliber = "$tv_cal_9x18", Damage = 32, CraftAmount = 20,
                Requirements = new[] { R("Bronze", 1), R("Coal", 2) }
            },
            new AmmoSpec
            {
                Id = "TV_Ammo_12ga", Caliber = "$tv_cal_12ga", Damage = 11, Pellets = 8, PelletSpread = 4.5f,
                VelocityFactor = 0.75f, CraftAmount = 10,
                Requirements = new[] { R("Bronze", 1), R("Coal", 2), R("Resin", 2) }
            },
            new AmmoSpec
            {
                Id = "TV_Ammo_12ga_Slug", Caliber = "$tv_cal_12ga", Damage = 85, VelocityFactor = 0.8f, CraftAmount = 10,
                Requirements = new[] { R("Iron", 1), R("Coal", 2), R("Resin", 2) }
            },
            new AmmoSpec
            {
                Id = "TV_Ammo_545x39", Caliber = "$tv_cal_545x39", Damage = 40, CraftAmount = 30,
                Requirements = new[] { R("BlackMetal", 1), R("Coal", 4) }
            },
            new AmmoSpec
            {
                Id = "TV_Ammo_545x39_BS", Caliber = "$tv_cal_545x39", Damage = 56, CraftAmount = 20,
                Requirements = new[] { R("BlackMetal", 1), R("Silver", 1), R("Coal", 4) }
            },
            new AmmoSpec
            {
                Id = "TV_Ammo_762x54R", Caliber = "$tv_cal_762x54r", Damage = 80, CraftAmount = 10,
                Requirements = new[] { R("Iron", 1), R("Coal", 3) }
            },
            new AmmoSpec
            {
                Id = "TV_Ammo_762x54R_7N1", Caliber = "$tv_cal_762x54r", Damage = 105, CraftAmount = 10,
                Requirements = new[] { R("Silver", 1), R("Coal", 3) }
            },
        };

        internal static readonly GunSpec[] Guns =
        {
            new GunSpec
            {
                Id = "TV_PM", Class = GunClass.Handgun, Caliber = "$tv_cal_9x18", SizeW = 2, SizeH = 1, Holster = true,
                MagSize = 8, ReloadTime = 2.2f, Rpm = 450, Modes = new[] { FireMode.Single },
                RecoilUp = 1.4f, RecoilSide = 0.5f, HipSpread = 2.2f, AdsSpread = 0.7f, AdsFov = 52f,
                Velocity = 310f, Durability = 300f, Weight = 0.75f,
                Requirements = new[] { R("Bronze", 8), R("FineWood", 2), R("LeatherScraps", 2) }
            },
            new GunSpec
            {
                Id = "TV_Kedr", Class = GunClass.Smg, Caliber = "$tv_cal_9x18", SizeW = 3, SizeH = 1,
                MagSize = 30, ReloadTime = 2.4f, Rpm = 900, Modes = new[] { FireMode.Auto, FireMode.Single },
                RecoilUp = 0.75f, RecoilSide = 0.6f, HipSpread = 2.4f, AdsSpread = 0.9f, AdsFov = 55f,
                Velocity = 310f, Durability = 350f, Weight = 1.6f,
                Requirements = new[] { R("Iron", 10), R("Bronze", 4), R("LeatherScraps", 2) }
            },
            new GunSpec
            {
                Id = "TV_MP133", Class = GunClass.Shotgun, Caliber = "$tv_cal_12ga", SizeW = 5, SizeH = 1,
                MagSize = 6, PerRoundReload = true, ReloadTime = 0.55f, Rpm = 55, Modes = new[] { FireMode.Single },
                RecoilUp = 5.5f, RecoilSide = 1.5f, HipSpread = 2.5f, AdsSpread = 1.2f, AdsFov = 55f,
                Velocity = 380f, Durability = 300f, Weight = 3.5f,
                Requirements = new[] { R("Bronze", 14), R("FineWood", 8), R("DeerHide", 2) }
            },
            new GunSpec
            {
                Id = "TV_Mosin", Class = GunClass.Marksman, Caliber = "$tv_cal_762x54r", SizeW = 5, SizeH = 1,
                MagSize = 5, PerRoundReload = true, ReloadTime = 0.65f, Rpm = 30, Modes = new[] { FireMode.Single },
                RecoilUp = 6f, RecoilSide = 1f, HipSpread = 3.5f, AdsSpread = 0.08f, AdsFov = 42f,
                Velocity = 820f, Durability = 400f, Weight = 4f,
                Requirements = new[] { R("Iron", 16), R("FineWood", 12), R("LeatherScraps", 4) }
            },
            new GunSpec
            {
                Id = "TV_SVD", Class = GunClass.Marksman, Caliber = "$tv_cal_762x54r", SizeW = 5, SizeH = 1,
                MagSize = 10, ReloadTime = 3f, Rpm = 300, Modes = new[] { FireMode.Single },
                RecoilUp = 3.2f, RecoilSide = 1.2f, HipSpread = 3.5f, AdsSpread = 0.12f, AdsFov = 16f,
                Sight = SightType.Scope,
                Velocity = 800f, Durability = 500f, Weight = 4.3f,
                Requirements = new[] { R("BlackMetal", 12), R("Iron", 10), R("FineWood", 8), R("Silver", 4) }
            },
            new GunSpec
            {
                Id = "TV_AK74", Class = GunClass.AssaultRifle, Caliber = "$tv_cal_545x39", SizeW = 4, SizeH = 2,
                MagSize = 30, ReloadTime = 2.7f, Rpm = 650, Modes = new[] { FireMode.Auto, FireMode.Single },
                RecoilUp = 1f, RecoilSide = 0.45f, HipSpread = 3f, AdsSpread = 0.35f, AdsFov = 50f,
                Velocity = 880f, Durability = 600f, Weight = 3.4f,
                Requirements = new[] { R("BlackMetal", 14), R("FineWood", 6), R("Iron", 6) }
            },
        };

        public static GunSpec FindGun(string prefabName)
        {
            if (prefabName == null)
            {
                return null;
            }
            foreach (var gun in Guns)
            {
                if (gun.Id == prefabName)
                {
                    return gun;
                }
            }
            return null;
        }

        public static AmmoSpec FindAmmo(string prefabName)
        {
            if (prefabName == null)
            {
                return null;
            }
            foreach (var ammo in Ammo)
            {
                if (ammo.Id == prefabName)
                {
                    return ammo;
                }
            }
            return null;
        }

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
            var token = "$" + spec.Id.ToLowerInvariant();
            var item = new CustomItem(spec.Id, AmmoBase, new ItemConfig
            {
                Name = token,
                Description = token + "_desc",
                CraftingStation = Hideout.PrefabName,
                Amount = spec.CraftAmount,
                Requirements = spec.Requirements
            });
            if (!item.IsValid())
            {
                Plugin.Log.LogError($"Could not clone {AmmoBase} for {spec.Id}.");
                return;
            }

            // The pierce value is only for Valheim's tooltip; hits are computed
            // from the spec.
            var shared = item.ItemDrop.m_itemData.m_shared;
            shared.m_ammoType = spec.Caliber;
            shared.m_damages = new HitData.DamageTypes { m_pierce = spec.Damage };
            shared.m_weight = 0.02f;
            shared.m_maxStackSize = 60;

            ItemManager.Instance.AddItem(item);
        }

        private static void RegisterGun(GunSpec spec)
        {
            var token = "$" + spec.Id.ToLowerInvariant();
            var item = new CustomItem(spec.Id, GunBase, new ItemConfig
            {
                Name = token,
                Description = token + "_desc",
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
            shared.m_damages = new HitData.DamageTypes();
            shared.m_maxDurability = spec.Durability;
            shared.m_weight = spec.Weight;
            shared.m_maxQuality = 1;
            shared.m_attack.m_requiresReload = false;

            ItemManager.Instance.AddItem(item);
        }

        private static RequirementConfig R(string item, int amount)
        {
            return new RequirementConfig(item, amount, 0, true);
        }
    }
}
