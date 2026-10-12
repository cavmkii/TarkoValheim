using TarkoValheim.Content;
using TarkoValheim.Util;
using UnityEngine;

namespace TarkoValheim.Raid
{
    /// <summary>
    /// Polls the local player's position once a second. Leaving every
    /// hideout's radius starts a raid; coming back extracts.
    /// </summary>
    internal class RaidTracker : MonoBehaviour
    {
        // Hideout pieces stream in with their zone, which can be a moment
        // after the player spawns. Without a delay, logging in at home reads
        // as "raid started" followed by "extracted".
        private const float SpawnGrace = 5f;

        private static RaidTracker _instance;

        private Player _player;
        private float _playerSeenAt;
        private float _nextCheck;
        private bool _known;
        private bool _inRaid;
        private float _raidStart;

        private void Awake()
        {
            _instance = this;
        }

        internal static void OnKilledInRaid()
        {
            if (_instance == null)
            {
                return;
            }

            Show(Localization.instance.Localize("$tv_msg_kia") + _instance.RaidTimeSuffix());
            _instance._known = false;
        }

        private void Update()
        {
            try
            {
                if (Time.time < _nextCheck)
                {
                    return;
                }
                _nextCheck = Time.time + 1f;

                var player = Player.m_localPlayer;
                if (player == null || player.IsDead())
                {
                    _player = null;
                    _known = false;
                    return;
                }

                if (player != _player)
                {
                    _player = player;
                    _playerSeenAt = Time.time;
                }
                if (Time.time - _playerSeenAt < SpawnGrace)
                {
                    return;
                }

                bool inRaid = !Hideout.IsNear(player.transform.position);
                if (_known && inRaid == _inRaid)
                {
                    return;
                }

                if (inRaid)
                {
                    _raidStart = Time.time;
                    Show(Localization.instance.Localize("$tv_msg_raid_start"));
                }
                else if (_known)
                {
                    Show(Localization.instance.Localize("$tv_msg_extracted") + RaidTimeSuffix());
                }

                _inRaid = inRaid;
                _known = true;
            }
            catch (System.Exception e)
            {
                GameAccess.WarnOnce("RaidTracker.Update", e);
            }
        }

        private static void Show(string text)
        {
            var player = Player.m_localPlayer;
            if (player != null)
            {
                player.Message(MessageHud.MessageType.Center, text, 0, null, false);
            }
        }

        // Empty when no raid was registered, e.g. a death inside the spawn grace.
        private string RaidTimeSuffix()
        {
            if (!_known || !_inRaid)
            {
                return "";
            }

            int total = Mathf.Max(0, Mathf.FloorToInt(Time.time - _raidStart));
            return " " + Localization.instance.Localize("$tv_msg_raid_time") + $" {total / 60:00}:{total % 60:00}";
        }
    }
}
