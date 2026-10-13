using System.Collections.Generic;
using TarkoValheim.Content;
using TarkoValheim.Modding;
using TarkoValheim.Stash;
using TarkoValheim.Util;
using UnityEngine;

namespace TarkoValheim.Crafting
{
    /// <summary>
    /// Right-click > Dismantle, in a hideout: breaks a crafted item back into
    /// what it was made from and puts that in the stash (what doesn't fit goes
    /// to you, then to the ground). Upgrade costs come back too. A weapon
    /// built from parts gives back its actual parts, at their levels. Idols
    /// and other upgrader resources are not returned.
    /// </summary>
    internal static class Dismantle
    {
        /// <summary>How many of the item one dismantle takes (a recipe that
        /// makes 20 arrows needs 20), or 0 if it can't be dismantled.</summary>
        public static int BatchSize(Player player, ItemDrop.ItemData item)
        {
            if (player == null || item == null || Loc.IsLocked(item) || !HideoutStash.PlayerInHideout(player))
            {
                return 0;
            }
            // Worn rigs, packs and pouches carry other items; take them off first.
            if (Reconciler.Current != null && Reconciler.Current.TryGetSlotOf(item, out var slot) && Loadout.GridOf(slot) != null)
            {
                return 0;
            }
            var recipe = ObjectDB.instance != null ? ObjectDB.instance.GetRecipe(item) : null;
            if (recipe == null || recipe.m_resources == null || recipe.m_resources.Length == 0)
            {
                return 0;
            }
            int batch = Mathf.Max(1, recipe.m_amount);
            return item.m_stack >= batch ? batch : 0;
        }

        public static bool Run(Player player, ItemDrop.ItemData item, Inventory from)
        {
            int batch = BatchSize(player, item);
            if (batch == 0)
            {
                return false;
            }
            var recipe = ObjectDB.instance.GetRecipe(item);

            // What comes back: prefab name -> (amount, level).
            var back = new List<KeyValuePair<string, int>>();
            var levels = new List<int>();

            var parts = WeaponMods.IsModular(item) ? WeaponMods.AllInstalled(item) : new List<Installed>();
            foreach (var p in parts)
            {
                back.Add(new KeyValuePair<string, int>(p.Part.Id, 1));
                levels.Add(p.Level);
            }

            int quality = Mathf.Max(1, item.m_quality);
            foreach (var req in recipe.m_resources)
            {
                if (req?.m_resItem == null || IsUpgrader(req))
                {
                    continue;
                }
                // Parts in the recipe are already covered by the ones fitted.
                if (parts.Count > 0 && WeaponParts.Find(req.m_resItem.name) != null)
                {
                    continue;
                }
                int amount = 0;
                for (int q = 1; q <= quality; q++)
                {
                    amount += req.GetAmount(q);
                }
                if (amount > 0)
                {
                    back.Add(new KeyValuePair<string, int>(req.m_resItem.name, amount));
                    levels.Add(1);
                }
            }

            if (back.Count == 0)
            {
                return false;
            }

            if (item.m_equipped)
            {
                player.UnequipItem(item, false);
            }
            from.RemoveItem(item, batch);
            GameAccess.Changed(from);

            var stash = HideoutStash.Containers(player, null);
            HideoutStash.Claim(stash);
            for (int i = 0; i < back.Count; i++)
            {
                Give(player, stash, back[i].Key, back[i].Value, levels[i]);
            }
            return true;
        }

        private static bool IsUpgrader(Piece.Requirement req)
        {
            // Idols only count at an upgrader station, so with none they don't apply.
            return !GameAccess.RequirementApplies(req, null);
        }

        private static void Give(Player player, List<Container> stash, string prefab, int amount, int level)
        {
            while (amount > 0)
            {
                var probe = Reconciler.CreateItem(prefab, 1);
                if (probe == null)
                {
                    return;
                }
                int n = Mathf.Min(amount, Mathf.Max(1, probe.m_shared.m_maxStackSize));
                var item = Reconciler.CreateItem(prefab, n);
                item.m_quality = level;
                item.m_durability = item.GetMaxDurability();
                amount -= n;

                bool placed = false;
                foreach (var c in stash)
                {
                    var inventory = c.GetInventory();
                    if (ContainerLayout.Build(inventory).Grid.FindSpot(item, out _, out _, out _) && inventory.AddItem(item))
                    {
                        GameAccess.Changed(inventory);
                        placed = true;
                        break;
                    }
                }
                if (!placed && Reconciler.CanFit(player, item, n))
                {
                    placed = player.GetInventory().AddItem(item);
                }
                if (!placed)
                {
                    var pos = player.transform.position + player.transform.forward + Vector3.up;
                    ItemDrop.DropItem(item, n, pos, Quaternion.identity);
                    StashOps.LastError = "$tv_msg_dismantle_dropped";
                }
            }
        }
    }
}
