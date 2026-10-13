using System.Globalization;
using TarkoValheim.Util;
using UnityEngine;

namespace TarkoValheim.Health
{
    internal enum BodyPart
    {
        Head,
        Thorax,
        Stomach,
        LeftArm,
        RightArm,
        LeftLeg,
        RightLeg
    }

    /// <summary>
    /// Tarkov's body parts on top of Valheim's one health bar. Each part has
    /// a share of max health (Tarkov's 35 / 85 / 70 / 60 / 60 / 65 / 65 out of
    /// 440) and its own health, stored as a fraction so it scales with food.
    /// The parts always add up to Valheim's health:
    ///
    /// - A hit takes its damage from the part it struck (by height and side;
    ///   falls hit the legs). Damage to a destroyed part spreads to the rest.
    /// - Anything else that changes health (food regeneration, poison,
    ///   burning, other mods) is spread over the parts: healing over the
    ///   damaged ones that aren't destroyed, damage over all of them.
    /// - A destroyed part (0) doesn't heal on its own: it needs a surgical
    ///   kit. Until then health can't climb past what the other parts hold.
    /// </summary>
    internal static class Body
    {
        public static readonly BodyPart[] Parts =
        {
            BodyPart.Head, BodyPart.Thorax, BodyPart.Stomach, BodyPart.LeftArm, BodyPart.RightArm, BodyPart.LeftLeg, BodyPart.RightLeg
        };

        private static readonly float[] Share = { 35f / 440f, 85f / 440f, 70f / 440f, 60f / 440f, 60f / 440f, 65f / 440f, 65f / 440f };

        private const string Key = "tv_body";
        private static readonly float[] Fraction = { 1f, 1f, 1f, 1f, 1f, 1f, 1f };
        private static Player _loadedFor;
        private static bool _dirty;
        private static float _nextSave;

        public static bool IsLimb(BodyPart p) => p >= BodyPart.LeftArm;
        public static bool IsArm(BodyPart p) => p == BodyPart.LeftArm || p == BodyPart.RightArm;
        public static bool IsLeg(BodyPart p) => p == BodyPart.LeftLeg || p == BodyPart.RightLeg;

        public static float Max(Player player, BodyPart p) => Mathf.Max(1f, player.GetMaxHealth()) * Share[(int)p];

        public static float Health(Player player, BodyPart p)
        {
            Load(player);
            return Fraction[(int)p] * Max(player, p);
        }

        public static float Percent(Player player, BodyPart p)
        {
            Load(player);
            return Fraction[(int)p];
        }

        public static bool Destroyed(Player player, BodyPart p)
        {
            Load(player);
            return Fraction[(int)p] <= 0.0001f;
        }

        /// <summary>What the parts add up to, as a fraction of max health.</summary>
        private static float Total()
        {
            float t = 0f;
            for (int i = 0; i < Fraction.Length; i++)
            {
                t += Fraction[i] * Share[i];
            }
            return t;
        }

        // ------------------------------------------------------------------
        // Changes

        /// <summary>Takes damage (in health points) from one part. What the
        /// part can't take spreads over the others still standing.</summary>
        public static void Damage(Player player, BodyPart part, float amount)
        {
            Load(player);
            float max = Mathf.Max(1f, player.GetMaxHealth());
            float frac = amount / max;   // in units of total health
            int i = (int)part;
            float can = Fraction[i] * Share[i];
            float here = Mathf.Min(frac, can);
            Fraction[i] -= here / Share[i];
            Spread(-(frac - here), i);
            Clamp();
            _dirty = true;
        }

        /// <summary>Heals one part, in health points. Returns what it took.
        /// A destroyed part only heals if <paramref name="revive"/>.</summary>
        public static float Heal(Player player, BodyPart part, float amount, bool revive)
        {
            Load(player);
            int i = (int)part;
            if (Fraction[i] <= 0.0001f && !revive)
            {
                return 0f;
            }
            float max = Max(player, part);
            float room = (1f - Fraction[i]) * max;
            float took = Mathf.Min(room, amount);
            Fraction[i] = Mathf.Clamp01(Fraction[i] + took / max);
            if (revive && Fraction[i] < 0.05f)
            {
                Fraction[i] = 0.05f;
            }
            _dirty = true;
            return took;
        }

        public static void Reset(Player player)
        {
            Load(player);
            for (int i = 0; i < Fraction.Length; i++)
            {
                Fraction[i] = 1f;
            }
            _dirty = true;
            Save(player);
        }

