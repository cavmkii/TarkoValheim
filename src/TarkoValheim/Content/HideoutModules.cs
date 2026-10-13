using System.Collections.Generic;
using TarkoValheim.Util;
using UnityEngine;

namespace TarkoValheim.Content
{
    internal enum ModuleId
    {
        Workshop,
        Medstation,
        Perimeter,
        Intel
    }

    internal sealed class ModuleLevel
    {
        public KeyValuePair<string, int>[] Cost;
        public KeyValuePair<ModuleId, int>[] Needs = new KeyValuePair<ModuleId, int>[0];
        public string Effect;
    }

    internal sealed class ModuleSpec
    {
        public ModuleId Id;
        public ModuleLevel[] Levels;
        public int Max => Levels.Length;
    }

    /// <summary>
    /// Tarkov's hideout modules, built and upgraded at the Hideout bench.
    /// Each hideout keeps its own levels (on the bench itself, so they sync
    /// and save with the world), and the bonuses apply inside that hideout's
    /// radius:
    ///
    /// - Workshop: faster crafting in the hideout; unlocks better rigs,
    ///   backpacks and secure containers.
    /// - Medstation: unlocks better meds; in the hideout bleeds stop, then
    ///   fractures mend, then health regenerates.
    /// - Perimeter: a bigger hideout radius.
    /// - Intel center: a longer calm period at the start of each raid, and
    ///   warnings when a hunting party is sent. It's remembered from the last
    ///   hideout you were in, since home isn't loaded while you're out.
    /// </summary>
    internal static class HideoutModules
    {
        internal static readonly ModuleSpec[] All =
        {
            new ModuleSpec
            {
                Id = ModuleId.Workshop,
                Levels = new[]
                {
                    L("$tv_mod_workshop_1", C("Wood", 20), C("Stone", 10), C("Flint", 10)),
                    L("$tv_mod_workshop_2", C("FineWood", 10), C("Bronze", 10), C("BronzeNails", 20)),
                    L("$tv_mod_workshop_3", C("RoundLog", 10), C("Iron", 15), C("IronNails", 20)),
                }
            },
            new ModuleSpec
            {
                Id = ModuleId.Medstation,
                Levels = new[]
                {
                    L("$tv_mod_medstation_1", C("Wood", 10), C("LeatherScraps", 10), C("Resin", 10)),
                    L("$tv_mod_medstation_2", new[] { N(ModuleId.Workshop, 1) }, C("FineWood", 10), C("Bronze", 5), C("Honey", 10)),
                    L("$tv_mod_medstation_3", new[] { N(ModuleId.Workshop, 2) }, C("Iron", 10), C("Guck", 10), C("Honey", 20)),
                }
            },
            new ModuleSpec
            {
                Id = ModuleId.Perimeter,
                Levels = new[]
                {
                    L("$tv_mod_perimeter_1", C("Wood", 30), C("Stone", 20)),
                    L("$tv_mod_perimeter_2", new[] { N(ModuleId.Workshop, 1) }, C("Wood", 30), C("Stone", 40), C("Bronze", 5)),
                    L("$tv_mod_perimeter_3", new[] { N(ModuleId.Workshop, 2) }, C("Stone", 60), C("Iron", 15)),
                }
            },
            new ModuleSpec
            {
                Id = ModuleId.Intel,
                Levels = new[]
                {
                    L("$tv_mod_intel_1", new[] { N(ModuleId.Perimeter, 1) }, C("Wood", 10), C("Resin", 10), C("Feathers", 10)),
                    L("$tv_mod_intel_2", new[] { N(ModuleId.Workshop, 2) }, C("FineWood", 10), C("Bronze", 5), C("Coal", 20)),
                }
            },
        };

        /// <summary>Recipes gated behind a module level, by prefab name.</summary>
        private static readonly Dictionary<string, KeyValuePair<ModuleId, int>> Gates = new Dictionary<string, KeyValuePair<ModuleId, int>>
        {
            { "TV_Rig_Carrier", N(ModuleId.Workshop, 2) },
            { "TV_Pack_Scav", N(ModuleId.Workshop, 1) },
            { "TV_Pack_Pilgrim", N(ModuleId.Workshop, 2) },
            { "TV_Secure_Beta", N(ModuleId.Workshop, 1) },
            { "TV_Secure_Gamma", N(ModuleId.Workshop, 2) },
            { "TV_Secure_Kappa", N(ModuleId.Workshop, 3) },
            { "TV_Bandage", N(ModuleId.Medstation, 1) },
            { "TV_Tourniquet", N(ModuleId.Medstation, 1) },
            { "TV_Splint", N(ModuleId.Medstation, 1) },
            { "TV_Painkillers", N(ModuleId.Medstation, 2) },
            { "TV_FieldKit", N(ModuleId.Medstation, 2) },
            { "TV_SurgicalKit", N(ModuleId.Medstation, 3) },
        };

        public const float PerimeterStep = 10f;
        public const float IntelCalmMinutes = 3f;
        private const string IntelKey = "tv_intel_level";

