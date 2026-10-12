using System.Collections.Generic;
using TarkoValheim.Content;
using TarkoValheim.Guns;
using TarkoValheim.Util;
using UnityEngine;

namespace TarkoValheim.Stash.UI
{
    /// <summary>SKILLS and HEALTH tabs: read-only character pages.</summary>
    internal partial class StashUi
    {
        // ------------------------------------------------------------------
        // Skills

        private void BuildSkills(Player player)
        {
            var col = UiKit.Rect("Skills", _frame);
            float w = FrameW - 80f;
            float h = FrameH - BodyTop - 30f;
            UiKit.TopLeft(col, 40f, BodyTop, w, h);

            var skills = player.GetSkills() != null ? player.GetSkills().GetSkillList() : null;
            var list = skills != null ? new List<Skills.Skill>(skills) : new List<Skills.Skill>();
            list.RemoveAll(s => s == null || s.m_info == null);
            list.Sort((a, b) => b.m_level.CompareTo(a.m_level));

            UiKit.Header(col, UiKit.L("$tv_tab_skills"), list.Count.ToString(), 0f, 0f, w);

            const int perRow = 3;
            const float cardH = 96f;
            float cardW = (w - (perRow - 1) * 12f) / perRow;
            int rows = Mathf.CeilToInt(list.Count / (float)perRow);
            var content = ScrollArea(col, "skills", 0f, 32f, w, h - 32f, rows * (cardH + 10f), out _);

            for (int i = 0; i < list.Count; i++)
            {
                SkillCard(content, list[i], (i % perRow) * (cardW + 12f), (i / perRow) * (cardH + 10f), cardW, cardH);
            }
        }

        private static void SkillCard(RectTransform parent, Skills.Skill skill, float x, float y, float w, float h)
        {
            var bg = UiKit.Image(parent, "Skill", new Color(0.1f, 0.105f, 0.1f, 0.95f), false);
            UiKit.TopLeft(bg.rectTransform, x, y, w, h);
            UiKit.Border(bg.transform, new Color(1f, 1f, 1f, 0.06f), 1f);
            var rt = bg.rectTransform;

            var iconBg = UiKit.Image(rt, "IconBg", UiKit.CellBg, false);
            UiKit.TopLeft(iconBg.rectTransform, 10f, 10f, 64f, 64f);
            if (skill.m_info.m_icon != null)
            {
                var icon = UiKit.Image(iconBg.transform, "Icon", Color.white, false);
                icon.sprite = skill.m_info.m_icon;
                icon.preserveAspect = true;
                UiKit.Stretch(icon.rectTransform, 6f);
            }

            var type = skill.m_info.m_skill;
            bool gun = GunSkills.TryClass(type, out var cls);
            var name = UiKit.Text(rt, "Name", SkillName(type, gun, cls), 17, Color.white);
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.TopLeft(name.rectTransform, 86f, 10f, w - 160f, 24f);

            int level = Mathf.FloorToInt(skill.m_level);
            var lv = UiKit.Text(rt, "Level", level.ToString(), 30, UiKit.Accent, TextAnchor.UpperRight);
            UiKit.TopLeft(lv.rectTransform, w - 80f, 6f, 70f, 40f);

            if (gun)
            {
                // What the level actually buys, per GunStats.
                float f = Mathf.Clamp01(skill.m_level / 100f);
                var effect = UiKit.Text(rt, "Effect",
                    string.Format(UiKit.L("$tv_ui_gun_skill_effect"), 40f * f, 30f * f, 30f * f), 12, UiKit.Muted);
                UiKit.TopLeft(effect.rectTransform, 86f, 36f, w - 96f, 20f);
            }

            var barBg = UiKit.Image(rt, "Bar", new Color(0f, 0f, 0f, 0.6f), false);
            UiKit.TopLeft(barBg.rectTransform, 86f, h - 26f, w - 96f, 12f);
            float p = level >= 100 ? 1f : GameAccess.SkillProgress(skill);
            var fill = UiKit.Image(barBg.transform, "Fill", UiKit.Accent, false);
            var frt = fill.rectTransform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = new Vector2(p, 1f);
            frt.offsetMin = frt.offsetMax = Vector2.zero;
        }

        private static string SkillName(Skills.SkillType type, bool gun, GunClass cls)
        {
            if (gun)
            {
                switch (cls)
                {
                    case GunClass.Handgun: return UiKit.L("$tv_skill_handguns");
                    case GunClass.Smg: return UiKit.L("$tv_skill_smgs");
                    case GunClass.Shotgun: return UiKit.L("$tv_skill_shotguns");
                    case GunClass.AssaultRifle: return UiKit.L("$tv_skill_assault_rifles");
                    case GunClass.Marksman: return UiKit.L("$tv_skill_marksman");
                }
            }
            string token = "$skill_" + type.ToString().ToLowerInvariant();
            string name = UiKit.L(token);
            // Valheim shows unknown tokens as "[skill_x]".
            return string.IsNullOrEmpty(name) || name.StartsWith("[") || name == token ? type.ToString() : name;
        }

        // ------------------------------------------------------------------
        // Health

