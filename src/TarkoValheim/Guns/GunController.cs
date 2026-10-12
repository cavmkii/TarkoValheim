using System.Reflection;
using HarmonyLib;
using TarkoValheim.Content;
using TarkoValheim.Stash.UI;
using TarkoValheim.Util;
using UnityEngine;

namespace TarkoValheim.Guns
{
    /// <summary>
    /// Runs the gun the local player is holding: trigger, fire modes,
    /// magazine, reload, malfunctions, recoil and aim-down-sights. Valheim's
    /// crossbow attack is blocked for these items (see <see cref="GunPatches"/>).
    ///
    /// Keys: LMB fire, RMB aim, R reload / clear jam / (with a full mag)
    /// switch ammo type, B fire mode, T check magazine.
    /// </summary>
    internal class GunController : MonoBehaviour
    {
        public static GunController Instance { get; private set; }

        public const string ReloadButton = "TV_Reload";
        public const string FireModeButton = "TV_FireMode";
        public const string MagCheckButton = "TV_MagCheck";

        private const float DurabilityPerShot = 0.25f;

        // Player look angles, private in Valheim. Recoil turns itself off if
        // they are ever renamed.
        private static readonly FieldInfo LookPitch = AccessTools.Field(typeof(Player), "m_lookPitch");
        private static readonly FieldInfo LookYaw = AccessTools.Field(typeof(Player), "m_lookYaw");

        public GunSpec Gun { get; private set; }
        public ItemDrop.ItemData Item { get; private set; }

        /// <summary>0 = hip, 1 = fully aimed. Drives camera, spread and HUD.</summary>
        public float Ads { get; private set; }

        public bool Reloading => _reloading;
        public bool ClearingJam => _clearingJam;
        public float MagCheckUntil { get; private set; }

        private float _nextShot;
        private int _burstLeft;
        private bool _reloading;
        private bool _clearingJam;
        private float _reloadEnd;
        private string _reloadAmmo;
        private float _bloom;
        private bool _adsToggled;
        private Vector3 _lastPos;
        private float _speed;

        private void Awake()
        {
            Instance = this;
            if (!RecoilAvailable)
            {
                Plugin.Log.LogWarning("Player look fields not found; gun recoil is disabled.");
            }
        }

        private void Update()
        {
            try
            {
                var player = Player.m_localPlayer;
                var item = player != null && !player.IsDead() ? player.GetCurrentWeapon() : null;
                var gun = item != null ? Firearms.FindGun(Content.Gear.PrefabName(item)) : null;

                if (gun != Gun || item != Item)
                {
                    CancelReload();
                    _burstLeft = 0;
                    _adsToggled = false;
                }
                Gun = gun;
                Item = gun != null ? item : null;

                if (player != null)
                {
                    float dt = Mathf.Max(Time.deltaTime, 0.0001f);
                    _speed = Mathf.Lerp(_speed, (player.transform.position - _lastPos).magnitude / dt, 0.2f);
                    _lastPos = player.transform.position;
                }

                bool input = gun != null && GameAccess.TakeInput(player) && !player.InPlaceMode();

                bool aim;
                if (Plugin.AdsToggle.Value)
                {
                    if (input && ZInput.GetButtonDown("Block"))
                    {
                        _adsToggled = !_adsToggled;
                    }
                    aim = input && _adsToggled;
                }
                else
                {
                    aim = input && ZInput.GetButton("Block");
                }

                Ads = Mathf.MoveTowards(Ads, aim ? 1f : 0f, Time.deltaTime / 0.15f);
                _bloom = Mathf.MoveTowards(_bloom, 0f, Time.deltaTime * 3f);

                if (gun == null)
                {
                    return;
                }

                UpdateReload(player, gun, Item);

                if (!input)
                {
                    _burstLeft = 0;
                    return;
                }

                if (ZInput.GetButtonDown(FireModeButton) && gun.Modes.Length > 1)
                {
                    var mode = GunState.CycleMode(Item, gun);
                    Message(mode.ToString().ToUpperInvariant());
                    StashUi.MarkDirty();
                }

                if (ZInput.GetButtonDown(MagCheckButton))
                {
                    MagCheckUntil = Time.time + 3f;
                }

                if (ZInput.GetButtonDown(ReloadButton))
                {
                    StartReload(player, gun, Item);
                }

                var fireMode = GunState.Mode(Item, gun);
                bool pressed = ZInput.GetButtonDown("Attack");
                bool held = ZInput.GetButton("Attack");

                // A tube-fed gun stops loading as soon as you pull the trigger.
                if (_reloading && gun.PerRoundReload && pressed && !_clearingJam && GunState.Mag(Item) > 0)
                {
                    CancelReload();
                }

                if (fireMode == FireMode.Burst && pressed && _burstLeft == 0)
                {
                    _burstLeft = 3;
                }

                bool wantShot = fireMode == FireMode.Auto ? held
                    : fireMode == FireMode.Single ? pressed
                    : _burstLeft > 0;

                if (wantShot && !_reloading && Time.time >= _nextShot)
                {
                    Fire(player, gun, Item);
                }
            }
            catch (System.Exception e)
            {
                GameAccess.WarnOnce("GunController.Update", e);
            }
        }

