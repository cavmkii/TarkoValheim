using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using TarkoValheim.Util;
using UnityEngine;

namespace TarkoValheim.Health
{
    internal enum Condition
    {
        LightBleed,
        HeavyBleed,
        Fracture
    }

    /// <summary>
    /// Tarkov-style injuries on top of Valheim's single health bar.
    ///
    /// - Cutting and piercing hits can open a light or heavy bleed. Bleeds
    ///   drain health until treated: a bandage stops a light bleed, a
    ///   tourniquet stops either.
    /// - Heavy blunt hits and bad falls can fracture a bone: much slower
    ///   sprinting and a slower jog until splinted. Painkillers hide the
    ///   effect for a few minutes without fixing it.
    ///
    /// The bigger the hit compared with your max health, the likelier. State
    /// lives in the player's saved data, so it survives logging out; death
    /// clears it.
    /// </summary>
    internal class Conditions : MonoBehaviour
    {
        private const string KeyPrefix = "tv_cond_";
        private const string PainKey = "tv_cond_painkiller";

        private static readonly FieldInfo HitTypeField = AccessTools.Field(typeof(HitData), "m_hitType");
        private static readonly FieldInfo HitAttacker = AccessTools.Field(typeof(HitData), "m_attacker");
        private static readonly MethodInfo SetHealthMethod = AccessTools.Method(typeof(Character), "SetHealth", new[] { typeof(float) });

        private static bool _ticking;
        private float _nextTick;
        private float _fractureInHideout;

        public static void Patch(Harmony harmony)
        {
            var rpc = AccessTools.Method(typeof(Character), "RPC_Damage");
            if (rpc != null)
            {
                harmony.Patch(rpc,
                    prefix: new HarmonyMethod(typeof(Conditions), nameof(DamagePrefix)),
                    postfix: new HarmonyMethod(typeof(Conditions), nameof(DamagePostfix)));
            }
            else
            {
                Plugin.Log.LogWarning("Character.RPC_Damage not found; hits won't cause bleeds or fractures.");
            }

            foreach (var name in new[] { "GetRunSpeedFactor", "GetJogSpeedFactor" })
            {
                var m = AccessTools.Method(typeof(Player), name);
                if (m != null && m.ReturnType == typeof(float))
                {
                    harmony.Patch(m, postfix: new HarmonyMethod(typeof(Conditions),
                        name == "GetRunSpeedFactor" ? nameof(RunPostfix) : nameof(JogPostfix)));
                }
                else
                {
                    Plugin.Log.LogWarning($"Player.{name} not found; fractures won't slow you down.");
                }
            }

            var death = AccessTools.Method(typeof(Player), "OnDeath");
            if (death != null)
            {
                harmony.Patch(death, postfix: new HarmonyMethod(typeof(Conditions), nameof(DeathPostfix)));
            }
        }

        // ------------------------------------------------------------------
        // State

        public static bool Has(Player player, Condition c)
        {
            var data = player != null ? GameAccess.CustomData(player) : null;
            return data != null && data.ContainsKey(KeyPrefix + c);
        }

        public static void Set(Player player, Condition c, bool on)
        {
            var data = player != null ? GameAccess.CustomData(player) : null;
            if (data == null)
            {
                return;
            }
            if (on)
            {
                data[KeyPrefix + c] = "1";
            }
            else
            {
                data.Remove(KeyPrefix + c);
            }
        }

        public static List<Condition> Active(Player player)
        {
            var list = new List<Condition>();
            foreach (Condition c in System.Enum.GetValues(typeof(Condition)))
            {
                if (Has(player, c))
                {
                    list.Add(c);
                }
            }
            return list;
        }

        public static float PainkillerLeft(Player player)
        {
            var data = player != null ? GameAccess.CustomData(player) : null;
            return data != null && data.TryGetValue(PainKey, out var raw)
                && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float s) ? s : 0f;
        }

        public static void SetPainkiller(Player player, float seconds)
        {
            var data = player != null ? GameAccess.CustomData(player) : null;
            if (data == null)
            {
                return;
            }
            if (seconds <= 0f)
            {
                data.Remove(PainKey);
            }
            else
            {
                data[PainKey] = seconds.ToString("0.0", CultureInfo.InvariantCulture);
            }
        }

        public static void ClearAll(Player player)
        {
            foreach (Condition c in System.Enum.GetValues(typeof(Condition)))
            {
                Set(player, c, false);
            }
            SetPainkiller(player, 0f);
        }

        // ------------------------------------------------------------------
        // Getting hurt

        private sealed class HitState
        {
            public float Health;
            public float Slash;
            public float Blunt;
        }

        private static void DamagePrefix(Character __instance, HitData hit, out HitState __state)
        {
            __state = null;
            try
            {
                if (_ticking || !Plugin.HealthConditions.Value || __instance != Player.m_localPlayer || hit == null)
                {
                    return;
                }
                __state = new HitState
                {
                    Health = __instance.GetHealth(),
                    Slash = hit.m_damage.m_slash + hit.m_damage.m_pierce,
                    Blunt = hit.m_damage.m_blunt
                };
            }
            catch (System.Exception e)
            {
                GameAccess.WarnOnce("Conditions.DamagePrefix", e);
            }
        }

