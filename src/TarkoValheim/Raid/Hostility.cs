using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TarkoValheim.Util;
using UnityEngine;

namespace TarkoValheim.Raid
{
    /// <summary>
    /// The longer a raid lasts, the more the land turns on you. The first few
    /// minutes are calm, so a quick run for materials stays cheap; after that,
    /// every few minutes the threat level rises. Each level sends hunting
    /// parties of the local biome's monsters more often, and gives monsters
    /// that spawn nearby a better chance of stars.
    /// </summary>
    internal class Hostility : MonoBehaviour
    {
        public const int MaxThreat = 4;

        private static readonly Dictionary<Heightmap.Biome, string[]> Hunters = new Dictionary<Heightmap.Biome, string[]>
        {
            { Heightmap.Biome.Meadows, new[] { "Greyling", "Greydwarf", "Neck" } },
            { Heightmap.Biome.BlackForest, new[] { "Greydwarf", "Greydwarf_Elite", "Greydwarf_Shaman", "Skeleton" } },
            { Heightmap.Biome.Swamp, new[] { "Draugr", "Draugr_Ranged", "Skeleton", "Blob" } },
            { Heightmap.Biome.Mountain, new[] { "Wolf", "Hatchling", "Fenring" } },
            { Heightmap.Biome.Plains, new[] { "Goblin", "GoblinArcher", "Deathsquito" } },
            { Heightmap.Biome.Mistlands, new[] { "Seeker", "Tick", "SeekerBrute" } },
            { Heightmap.Biome.AshLands, new[] { "Charred_Melee", "Charred_Archer", "Charred_Twitcher" } },
        };

        // Per threat level: seconds between hunting parties, party size, star chances.
        private static readonly float[] WaveInterval = { 0f, 150f, 100f, 70f, 50f };
        private static readonly int[] WaveSize = { 0, 1, 2, 3, 3 };
        private static readonly float[] OneStar = { 0f, 0.1f, 0.2f, 0.35f, 0.5f };
        private static readonly float[] TwoStar = { 0f, 0f, 0.05f, 0.1f, 0.2f };

        private static readonly MethodInfo SetHuntPlayer = AccessTools.Method(typeof(BaseAI), "SetHuntPlayer");
        private static readonly FieldInfo BossField = AccessTools.Field(typeof(Character), "m_boss");
        private static readonly MethodInfo IsTamed = AccessTools.Method(typeof(Character), "IsTamed", new System.Type[0]);

        private static int _announced;
        private float _nextWave;

        /// <summary>0 while calm or out of raid, up to <see cref="MaxThreat"/>.</summary>
        public static int Threat
        {
            get
            {
                if (!Plugin.RaidHostility.Value || RaidTracker.InRaid != true)
                {
                    return 0;
                }
                float minutes = RaidTracker.RaidSeconds / 60f;
                float calm = Plugin.CalmMinutes.Value;
                if (minutes < calm)
                {
                    return 0;
                }
                return Mathf.Min(MaxThreat, 1 + Mathf.FloorToInt((minutes - calm) / Mathf.Max(1f, Plugin.ThreatStepMinutes.Value)));
            }
        }

        /// <summary>Raid seconds until the threat goes up a level, or -1.</summary>
        public static float SecondsToNextThreat
        {
            get
            {
                int threat = Threat;
                if (!Plugin.RaidHostility.Value || RaidTracker.InRaid != true || threat >= MaxThreat)
                {
                    return -1f;
                }
                float next = (Plugin.CalmMinutes.Value + threat * Plugin.ThreatStepMinutes.Value) * 60f;
                return Mathf.Max(0f, next - RaidTracker.RaidSeconds);
            }
        }

        public static void Patch(Harmony harmony)
        {
            var setLevel = AccessTools.Method(typeof(Character), "SetLevel", new[] { typeof(int) });
            if (setLevel == null)
            {
                Plugin.Log.LogWarning("Character.SetLevel not found; raid threat won't add stars.");
                return;
            }
            harmony.Patch(setLevel, prefix: new HarmonyMethod(typeof(Hostility), nameof(SetLevelPrefix)));
        }

