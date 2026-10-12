using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TarkoValheim.Content;
using UnityEngine;

namespace TarkoValheim.Raid
{
    /// <summary>
    /// Death in raid. Player.OnDeath calls Player.CreateTombStone, which
    /// moves the inventory into a grave. A prefix on CreateTombStone pulls
    /// secure-container items out first (and, under LoseEverything, empties
    /// the inventory so no grave is made); a postfix puts the secure items
    /// back. The respawned player is loaded from the profile saved after
    /// OnDeath, so whatever is left in the inventory carries over.
    /// </summary>
    internal static class DeathRules
    {
        private sealed class Secured
        {
            public ItemDrop.ItemData Item;
            public Vector2i Pos;
        }

        private static bool _diedInRaid;
        private static List<Secured> _secured;

        public static void Patch(Harmony harmony)
        {
            PatchOrWarn(harmony, "OnDeath", nameof(OnDeathPrefix), nameof(OnDeathPostfix));
            PatchOrWarn(harmony, "CreateTombStone", nameof(TombPrefix), nameof(TombPostfix));

            if (AccessTools.Field(typeof(Inventory), "m_width") == null)
            {
                Plugin.Log.LogError("Inventory.m_width not found; the secure container is disabled.");
            }
        }

        private static void PatchOrWarn(Harmony harmony, string method, string prefix, string postfix)
        {
            var target = AccessTools.Method(typeof(Player), method);
            if (target == null)
            {
                Plugin.Log.LogError($"Player.{method} not found; raid death rules are disabled.");
                return;
            }

            harmony.Patch(target,
                prefix: new HarmonyMethod(typeof(DeathRules), prefix),
                postfix: new HarmonyMethod(typeof(DeathRules), postfix));
        }

        private static void OnDeathPrefix(Player __instance)
        {
            _diedInRaid = __instance == Player.m_localPlayer
                && !Hideout.IsNear(__instance.transform.position);
        }

        private static void TombPrefix(Player __instance)
        {
            _secured = null;
            if (!_diedInRaid || Plugin.RaidDeathRule.Value == DeathRule.Vanilla)
            {
                return;
            }

            var inventory = __instance.GetInventory();
            _secured = new List<Secured>();
            foreach (var item in new List<ItemDrop.ItemData>(inventory.GetAllItems()))
            {
                if (!SecureContainer.Contains(inventory, item))
                {
                    continue;
                }

                if (item.m_equipped)
                {
                    __instance.UnequipItem(item, false);
                }
                _secured.Add(new Secured { Item = item, Pos = item.m_gridPos });
                inventory.RemoveItem(item);
            }

            if (Plugin.RaidDeathRule.Value == DeathRule.LoseEverything)
            {
                // With the inventory empty, CreateTombStone has nothing to
                // bury and makes no grave.
                foreach (var item in new List<ItemDrop.ItemData>(inventory.GetAllItems()))
                {
                    if (item.m_equipped)
                    {
                        __instance.UnequipItem(item, false);
                    }
                    inventory.RemoveItem(item);
                }
            }
        }

        private static void TombPostfix(Player __instance)
        {
            if (_secured == null)
            {
                return;
            }

            var inventory = __instance.GetInventory();
            foreach (var entry in _secured)
            {
                inventory.AddItem(entry.Item);

                // AddItem places items top-first. Put them back where they
                // were so they are still in the secure container next raid.
                // Those cells were emptied in the prefix and AddItem only
                // fills empty cells, so restoring cannot overlap anything.
                if (inventory.GetAllItems().Contains(entry.Item))
                {
                    entry.Item.m_gridPos = entry.Pos;
                }
            }
            _secured = null;
        }

        private static void OnDeathPostfix(Player __instance)
        {
            if (!_diedInRaid)
            {
                return;
            }
            _diedInRaid = false;

            if (Plugin.WipeSkillsOnRaidDeath.Value)
            {
                var skills = __instance.GetSkills();
                foreach (Skills.SkillType type in Enum.GetValues(typeof(Skills.SkillType)))
                {
                    var name = type.ToString();
                    if (name != "None" && name != "All")
                    {
                        skills.ResetSkill(type);
                    }
                }
            }

            RaidTracker.OnKilledInRaid();
        }
    }

    internal static class SecureContainer
    {
        // Read through the fields rather than accessor methods: m_width and
        // m_height are the names other open-source mods use against the game.
        private static readonly FieldInfo WidthField = AccessTools.Field(typeof(Inventory), "m_width");
        private static readonly FieldInfo HeightField = AccessTools.Field(typeof(Inventory), "m_height");

        /// <summary>
        /// The secure container is the last N cells of the bottom inventory
        /// row, counted leftward from the bottom-right corner.
        /// </summary>
        public static bool Contains(Inventory inventory, ItemDrop.ItemData item)
        {
            int slots = Plugin.SecureSlots.Value;
            if (slots <= 0 || WidthField == null || HeightField == null)
            {
                return false;
            }

            int width = (int)WidthField.GetValue(inventory);
            int height = (int)HeightField.GetValue(inventory);
            return item.m_gridPos.y == height - 1
                && item.m_gridPos.x >= width - slots;
        }
    }
}