        private static void DamagePostfix(Character __instance, HitData hit, HitState __state)
        {
            try
            {
                if (__state == null || !(__instance is Player player) || player.IsDead())
                {
                    return;
                }
                float taken = __state.Health - player.GetHealth();
                float max = Mathf.Max(1f, player.GetMaxHealth());
                if (taken <= 0f)
                {
                    return;
                }
                float frac = taken / max;

                string type = HitTypeField != null ? HitTypeField.GetValue(hit)?.ToString() : null;
                if (type == "Fall")
                {
                    if (frac >= 0.1f && Random.value < Mathf.Clamp(frac * 2.5f, 0f, 0.9f))
                    {
                        Inflict(player, Condition.Fracture);
                    }
                    return;
                }

                // Only blows from something: not burning, poison, drowning...
                if (HitAttacker != null && HitAttacker.GetValue(hit) is ZDOID id && id.IsNone())
                {
                    return;
                }

                float physical = __state.Slash + __state.Blunt;
                if (physical <= 0f)
                {
                    return;
                }
                float cut = __state.Slash / physical;
                float crush = __state.Blunt / physical;

                if (frac >= 0.2f && Random.value < Mathf.Clamp((frac - 0.15f) * 2f, 0f, 0.5f) * cut)
                {
                    Inflict(player, Condition.HeavyBleed);
                }
                else if (frac >= 0.06f && Random.value < Mathf.Clamp(frac * 2.5f, 0f, 0.6f) * cut)
                {
                    Inflict(player, Condition.LightBleed);
                }

                if (frac >= 0.15f && Random.value < Mathf.Clamp((frac - 0.1f) * 1.5f, 0f, 0.45f) * crush)
                {
                    Inflict(player, Condition.Fracture);
                }
            }
            catch (System.Exception e)
            {
                GameAccess.WarnOnce("Conditions.DamagePostfix", e);
            }
        }

        private static void Inflict(Player player, Condition c)
        {
            if (Has(player, c))
            {
                return;
            }
            Set(player, c, true);
            player.Message(MessageHud.MessageType.Center,
                Localization.instance.Localize("$tv_msg_" + c.ToString().ToLowerInvariant()), 0, null, false);
        }

        private static void DeathPostfix(Player __instance)
        {
            if (__instance == Player.m_localPlayer)
            {
                ClearAll(__instance);
            }
        }

        // ------------------------------------------------------------------
        // Effects

        private static bool FractureHurts(Player player)
        {
            return Plugin.HealthConditions.Value && Has(player, Condition.Fracture) && PainkillerLeft(player) <= 0f;
        }

        private static void RunPostfix(Player __instance, ref float __result)
        {
            if (__instance == Player.m_localPlayer && FractureHurts(__instance))
            {
                __result *= 0.55f;
            }
        }

        private static void JogPostfix(Player __instance, ref float __result)
        {
            if (__instance == Player.m_localPlayer && FractureHurts(__instance))
            {
                __result *= 0.8f;
            }
        }

        private void Update()
        {
            try
            {
                var player = Player.m_localPlayer;
                if (player == null || player.IsDead() || !Plugin.HealthConditions.Value)
                {
                    return;
                }

                float pain = PainkillerLeft(player);
                if (pain > 0f)
                {
                    SetPainkiller(player, pain - Time.deltaTime);
                }

                if (Time.time < _nextTick)
                {
                    return;
                }
                _nextTick = Time.time + 2f;

                HideoutCare(player);

                float max = player.GetMaxHealth();
                float drain = 0f;
                if (Has(player, Condition.HeavyBleed))
                {
                    drain += Mathf.Max(1f, max * 0.012f);
                }
                if (Has(player, Condition.LightBleed))
                {
                    drain += Mathf.Max(0.3f, max * 0.004f);
                }
                if (drain > 0f)
                {
                    Bleed(player, drain * 2f);
                }
            }
            catch (System.Exception e)
            {
                GameAccess.WarnOnce("Conditions.Update", e);
            }
        }

        // The Medstation looks after you at home: bleeds stop (level 1),
        // fractures mend after a minute (level 2), health comes back faster
        // (level 3). Runs every 2 seconds.
        private void HideoutCare(Player player)
        {
            int med = Content.HideoutModules.LevelAt(player.transform.position, Content.ModuleId.Medstation);
            if (med <= 0)
            {
                _fractureInHideout = 0f;
                return;
            }

            if (Has(player, Condition.LightBleed) || Has(player, Condition.HeavyBleed))
            {
                Set(player, Condition.LightBleed, false);
                Set(player, Condition.HeavyBleed, false);
                player.Message(MessageHud.MessageType.TopLeft, Localization.instance.Localize("$tv_msg_medstation_bleed"), 0, null, false);
            }

            if (med >= 2 && Has(player, Condition.Fracture))
            {
                _fractureInHideout += 2f;
                if (_fractureInHideout >= 60f)
                {
                    Set(player, Condition.Fracture, false);
                    _fractureInHideout = 0f;
                    player.Message(MessageHud.MessageType.TopLeft, Localization.instance.Localize("$tv_msg_medstation_fracture"), 0, null, false);
                }
            }
            else
            {
                _fractureInHideout = 0f;
            }

            if (med >= 3 && player.GetHealth() < player.GetMaxHealth())
            {
                player.Heal(player.GetMaxHealth() * 0.01f, false);
            }
        }

        // Bleeding lowers health directly (no armour, no stagger); only a
        // fatal amount goes through Valheim's damage so death works normally.
        private static void Bleed(Player player, float amount)
        {
            float health = player.GetHealth();
            if (health - amount > 0.5f && SetHealthMethod != null)
            {
                SetHealthMethod.Invoke(player, new object[] { health - amount });
                return;
            }

            var hit = new HitData();
            hit.m_damage.m_damage = amount + 1f;
            hit.m_point = player.transform.position + Vector3.up;
            _ticking = true;
            try
            {
                player.Damage(hit);
            }
            finally
            {
                _ticking = false;
            }
        }
    }
}
