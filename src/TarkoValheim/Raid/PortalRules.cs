using System.Reflection;
using HarmonyLib;
using TarkoValheim.Content;
using TarkoValheim.Util;
using UnityEngine;

namespace TarkoValheim.Raid
{
    /// <summary>
    /// Portals only work inside a hideout's radius. You can't build one
    /// outside, and one that is outside (built before the rule, or whose
    /// hideout was torn down) won't send you anywhere. Getting home from a
    /// raid means walking, or building a forward hideout (an FOB) with its own
    /// portal first.
    /// </summary>
    internal static class PortalRules
    {
        private static FieldInfo _placementGhost;

        public static void Patch(Harmony harmony)
        {
            _placementGhost = AccessTools.Field(typeof(Player), "m_placementGhost");
            int placed = 0;
            foreach (var name in new[] { "TryPlacePiece", "PlacePiece" })
            {
                foreach (var method in AccessTools.GetDeclaredMethods(typeof(Player)))
                {
                    if (method.Name == name && method.ReturnType == typeof(bool) && HasPieceParam(method))
                    {
                        try
                        {
                            harmony.Patch(method, prefix: new HarmonyMethod(typeof(PortalRules), nameof(PlacePrefix)));
                            placed++;
                        }
                        catch (System.Exception e)
                        {
                            Plugin.Log.LogWarning($"Player.{name}: {e.Message}");
                        }
                    }
                }
            }
            if (placed == 0)
            {
                Plugin.Log.LogWarning("Player.PlacePiece not found; portals can still be built anywhere (but only work in hideouts).");
            }

            var teleport = AccessTools.Method(typeof(TeleportWorld), "Teleport");
            if (teleport != null)
            {
                harmony.Patch(teleport, prefix: new HarmonyMethod(typeof(PortalRules), nameof(TeleportPrefix)));
            }
            else
            {
                Plugin.Log.LogWarning("TeleportWorld.Teleport not found; portals work anywhere.");
            }
        }

        private static bool HasPieceParam(MethodInfo method)
        {
            foreach (var p in method.GetParameters())
            {
                if (p.ParameterType == typeof(Piece) && p.Name == "piece")
                {
                    return true;
                }
            }
            return false;
        }

        private static bool PlacePrefix(Player __instance, Piece piece, object[] __args, ref bool __result)
        {
            try
            {
                if (!Plugin.PortalsOnlyInHideouts.Value || piece == null || piece.GetComponent<TeleportWorld>() == null)
                {
                    return true;
                }

                // The placement position: a Vector3 argument if the method has
                // one, otherwise the ghost's.
                Vector3? pos = null;
                foreach (var arg in __args)
                {
                    if (arg is Vector3 v)
                    {
                        pos = v;
                        break;
                    }
                }
                if (pos == null && _placementGhost != null && _placementGhost.GetValue(__instance) is GameObject ghost && ghost != null)
                {
                    pos = ghost.transform.position;
                }
                if (pos == null || Hideout.IsNear(pos.Value))
                {
                    return true;
                }

                __instance.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$tv_msg_portal_build"), 0, null, false);
                __result = false;
                return false;
            }
            catch (System.Exception e)
            {
                GameAccess.WarnOnce("PortalRules.Place", e);
                return true;
            }
        }

        private static float _nextMessage;

        private static bool TeleportPrefix(TeleportWorld __instance, object[] __args)
        {
            try
            {
                if (!Plugin.PortalsOnlyInHideouts.Value || Hideout.IsNear(__instance.transform.position))
                {
                    return true;
                }
                var player = __args.Length > 0 ? __args[0] as Player : null;
                if (player != null && player == Player.m_localPlayer && Time.time >= _nextMessage)
                {
                    _nextMessage = Time.time + 3f;
                    player.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$tv_msg_portal_dead"), 0, null, false);
                }
                return false;
            }
            catch (System.Exception e)
            {
                GameAccess.WarnOnce("PortalRules.Teleport", e);
                return true;
            }
        }
    }
}
