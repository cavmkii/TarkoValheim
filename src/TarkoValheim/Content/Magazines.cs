using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;

namespace TarkoValheim.Content
{
    /// <summary>
    /// A detachable magazine. Like a gun, it holds rounds of one type in its
    /// m_customData (tv_mag / tv_ammo), so the same GunState helpers read
    /// both. Load it by dragging rounds onto it; R swaps it into the gun.
    /// </summary>
    internal sealed class MagSpec
    {
        public string Id;
        public string Caliber;
        public int Capacity;
        public string[] Fits;
        public int SizeW = 1;
        public int SizeH = 2;
        public float Weight = 0.2f;

        // Fractions, like weapon parts: bigger magazines are slower to swap.
        public float Reload;
        public float Recoil;

        public RequirementConfig[] Requirements;

        public bool FitsGun(GunSpec gun)
        {
            if (gun == null || gun.PerRoundReload)
            {
                return false;
            }
            foreach (var id in Fits)
            {
                if (id == gun.Id)
                {
                    return true;
                }
            }
            return false;
        }
    }

    internal static class Magazines
    {
        private const string Base = "IronScrap";

        internal static readonly MagSpec[] All =
        {
            new MagSpec
            {
                Id = "TV_Mag_PM8", Caliber = "$tv_cal_9x18", Capacity = 8, Fits = new[] { "TV_PM" },
                SizeH = 1, Weight = 0.1f, Requirements = new[] { R("Bronze", 1) }
            },
            new MagSpec
            {
                Id = "TV_Mag_Kedr30", Caliber = "$tv_cal_9x18", Capacity = 30, Fits = new[] { "TV_Kedr" },
                Requirements = new[] { R("Iron", 2) }
            },
            new MagSpec
            {
                Id = "TV_Mag_AK30", Caliber = "$tv_cal_545x39", Capacity = 30, Fits = new[] { "TV_AK74" },
                Requirements = new[] { R("Iron", 3) }
            },
            new MagSpec
            {
                Id = "TV_Mag_AK45", Caliber = "$tv_cal_545x39", Capacity = 45, Fits = new[] { "TV_AK74" },
                Reload = 0.1f, Weight = 0.3f, Requirements = new[] { R("Iron", 5) }
            },
            new MagSpec
            {
                Id = "TV_Mag_AK95", Caliber = "$tv_cal_545x39", Capacity = 95, Fits = new[] { "TV_AK74" },
                SizeW = 2, SizeH = 2, Reload = 0.45f, Recoil = 0.05f, Weight = 0.9f,
                Requirements = new[] { R("BlackMetal", 6) }
            },
            new MagSpec
            {
                Id = "TV_Mag_SVD10", Caliber = "$tv_cal_762x54r", Capacity = 10, Fits = new[] { "TV_SVD" },
                Requirements = new[] { R("Iron", 3) }
            },
            new MagSpec
            {
                Id = "TV_Mag_SVD20", Caliber = "$tv_cal_762x54r", Capacity = 20, Fits = new[] { "TV_SVD" },
                Reload = 0.15f, Weight = 0.35f, Requirements = new[] { R("BlackMetal", 3) }
            },
        };

        /// <summary>The magazine a newly made gun comes with, or null for
        /// tube-fed and internal-magazine guns.</summary>
        public static string DefaultFor(GunSpec gun)
        {
            if (gun == null || gun.PerRoundReload)
            {
                return null;
            }
            foreach (var mag in All)
            {
                if (mag.FitsGun(gun))
                {
                    return mag.Id;
                }
            }
            return null;
        }

        public static MagSpec Find(string prefabName)
        {
            if (prefabName == null)
            {
                return null;
            }
            foreach (var mag in All)
            {
                if (mag.Id == prefabName)
                {
                    return mag;
                }
            }
            return null;
        }

        public static void Register()
        {
            foreach (var spec in All)
            {
                var token = "$" + spec.Id.ToLowerInvariant();
                var item = new CustomItem(spec.Id, Base, new ItemConfig
                {
                    Name = token,
                    Description = token + "_desc",
                    CraftingStation = Hideout.PrefabName,
                    Requirements = spec.Requirements
                });
                if (!item.IsValid())
                {
                    Plugin.Log.LogError($"Could not clone {Base} for {spec.Id}.");
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