        public static ModuleSpec Find(ModuleId id)
        {
            foreach (var m in All)
            {
                if (m.Id == id)
                {
                    return m;
                }
            }
            return null;
        }

        public static bool TryGate(string prefabName, out ModuleId module, out int level)
        {
            module = default(ModuleId);
            level = 0;
            if (prefabName == null || !Gates.TryGetValue(prefabName, out var gate))
            {
                return false;
            }
            module = gate.Key;
            level = gate.Value;
            return true;
        }

        // ------------------------------------------------------------------
        // Levels, stored on the bench

        // A stable hash: string.GetHashCode isn't guaranteed to match across
        // machines, and every client must read the same key.
        private static int Key(ModuleId id)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char c in "tv_module_" + id)
                {
                    h = (h ^ c) * 16777619;
                }
                return (int)h;
            }
        }

        public static int LevelOf(Component bench, ModuleId id)
        {
            var nview = bench != null ? bench.GetComponent<ZNetView>() : null;
            if (nview == null || !nview.IsValid())
            {
                return 0;
            }
            return nview.GetZDO().GetInt(Key(id), 0);
        }

        public static void SetLevel(Component bench, ModuleId id, int level)
        {
            var nview = bench != null ? bench.GetComponent<ZNetView>() : null;
            if (nview == null || !nview.IsValid())
            {
                return;
            }
            if (!nview.IsOwner())
            {
                nview.ClaimOwnership();
            }
            nview.GetZDO().Set(Key(id), level, false);
            var marker = bench.GetComponent<HideoutMarker>();
            if (marker != null)
            {
                marker.Refresh();
            }
        }

        /// <summary>The best level of a module among the hideouts covering a spot.</summary>
        public static int LevelAt(Vector3 pos, ModuleId id)
        {
            int best = 0;
            foreach (var marker in HideoutMarker.Active)
            {
                if (marker != null && marker.Covers(pos))
                {
                    best = Mathf.Max(best, marker.Level(id));
                }
            }
            return best;
        }

        /// <summary>The Hideout bench being used, if the station is one.</summary>
        public static HideoutMarker BenchOf(CraftingStation station)
        {
            return station != null ? station.GetComponent<HideoutMarker>() : null;
        }

        // ------------------------------------------------------------------
        // Building

        /// <summary>The next level's requirements, or null at max.</summary>
        public static ModuleLevel Next(HideoutMarker bench, ModuleSpec spec)
        {
            int level = bench != null ? bench.Level(spec.Id) : 0;
            return level < spec.Max ? spec.Levels[level] : null;
        }

        public static bool NeedsMet(HideoutMarker bench, ModuleLevel next)
        {
            foreach (var need in next.Needs)
            {
                if (bench.Level(need.Key) < need.Value)
                {
                    return false;
                }
            }
            return true;
        }

        public static bool HaveCost(Player player, ModuleLevel next)
        {
            foreach (var c in next.Cost)
            {
                if (Crafting.CraftService.CountPrefab(player, c.Key) < c.Value)
                {
                    return false;
                }
            }
            return true;
        }

        public static bool CanBuild(Player player, HideoutMarker bench, ModuleSpec spec)
        {
            var next = Next(bench, spec);
            return bench != null && next != null && NeedsMet(bench, next) && HaveCost(player, next);
        }

        /// <summary>Takes the cost and raises the level. Checks again first.</summary>
        public static bool Build(Player player, HideoutMarker bench, ModuleSpec spec)
        {
            if (!CanBuild(player, bench, spec))
            {
                Stash.StashOps.LastError = "$tv_err_missing_resources";
                return false;
            }
            var next = Next(bench, spec);
            int level = bench.Level(spec.Id);
            foreach (var c in next.Cost)
            {
                Crafting.CraftService.TakePrefab(player, c.Key, c.Value);
            }
            SetLevel(bench, spec.Id, level + 1);
            return true;
        }

        // ------------------------------------------------------------------
        // Intel, carried into the raid

        public static void RememberIntel(Player player)
        {
            var data = GameAccess.CustomData(player);
            if (data != null)
            {
                data[IntelKey] = LevelAt(player.transform.position, ModuleId.Intel).ToString();
            }
        }

        public static int Intel(Player player)
        {
            var data = player != null ? GameAccess.CustomData(player) : null;
            return data != null && data.TryGetValue(IntelKey, out var raw) && int.TryParse(raw, out int level) ? level : 0;
        }

        // ------------------------------------------------------------------

        private static KeyValuePair<string, int> C(string item, int amount) => new KeyValuePair<string, int>(item, amount);

        private static KeyValuePair<ModuleId, int> N(ModuleId id, int level) => new KeyValuePair<ModuleId, int>(id, level);

        private static ModuleLevel L(string effect, params KeyValuePair<string, int>[] cost) =>
            new ModuleLevel { Effect = effect, Cost = cost };

        private static ModuleLevel L(string effect, KeyValuePair<ModuleId, int>[] needs, params KeyValuePair<string, int>[] cost) =>
            new ModuleLevel { Effect = effect, Cost = cost, Needs = needs };
    }
}
