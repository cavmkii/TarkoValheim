using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;

namespace TarkoValheim.Content
{
    internal enum GearKind
    {
        Rig,
        Backpack,
        Secure
    }

    /// <summary>
    /// Rigs, backpacks and secure containers. They are plain materials as far
    /// as Valheim is concerned; the Tetris inventory gives them meaning when
    /// they sit in the matching equipment slot.
    /// </summary>
    internal sealed class GearSpec
    {
        public string Id;
        public string Base;
        public GearKind Kind;
        public int GridW;
        public int GridH;
        public int SizeW;
        public int SizeH;
        public float Weight;
        public RequirementConfig[] Requirements;
    }

    internal static class Gear
    {
        public const string StarterSecure = "TV_Secure_Alpha";
        public const string StarterRig = "TV_Rig_Scav";
        public const string StarterPack = "TV_Pack_Sling";

        internal static readonly GearSpec[] All =
        {
            new GearSpec
            {
                Id = "TV_Rig_Scav", Base = "ArmorRagsChest", Kind = GearKind.Rig,
                GridW = 4, GridH = 2, SizeW = 2, SizeH = 2, Weight = 1f,
                Requirements = new[] { R("LeatherScraps", 6), R("DeerHide", 2) }
            },
            new GearSpec
            {
                Id = "TV_Rig_Carrier", Base = "ArmorLeatherChest", Kind = GearKind.Rig,
                GridW = 5, GridH = 3, SizeW = 3, SizeH = 3, Weight = 2f,
                Requirements = new[] { R("LeatherScraps", 10), R("TrollHide", 4), R("BronzeNails", 20) }
            },
            new GearSpec
            {
                Id = "TV_Pack_Sling", Base = "CapeDeerHide", Kind = GearKind.Backpack,
                GridW = 3, GridH = 3, SizeW = 2, SizeH = 3, Weight = 1f,
                Requirements = new[] { R("DeerHide", 4), R("LeatherScraps", 4) }
            },
            new GearSpec
            {
                Id = "TV_Pack_Scav", Base = "CapeTrollHide", Kind = GearKind.Backpack,
                GridW = 4, GridH = 5, SizeW = 4, SizeH = 4, Weight = 2f,
                Requirements = new[] { R("TrollHide", 6), R("LeatherScraps", 8), R("BronzeNails", 10) }
            },
            new GearSpec
            {
                Id = "TV_Pack_Pilgrim", Base = "CapeTrollHide", Kind = GearKind.Backpack,
                GridW = 5, GridH = 7, SizeW = 5, SizeH = 5, Weight = 3f,
                Requirements = new[] { R("TrollHide", 10), R("WolfPelt", 6), R("LinenThread", 10), R("IronNails", 20) }
            },
            new GearSpec
            {
                Id = "TV_Secure_Alpha", Base = "CryptKey", Kind = GearKind.Secure,
                GridW = 2, GridH = 2, SizeW = 2, SizeH = 2, Weight = 0.5f,
                Requirements = new[] { R("Bronze", 5) }
            },
            new GearSpec
            {
                Id = "TV_Secure_Beta", Base = "CryptKey", Kind = GearKind.Secure,
                GridW = 3, GridH = 2, SizeW = 2, SizeH = 2, Weight = 0.5f,
                Requirements = new[] { R("Bronze", 10), R("Iron", 5) }
            },
            new GearSpec
            {
                Id = "TV_Secure_Gamma", Base = "CryptKey", Kind = GearKind.Secure,
                GridW = 3, GridH = 3, SizeW = 2, SizeH = 2, Weight = 0.5f,
                Requirements = new[] { R("Iron", 15), R("Silver", 10) }
            },
            new GearSpec
            {
                Id = "TV_Secure_Kappa", Base = "CryptKey", Kind = GearKind.Secure,
                GridW = 3, GridH = 4, SizeW = 2, SizeH = 2, Weight = 0.5f,
                Requirements = new[] { R("BlackMetal", 20), R("Silver", 20) }
            },
        };

        public static GearSpec Find(ItemDrop.ItemData item)
        {
            var name = PrefabName(item);
            if (name == null)
            {
                return null;
            }

            foreach (var spec in All)
            {
                if (spec.Id == name)
                {
                    return spec;
                }
            }
            return null;
        }

        public static string PrefabName(ItemDrop.ItemData item)
        {
            return item?.m_dropPrefab != null ? item.m_dropPrefab.name : null;
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

                // A material can't be equipped by Valheim, so only the Tetris
                // slots decide what a rig or backpack does.
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