        // Spawners set a new monster's level right after creating it; near
        // a player deep in a raid, roll for extra stars.
        private static void SetLevelPrefix(Character __instance, ref int level)
        {
            try
            {
                int threat = Threat;
                var player = Player.m_localPlayer;
                if (threat == 0 || player == null || __instance == null || __instance is Player || level > 1
                    || __instance.GetComponent<BaseAI>() == null || (BossField != null && BossField.GetValue(__instance) is bool b && b)
                    || (IsTamed != null && IsTamed.Invoke(__instance, new object[0]) is bool tamed && tamed)
                    || (__instance.transform.position - player.transform.position).sqrMagnitude > 90f * 90f)
                {
                    return;
                }
                level = RollLevel(threat);
            }
            catch (System.Exception e)
            {
                GameAccess.WarnOnce("Hostility.SetLevel", e);
            }
        }

        private static int RollLevel(int threat)
        {
            float r = Random.value;
            if (r < TwoStar[threat])
            {
                return 3;
            }
            return r < TwoStar[threat] + OneStar[threat] ? 2 : 1;
        }

        private void Update()
        {
            try
            {
                var player = Player.m_localPlayer;
                int threat = Threat;
                if (player == null || player.IsDead())
                {
                    _announced = 0;
                    return;
                }

                if (threat != _announced)
                {
                    if (threat > _announced && threat > 0)
                    {
                        player.Message(MessageHud.MessageType.Center,
                            Localization.instance.Localize("$tv_msg_threat_" + threat), 0, null, false);
                        _nextWave = Time.time + 20f;
                    }
                    _announced = threat;
                }

                if (threat == 0 || !Plugin.HuntingParties.Value || Time.time < _nextWave)
                {
                    return;
                }
                _nextWave = Time.time + WaveInterval[threat] * Random.Range(0.8f, 1.2f);
                SendParty(player, threat);
            }
            catch (System.Exception e)
            {
                GameAccess.WarnOnce("Hostility.Update", e);
            }
        }

        private static void SendParty(Player player, int threat)
        {
            if (ZNetScene.instance == null || player.InPlaceMode())
            {
                return;
            }

            var origin = player.transform.position;
            var biome = GameAccess.BiomeAt(origin);
            if (!Hunters.TryGetValue(biome, out var names))
            {
                return;
            }

            int count = WaveSize[threat];
            for (int i = 0; i < count; i++)
            {
                var prefab = ZNetScene.instance.GetPrefab(names[Random.Range(0, names.Length)]);
                if (prefab == null || !FindSpot(origin, out var pos))
                {
                    continue;
                }

                var go = Object.Instantiate(prefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                var character = go.GetComponent<Character>();
                if (character != null)
                {
                    character.SetLevel(RollLevel(threat));
                }
                var ai = go.GetComponent<BaseAI>();
                if (ai != null && SetHuntPlayer != null)
                {
                    SetHuntPlayer.Invoke(ai, new object[] { true });
                }
            }
        }

        // Somewhere 30-45 m away, on dry ground, out of the player's face.
        private static bool FindSpot(Vector3 origin, out Vector3 pos)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                var dir = Random.insideUnitCircle.normalized;
                float dist = Random.Range(30f, 45f);
                var p = origin + new Vector3(dir.x, 0f, dir.y) * dist;
                if (Physics.Raycast(p + Vector3.up * 200f, Vector3.down, out var hit, 400f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    && hit.point.y > 30.5f && Mathf.Abs(hit.point.y - origin.y) < 25f)
                {
                    pos = hit.point + Vector3.up * 0.5f;
                    return true;
                }
            }
            pos = Vector3.zero;
            return false;
        }
    }
}
