using System.Collections.Generic;
using TarkoValheim.Content;
using TarkoValheim.Modding;
using TarkoValheim.Stash;
using TarkoValheim.Util;
using UnityEngine;

namespace TarkoValheim.Crafting
{
    /// <summary>One line in the crafting list: a new item, or an upgrade of
    /// one the player carries.</summary>
    internal sealed class CraftEntry
    {
        public Recipe Recipe;
        public ItemDrop.ItemData Upgrade;
        public int Quality;
        public int StationLevelNeeded;
        public bool StationOk;
        public bool HaveAll;

        // Hideout recipes locked behind a module level (Workshop, Medstation).
        public bool Gated;
        public ModuleId GateModule;
        public int GateLevel;
        public bool GateOk = true;

        public bool CanCraft => StationOk && HaveAll && GateOk;
        public ItemDrop.ItemData Item => Recipe.m_item.m_itemData;
    }

    /// <summary>
    /// Crafting, upgrading and repairing without Valheim's crafting panel,
    /// which is hidden under the Tarkov screen. Recipes, station levels and
    /// costs are Valheim's own; only the screen is new. Crafting is instant
    /// (no progress bar).
    ///
    /// In a hideout, materials in the stash (every chest in its radius)
    /// count and are used, like Tarkov's hideout crafting. Weapons and
    /// shields don't level up here: they get better through their parts,
    /// and the parts level up instead.
    /// </summary>
    internal static class CraftService
    {
        public static List<CraftEntry> Entries(Player player)
        {
            var list = new List<CraftEntry>();
            var recipes = new List<Recipe>();
            player.GetAvailableRecipes(ref recipes);

            var station = player.GetCurrentCraftingStation();
            int level = GameAccess.StationLevel(station);
            var inventory = player.GetInventory();

            foreach (var recipe in recipes)
            {
                if (recipe == null || recipe.m_item == null)
                {
                    continue;
                }

                list.Add(Make(player, recipe, null, 1, level));

                var shared = recipe.m_item.m_itemData.m_shared;
                if (shared.m_maxQuality <= 1 || WeaponMods.IsModular(recipe.m_item.m_itemData))
                {
                    continue;
                }
                foreach (var item in inventory.GetAllItems())
                {
                    if (item.m_shared.m_name == shared.m_name && item.m_quality < shared.m_maxQuality)
                    {
                        list.Add(Make(player, recipe, item, item.m_quality + 1, level));
                    }
                }
            }

            // Craftable first, then what only needs a better station, then the rest.
            list.Sort((a, b) =>
            {
                int ra = a.CanCraft ? 0 : a.HaveAll ? 1 : 2;
                int rb = b.CanCraft ? 0 : b.HaveAll ? 1 : 2;
                if (ra != rb)
                {
                    return ra.CompareTo(rb);
                }
                if ((a.Upgrade != null) != (b.Upgrade != null))
                {
                    return a.Upgrade != null ? 1 : -1;
                }
                return string.CompareOrdinal(UiName(a), UiName(b));
            });
            return list;
        }

        private static string UiName(CraftEntry e)
        {
            return Localization.instance != null ? Localization.instance.Localize(e.Item.m_shared.m_name) : e.Item.m_shared.m_name;
        }

        private static CraftEntry Make(Player player, Recipe recipe, ItemDrop.ItemData upgrade, int quality, int stationLevel)
        {
            int needed = recipe.m_craftingStation != null ? Mathf.Max(1, recipe.m_minStationLevel) + quality - 1 : 0;
            var entry = new CraftEntry
            {
                Recipe = recipe,
                Upgrade = upgrade,
                Quality = quality,
                StationLevelNeeded = needed,
                StationOk = recipe.m_craftingStation == null || stationLevel >= needed
            };
            entry.HaveAll = HaveResources(player, recipe, quality);

            if (upgrade == null && HideoutModules.TryGate(recipe.m_item.name, out var module, out int gateLevel))
            {
                var bench = HideoutModules.BenchOf(player.GetCurrentCraftingStation());
                int have = bench != null ? bench.Level(module) : HideoutModules.LevelAt(player.transform.position, module);
                entry.Gated = true;
                entry.GateModule = module;
                entry.GateLevel = gateLevel;
                entry.GateOk = have >= gateLevel;
            }
            return entry;
        }

        /// <summary>Where materials come from: the player, then the stash if in a hideout.</summary>
        public static List<Inventory> Sources(Player player)
        {
            // Rebuilt at most once a frame: the recipe list asks for every ingredient.
            if (_sources == null || _sourcesFrame != Time.frameCount)
            {
                _sources = new List<Inventory> { player.GetInventory() };
                _sources.AddRange(HideoutStash.Inventories(player, null));
                _sourcesFrame = Time.frameCount;
            }
            return _sources;
        }

