using HarmonyLib;
using UnityEngine;

namespace TarkoValheim.Stash
{
    /// <summary>
    /// Valheim-side plumbing for the Tetris inventory.
    ///
    /// The player's real Inventory is widened to 8 x 16 cells so it can hold
    /// everything the Tetris grids can (vanilla is 8 x 4). Row 0 stays the
    /// hotbar. Vanilla resets the height in a few places (Haldor's row
    /// purchases, DropInvalidItems, graves); each is patched to keep it. This
    /// stays on even with the Tetris UI disabled, so items saved in rows 4-15
    /// are never thrown on the ground.
    /// </summary>
    internal static class InventoryHooks
    {
        public const int InternalHeight = 16;

        public static void Patch(Harmony harmony)
        {
            Hook(harmony, typeof(Player), "Awake", postfix: nameof(KeepHeight));
            Hook(harmony, typeof(Player), "OnSpawned", postfix: nameof(OnSpawned));
            Hook(harmony, typeof(Player), "SetInventorySize", postfix: nameof(KeepHeight));
            Hook(harmony, typeof(Player), "Update", postfix: nameof(KeepHeight));
            Hook(harmony, typeof(Humanoid), "DropInvalidItems", prefix: nameof(KeepHeightHumanoid));
            Hook(harmony, typeof(Inventory), "MoveInventoryToGrave", prefix: nameof(GravePrefix));
            Hook(harmony, typeof(Inventory), "Changed", postfix: nameof(ChangedPostfix));

            var gate = new HarmonyMethod(typeof(InventoryHooks), nameof(CanAddItemPostfix));
            int gated = 0;
            foreach (var method in AccessTools.GetDeclaredMethods(typeof(Inventory)))
            {
                if (method.Name == "CanAddItem")
                {
                    harmony.Patch(method, postfix: gate);
                    gated++;
                }
            }
            if (gated == 0)
            {
                Plugin.Log.LogError("Inventory.CanAddItem not found; pickups ignore Tetris space.");
            }
        }

        private static void Hook(Harmony harmony, System.Type type, string method, string prefix = null, string postfix = null)
        {
            var target = AccessTools.Method(type, method);
            if (target == null)
            {
                Plugin.Log.LogWarning($"{type.Name}.{method} not found; skipping its inventory hook.");
                return;
            }

            harmony.Patch(target,
                prefix: prefix != null ? new HarmonyMethod(typeof(InventoryHooks), prefix) : null,
                postfix: postfix != null ? new HarmonyMethod(typeof(InventoryHooks), postfix) : null);
        }

        private static void KeepHeight(Player __instance)
        {
            var inventory = __instance.GetInventory();
            if (inventory != null && inventory.m_height < InternalHeight)
            {
                inventory.m_height = InternalHeight;
            }
        }

        private static void KeepHeightHumanoid(Humanoid __instance)
        {
            if (__instance is Player player)
            {
                KeepHeight(player);
            }
        }

        private static void OnSpawned(Player __instance)
        {
            KeepHeight(__instance);
            if (__instance == Player.m_localPlayer)
            {
                Reconciler.OnSpawned(__instance);
                Reconciler.Run(__instance);
            }
        }

        private static void GravePrefix(Inventory original)
        {
            if (original != null && original.m_height < InternalHeight && StashOps.IsPlayerInventory(original))
            {
                original.m_height = InternalHeight;
            }
        }

        private static void ChangedPostfix(Inventory __instance)
        {
            if (!Reconciler.Busy && StashOps.IsPlayerInventory(__instance))
            {
                Reconciler.Run(Player.m_localPlayer);
            }
        }

        // Covers every CanAddItem overload: the first argument is either the
        // item or its prefab, the second (when present) the stack size.
        private static void CanAddItemPostfix(Inventory __instance, object[] __args, ref bool __result)
        {
            if (!__result || Reconciler.Busy || !Reconciler.Active || !StashOps.IsPlayerInventory(__instance)
                || __args == null || __args.Length == 0)
            {
                return;
            }

            var item = __args[0] as ItemDrop.ItemData;
            if (item == null && __args[0] is GameObject prefab)
            {
                var drop = prefab.GetComponent<ItemDrop>();
                item = drop != null ? drop.m_itemData : null;
            }
            if (item == null)
            {
                return;
            }

            int stack = __args.Length > 1 && __args[1] is int s && s > 0 ? s : item.m_stack;
            __result = Reconciler.CanFit(Player.m_localPlayer, item, stack);
        }
    }
}
