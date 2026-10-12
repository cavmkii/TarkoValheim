using System.Collections.Generic;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using TarkoValheim.Util;
using UnityEngine;

namespace TarkoValheim.Content
{
    /// <summary>
    /// The Hideout workbench is a cloned workbench with its own station name.
    /// Gear, rigs and meds are crafted here, and the area around any built
    /// Hideout counts as out of raid.
    /// </summary>
    internal static class Hideout
    {
        public const string PrefabName = "TV_Hideout";
        public const string StashPrefab = "TV_Stash";
        private const string BasePrefab = "piece_workbench";
        private const string StashBase = "piece_chest_blackmetal";

        public static void Register()
        {
            var piece = new CustomPiece(PrefabName, BasePrefab, new PieceConfig
            {
                Name = "$tv_hideout",
                Description = "$tv_hideout_desc",
                PieceTable = PieceTables.Hammer,
                Category = PieceCategories.Crafting,
                Requirements = new[]
                {
                    new RequirementConfig("Wood", 20, 0, true),
                    new RequirementConfig("Stone", 10, 0, true),
                    new RequirementConfig("Flint", 5, 0, true),
                }
            });
            if (!piece.IsValid())
            {
                Plugin.Log.LogError($"Could not clone {BasePrefab} for the hideout.");
                return;
            }

            // A distinct station name keeps vanilla workbench extensions and
            // recipes from treating the hideout as an ordinary workbench.
            var station = piece.PiecePrefab.GetComponent<CraftingStation>();
            if (station != null)
            {
                GameAccess.SetStationName(station, "$tv_hideout");
            }

            piece.PiecePrefab.AddComponent<HideoutMarker>();
            PieceManager.Instance.AddPiece(piece);

            RegisterStash();
        }

        // Tarkov's stash: a big chest, 10 x 12 Valheim cells, which the Tetris
        // view shows as 10 wide and 24 (or 12 x ContainerHeightFactor) tall.
        private static void RegisterStash()
        {
            var piece = new CustomPiece(StashPrefab, StashBase, new PieceConfig
            {
                Name = "$tv_stash",
                Description = "$tv_stash_desc",
                PieceTable = PieceTables.Hammer,
                Category = PieceCategories.Furniture,
                CraftingStation = PrefabName,
                Requirements = new[]
                {
                    new RequirementConfig("FineWood", 10, 0, true),
                    new RequirementConfig("BronzeNails", 10, 0, true),
                    new RequirementConfig("Bronze", 2, 0, true),
                }
            });
            if (!piece.IsValid())
            {
                Plugin.Log.LogError($"Could not clone {StashBase} for the stash.");
                return;
            }

            var container = piece.PiecePrefab.GetComponent<Container>();
            if (container != null)
            {
                GameAccess.SetContainer(container, "$tv_stash", 10, 12);
            }

            PieceManager.Instance.AddPiece(piece);
        }

        public static bool IsNear(Vector3 position)
        {
            float radius = Plugin.HideoutRadius.Value;
            float sqr = radius * radius;
            foreach (var marker in HideoutMarker.Active)
            {
                if ((marker.transform.position - position).sqrMagnitude <= sqr)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Tracks placed hideouts. Placement ghosts are clones of the prefab but
    /// carry no network object, so they are skipped.
    /// </summary>
    internal class HideoutMarker : MonoBehaviour
    {
        internal static readonly HashSet<HideoutMarker> Active = new HashSet<HideoutMarker>();

        private void Start()
        {
            var nview = GetComponent<ZNetView>();
            if (nview != null && nview.IsValid())
            {
                Active.Add(this);
            }
        }

        private void OnDestroy()
        {
            Active.Remove(this);
        }
    }
}
