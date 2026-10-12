using System.Collections.Generic;
using Jotunn.Configs;
using Jotunn.Managers;
using TarkoValheim.Content;
using UnityEngine;

namespace TarkoValheim.Guns
{
    /// <summary>
    /// One Valheim skill per weapon class. Landing hits trains it; a higher
    /// level means less recoil, faster reloads and a steadier aim (see
    /// <see cref="GunStats"/>). Registered in Awake, before skills load.
    /// </summary>
    internal static class GunSkills
    {
        private static readonly Dictionary<GunClass, Skills.SkillType> Types = new Dictionary<GunClass, Skills.SkillType>();

        public static void Register()
        {
            Add(GunClass.Handgun, "handguns", new Color(0.85f, 0.75f, 0.45f));
            Add(GunClass.Smg, "smgs", new Color(0.6f, 0.8f, 0.9f));
            Add(GunClass.Shotgun, "shotguns", new Color(0.9f, 0.45f, 0.3f));
            Add(GunClass.AssaultRifle, "assault_rifles", new Color(0.55f, 0.85f, 0.45f));
            Add(GunClass.Marksman, "marksman", new Color(0.8f, 0.55f, 0.9f));
        }

        private static void Add(GunClass cls, string key, Color tint)
        {
            var type = SkillManager.Instance.AddSkill(new SkillConfig
            {
                Identifier = "cavmkii.tarkovalheim.skill." + key,
                Name = "$tv_skill_" + key,
                Description = "$tv_skill_" + key + "_desc",
                IncreaseStep = 1f,
                Icon = Icon(tint)
            });
            Types[cls] = type;
        }

        public static Skills.SkillType For(GunClass cls)
        {
            return Types.TryGetValue(cls, out var type) ? type : Skills.SkillType.Crossbows;
        }

        /// <summary>0 at level 0, 1 at level 100.</summary>
        public static float Factor(Player player, GunClass cls)
        {
            return player != null ? Mathf.Clamp01(player.GetSkillFactor(For(cls))) : 0f;
        }

        // A cartridge silhouette, drawn in code: no asset files needed.
        private static Sprite Icon(Color tint)
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var pixels = new Color32[n * n];
            var brass = (Color32)tint;
            var tip = (Color32)Color.Lerp(tint, Color.white, 0.35f);
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    // Case: x 24..40, y 6..40. Bullet: tapering from y 40 to 58.
                    bool body = x >= 24 && x < 40 && y >= 6 && y < 40;
                    bool rim = x >= 22 && x < 42 && y >= 6 && y < 10;
                    float half = 8f * (1f - (y - 40) / 18f);
                    bool bullet = y >= 40 && y < 58 && Mathf.Abs(x - 31.5f) < half;
                    pixels[y * n + x] = bullet ? tip : (body || rim) ? brass : new Color32(0, 0, 0, 0);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f));
        }
    }
}
