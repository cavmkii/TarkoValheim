using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TarkoValheim.Util;
using UnityEngine;

namespace TarkoValheim.Stash
{
    /// <summary>
    /// Building with the hammer inside a hideout can use materials from the
    /// stash (every chest in its radius), not just what you carry. What you
    /// carry is used first; the stash makes up the rest. Outside a hideout,
    /// building is untouched.
    ///
    /// Three hooks: Player.HaveRequirements(Piece, mode) so the build menu
    /// and placement see stash materials; Player.ConsumeResources so placing
    /// takes them; InventoryGui.SetupRequirement so the build menu's counts
    /// aren't shown red when the stash covers them.
    /// </summary>
    internal static class StashBuilding
    {
        private static MethodInfo _stationInRange;
        private static bool _consuming;

        public static void Patch(Harmony harmony)
        {
            _stationInRange = AccessTools.Method(typeof(CraftingStation), "HaveBuildStationInRange");

            int found = 0;
            foreach (var m in AccessTools.GetDeclaredMethods(typeof(Player)))
            {
                var ps = m.GetParameters();
                if (m.Name == "HaveRequirements" && m.ReturnType == typeof(bool) && ps.Length == 2 && ps[0].ParameterType == typeof(Piece))
                {
                    harmony.Patch(m, postfix: new HarmonyMethod(typeof(StashBuilding), nameof(HavePostfix)));
                    found++;
                }
                if (m.Name == "ConsumeResources" && ps.Length >= 2 && ps[0].ParameterType == typeof(Piece.Requirement[]))
                {
                    harmony.Patch(m, prefix: new HarmonyMethod(typeof(StashBuilding), nameof(ConsumePrefix)));
                    found++;
                }
            }
            if (found < 2)
            {
                Plugin.Log.LogWarning("Player.HaveRequirements / ConsumeResources not found; building won't use the hideout stash.");
            }

            var setup = AccessTools.Method(typeof(InventoryGui), "SetupRequirement");
            if (setup != null)
            {
                harmony.Patch(setup, postfix: new HarmonyMethod(typeof(StashBuilding), nameof(SetupPostfix)));
            }
        }

        private static bool Active(Player player)
        {
            return Plugin.BuildFromStash.Value && player != null && player == Player.m_localPlayer
                && Reconciler.Active && HideoutStash.PlayerInHideout(player);
        }

        private static int Count(Inventory inventory, string name)
        {
            int n = 0;
            foreach (var item in inventory.GetAllItems())
            {
                if (item.m_shared.m_name == name && !Loc.IsLocked(item))
                {
                    n += item.m_stack;
                }
            }
            return n;
        }

        private static int StashCount(Player player, string name)
        {
            int n = 0;
            foreach (var inventory in HideoutStash.Inventories(player, null))
            {
                n += Count(inventory, name);
            }
            return n;
        }

        // ------------------------------------------------------------------
        // Can I build it?

        private static void HavePostfix(Player __instance, object[] __args, ref bool __result)
        {
            try
            {
                if (__result || !Active(__instance) || !(__args[0] is Piece piece) || piece.m_resources == null)
                {
                    return;
                }
                string mode = __args[1]?.ToString();
                if (mode != "CanBuild" && mode != "CanAlmostBuild")
                {
                    return;
                }

                // A missing build station is a reason the stash can't fix.
                if (piece.m_craftingStation != null && _stationInRange != null
                    && _stationInRange.Invoke(null, new object[] { GameAccess.GetStationName(piece.m_craftingStation), __instance.transform.position }) is bool inRange
                    && !inRange)
                {
                    return;
                }

                var inventory = __instance.GetInventory();
                foreach (var req in piece.m_resources)
                {
                    if (req?.m_resItem == null || req.m_amount <= 0)
                    {
                        continue;
                    }
                    string name = req.m_resItem.m_itemData.m_shared.m_name;
                    int have = Count(inventory, name) + StashCount(__instance, name);
                    if (mode == "CanBuild" ? have < req.m_amount : have <= 0)
                    {
                        return;
                    }
                }
                __result = true;
            }
            catch (System.Exception e)
            {
                GameAccess.WarnOnce("StashBuilding.Have", e);
            }
        }

        // ------------------------------------------------------------------
        // Taking the materials