        /// <summary>Current cone of fire in degrees (for shots and the crosshair).</summary>
        public float CurrentSpread()
        {
            if (Gun == null)
            {
                return 0f;
            }

            float spread = Mathf.Lerp(Gun.HipSpread, Gun.AdsSpread, Ads) + _bloom;
            if (_speed > 1f)
            {
                spread += Gun.HipSpread * 0.6f * (1f - Ads * 0.7f);
            }
            return spread;
        }

        private void Fire(Player player, GunSpec gun, ItemDrop.ItemData item)
        {
            _nextShot = Time.time + 60f / gun.Rpm;

            if (GunState.Jammed(item))
            {
                _burstLeft = 0;
                _nextShot = Time.time + 0.4f;
                Message("$tv_msg_jammed");
                return;
            }

            int mag = GunState.Mag(item);
            if (mag <= 0)
            {
                _burstLeft = 0;
                _nextShot = Time.time + 0.4f;
                Message("$tv_msg_empty");
                return;
            }

            if (item.m_shared.m_useDurability && item.m_durability <= 0f)
            {
                _burstLeft = 0;
                _nextShot = Time.time + 0.4f;
                Message("$tv_msg_broken");
                return;
            }

            var ammo = Firearms.FindAmmo(GunState.LoadedAmmo(item));
            if (ammo == null)
            {
                GunState.SetMag(item, 0);
                return;
            }

            GunState.SetMag(item, mag - 1);
            if (_burstLeft > 0)
            {
                _burstLeft--;
            }

            if (item.m_shared.m_useDurability)
            {
                item.m_durability = Mathf.Max(0f, item.m_durability - DurabilityPerShot);
            }

            var cam = GameCamera.instance != null ? GameCamera.instance.transform : null;
            if (cam == null)
            {
                return;
            }

            // Shoot from the point on the camera ray level with the player's
            // eyes, so third-person shots don't start behind the character.
            Vector3 forward = cam.forward;
            var eyeTransform = GameAccess.Eye(player);
            Vector3 eye = eyeTransform != null ? eyeTransform.position : player.transform.position + Vector3.up * 1.6f;
            Vector3 origin = cam.position + forward * Mathf.Max(0f, Vector3.Dot(eye - cam.position, forward));

            float spread = CurrentSpread();
            float damage = ammo.Damage * Plugin.GunDamage.Value;
            for (int i = 0; i < ammo.Pellets; i++)
            {
                var dir = Spread(forward, spread + ammo.PelletSpread);
                Ballistics.Fire(origin, dir * gun.Velocity * ammo.VelocityFactor, damage, player);
            }

            var hand = GameAccess.LeftHand(player);
            var muzzle = hand != null ? hand.position : eye;
            item.m_shared.m_triggerEffect?.Create(muzzle, Quaternion.LookRotation(forward));

            ApplyRecoil(player, gun);
            _bloom = Mathf.Min(_bloom + gun.RecoilUp * 0.12f, 3f);

            // Malfunction chance climbs with wear: rare when new, common when worn out.
            if (item.m_shared.m_useDurability && item.GetMaxDurability() > 0f)
            {
                float wear = 1f - item.m_durability / item.GetMaxDurability();
                if (Random.value < 0.001f + wear * wear * 0.06f)
                {
                    GunState.SetJammed(item, true);
                    _burstLeft = 0;
                    Message("$tv_msg_jammed");
                }
            }

            StashUi.MarkDirty();
        }