        private static List<Inventory> _sources;
        private static int _sourcesFrame = -1;

        public static int Count(Player player, ItemDrop resource)
        {
            return resource == null ? 0 : Count(Sources(player), resource.m_itemData.m_shared.m_name);
        }

        /// <summary>How many of an item (by prefab name) the player and stash hold.</summary>
        public static int CountPrefab(Player player, string prefab)
        {
            var go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            var drop = go != null ? go.GetComponent<ItemDrop>() : null;
            return drop == null ? 0 : Count(Sources(player), drop.m_itemData.m_shared.m_name);
        }

        /// <summary>Takes items by prefab name: carried ones first, then the stash.</summary>
        public static void TakePrefab(Player player, string prefab, int amount)
        {
            var go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            var drop = go != null ? go.GetComponent<ItemDrop>() : null;
            if (drop == null)
            {
                return;
            }
            string name = drop.m_itemData.m_shared.m_name;
            HideoutStash.Claim(HideoutStash.Containers(player, null));
            foreach (var inventory in Sources(player))
            {
                var matches = inventory.GetAllItems().FindAll(i => i.m_shared.m_name == name && !Loc.IsLocked(i) && !i.m_equipped);
                matches.Sort((a, b) => a.m_quality.CompareTo(b.m_quality));
                foreach (var item in matches)
                {
                    if (amount <= 0)
                    {
                        break;
                    }
                    int n = Mathf.Min(amount, item.m_stack);
                    inventory.RemoveItem(item, n);
                    amount -= n;
                }
                GameAccess.Changed(inventory);
                if (amount <= 0)
                {
                    return;
                }
            }
        }

        private static int Count(List<Inventory> sources, string name)
        {
            int n = 0;
            foreach (var inventory in sources)
            {
                foreach (var item in inventory.GetAllItems())
                {
                    if (item.m_shared.m_name == name && !Loc.IsLocked(item))
                    {
                        n += item.m_stack;
                    }
                }
            }
            return n;
        }

        /// <summary>The ingredients that count where the player is standing
        /// (idols only at an upgrader station, see GameAccess.RequirementApplies).</summary>
        public static List<Piece.Requirement> Requirements(Player player, Recipe recipe)
        {
            var station = player.GetCurrentCraftingStation();
            var list = new List<Piece.Requirement>();
            foreach (var req in recipe.m_resources ?? new Piece.Requirement[0])
            {
                if (req != null && GameAccess.RequirementApplies(req, station))
                {
                    list.Add(req);
                }
            }
            return list;
        }

        private static bool HaveResources(Player player, Recipe recipe, int quality)
        {
            bool onlyOne = GameAccess.RequireOnlyOneIngredient(recipe);
            bool any = false;
            foreach (var req in Requirements(player, recipe))
            {
                int need = req.GetAmount(quality);
                if (req.m_resItem == null || need <= 0)
                {
                    continue;
                }
                bool have = Count(player, req.m_resItem) >= need;
                if (onlyOne && have)
                {
                    return true;
                }
                if (!onlyOne && !have)
                {
                    return false;
                }
                any |= have;
            }
            return !onlyOne || any;
        }

        /// <summary>Crafts or upgrades. On failure, StashOps.LastError says why.</summary>
        public static bool Craft(Player player, CraftEntry entry)
        {
            StashOps.LastError = null;
            if (!entry.StationOk)
            {
                StashOps.LastError = "$tv_err_station_level";
                return false;
            }
            if (!HaveResources(player, entry.Recipe, entry.Quality))
            {
                StashOps.LastError = "$tv_err_missing_resources";
                return false;
            }

            var inventory = player.GetInventory();
            if (entry.Upgrade != null)
            {
                if (!inventory.GetAllItems().Contains(entry.Upgrade))
                {
                    return false;
                }
                Reconciler.Batch(player, () =>
                {
                    Consume(player, entry.Recipe, entry.Quality, entry.Upgrade);
                    entry.Upgrade.m_quality = entry.Quality;
                    entry.Upgrade.m_durability = entry.Upgrade.GetMaxDurability();
                    GameAccess.Changed(inventory);
                });
                player.RaiseSkill(Skills.SkillType.Crafting, 1f);
                return true;
            }

            int amount = Mathf.Max(1, entry.Recipe.m_amount);
            var item = Reconciler.CreateItem(entry.Recipe.m_item.name, amount);
            if (item == null)
            {
                return false;
            }
            item.m_quality = 1;
            item.m_durability = item.GetMaxDurability();
            item.m_crafterID = player.GetPlayerID();
            item.m_crafterName = player.GetPlayerName();

            bool ok = false;
            Reconciler.Batch(player, () =>
            {
                var parts = Consume(player, entry.Recipe, 1, null);

                // A weapon assembled from parts keeps them, at their levels.
                if (parts.Count > 0 && WeaponMods.IsModular(item))
                {
                    WeaponMods.SetBuilt(item, parts);
                    item.m_durability = item.GetMaxDurability();
                }

                if (Reconciler.CanFit(player, item, amount))
                {
                    ok = inventory.AddItem(item);
                }
                if (!ok)
                {
                    // No room: it lands at the player's feet rather than vanishing.
                    var pos = player.transform.position + player.transform.forward + Vector3.up;
                    ItemDrop.DropItem(item, amount, pos, Quaternion.identity);
                    StashOps.LastError = "$tv_msg_crafted_dropped";
                    ok = true;
                }
            });
            player.RaiseSkill(Skills.SkillType.Crafting, 1f);
            return ok;
        }

