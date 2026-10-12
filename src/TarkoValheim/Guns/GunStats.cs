using TarkoValheim.Content;
using UnityEngine;

namespace TarkoValheim.Guns
{
    /// <summary>
    /// A gun's effective numbers: its base spec, changed by installed parts,
    /// then by the shooter's skill. Everything that fires, reloads, aims or
    /// displays a gun reads these instead of the raw spec.
    /// </summary>
    internal sealed class GunStats
    {
        public float DamageMult = 1f;
        public float Velocity;
        public float HipSpread;
        public float AdsSpread;
        public float RecoilUp;
        public float RecoilSide;
        public float ReloadTime;
        public int MagSize;
        public float AdsFov;
        public SightType Sight;

        public static GunStats Of(GunSpec gun, ItemDrop.ItemData item, Player shooter = null)
        {
            float damage = 0f, velocity = 0f, spread = 0f, adsSpread = 0f, recoil = 0f, reload = 0f;
            var stats = new GunStats { AdsFov = gun.AdsFov, Sight = gun.Sight };

            if (item != null)
            {
                foreach (PartSlot slot in System.Enum.GetValues(typeof(PartSlot)))
                {
                    var part = GunMods.Installed(item, slot);
                    if (part == null || !part.FitsGun(gun))
                    {
                        continue;
                    }

                    damage += part.Damage;
                    velocity += part.Velocity;
                    spread += part.Spread;
                    adsSpread += part.AdsSpread;
                    recoil += part.Recoil;
                    if (part.Sight.HasValue)
                    {
                        stats.Sight = part.Sight.Value;
                        stats.AdsFov = part.AdsFov;
                    }
                }
            }

            // Detachable magazine: capacity comes from the inserted magazine
            // (none inserted means nothing to fire). Tube-fed guns use the spec.
            int capacity = gun.MagSize;
            if (!gun.PerRoundReload)
            {
                var mag = item != null ? GunMods.InstalledMag(item) : null;
                capacity = mag != null ? mag.Capacity : 0;
                if (mag != null)
                {
                    reload += mag.Reload;
                    recoil += mag.Recoil;
                }
            }

            float skill = GunSkills.Factor(shooter, gun.Class);

            stats.DamageMult = Mult(damage);
            stats.Velocity = gun.Velocity * Mult(velocity);
            stats.HipSpread = gun.HipSpread * Mult(spread);
            stats.AdsSpread = gun.AdsSpread * Mult(spread + adsSpread) * (1f - 0.3f * skill);
            stats.RecoilUp = gun.RecoilUp * Mult(recoil) * (1f - 0.4f * skill);
            stats.RecoilSide = gun.RecoilSide * Mult(recoil) * (1f - 0.4f * skill);
            stats.ReloadTime = gun.ReloadTime * Mult(reload) * (1f - 0.3f * skill);
            stats.MagSize = capacity;
            return stats;
        }

        // Fractions add up; the total never takes a stat below 20% of base.
        private static float Mult(float fraction)
        {
            return Mathf.Max(0.2f, 1f + fraction);
        }
    }
}
