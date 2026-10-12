using System.Collections.Generic;
using HarmonyLib;
using TarkoValheim.Util;
using UnityEngine;

namespace TarkoValheim.Guns
{
    /// <summary>
    /// Keeps Valheim out of the way while a gun is in hand: no crossbow
    /// attack, no blocking (right mouse aims instead), and a first-person
    /// camera while aiming down sights.
    /// </summary>
    internal static class GunPatches
    {
        private static readonly List<Renderer> Hidden = new List<Renderer>();
        private static float _baseFov = -1f;

        public static void Patch(Harmony harmony)
        {
            Hook(harmony, typeof(Humanoid), "StartAttack", prefix: nameof(StartAttackPrefix));
            Hook(harmony, typeof(Humanoid), "IsBlocking", postfix: nameof(IsBlockingPostfix));
            Hook(harmony, typeof(GameCamera), "UpdateCamera", postfix: nameof(UpdateCameraPostfix));
            // Last: Jotunn's own GetButtonDown postfix recomputes the result.
            Hook(harmony, typeof(ZInput), "GetButtonDown", postfix: nameof(GetButtonDownPostfix), priority: Priority.Last);
        }

        // Valheim binds R to "Hide" (sheathe weapons), the same key as reload.
        // While a gun is out, R reloads and doesn't put the gun away.
        private static void GetButtonDownPostfix(string name, ref bool __result)
        {
            try
            {
                if (__result && (name == "Hide" || name == "JoyHide") && Player.m_localPlayer != null
                    && HoldingGun(Player.m_localPlayer))
                {
                    __result = false;
                }
            }
            catch (System.Exception e)
            {
                GameAccess.WarnOnce("GunPatches.GetButtonDownPostfix", e);
            }
        }

        private static void Hook(Harmony harmony, System.Type type, string method, string prefix = null, string postfix = null,
            int priority = Priority.Normal)
        {
            var target = AccessTools.Method(type, method);
            if (target == null)
            {
                Plugin.Log.LogError($"{type.Name}.{method} not found; part of the gun handling is disabled.");
                return;
            }

            harmony.Patch(target,
                prefix: prefix != null ? new HarmonyMethod(typeof(GunPatches), prefix) { priority = priority } : null,
                postfix: postfix != null ? new HarmonyMethod(typeof(GunPatches), postfix) { priority = priority } : null);
        }

        private static bool HoldingGun(Humanoid humanoid)
        {
            var controller = GunController.Instance;
            return controller != null && controller.Gun != null && humanoid == Player.m_localPlayer;
        }

        private static bool StartAttackPrefix(Humanoid __instance, ref bool __result)
        {
            try
            {
                if (HoldingGun(__instance))
                {
                    __result = false;
                    return false;
                }
                return true;
            }
            catch (System.Exception e)
            {
                GameAccess.WarnOnce("GunPatches.StartAttackPrefix", e);
                return true;
            }
        }

        private static void IsBlockingPostfix(Humanoid __instance, ref bool __result)
        {
            try
            {
                if (__result && HoldingGun(__instance))
                {
                    __result = false;
                }
            }
            catch (System.Exception e)
            {
                GameAccess.WarnOnce("GunPatches.IsBlockingPostfix", e);
            }
        }

        // Aim-down-sights: move the camera to the player's eye, narrow the
        // field of view, and hide the player's own model (there is no gun
        // model to look down yet, so a clear view beats the inside of a head).
        private static void UpdateCameraPostfix(GameCamera __instance)
        {
            try
            {
                var controller = GunController.Instance;
                var player = Player.m_localPlayer;
                var camera = __instance.GetComponent<Camera>();
                if (camera == null)
                {
                    return;
                }

                float ads = controller != null && controller.Gun != null ? controller.Ads : 0f;
                var eyeTransform = player != null ? GameAccess.Eye(player) : null;
                if (ads <= 0f || eyeTransform == null)
                {
                    if (_baseFov > 0f)
                    {
                        camera.fieldOfView = _baseFov;
                        _baseFov = -1f;
                    }
                    ShowPlayer();
                    return;
                }

                if (_baseFov < 0f)
                {
                    _baseFov = camera.fieldOfView;
                }

                var t = __instance.transform;
                var eye = eyeTransform.position + t.forward * 0.12f;
                t.position = Vector3.Lerp(t.position, eye, ads);
                camera.fieldOfView = Mathf.Lerp(_baseFov, controller.Gun.AdsFov, ads);

                if (ads > 0.6f)
                {
                    HidePlayer(player);
                }
                else
                {
                    ShowPlayer();
                }
            }
            catch (System.Exception e)
            {
                GameAccess.WarnOnce("GunPatches.UpdateCameraPostfix", e);
            }
        }

        private static void HidePlayer(Player player)
        {
            foreach (var r in player.GetComponentsInChildren<Renderer>())
            {
                if (r.enabled)
                {
                    r.enabled = false;
                    Hidden.Add(r);
                }
            }
        }

        private static void ShowPlayer()
        {
            if (Hidden.Count == 0)
            {
                return;
            }
            foreach (var r in Hidden)
            {
                if (r != null)
                {
                    r.enabled = true;
                }
            }
            Hidden.Clear();
        }
    }
}
