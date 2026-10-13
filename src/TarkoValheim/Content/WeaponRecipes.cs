using System.Collections.Generic;
using TarkoValheim.Modding;
using UnityEngine;

namespace TarkoValheim.Content
{
    /// <summary>
    /// Weapons and armor are assembled from parts. Each vanilla recipe that uses
    /// metal is rewritten to take its standard build instead of raw materials:
    /// a head and guard of its metal, a grip of its wood, a wrap of its hide,
    /// one of each its family has. Anything else the recipe needed (eitr,
    /// trophies, black cores...) is still needed.
    ///
    /// The standard build is also the baseline for stats: a weapon built from
    /// its standard parts at level 1 is exactly the vanilla weapon; better or
    /// different parts move it from there. Weapons with no metal (wood,
    /// flint, bone) keep their vanilla recipes and have no baseline.
    /// </summary>
    internal static class WeaponRecipes
    {
        private static readonly string[][] MetalTiers =
        {
            new[] { "Bronze" }, new[] { "Iron" }, new[] { "Silver" }, new[] { "BlackMetal" }, new[] { "FlametalNew", "Flametal" }
        };
        private static readonly string[] WoodTiers = { "Wood", "FineWood", "RoundLog", "ElderBark", "YggdrasilWood" };
        private static readonly string[] HideTiers = { "LeatherScraps", "DeerHide", "TrollHide", "WolfPelt", "LoxPelt" };

        private static readonly HashSet<string> Raw = BuildRaw();

        private static readonly Dictionary<string, Dictionary<PartSlot, PartSpec>> Stock =
            new Dictionary<string, Dictionary<PartSlot, PartSpec>>();

        /// <summary>The standard parts a weapon is built from, or null.</summary>
        public static Dictionary<PartSlot, PartSpec> StockFor(string prefabName)
        {
            return prefabName != null && Stock.TryGetValue(prefabName, out var s) ? s : null;
        }

        private static HashSet<string> BuildRaw()
        {
            var set = new HashSet<string>(WoodTiers);
            set.UnionWith(HideTiers);
            foreach (var tier in MetalTiers)
            {
                set.UnionWith(tier);
            }
            return set;
        }

        /// <summary>Runs whenever the ObjectDB is (re)built. Recipe objects are
        /// shared assets, so a recipe already rewritten is recognised and left
        /// alone.</summary>
        public static void Apply()
        {
            var db = ObjectDB.instance;
            if (db == null || db.m_recipes == null)
            {
                return;
            }

            int rewritten = 0;
            foreach (var recipe in db.m_recipes)
            {
                if (recipe == null || recipe.m_item == null)
                {
                    continue;
                }
                var item = recipe.m_item.m_itemData;
                var family = WeaponMods.FamilyOf(item);
                if (family == WeaponFamily.None)
                {
                    continue;
                }

                string prefab = recipe.m_item.name;
                bool alreadyParts = UsesParts(recipe);
                if (!alreadyParts)
                {
                    var stock = StockBuild(recipe, family);
                    if (stock == null)
                    {
                        continue;
                    }
                    Stock[prefab] = stock;
                    if (Plugin.PartRecipes.Value && Rewrite(db, recipe, stock))
                    {
                        rewritten++;
                    }
                }
            }

            if (rewritten > 0)
            {
                Plugin.Log.LogInfo($"Rewrote {rewritten} weapon recipes to use parts.");
            }
        }

        private static bool UsesParts(Recipe recipe)
        {
            foreach (var req in recipe.m_resources ?? new Piece.Requirement[0])
            {
                if (req?.m_resItem != null && WeaponParts.Find(req.m_resItem.name) != null)
                {
                    return true;
                }
            }
            return false;
        }

        // The weapon's tier is its best metal; wood and hide likewise. No
        // metal, no standard build.
        private static Dictionary<PartSlot, PartSpec> StockBuild(Recipe recipe, WeaponFamily family)
        {
            int metal = 0, wood = 0, hide = 0;
            foreach (var req in recipe.m_resources ?? new Piece.Requirement[0])
            {
                string name = req?.m_resItem != null ? req.m_resItem.name : null;
                if (name == null)
                {
                    continue;
                }
                for (int t = 0; t < MetalTiers.Length; t++)
                {
                    if (System.Array.IndexOf(MetalTiers[t], name) >= 0)
                    {
                        metal = Mathf.Max(metal, t + 1);
                    }
                }
                wood = Mathf.Max(wood, System.Array.IndexOf(WoodTiers, name) + 1);
                hide = Mathf.Max(hide, System.Array.IndexOf(HideTiers, name) + 1);
            }
            if (metal == 0)
            {
                return null;
            }

            var stock = new Dictionary<PartSlot, PartSpec>();
            foreach (var slot in WeaponParts.SlotsOf(family))
            {
                PartSpec part;
                switch (slot)
                {
                    case PartSlot.Head:
                        part = WeaponParts.FindBy(PartSlot.Head, WeaponParts.HeadFor(family), metal);
                        break;
                    case PartSlot.Guard:
                        part = WeaponParts.FindBy(PartSlot.Guard, HeadKind.None, metal);
                        break;
                    case PartSlot.Plate:
                        part = WeaponParts.FindBy(PartSlot.Plate, HeadKind.None, metal);
                        break;
                    case PartSlot.Padding:
                        part = WeaponParts.FindBy(PartSlot.Padding, HeadKind.None, Mathf.Max(1, hide));
                        break;
                    case PartSlot.Handle:
                        part = WeaponParts.FindBy(PartSlot.Handle, HeadKind.None, Mathf.Max(1, wood));
                        break;
                    default:
                        part = WeaponParts.FindBy(PartSlot.Wrap, HeadKind.None, Mathf.Max(1, hide));
                        break;
                }
                if (part != null)
                {
                    stock[slot] = part;
                }
            }
            return stock.Count > 0 ? stock : null;
        }

        // New ingredients: one of each standard part, plus whatever the
        // recipe needed that isn't plain metal, wood or hide.
        private static bool Rewrite(ObjectDB db, Recipe recipe, Dictionary<PartSlot, PartSpec> stock)
        {
            var list = new List<Piece.Requirement>();
            foreach (var part in stock.Values)
            {
                var prefab = db.GetItemPrefab(part.Id);
                var drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (drop == null)
                {
                    return false;
                }
                list.Add(new Piece.Requirement { m_resItem = drop, m_amount = 1, m_amountPerLevel = 0, m_recover = true });
            }
            foreach (var req in recipe.m_resources ?? new Piece.Requirement[0])
            {
                if (req?.m_resItem != null && !Raw.Contains(req.m_resItem.name))
                {
                    list.Add(req);
                }
            }
            recipe.m_resources = list.ToArray();
            return true;
        }
    }
}
