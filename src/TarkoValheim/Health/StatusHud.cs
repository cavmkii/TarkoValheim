using System.Text;
using TarkoValheim.Raid;
using TarkoValheim.Stash.UI;
using TarkoValheim.Util;
using UnityEngine;
using UnityEngine.UI;

namespace TarkoValheim.Health
{
    /// <summary>
    /// A few lines at the bottom left while playing: raid time and threat
    /// level, and any bleed or fracture with what treats it.
    /// </summary>
    internal class StatusHud : MonoBehaviour
    {
        private GameObject _canvasGo;
        private Text _text;
        private float _nextUpdate;

        private void Update()
        {
            try
            {
                if (Time.time < _nextUpdate)
                {
                    return;
                }
                _nextUpdate = Time.time + 0.25f;

                var player = Player.m_localPlayer;
                string text = player != null && !player.IsDead() ? Build(player) : "";
                if (string.IsNullOrEmpty(text))
                {
                    if (_canvasGo != null)
                    {
                        _canvasGo.SetActive(false);
                    }
                    return;
                }

                Ensure();
                _canvasGo.SetActive(true);
                _text.text = text;
            }
            catch (System.Exception e)
            {
                GameAccess.WarnOnce("StatusHud.Update", e);
            }
        }

        public static string Build(Player player)
        {
            var sb = new StringBuilder();
            if (RaidTracker.InRaid == true)
            {
                int s = Mathf.FloorToInt(RaidTracker.RaidSeconds);
                int threat = Hostility.Threat;
                sb.Append($"<color=#e0604f>{UiKit.L("$tv_ui_in_raid")}</color>  {s / 60:00}:{s % 60:00}");
                if (Plugin.RaidHostility.Value)
                {
                    sb.Append("   ").Append(UiKit.L("$tv_ui_threat")).Append(' ').Append(ThreatBar(threat));
                }
                sb.Append('\n');
            }

            foreach (var cp in Conditions.ActiveParts(player))
            {
                var c = cp.Key;
                string color = c == Condition.Fracture ? "#e3b23b" : "#e0604f";
                sb.Append($"<color={color}>{UiKit.L("$tv_cond_" + c.ToString().ToLowerInvariant())}</color>");
                sb.Append($" <color=#c8c2b2>{UiKit.L("$tv_part_" + cp.Value.ToString().ToLowerInvariant())}</color>");
                sb.Append($"  <color=#8a8a84>{UiKit.L("$tv_cond_fix_" + c.ToString().ToLowerInvariant())}</color>\n");
            }
            foreach (var p in Body.Parts)
            {
                if (Body.Destroyed(player, p))
                {
                    sb.Append($"<color=#e0604f>{UiKit.L("$tv_part_" + p.ToString().ToLowerInvariant()).ToUpperInvariant()} {UiKit.L("$tv_tag_destroyed")}</color>");
                    sb.Append($"  <color=#8a8a84>{UiKit.L("$tv_cond_fix_destroyed")}</color>\n");
                }
            }
            float pain = Conditions.PainkillerLeft(player);
            if (pain > 0f)
            {
                int p = Mathf.CeilToInt(pain);
                sb.Append($"<color=#7fd36b>{UiKit.L("$tv_cond_painkiller")}</color> {p / 60}:{p % 60:00}\n");
            }
            return sb.ToString().TrimEnd('\n');
        }

        public static string ThreatBar(int threat)
        {
            var sb = new StringBuilder();
            for (int i = 1; i <= Hostility.MaxThreat; i++)
            {
                sb.Append(i <= threat ? "<color=#e39b3b>|</color>" : "<color=#555550>|</color>");
            }
            return sb.ToString();
        }

        private void Ensure()
        {
            if (_canvasGo != null)
            {
                return;
            }
            _canvasGo = new GameObject("TarkoValheim_StatusHud");
            _canvasGo.layer = 5;
            DontDestroyOnLoad(_canvasGo);
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            _text = UiKit.Text(_canvasGo.transform, "Status", "", 18, Color.white, TextAnchor.LowerLeft);
            var rt = _text.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(30f, 230f);
            rt.sizeDelta = new Vector2(700f, 200f);
        }

        private void OnDestroy()
        {
            if (_canvasGo != null)
            {
                Destroy(_canvasGo);
            }
        }
    }
}