        private static Vector3 Spread(Vector3 dir, float degrees)
        {
            if (degrees <= 0f)
            {
                return dir;
            }

            var r = Random.insideUnitCircle * Mathf.Tan(degrees * Mathf.Deg2Rad);
            return (Quaternion.LookRotation(dir) * new Vector3(r.x, r.y, 1f)).normalized;
        }

        private static bool RecoilAvailable =>
            LookPitch != null && LookPitch.FieldType == typeof(float)
            && LookYaw != null && LookYaw.FieldType == typeof(Quaternion);

        private void ApplyRecoil(Player player, GunSpec gun)
        {
            if (!RecoilAvailable)
            {
                return;
            }

            float skill = player.GetSkillFactor(Skills.SkillType.Crossbows);
            float k = Plugin.RecoilMultiplier.Value * (1f - 0.4f * skill) * (1f - 0.3f * Ads);
            float up = gun.RecoilUp * k;
            float side = Random.Range(-1f, 1f) * gun.RecoilSide * k;

            // Lower pitch looks up (Valheim subtracts mouse Y from it).
            LookPitch.SetValue(player, (float)LookPitch.GetValue(player) - up);
            LookYaw.SetValue(player, (Quaternion)LookYaw.GetValue(player) * Quaternion.Euler(0f, side, 0f));
        }

        // ------------------------------------------------------------------
        // Reloading

        private void StartReload(Player player, GunSpec gun, ItemDrop.ItemData item)
        {
            if (_reloading)
            {
                return;
            }

            if (GunState.Jammed(item))
            {
                _clearingJam = true;
                _reloading = true;
                _reloadEnd = Time.time + 1.1f;
                Message("$tv_msg_clearing");
                return;
            }

            bool full = GunState.Mag(item) >= gun.MagSize;
            var ammo = GunState.PickAmmo(player, gun, item, next: full);
            if (ammo == null)
            {
                if (!full)
                {
                    Message("$tv_msg_no_ammo");
                }
                return;
            }

            // Switching ammo: unload first, then load the new type.
            if (ammo != GunState.LoadedAmmo(item) && GunState.Mag(item) > 0)
            {
                GunState.Unload(player, item);
            }

            _reloadAmmo = ammo;
            _reloading = true;
            _reloadEnd = Time.time + gun.ReloadTime;
            Message(Localization.instance.Localize("$tv_msg_reloading") + " " + Localization.instance.Localize("$" + ammo.ToLowerInvariant()));
        }

        private void UpdateReload(Player player, GunSpec gun, ItemDrop.ItemData item)
        {
            if (!_reloading || Time.time < _reloadEnd)
            {
                return;
            }

            if (_clearingJam)
            {
                GunState.SetJammed(item, false);
                CancelReload();
                StashUi.MarkDirty();
                return;
            }

            int mag = GunState.Mag(item);
            if (GunState.LoadedAmmo(item) != _reloadAmmo && mag > 0)
            {
                GunState.Unload(player, item);
                mag = 0;
            }

            int want = gun.PerRoundReload ? 1 : gun.MagSize - mag;
            int got = GunState.TakeRounds(player, gun.Caliber, _reloadAmmo, want);
            if (got > 0)
            {
                GunState.SetLoadedAmmo(item, _reloadAmmo);
                GunState.SetMag(item, mag + got);
            }

            if (gun.PerRoundReload && got > 0 && mag + got < gun.MagSize)
            {
                _reloadEnd = Time.time + gun.ReloadTime;
            }
            else
            {
                CancelReload();
            }
            StashUi.MarkDirty();
        }

        private void CancelReload()
        {
            _reloading = false;
            _clearingJam = false;
        }

        private static void Message(string text)
        {
            Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, Localization.instance.Localize(text), 0, null, false);
        }
    }
}