        /// <summary>Takes the recipe's ingredients. Returns the weapon parts
        /// among them, with their levels.</summary>
        private static List<Installed> Consume(Player player, Recipe recipe, int quality, ItemDrop.ItemData keep)
        {
            var parts = new List<Installed>();
            bool onlyOne = GameAccess.RequireOnlyOneIngredient(recipe);
            var sources = Sources(player);
            HideoutStash.Claim(HideoutStash.Containers(player, null));
            foreach (var req in Requirements(player, recipe))
            {
                int need = req.GetAmount(quality);
                if (req.m_resItem == null || need <= 0)
                {
                    continue;
                }
                if (onlyOne && Count(player, req.m_resItem) < need)
                {
                    continue;
                }

                string name = req.m_resItem.m_itemData.m_shared.m_name;
                // What the player carries goes first, then the stash.
                foreach (var inventory in sources)
                {
                    var matches = inventory.GetAllItems().FindAll(i => i.m_shared.m_name == name && i != keep && !Loc.IsLocked(i));
                    var partSpec = WeaponParts.Find(req.m_resItem.name);
                    // Unequipped first; then lowest quality, except weapon
                    // parts, where the best one goes into the weapon.
                    matches.Sort((a, b) => a.m_equipped != b.m_equipped ? (a.m_equipped ? 1 : -1)
                        : partSpec != null ? b.m_quality.CompareTo(a.m_quality) : a.m_quality.CompareTo(b.m_quality));
                    foreach (var item in matches)
                    {
                        if (need <= 0)
                        {
                            break;
                        }
                        int n = Mathf.Min(need, item.m_stack);
                        if (item.m_equipped && n >= item.m_stack)
                        {
                            player.UnequipItem(item, false);
                        }
                        if (partSpec != null)
                        {
                            parts.Add(new Installed { Part = partSpec, Level = Mathf.Clamp(item.m_quality, 1, WeaponParts.MaxLevel) });
                        }
                        inventory.RemoveItem(item, n);
                        need -= n;
                    }
                    if (inventory != player.GetInventory())
                    {
                        GameAccess.Changed(inventory);
                    }
                }
                if (onlyOne)
                {
                    break;
                }
            }
            return parts;
        }

        // ------------------------------------------------------------------
        // Repair

        public static List<ItemDrop.ItemData> Repairable(Player player)
        {
            var list = new List<ItemDrop.ItemData>();
            var station = player.GetCurrentCraftingStation();
            if (station == null)
            {
                return list;
            }

            foreach (var item in player.GetInventory().GetAllItems())
            {
                if (!item.m_shared.m_useDurability || item.m_durability >= item.GetMaxDurability())
                {
                    continue;
                }
                bool? vanilla = GameAccess.CanRepair(InventoryGui.instance, item);
                if (vanilla ?? CanRepairHere(item, station))
                {
                    list.Add(item);
                }
            }
            return list;
        }

        // Fallback for Valheim's own CanRepair: the item's recipe has to be
        // made or repaired at this kind of station, at a high enough level.
        private static bool CanRepairHere(ItemDrop.ItemData item, CraftingStation station)
        {
            var recipe = ObjectDB.instance != null ? ObjectDB.instance.GetRecipe(item) : null;
            if (recipe == null)
            {
                return false;
            }
            string here = GameAccess.GetStationName(station);
            bool type = (recipe.m_craftingStation != null && GameAccess.GetStationName(recipe.m_craftingStation) == here)
                || (recipe.m_repairStation != null && GameAccess.GetStationName(recipe.m_repairStation) == here);
            return type && Mathf.Min(GameAccess.StationLevel(station), 4) >= item.m_quality;
        }

        public static int RepairAll(Player player)
        {
            var items = Repairable(player);
            foreach (var item in items)
            {
                item.m_durability = item.GetMaxDurability();
            }
            if (items.Count > 0)
            {
                GameAccess.Changed(player.GetInventory());
            }
            return items.Count;
        }
    }
}
