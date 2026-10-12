using System.Collections.Generic;
using TarkoValheim.Util;
using UnityEngine;

namespace TarkoValheim.Guns
{
    /// <summary>
    /// Simulated bullets: real travel time and gravity drop, stepped each
    /// frame with a raycast between positions, so long shots need leading
    /// and holdover. Hits go through Valheim's normal damage path
    /// (IDestructible.Damage), which handles networking, armor and PvP rules.
    /// </summary>
    internal class Ballistics : MonoBehaviour
    {
        private const float MaxLife = 2.5f;

        private sealed class Bullet
        {
            public Vector3 Pos;
            public Vector3 Vel;
            public float Damage;
            public Player Owner;
            public float Life;
            public LineRenderer Tracer;
        }

        private static readonly List<Bullet> Live = new List<Bullet>();
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[16];
        private static int _mask;
        private static Material _tracerMaterial;
        private static bool _tracerMaterialTried;

        public static void Fire(Vector3 origin, Vector3 velocity, float damage, Player owner)
        {
            if (_mask == 0)
            {
                _mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid",
                    "terrain", "vehicle", "character", "character_net", "character_ghost", "hitbox", "character_noenv");
            }

            Live.Add(new Bullet
            {
                Pos = origin,
                Vel = velocity,
                Damage = damage,
                Owner = owner,
                Tracer = Plugin.Tracers.Value ? MakeTracer() : null
            });
        }

        private void Update()
        {
            try
            {
                float dt = Time.deltaTime;
                if (dt <= 0f || Live.Count == 0)
                {
                    return;
                }

                var gravity = Physics.gravity;
                for (int i = Live.Count - 1; i >= 0; i--)
                {
                    var b = Live[i];
                    var next = b.Pos + b.Vel * dt + 0.5f * gravity * dt * dt;
                    var step = next - b.Pos;
                    float length = step.magnitude;

                    if (length > 0f && Cast(b, step / length, length, out var hit))
                    {
                        Impact(b, hit);
                        Retire(i);
                        continue;
                    }

                    if (b.Tracer != null)
                    {
                        b.Tracer.SetPosition(0, b.Pos);
                        b.Tracer.SetPosition(1, next);
                    }

                    b.Pos = next;
                    b.Vel += gravity * dt;
                    b.Life += dt;
                    if (b.Life > MaxLife)
                    {
                        Retire(i);
                    }
                }
            }
            catch (System.Exception e)
            {
                GameAccess.WarnOnce("Ballistics.Update", e);
            }
        }

        private static bool Cast(Bullet b, Vector3 dir, float length, out RaycastHit best)
        {
            best = default(RaycastHit);
            int count = Physics.RaycastNonAlloc(b.Pos, dir, HitBuffer, length, _mask, QueryTriggerInteraction.Ignore);
            float bestDistance = float.MaxValue;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                var h = HitBuffer[i];
                if (h.distance >= bestDistance || h.collider == null)
                {
                    continue;
                }
                if (b.Owner != null && h.collider.transform.IsChildOf(b.Owner.transform))
                {
                    continue;
                }
                best = h;
                bestDistance = h.distance;
                found = true;
            }
            return found;
        }

        private static void Impact(Bullet b, RaycastHit h)
        {
            var target = GameAccess.FindHitObject(h.collider);
            var destructible = target != null ? target.GetComponent<IDestructible>() : null;
            if (destructible == null)
            {
                return;
            }

            var hit = new HitData();
            hit.m_damage.m_pierce = b.Damage;
            hit.m_point = h.point;
            hit.m_dir = b.Vel.normalized;
            hit.m_pushForce = 4f;
            hit.m_skill = Skills.SkillType.Crossbows;
            Reflect.TrySetQuiet(hit, "m_hitCollider", h.collider);
            if (b.Owner != null)
            {
                hit.SetAttacker(b.Owner);
            }
            destructible.Damage(hit);

            if (b.Owner != null && b.Owner == Player.m_localPlayer && destructible is Character)
            {
                b.Owner.RaiseSkill(Skills.SkillType.Crossbows, 0.5f);
            }
        }

        private static void Retire(int index)
        {
            var b = Live[index];
            if (b.Tracer != null)
            {
                Destroy(b.Tracer.gameObject);
            }
            Live.RemoveAt(index);
        }

        private static LineRenderer MakeTracer()
        {
            if (!_tracerMaterialTried)
            {
                _tracerMaterialTried = true;
                var shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    _tracerMaterial = new Material(shader);
                }
                else
                {
                    Plugin.Log.LogWarning("Tracer shader not found; tracers are off.");
                }
            }
            if (_tracerMaterial == null)
            {
                return null;
            }

            var line = new GameObject("TV_Tracer").AddComponent<LineRenderer>();
            line.sharedMaterial = _tracerMaterial;
            line.positionCount = 2;
            line.startWidth = 0.025f;
            line.endWidth = 0.01f;
            line.startColor = new Color(1f, 0.85f, 0.45f, 0.9f);
            line.endColor = new Color(1f, 0.6f, 0.2f, 0.2f);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }
    }
}