        private void BuildHealth(Player player)
        {
            float colW = 580f;

            // Vitals.
            var left = UiKit.Rect("Vitals", _frame);
            UiKit.TopLeft(left, 40f, BodyTop, colW, 600f);
            UiKit.Header(left, UiKit.L("$tv_ui_vitals"), "", 0f, 0f, colW);
            float y = 34f;
            UiKit.Bar(left, UiKit.L("$tv_ui_health"), player.GetHealth(), player.GetMaxHealth(),
                new Color(0.6f, 0.16f, 0.14f, 1f), 0f, y, colW, 30f);
            y += 38f;
            UiKit.Bar(left, UiKit.L("$tv_ui_stamina"), player.GetStamina(), player.GetMaxStamina(),
                new Color(0.62f, 0.52f, 0.16f, 1f), 0f, y, colW, 30f);
            y += 38f;
            if (player.GetMaxEitr() > 0f)
            {
                UiKit.Bar(left, UiKit.L("$tv_ui_eitr"), player.GetEitr(), player.GetMaxEitr(),
                    new Color(0.4f, 0.3f, 0.65f, 1f), 0f, y, colW, 30f);
                y += 38f;
            }
            float weight = player.GetInventory().GetTotalWeight();
            float max = GameAccess.MaxCarryWeight(player);
            UiKit.Bar(left, UiKit.L("$tv_ui_weight"), weight, max,
                weight > max ? new Color(0.7f, 0.2f, 0.15f, 1f) : new Color(0.32f, 0.36f, 0.3f, 1f), 0f, y, colW, 30f);
            y += 44f;

            var facts = new List<string>
            {
                $"{UiKit.L("$tv_ui_armor")}: {GameAccess.BodyArmor(player):0}"
            };
            var raid = Raid.RaidTracker.InRaid;
            if (raid.HasValue)
            {
                facts.Add(UiKit.L(raid.Value ? "$tv_ui_in_raid_long" : "$tv_ui_in_hideout_long"));
            }
            facts.Add(UiKit.L("$tv_ui_death_rule_" + Plugin.RaidDeathRule.Value.ToString().ToLowerInvariant()));
            var factText = UiKit.Text(left, "Facts", string.Join("\n\n", facts.ToArray()), 15, UiKit.Label);
            UiKit.TopLeft(factText.rectTransform, 0f, y, colW, 300f);

            // Food.
            var middle = UiKit.Rect("Food", _frame);
            UiKit.TopLeft(middle, 670f, BodyTop, colW, 600f);
            var foods = GameAccess.Foods(player);
            UiKit.Header(middle, UiKit.L("$tv_ui_food"), $"{foods.Count}/3", 0f, 0f, colW);
            y = 34f;
            if (foods.Count == 0)
            {
                var none = UiKit.Text(middle, "None", UiKit.L("$tv_ui_no_food"), 15, UiKit.Muted);
                UiKit.TopLeft(none.rectTransform, 0f, y, colW, 40f);
            }
            foreach (var food in foods)
            {
                var item = food.Key;
                var row = UiKit.Image(middle, "Row", new Color(0.1f, 0.105f, 0.1f, 0.95f), false);
                UiKit.TopLeft(row.rectTransform, 0f, y, colW, 72f);
                var icon = UiKit.Image(row.transform, "Icon", Color.white, false);
                icon.sprite = item.GetIcon();
                icon.preserveAspect = true;
                UiKit.TopLeft(icon.rectTransform, 8f, 8f, 56f, 56f);
                var name = UiKit.Text(row.transform, "Name", UiKit.L(item.m_shared.m_name), 17, Color.white);
                UiKit.TopLeft(name.rectTransform, 76f, 8f, colW - 200f, 24f);
                string stats = $"<color=#d36b5f>+{item.m_shared.m_food:0} HP</color>   <color=#d3bf5f>+{item.m_shared.m_foodStamina:0} ST</color>";
                if (item.m_shared.m_foodEitr > 0f)
                {
                    stats += $"   <color=#9f8fe0>+{item.m_shared.m_foodEitr:0} EI</color>";
                }
                var st = UiKit.Text(row.transform, "Stats", stats, 14, UiKit.Label);
                UiKit.TopLeft(st.rectTransform, 76f, 38f, colW - 200f, 24f);
                int s = Mathf.Max(0, Mathf.FloorToInt(food.Value));
                var time = UiKit.Text(row.transform, "Time", $"{s / 60}:{s % 60:00}", 20, UiKit.Accent, TextAnchor.MiddleRight);
                UiKit.TopLeft(time.rectTransform, colW - 120f, 0f, 110f, 72f);
                if (item.m_shared.m_foodBurnTime > 0f)
                {
                    UiKit.Bar(row.rectTransform, "", food.Value, item.m_shared.m_foodBurnTime, UiKit.AccentDim, 0f, 68f, colW, 4f);
                }
                y += 80f;
            }

            // Status effects.
            var right = UiKit.Rect("Effects", _frame);
            UiKit.TopLeft(right, 1300f, BodyTop, colW, 600f);
            var effects = GameAccess.StatusEffects(player);
            UiKit.Header(right, UiKit.L("$tv_ui_effects"), effects.Count.ToString(), 0f, 0f, colW);
            y = 34f;
            if (effects.Count == 0)
            {
                var none = UiKit.Text(right, "None", UiKit.L("$tv_ui_no_effects"), 15, UiKit.Muted);
                UiKit.TopLeft(none.rectTransform, 0f, y, colW, 40f);
            }
            foreach (var effect in effects)
            {
                var row = UiKit.Image(right, "Row", new Color(0.1f, 0.105f, 0.1f, 0.95f), false);
                UiKit.TopLeft(row.rectTransform, 0f, y, colW, 48f);
                if (effect.Value != null)
                {
                    var icon = UiKit.Image(row.transform, "Icon", Color.white, false);
                    icon.sprite = effect.Value;
                    icon.preserveAspect = true;
                    UiKit.TopLeft(icon.rectTransform, 8f, 6f, 36f, 36f);
                }
                var name = UiKit.Text(row.transform, "Name", UiKit.L(effect.Key), 16, Color.white, TextAnchor.MiddleLeft);
                UiKit.TopLeft(name.rectTransform, 54f, 0f, colW - 64f, 48f);
                y += 52f;
                if (y > 860f)
                {
                    break;
                }
            }
        }
    }
}
