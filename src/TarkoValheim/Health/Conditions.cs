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
    /// Tarkov-style injuries, per body part (see <see cref="Body"/>).
    ///
    /// - Cutting and piercing hits can open a light or heavy bleed where they
    ///   land. Bleeds drain that part until treated.
    /// - Heavy blunt hits to an arm or leg, and bad falls, can fracture it.
    ///   A broken or destroyed leg slows you down; a broken or destroyed arm
    ///   makes attacks cost more stamina. Painkillers hide both for a while.
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
        private float _nextReconcile;
        private Player _migratedFor;
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

            var stamina = AccessTools.Method(typeof(Attack), "GetAttackStamina");
            if (stamina != null)
            {
                harmony.Patch(stamina, postfix: new HarmonyMethod(typeof(Conditions), nameof(AttackStaminaPostfix)));
            }

            var death = AccessTools.Method(typeof(Player), "OnDeath");
            if (death != null)
            {
                harmony.Patch(death, postfix: new HarmonyMethod(typeof(Conditions), nameof(DeathPostfix)));
            }
        }

        // ------------------------------------------------------------------
        // State

        private static string KeyOf(Condition c, BodyPart p) => KeyPrefix + c + "_" + p;

        /// <summary>Anywhere on the body.</summary>
        public static bool Has(Player player, Condition c)
        {
            foreach (var p in Body.Parts)
            {
                if (Has(player, c, p))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool Has(Player player, Condition c, BodyPart p)
        {
            var data = player != null ? GameAccess.CustomData(player) : null;
            return data != null && data.ContainsKey(KeyOf(c, p));
        }

        public static void Set(Player player, Condition c, BodyPart p, bool on)
        {
            var data = player != null ? GameAccess.CustomData(player) : null;
            if (data == null)
            {
                return;
            }
            if (on)
            {
                data[KeyOf(c, p)] = "1";
            }
            else
            {
                data.Remove(KeyOf(c, p));
            }
        }

        /// <summary>Clears a condition everywhere.</summary>
        public static void Set(Player player, Condition c, bool on)
        {
            if (on)
            {
                Set(player, c, c == Condition.Fracture ? BodyPart.LeftLeg : BodyPart.Thorax, true);
                return;
            }
            foreach (var p in Body.Parts)
            {
                Set(player, c, p, false);
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

        /// <summary>Every (condition, part) pair present.</summary>
        public static List<KeyValuePair<Condition, BodyPart>> ActiveParts(Player player)
        {
            var list = new List<KeyValuePair<Condition, BodyPart>>();
            foreach (Condition c in System.Enum.GetValues(typeof(Condition)))
            {
                foreach (var p in Body.Parts)
                {
                    if (Has(player, c, p))
                    {
                        list.Add(new KeyValuePair<Condition, BodyPart>(c, p));
                    }
                }
            }
            return list;
        }

        // Conditions saved before body parts existed: put them somewhere.
        private static void Migrate(Player player)
        {
            var data = GameAccess.CustomData(player);
            if (data == null)
            {
                return;
            }
            foreach (Condition c in System.Enum.GetValues(typeof(Condition)))
            {
                if (data.Remove(KeyPrefix + c))
                {
                    Set(player, c, c == Condition.Fracture ? BodyPart.LeftLeg : BodyPart.Thorax, true);
                }
            }
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
            Body.Reset(player);
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
                bool fall = type == "Fall";
                var part = Body.Locate(player, hit, fall);
                Body.Damage(player, part, taken);
                CheckLethal(player);

                if (fall)
                {
                    if (frac >= 0.1f && Random.value < Mathf.Clamp(frac * 2.5f, 0f, 0.9f))
                    {
                        Inflict(player, Condition.Fracture, part);
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
                    Inflict(player, Condition.HeavyBleed, part);
                }
                else if (frac >= 0.06f && Random.value < Mathf.Clamp(frac * 2.5f, 0f, 0.6f) * cut)
                {
                    Inflict(player, Condition.LightBleed, part);
                }

                // Only arms and legs break.
                if (Body.IsLimb(part) && frac >= 0.15f && Random.value < Mathf.Clamp((frac - 0.1f) * 1.5f, 0f, 0.45f) * crush)
                {
                    Inflict(player, Condition.Fracture, part);
                }
            }
            catch (System.Exception e)
            {
                GameAccess.WarnOnce("Conditions.DamagePostfix", e);
            }
        }

        private static void Inflict(Player player, Condition c, BodyPart part)
        {
            if (Has(player, c, part))
            {
                return;
            }
            Set(player, c, part, true);
            player.Message(MessageHud.MessageType.Center,
                Localization.instance.Localize("$tv_msg_" + c.ToString().ToLowerInvariant()) + " - "
                + Localization.instance.Localize("$tv_part_" + part.ToString().ToLowerInvariant()), 0, null, false);
        }

        // Tarkov: a destroyed head or thorax is death. Off by default.
        private static void CheckLethal(Player player)
        {
            if (Plugin.LethalHeadThorax.Value && !player.IsDead()
                && (Body.Destroyed(player, BodyPart.Head) || Body.Destroyed(player, BodyPart.Thorax)))
            {
                Bleed(player, player.GetHealth() + 10f);
            }
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

        private static bool LimbHurts(Player player, BodyPart p)
        {
            return Has(player, Condition.Fracture, p) || Body.Destroyed(player, p);
        }

        private static bool FractureHurts(Player player)
        {
            return Plugin.HealthConditions.Value && PainkillerLeft(player) <= 0f
                && (LimbHurts(player, BodyPart.LeftLeg) || LimbHurts(player, BodyPart.RightLeg));
        }

        private static bool ArmHurts(Player player)
        {
            return Plugin.HealthConditions.Value && PainkillerLeft(player) <= 0f
                && (LimbHurts(player, BodyPart.LeftArm) || LimbHurts(player, BodyPart.RightArm));
        }

        private static readonly FieldInfo AttackCharacter = AccessTools.Field(typeof(Attack), "m_character");

        private static void AttackStaminaPostfix(Attack __instance, ref float __result)
        {
            try
            {
                var player = Player.m_localPlayer;
                if (player != null && AttackCharacter != null && ReferenceEquals(AttackCharacter.GetValue(__instance), player) && ArmHurts(player))
                {
                    __result *= 1.5f;
                }
            }
            catch (System.Exception e)
            {
                GameAccess.WarnOnce("Conditions.AttackStamina", e);
            }
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

                // Body parts follow Valheim's health (regeneration, poison...).
                if (Time.time >= _nextReconcile)
                {
                    _nextReconcile = Time.time + 0.25f;
                    if (_migratedFor != player)
                    {
                        _migratedFor = player;
                        Migrate(player);
                    }
                    Body.Reconcile(player, SetHealth);
                    Body.Save(player, false);
                }

                if (Time.time < _nextTick)
                {
                    return;
                }
                _nextTick = Time.time + 2f;

                HideoutCare(player);

                // Each bleeding part loses health; heavy bleeds three times as fast.
                float max = player.GetMaxHealth();
                foreach (var p in Body.Parts)
                {
                    float drain = 0f;
                    if (Has(player, Condition.HeavyBleed, p))
                    {
                        drain += Mathf.Max(1f, max * 0.012f);
                    }
                    if (Has(player, Condition.LightBleed, p))
                    {
                        drain += Mathf.Max(0.3f, max * 0.004f);
                    }
                    if (drain > 0f && !player.IsDead())
                    {
                        float amount = Mathf.Min(drain * 2f, player.GetHealth());
                        Body.Damage(player, p, amount);
                        Bleed(player, drain * 2f);
                        CheckLethal(player);
                    }
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

        private static void SetHealth(float health)
        {
            var player = Player.m_localPlayer;
            if (player != null && SetHealthMethod != null)
            {
                SetHealthMethod.Invoke(player, new object[] { health });
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