        private static bool ConsumePrefix(Player __instance, object[] __args)
        {
            try
            {
                if (_consuming || !Active(__instance) || !(__args[0] is Piece.Requirement[] requirements))
                {
                    return true;
                }
                int quality = __args.Length > 1 && __args[1] is int q ? q : 1;
                int multiplier = __args.Length > 3 && __args[3] is int mul ? Mathf.Max(1, mul) : 1;
                var station = __instance.GetCurrentCraftingStation();
                var inventory = __instance.GetInventory();

                // What counts, and how much of it.
                var needs = new List<KeyValuePair<string, int>>();
                bool playerHasAll = true;
                foreach (var req in requirements)
                {
                    if (req?.m_resItem == null || !GameAccess.RequirementApplies(req, station))
                    {
                        continue;
                    }
                    int amount = req.GetAmount(quality) * multiplier;
                    if (amount <= 0)
                    {
                        continue;
                    }
                    string name = req.m_resItem.m_itemData.m_shared.m_name;
                    needs.Add(new KeyValuePair<string, int>(name, amount));
                    playerHasAll &= Count(inventory, name) >= amount;
                }

                // Carrying everything: Valheim's own code does the job.
                if (playerHasAll)
                {
                    return true;
                }

                var stash = HideoutStash.Containers(__instance, null);
                HideoutStash.Claim(stash);
                var sources = new List<Inventory> { inventory };
                foreach (var c in stash)
                {
                    sources.Add(c.GetInventory());
                }

                _consuming = true;
                try
                {
                    foreach (var need in needs)
                    {
                        Take(__instance, sources, need.Key, need.Value);
                    }
                }
                finally
                {
                    _consuming = false;
                }
                return false;
            }
            catch (System.Exception e)
            {
                GameAccess.WarnOnce("StashBuilding.Consume", e);
                return true;
            }
        }

        private static void Take(Player player, List<Inventory> sources, string name, int amount)
        {
            foreach (var inventory in sources)
            {
                if (amount <= 0)
                {
                    return;
                }
                var matches = inventory.GetAllItems().FindAll(i => i.m_shared.m_name == name && !Loc.IsLocked(i));
                matches.Sort((a, b) => a.m_equipped != b.m_equipped ? (a.m_equipped ? 1 : -1) : a.m_quality.CompareTo(b.m_quality));
                bool changed = false;
                foreach (var item in matches)
                {
                    if (amount <= 0)
                    {
                        break;
                    }
                    int n = Mathf.Min(amount, item.m_stack);
                    if (item.m_equipped && n >= item.m_stack)
                    {
                        player.UnequipItem(item, false);
                    }
                    inventory.RemoveItem(item, n);
                    amount -= n;
                    changed = true;
                }
                if (changed && inventory != player.GetInventory())
                {
                    GameAccess.Changed(inventory);
                }
            }
        }

        // ------------------------------------------------------------------
        // Build menu counts

        private static void SetupPostfix(object[] __args)
        {
            try
            {
                Transform root = null;
                Piece.Requirement req = null;
                Player player = null;
                bool craft = false;
                foreach (var arg in __args)
                {
                    if (arg is Transform t) root = t;
                    else if (arg is Piece.Requirement r) req = r;
                    else if (arg is Player p) player = p;
                    else if (arg is bool b) craft = b;
                }
                if (craft || root == null || req?.m_resItem == null || req.m_amount <= 0 || !Active(player))
                {
                    return;
                }

                string name = req.m_resItem.m_itemData.m_shared.m_name;
                int carried = Count(player.GetInventory(), name);
                if (carried >= req.m_amount || carried + StashCount(player, name) < req.m_amount)
                {
                    return;
                }

                // Enough with the stash: show the count in the normal colour.
                var label = root.Find("res_amount");
                if (label == null)
                {
                    return;
                }
                foreach (var c in label.GetComponents<Component>())
                {
                    var colorProp = c.GetType().GetProperty("color");
                    if (colorProp != null && colorProp.PropertyType == typeof(Color) && colorProp.CanWrite)
                    {
                        colorProp.SetValue(c, Color.white, null);
                    }
                }
            }
            catch (System.Exception e)
            {
                GameAccess.WarnOnce("StashBuilding.Setup", e);
            }
        }
    }
}
