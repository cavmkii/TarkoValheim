using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TarkoValheim.Content;
using TarkoValheim.Util;
using UnityEngine;

namespace TarkoValheim.Stash
{
    /// <summary>
    /// Tarkov's stash: inside a hideout, every chest within its radius is one
    /// shared storage. They're shown together, items move between them and the
    /// player freely, and crafting there can use what's in them.
    ///
    /// Carts, ships and graves don't count, nor do chests the player can't
    /// open (another player's private chest, or one someone else has open).
    /// </summary>
    internal static class HideoutStash
    {
        private static readonly HashSet<Container> All = new HashSet<Container>();
        private static readonly MethodInfo CheckAccess = AccessTools.Method(typeof(Container), "CheckAccess", new[] { typeof(long) });
        private static readonly MethodInfo IsInUse = AccessTools.Method(typeof(Container), "IsInUse");

        public static void Patch(Harmony harmony)
        {
            var awake = AccessTools.Method(typeof(Container), "Awake");
            if (awake == null)
            {
                Plugin.Log.LogWarning("Container.Awake not found; the hideout stash only shows the chest you open.");
                return;
            }
            harmony.Patch(awake, postfix: new HarmonyMethod(typeof(HideoutStash), nameof(AwakePostfix)));

            // Belt and braces: make sure the size is right before contents load.
            var load = AccessTools.Method(typeof(Container), "Load");
            if (load != null)
            {
                harmony.Patch(load, prefix: new HarmonyMethod(typeof(HideoutStash), nameof(LoadPrefix)));
            }
        }

        private static void AwakePostfix(Container __instance)
        {
            All.Add(__instance);
            Enlarge(__instance);
        }

        private static void LoadPrefix(Container __instance)
        {
            Enlarge(__instance);
        }

        /// <summary>
        /// Gives a chest's real inventory as many cells as its Tetris grid
        /// shows (rows x ContainerHeightFactor). Every item, even a 1x1 stack,
        /// takes one real cell, so without this a chest ran out of real room
        /// while its grid still looked half empty. Done before the chest loads
        /// its contents, and only ever grows. Graves are left alone: their
        /// size comes from the inventory moved into them.
        /// </summary>
        private static void Enlarge(Container container)
        {
            try
            {
                var inventory = container != null ? container.GetInventory() : null;
                if (inventory == null || container.GetComponent<TombStone>() != null)
                {
                    return;
                }
                int rows = GameAccess.ContainerRows(container);
                int want = rows * System.Math.Max(1, Plugin.ContainerHeightFactor.Value);
                if (rows > 0 && GameAccess.GetHeight(inventory) < want)
                {
                    GameAccess.SetHeight(inventory, want);
                }
            }
            catch (System.Exception e)
            {
                GameAccess.WarnOnce("HideoutStash.Enlarge", e);
            }
        }

        public static bool PlayerInHideout(Player player)
        {
            return player != null && Hideout.IsNear(player.transform.position);
        }

        /// <summary>The stash containers for where the player stands, nearest
        /// first, with <paramref name="open"/> (the chest they opened) on top.
        /// Empty outside a hideout.</summary>
        public static List<Container> Containers(Player player, Container open)
        {
            var list = new List<Container>();
            if (!PlayerInHideout(player))
            {
                return list;
            }

            var hideouts = new List<HideoutMarker>();
            foreach (var marker in HideoutMarker.Active)
            {
                if (marker != null && marker.Covers(player.transform.position))
                {
                    hideouts.Add(marker);
                }
            }

            All.RemoveWhere(c => c == null);
            foreach (var c in All)
            {
                if (c == open || Eligible(player, c, hideouts))
                {
                    list.Add(c);
                }
            }

            var origin = player.transform.position;
            list.Sort((a, b) =>
            {
                if (a == open || b == open)
                {
                    return a == open ? -1 : 1;
                }
                return (a.transform.position - origin).sqrMagnitude.CompareTo((b.transform.position - origin).sqrMagnitude);
            });
            return list;
        }

        private static bool Eligible(Player player, Container c, List<HideoutMarker> hideouts)
        {
            if (!c.isActiveAndEnabled || c.GetInventory() == null)
            {
                return false;
            }
            var nview = c.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid())
            {
                return false;
            }
            // Carts and ships move; graves belong to the dead.
            if (c.GetComponentInParent<Rigidbody>() != null || c.GetComponent<TombStone>() != null)
            {
                return false;
            }

            bool near = false;
            foreach (var h in hideouts)
            {
                if (h.Covers(c.transform.position))
                {
                    near = true;
                    break;
                }
            }
            if (!near)
            {
                return false;
            }

            try
            {
                if (CheckAccess != null && CheckAccess.Invoke(c, new object[] { player.GetPlayerID() }) is bool ok && !ok)
                {
                    return false;
                }
                if (IsInUse != null && IsInUse.Invoke(c, new object[0]) is bool used && used)
                {
                    return false;
                }
            }
            catch (System.Exception e)
            {
                GameAccess.WarnOnce("HideoutStash.Eligible", e);
            }
            return true;
        }

        /// <summary>Takes network ownership so changes made from here are saved.</summary>
        public static void Claim(List<Container> containers)
        {
            foreach (var c in containers)
            {
                var nview = c != null ? c.GetComponent<ZNetView>() : null;
                if (nview != null && nview.IsValid() && !nview.IsOwner())
                {
                    nview.ClaimOwnership();
                }
            }
        }

        public static List<Inventory> Inventories(Player player, Container open)
        {
            var list = new List<Inventory>();
            foreach (var c in Containers(player, open))
            {
                list.Add(c.GetInventory());
            }
            return list;
        }
    }
}