        // Spreads a change (in units of total health) over the parts: damage
        // over all parts still standing, healing over damaged ones still
        // standing. Skips <paramref name="except"/>.
        private static void Spread(float delta, int except = -1)
        {
            for (int pass = 0; pass < 4 && Mathf.Abs(delta) > 0.00001f; pass++)
            {
                float weight = 0f;
                for (int i = 0; i < Fraction.Length; i++)
                {
                    if (i != except && Eligible(i, delta))
                    {
                        weight += Share[i];
                    }
                }
                if (weight <= 0f)
                {
                    return;
                }
                float left = 0f;
                for (int i = 0; i < Fraction.Length; i++)
                {
                    if (i == except || !Eligible(i, delta))
                    {
                        continue;
                    }
                    float want = delta * Share[i] / weight;      // total-health units
                    float before = Fraction[i];
                    Fraction[i] = Mathf.Clamp01(Fraction[i] + want / Share[i]);
                    left += want - (Fraction[i] - before) * Share[i];
                }
                delta = left;
            }
        }

        private static bool Eligible(int i, float delta)
        {
            return delta < 0f ? Fraction[i] > 0.0001f : Fraction[i] > 0.0001f && Fraction[i] < 0.9999f;
        }

        private static void Clamp()
        {
            for (int i = 0; i < Fraction.Length; i++)
            {
                Fraction[i] = Mathf.Clamp01(Fraction[i]);
            }
        }

        /// <summary>
        /// Keeps the parts and Valheim's health in step (called every few
        /// frames). Unexplained damage spreads over the body; healing goes to
        /// damaged parts, and health can't rise past what the parts can hold
        /// (destroyed parts hold nothing until treated).
        /// </summary>
        public static void Reconcile(Player player, System.Action<float> setHealth)
        {
            Load(player);
            float max = Mathf.Max(1f, player.GetMaxHealth());
            float actual = Mathf.Clamp01(player.GetHealth() / max);
            float expected = Total();
            float diff = actual - expected;
            if (Mathf.Abs(diff) * max < 0.05f)
            {
                return;
            }
            Spread(diff);
            Clamp();
            _dirty = true;

            float now = Total();
            if (actual > now + 0.0005f && setHealth != null)
            {
                setHealth(now * max);
            }
        }

        // ------------------------------------------------------------------
        // Where a hit lands

        public static BodyPart Locate(Player player, HitData hit, bool fall)
        {
            if (fall)
            {
                return Random.value < 0.5f ? BodyPart.LeftLeg : BodyPart.RightLeg;
            }

            var origin = player.transform.position;
            var point = hit.m_point;
            // No usable hit point: Tarkov-ish odds, the torso most likely.
            if (point == Vector3.zero || (point - origin).sqrMagnitude > 9f)
            {
                return Weighted();
            }

            float h = point.y - origin.y;
            bool right = Vector3.Dot(point - origin, player.transform.right) > 0f;
            if (h > 1.55f)
            {
                return BodyPart.Head;
            }
            if (h > 1.15f)
            {
                return Random.value < 0.3f ? (right ? BodyPart.RightArm : BodyPart.LeftArm) : BodyPart.Thorax;
            }
            if (h > 0.85f)
            {
                return Random.value < 0.3f ? (right ? BodyPart.RightArm : BodyPart.LeftArm) : BodyPart.Stomach;
            }
            return right ? BodyPart.RightLeg : BodyPart.LeftLeg;
        }

        private static BodyPart Weighted()
        {
            float r = Random.value;
            float acc = 0f;
            foreach (var p in Parts)
            {
                acc += Share[(int)p];
                if (r <= acc)
                {
                    return p;
                }
            }
            return BodyPart.Thorax;
        }

        // ------------------------------------------------------------------
        // Saving: "1,1,0.5,..." in the player's custom data

        public static void Load(Player player)
        {
            if (player == null || _loadedFor == player)
            {
                return;
            }
            _loadedFor = player;
            for (int i = 0; i < Fraction.Length; i++)
            {
                Fraction[i] = 1f;
            }
            var data = GameAccess.CustomData(player);
            if (data != null && data.TryGetValue(Key, out var raw))
            {
                var bits = raw.Split(',');
                for (int i = 0; i < bits.Length && i < Fraction.Length; i++)
                {
                    if (float.TryParse(bits[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                    {
                        Fraction[i] = Mathf.Clamp01(f);
                    }
                }
            }
        }

        public static void Save(Player player, bool force = true)
        {
            if (player == null || !_dirty || (!force && Time.time < _nextSave))
            {
                return;
            }
            var data = GameAccess.CustomData(player);
            if (data == null)
            {
                return;
            }
            var parts = new string[Fraction.Length];
            for (int i = 0; i < Fraction.Length; i++)
            {
                parts[i] = Fraction[i].ToString("0.###", CultureInfo.InvariantCulture);
            }
            data[Key] = string.Join(",", parts);
            _dirty = false;
            _nextSave = Time.time + 2f;
        }
    }
}
