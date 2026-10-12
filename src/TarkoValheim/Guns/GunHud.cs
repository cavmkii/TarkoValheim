using TarkoValheim.Content;
using TarkoValheim.Stash.UI;
using TarkoValheim.Util;
using UnityEngine.UI;
using UnityEngine;

namespace TarkoValheim.Guns
{
    /// <summary>
    /// Ammo readout (bottom right) and a crosshair that shows the real cone
    /// of fire. In "Tarkov" ammo mode the count is only shown, as a rough
    /// estimate, for a few seconds after checking the magazine (T).
    /// </summary>
    internal class GunHud : MonoBehaviour
    {
        private GameObject _canvasGo;
        private Text _ammo;
        private RectTransform[] _ticks;
        private RectTransform _dot;
        private SightOverlay _sight;
        private bool _hidVanillaCrosshair;

        private void LateUpdate()
        {
            try
            {
                var controller = GunController.Instance;
                var player = Player.m_localPlayer;
                bool show = controller != null && controller.Gun != null && player != null && !player.IsDead()
                    && Hud.instance != null && !InventoryGui.IsVisible();

                SetVanillaCrosshair(!show);

                if (!show)
                {
                    if (_canvasGo != null)
                    {
                        _canvasGo.SetActive(false);
                    }
                    return;
                }

                EnsureCanvas();
                _canvasGo.SetActive(true);

                var gun = controller.Gun;
                var item = controller.Item;
                string name = Localization.instance.Localize(item.m_shared.m_name);
                string mode = GunState.Mode(item, gun).ToString().ToUpperInvariant();
                string count;
                if (controller.Stats.MagSize <= 0)
            {
                count = Localization.instance.Localize("$tv_msg_no_mag");
            }
            else if (Plugin.AmmoDisplay.Value == AmmoDisplayMode.Exact)
                {
                    count = $"{GunState.Mag(item)} / {controller.Stats.MagSize}";
                }
                else
                {
                    count = Time.time < controller.MagCheckUntil ? Estimate(GunState.Mag(item), controller.Stats.MagSize) : "";
                }

                string ammo = Localization.instance.Localize(GunState.LoadedAmmoName(item));
                string status = GunState.Jammed(item) ? "\n<color=#ff6050>" + Localization.instance.Localize("$tv_msg_jammed") + "</color>"
                    : controller.Reloading ? "\n" + Localization.instance.Localize("$tv_msg_reloading") : "";
                _ammo.text = $"<b>{name}</b>  [{mode}]\n<size=26>{count}</size>  {ammo}{status}";

                // Crosshair radius from the cone of fire and the camera's FOV.
                var cam = GameCamera.instance != null ? GameCamera.instance.GetComponent<Camera>() : null;
                float fov = cam != null ? cam.fieldOfView : 65f;
                float radius = 540f * Mathf.Tan(controller.CurrentSpread() * Mathf.Deg2Rad) / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
                radius = Mathf.Clamp(radius, 4f, 200f);
                bool covered = _sight.Update(controller.Stats, controller.Ads);
            bool ticks = Plugin.Crosshair.Value && controller.Ads < 0.9f && !covered;
                for (int i = 0; i < 4; i++)
                {
                    _ticks[i].gameObject.SetActive(ticks);
                    var dir = i == 0 ? Vector2.up : i == 1 ? Vector2.down : i == 2 ? Vector2.left : Vector2.right;
                    _ticks[i].anchoredPosition = dir * (radius + 5f);
                }
                _dot.gameObject.SetActive(Plugin.Crosshair.Value && !covered);
            }
            catch (System.Exception e)
            {
                GameAccess.WarnOnce("GunHud.LateUpdate", e);
            }
        }

        private static string Estimate(int mag, int size)
        {
            if (mag <= 0) return Localization.instance.Localize("$tv_mag_empty");
            float f = mag / (float)size;
            if (f >= 1f) return Localization.instance.Localize("$tv_mag_full");
            if (f > 0.8f) return Localization.instance.Localize("$tv_mag_almost_full");
            if (f > 0.4f) return Localization.instance.Localize("$tv_mag_half");
            if (f > 0.15f) return Localization.instance.Localize("$tv_mag_less_half");
            return Localization.instance.Localize("$tv_mag_almost_empty");
        }

        private void SetVanillaCrosshair(bool visible)
        {
            var crosshair = Hud.instance != null ? GameAccess.Crosshair(Hud.instance) : null;
            if (crosshair == null)
            {
                return;
            }

            if (!visible)
            {
                crosshair.enabled = false;
                _hidVanillaCrosshair = true;
            }
            else if (_hidVanillaCrosshair)
            {
                crosshair.enabled = true;
                _hidVanillaCrosshair = false;
            }
        }

        private void EnsureCanvas()
        {
            if (_canvasGo != null)
            {
                return;
            }

            _canvasGo = new GameObject("TarkoValheim_GunHud");
            _canvasGo.layer = 5;
            DontDestroyOnLoad(_canvasGo);
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            var root = (RectTransform)_canvasGo.transform;

            // First, so the ammo readout and crosshair draw on top of it.
            _sight = new SightOverlay(root);

            _ammo = UiKit.Text(root, "Ammo", "", 18, new Color(0.95f, 0.92f, 0.82f), TextAnchor.LowerRight);
            var art = _ammo.rectTransform;
            art.anchorMin = art.anchorMax = art.pivot = new Vector2(1f, 0f);
            art.anchoredPosition = new Vector2(-40f, 150f);
            art.sizeDelta = new Vector2(520f, 120f);

            _ticks = new RectTransform[4];
            for (int i = 0; i < 4; i++)
            {
                var tick = UiKit.Image(root, "Tick", new Color(1f, 1f, 1f, 0.8f), false).rectTransform;
                tick.anchorMin = tick.anchorMax = tick.pivot = new Vector2(0.5f, 0.5f);
                tick.sizeDelta = i < 2 ? new Vector2(2f, 9f) : new Vector2(9f, 2f);
                _ticks[i] = tick;
            }

            _dot = UiKit.Image(root, "Dot", new Color(1f, 0.25f, 0.2f, 0.9f), false).rectTransform;
            _dot.anchorMin = _dot.anchorMax = _dot.pivot = new Vector2(0.5f, 0.5f);
            _dot.sizeDelta = new Vector2(3f, 3f);
        }
    }

    internal enum AmmoDisplayMode
    {
        Exact,
        Tarkov
    }
}
