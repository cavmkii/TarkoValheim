using System.Collections.Generic;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace TarkoValheim.Content
{
    /// <summary>Where a part goes. Each weapon family names them its own way
    /// (a sword's Head is its blade, a bow's Wrap is its string).</summary>
    internal enum PartSlot
    {
        Head,
        Handle,
        Guard,
        Wrap
    }

    /// <summary>The kinds of head; each fits only its own weapon families.</summary>
    internal enum HeadKind
    {
        None,
        Blade,
        AxeHead,
        MaceHead,
        SpearHead,
        Limbs
    }

    internal enum WeaponFamily
    {
        None,
        Sword,
        Knife,
        Axe,
        Club,
        Spear,
        Polearm,
        Bow,
        Crossbow,
        Shield
    }

    /// <summary>
    /// A weapon part. Every modifier is a fraction at the part's first level
    /// plus a smaller step per level, so a maxed part of one material edges
    /// out the next material at level 1 in its main stat. Silver heads add
    /// spirit damage and flametal heads add fire.
    /// </summary>
    internal sealed class PartSpec
    {
        public string Id;
        public string Base;
        public PartSlot Slot;
        public HeadKind Head;
        public int Tier;
        public string Material;
        public string[] IconFrom;
        public int SizeW = 1;
        public int SizeH = 1;
        public float Weight = 0.5f;
        public string Station;
        public int StationLevel = 1;
        public RequirementConfig[] Requirements;

        // Modifiers at level 1, and added per level after the first.
        public float Damage, DamageStep;
        public float Durability, DurabilityStep;
        public float Stamina, StaminaStep;      // negative = cheaper attacks
        public float Block, BlockStep;
        public float Parry, ParryStep;
        public float Spirit, SpiritStep;        // extra spirit damage, fraction of physical
        public float Fire, FireStep;            // extra fire damage, fraction of physical

        public float At(float baseValue, float step, int level) => baseValue + step * (Mathf.Clamp(level, 1, 4) - 1);
    }

    internal static class WeaponParts
    {
        public const int MaxLevel = 4;

        private static readonly string[] Metals = { "Bronze", "Iron", "Silver", "BlackMetal", "FlametalNew" };
        private static readonly string[] MetalKeys = { "bronze", "iron", "silver", "blackmetal", "flametal" };
        private static readonly string[] Woods = { "Wood", "FineWood", "RoundLog", "ElderBark", "YggdrasilWood" };
        private static readonly string[] WoodKeys = { "wood", "finewood", "corewood", "elderbark", "yggdrasil" };
        private static readonly string[] Hides = { "LeatherScraps", "DeerHide", "TrollHide", "WolfPelt", "LoxPelt" };
        private static readonly string[] HideKeys = { "leather", "deerhide", "trollhide", "wolfpelt", "loxpelt" };

        // Vanilla items whose icons stand in for each head, per tier. The
        // first one that exists is used; otherwise the material's own icon.
        private static readonly Dictionary<HeadKind, string[][]> HeadIcons = new Dictionary<HeadKind, string[][]>
        {
            { HeadKind.Blade, new[] { new[] { "SwordBronze" }, new[] { "SwordIron" }, new[] { "SwordSilver" }, new[] { "SwordBlackmetal" }, new[] { "SwordDyrnwyn", "SwordMistwalker" } } },
            { HeadKind.AxeHead, new[] { new[] { "AxeBronze" }, new[] { "AxeIron" }, new[] { "AxeJotunBane", "AxeIron" }, new[] { "AxeBlackMetal" }, new[] { "AxeBerzerkr", "AxeBlackMetal" } } },
            { HeadKind.MaceHead, new[] { new[] { "MaceBronze" }, new[] { "MaceIron" }, new[] { "MaceSilver" }, new[] { "MaceNeedle" }, new[] { "MaceEldner", "MaceNeedle" } } },
            { HeadKind.SpearHead, new[] { new[] { "SpearBronze" }, new[] { "SpearElderbark" }, new[] { "SpearWolfFang" }, new[] { "SpearCarapace" }, new[] { "SpearSplitner", "SpearCarapace" } } },
            { HeadKind.Limbs, new[] { new[] { "BowFineWood" }, new[] { "BowHuntsman" }, new[] { "BowDraugrFang" }, new[] { "BowSpineSnap" }, new[] { "BowAshlands", "BowSpineSnap" } } },
        };

        internal static readonly List<PartSpec> All = Build();

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

        /// <summary>The part for a slot (and head kind) at a material tier, 1-5.</summary>
        public static PartSpec FindBy(PartSlot slot, HeadKind head, int tier)
        {
            foreach (var part in All)
            {
                if (part.Slot == slot && part.Tier == tier && (slot != PartSlot.Head || part.Head == head))
                {
                    return part;
                }
            }
            return null;
        }

        /// <summary>Which head a family takes, if any.</summary>
        public static HeadKind HeadFor(WeaponFamily family)
        {
            switch (family)
            {
                case WeaponFamily.Sword:
                case WeaponFamily.Knife:
                    return HeadKind.Blade;
                case WeaponFamily.Axe:
                    return HeadKind.AxeHead;
                case WeaponFamily.Club:
                    return HeadKind.MaceHead;
                case WeaponFamily.Spear:
                case WeaponFamily.Polearm:
                    return HeadKind.SpearHead;
                case WeaponFamily.Bow:
                case WeaponFamily.Crossbow:
                    return HeadKind.Limbs;
                default:
                    return HeadKind.None;
            }
        }

        public static IEnumerable<PartSlot> SlotsOf(WeaponFamily family)
        {
            if (family == WeaponFamily.None)
            {
                yield break;
            }
            if (HeadFor(family) != HeadKind.None)
            {
                yield return PartSlot.Head;
            }
            yield return PartSlot.Handle;
            if (family == WeaponFamily.Sword || family == WeaponFamily.Polearm || family == WeaponFamily.Shield)
            {
                yield return PartSlot.Guard;
            }
            yield return PartSlot.Wrap;
        }

        public static bool Fits(PartSpec part, WeaponFamily family)
        {
            foreach (var slot in SlotsOf(family))
            {
                if (slot == part.Slot)
                {
                    return part.Slot != PartSlot.Head || part.Head == HeadFor(family);
                }
            }
            return false;
        }

        /// <summary>The family's own name for a slot: blade, haft, string...</summary>
        public static string SlotToken(PartSlot slot, WeaponFamily family)
        {
            switch (slot)
            {
                case PartSlot.Head:
                    switch (HeadFor(family))
                    {
                        case HeadKind.Blade: return "$tv_slot_blade";
                        case HeadKind.AxeHead: return "$tv_slot_axehead";
                        case HeadKind.MaceHead: return "$tv_slot_macehead";
                        case HeadKind.SpearHead: return "$tv_slot_spearhead";
                        default: return "$tv_slot_limbs";
                    }
                case PartSlot.Handle:
                    switch (family)
                    {
                        case WeaponFamily.Axe:
                        case WeaponFamily.Club:
                            return "$tv_slot_haft";
                        case WeaponFamily.Spear:
                        case WeaponFamily.Polearm:
                            return "$tv_slot_shaft";
                        case WeaponFamily.Crossbow:
                            return "$tv_slot_stock";
                        case WeaponFamily.Shield:
                            return "$tv_slot_handle";
                        default:
                            return "$tv_slot_grip";
                    }
                case PartSlot.Guard:
                    return family == WeaponFamily.Shield ? "$tv_slot_rim" : family == WeaponFamily.Polearm ? "$tv_slot_langet" : "$tv_slot_guard";
                default:
                    return family == WeaponFamily.Bow || family == WeaponFamily.Crossbow ? "$tv_slot_string"
                        : family == WeaponFamily.Shield ? "$tv_slot_straps" : "$tv_slot_wrap";
            }
        }

        private static List<PartSpec> Build()
        {
            var list = new List<PartSpec>();
            for (int i = 0; i < 5; i++)
            {
                int t = i + 1;
                string metal = Metals[i];
                string forge = t == 5 ? "blackforge" : "forge";
                int forgeLevel = t == 5 ? 1 : t;

                AddHead(list, HeadKind.Blade, "Blade", t, metal, MetalKeys[i], forge, forgeLevel, 4, 1, 2);
                AddHead(list, HeadKind.AxeHead, "AxeHead", t, metal, MetalKeys[i], forge, forgeLevel, 5, 2, 2);
                AddHead(list, HeadKind.MaceHead, "MaceHead", t, metal, MetalKeys[i], forge, forgeLevel, 6, 2, 2);
                AddHead(list, HeadKind.SpearHead, "SpearHead", t, metal, MetalKeys[i], forge, forgeLevel, 3, 1, 2);
                AddHead(list, HeadKind.Limbs, "Limbs", t, metal, MetalKeys[i], forge, forgeLevel, 3, 1, 3);

                list.Add(new PartSpec
                {
                    Id = "TV_Guard_" + MetalKeys[i], Base = metal, Slot = PartSlot.Guard, Tier = t, Material = metal,
                    SizeW = 2, Weight = 0.4f, Station = forge, StationLevel = forgeLevel,
                    Block = 0.06f * t, BlockStep = 0.025f, Parry = 0.05f * t, ParryStep = 0.02f,
                    Requirements = new[] { R(metal, 2, 1) }
                });

                list.Add(new PartSpec
                {
                    Id = "TV_Handle_" + WoodKeys[i], Base = Woods[i], Slot = PartSlot.Handle, Tier = t, Material = Woods[i],
                    SizeH = 2, Weight = 0.5f, Station = "piece_workbench", StationLevel = WorkbenchLevel(t),
                    Stamina = -0.03f * t, StaminaStep = -0.015f, Durability = 0.04f * t, DurabilityStep = 0.04f,
                    Requirements = new[] { R(Woods[i], 4, 2) }
                });

                list.Add(new PartSpec
                {
                    Id = "TV_Wrap_" + HideKeys[i], Base = Hides[i], Slot = PartSlot.Wrap, Tier = t, Material = Hides[i],
                    Weight = 0.2f, Station = "piece_workbench", StationLevel = WorkbenchLevel(t),
                    Stamina = -0.02f * t, StaminaStep = -0.01f, Block = 0.02f * t, BlockStep = 0.01f,
                    Requirements = new[] { R(Hides[i], 2, 1) }
                });
            }
            return list;
        }

        // Levelling a part needs one station level more per part level. The
        // workbench tops out at 5, so its parts start low enough to reach 4.
        private static int WorkbenchLevel(int tier) => tier <= 2 ? 1 : 2;

        private static void AddHead(List<PartSpec> list, HeadKind kind, string key, int t, string metal, string metalKey,
            string station, int stationLevel, int amount, int w, int h)
        {
            var spec = new PartSpec
            {
                Id = "TV_" + key + "_" + metalKey, Base = metal, Slot = PartSlot.Head, Head = kind, Tier = t, Material = metal,
                SizeW = w, SizeH = h, Weight = 0.3f * amount, Station = station, StationLevel = stationLevel,
                IconFrom = HeadIcons[kind][t - 1],
                Damage = 0.06f * t, DamageStep = 0.025f, Durability = 0.05f * t, DurabilityStep = 0.05f,
                Requirements = new[] { R(metal, amount, Mathf.Max(1, amount / 2)) }
            };
            if (metal == "Silver")
            {
                spec.Spirit = 0.10f;
                spec.SpiritStep = 0.03f;
            }
            if (metal == "FlametalNew")
            {
                spec.Fire = 0.10f;
                spec.FireStep = 0.03f;
            }
            list.Add(spec);
        }

        public static void Register()
        {
            foreach (var spec in All)
            {
                var token = "$" + spec.Id.ToLowerInvariant();
                var item = new CustomItem(spec.Id, spec.Base, new ItemConfig
                {
                    Name = token,
                    Description = "$tv_part_desc_" + spec.Slot.ToString().ToLowerInvariant(),
                    CraftingStation = spec.Station,
                    MinStationLevel = spec.StationLevel,
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
                shared.m_maxQuality = MaxLevel;
                shared.m_weight = spec.Weight;
                shared.m_teleportable = true;

                if (spec.IconFrom != null)
                {
                    foreach (var name in spec.IconFrom)
                    {
                        var source = PrefabManager.Instance.GetPrefab(name);
                        var drop = source != null ? source.GetComponent<ItemDrop>() : null;
                        if (drop != null && drop.m_itemData.m_shared.m_icons != null && drop.m_itemData.m_shared.m_icons.Length > 0)
                        {
                            shared.m_icons = drop.m_itemData.m_shared.m_icons;
                            break;
                        }
                    }
                }

                ItemManager.Instance.AddItem(item);
            }
        }

        private static RequirementConfig R(string item, int amount, int perLevel)
        {
            return new RequirementConfig(item, amount, perLevel, true);
        }
    }
}
