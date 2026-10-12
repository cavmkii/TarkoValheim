using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using TarkoValheim.Content;
using TarkoValheim.Util;
using UnityEngine;

namespace TarkoValheim.Health
{
    internal sealed class MedSpec
    {
        public string Id;
        public string IconFrom;
        public int SizeW = 1;
        public int SizeH = 1;
        public int Stack = 10;
        public float Weight = 0.1f;
        public bool StopsLight;
        public bool StopsHeavy;
        public bool FixesFracture;
        public float Painkiller;    // seconds
        public float Heal;          // fraction of max health
        public RequirementConfig[] Requirements;
    }

    /// <summary>
    /// Medical items, made at the Hideout. Using one (double-click, a quick
    /// slot or the menu) treats what it can and is used up; if it would do
    /// nothing, it isn't spent.
    /// </summary>
    internal static class Meds
    {
        private const string Base = "MeadHealthMinor";

        internal static readonly MedSpec[] All =
        {
            new MedSpec
            {
                Id = "TV_Bandage", IconFrom = "LeatherScraps", StopsLight = true,
                Requirements = new[] { R("LeatherScraps", 2), R("Dandelion", 1) }
            },
            new MedSpec
            {
                Id = "TV_Tourniquet", IconFrom = "DeerHide", StopsLight = true, StopsHeavy = true, Stack = 5,
                Requirements = new[] { R("DeerHide", 1), R("LeatherScraps", 2), R("Resin", 2) }
            },
            new MedSpec
            {
                Id = "TV_Splint", IconFrom = "RoundLog", FixesFracture = true, SizeH = 2, Stack = 5, Weight = 0.5f,
                Requirements = new[] { R("Wood", 3), R("LeatherScraps", 2) }
            },
            new MedSpec
            {
                Id = "TV_Painkillers", IconFrom = "Thistle", Painkiller = 300f,
                Requirements = new[] { R("Thistle", 2), R("Dandelion", 2) }
            },
            new MedSpec
            {
                Id = "TV_FieldKit", IconFrom = "Honey", StopsLight = true, StopsHeavy = true, Heal = 0.3f, Stack = 3, Weight = 0.4f,
                Requirements = new[] { R("Honey", 2), R("LinenThread", 3), R("Resin", 4) }
            },
            new MedSpec
            {
                Id = "TV_SurgicalKit", IconFrom = "Iron", StopsLight = true, StopsHeavy = true, FixesFracture = true,
                Heal = 0.5f, SizeW = 2, Stack = 2, Weight = 1f,
                Requirements = new[] { R("Iron", 2), R("LinenThread", 4), R("Honey", 3), R("Thistle", 2) }
            },
        };

        public static MedSpec Find(string prefabName)
        {
            foreach (var m in All)
            {
                if (m.Id == prefabName)
                {
                    return m;
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
                shared.m_itemType = ItemDrop.ItemData.ItemType.Consumable;
                shared.m_consumeStatusEffect = null;
                shared.m_maxStackSize = spec.Stack;
                shared.m_weight = spec.Weight;
                shared.m_teleportable = true;

                var source = PrefabManager.Instance.GetPrefab(spec.IconFrom);
                var drop = source != null ? source.GetComponent<ItemDrop>() : null;
                if (drop != null && drop.m_itemData.m_shared.m_icons != null && drop.m_itemData.m_shared.m_icons.Length > 0)
                {
                    shared.m_icons = drop.m_itemData.m_shared.m_icons;
                }

                ItemManager.Instance.AddItem(item);
            }
        }

        public static void Patch(Harmony harmony)
        {
            var consume = AccessTools.Method(typeof(Player), "ConsumeItem");
            if (consume == null)
            {
                Plugin.Log.LogError("Player.ConsumeItem not found; medical items can't be used.");
                return;
            }
            harmony.Patch(consume, prefix: new HarmonyMethod(typeof(Meds), nameof(ConsumePrefix)));
        }

        private static bool ConsumePrefix(Player __instance, object[] __args, ref bool __result)
        {
            try
            {
                Inventory inventory = null;
                ItemDrop.ItemData item = null;
                foreach (var arg in __args)
                {
                    if (arg is Inventory inv)
                    {
                        inventory = inv;
                    }
                    else if (arg is ItemDrop.ItemData data)
                    {
                        item = data;
                    }
                }
                var spec = item != null ? Find(Gear.PrefabName(item)) : null;
                if (spec == null)
                {
                    return true;
                }

                __result = Use(__instance, spec);
                if (__result && inventory != null)
                {
                    inventory.RemoveItem(item, 1);
                }
                return false;
            }
            catch (System.Exception e)
            {
                GameAccess.WarnOnce("Meds.Consume", e);
                return true;
            }
        }

        private static bool Use(Player player, MedSpec spec)
        {
            bool did = false;
            if (spec.StopsHeavy && Conditions.Has(player, Condition.HeavyBleed))
            {
                Conditions.Set(player, Condition.HeavyBleed, false);
                did = true;
            }
            if (spec.StopsLight && Conditions.Has(player, Condition.LightBleed))
            {
                Conditions.Set(player, Condition.LightBleed, false);
                did = true;
            }
            if (spec.FixesFracture && Conditions.Has(player, Condition.Fracture))
            {
                Conditions.Set(player, Condition.Fracture, false);
                did = true;
            }
            if (spec.Painkiller > 0f)
            {
                Conditions.SetPainkiller(player, spec.Painkiller);
                did = true;
            }
            if (spec.Heal > 0f && player.GetHealth() < player.GetMaxHealth())
            {
                player.Heal(player.GetMaxHealth() * spec.Heal, true);
                did = true;
            }

            player.Message(MessageHud.MessageType.Center,
                Localization.instance.Localize(did ? "$tv_msg_treated" : "$tv_msg_nothing_to_treat"), 0, null, false);
            return did;
        }

        private static RequirementConfig R(string item, int amount)
        {
            return new RequirementConfig(item, amount, 0, true);
        }
    }
}
