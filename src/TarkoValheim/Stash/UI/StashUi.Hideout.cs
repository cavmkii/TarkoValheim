using TarkoValheim.Content;
using TarkoValheim.Crafting;
using UnityEngine.UI;
using UnityEngine;

namespace TarkoValheim.Stash.UI
{
    /// <summary>
    /// CRAFTING > HIDEOUT, at a Hideout bench: the hideout's modules, their
    /// levels, what the next level does and costs, and what it needs built
    /// first. Materials can come from the stash. Building one takes a while,
    /// shown with the same progress bar as crafting.
    /// </summary>
    internal partial class StashUi
    {
        private ModuleId _moduleSelection = ModuleId.Workshop;

        private static readonly string[] ModuleIcons = { "Hammer", "TV_Bandage", "Stone", "Feathers" };

        private static Sprite IconOf(string prefab)
        {
            var go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            var drop = go != null ? go.GetComponent<ItemDrop>() : null;
            return drop != null ? drop.m_itemData.GetIcon() : null;
        }

        private static string ModuleName(ModuleId id) => UiKit.L("$tv_module_" + id.ToString().ToLowerInvariant());

        private void BuildModuleList(Player player, HideoutMarker bench, RectTransform col, float y, float w)
        {
            var summary = UiKit.Text(col, "Summary", UiKit.L("$tv_ui_modules_summary"), 13, UiKit.Muted, TextAnchor.MiddleLeft);
            UiKit.TopLeft(summary.rectTransform, 0f, y, w, 22f);
            y += 26f;

            const float rowH = 64f;
            for (int i = 0; i < HideoutModules.All.Length; i++)
            {
                var spec = HideoutModules.All[i];
                ModuleRow(player, bench, col, spec, spec.Id == _moduleSelection, y + i * (rowH + 4f), w, rowH);
            }
        }

        private void ModuleRow(Player player, HideoutMarker bench, RectTransform parent, ModuleSpec spec, bool selected,
            float y, float w, float h)
        {
            var bg = UiKit.Image(parent, "Module", Color.white);
            UiKit.TopLeft(bg.rectTransform, 0f, y, w, h);
            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            var colors = button.colors;
            colors.normalColor = selected ? new Color(0.3f, 0.21f, 0.09f, 1f) : new Color(0.1f, 0.105f, 0.1f, 0.95f);
            colors.highlightedColor = new Color(0.36f, 0.25f, 0.1f, 1f);
            colors.selectedColor = colors.normalColor;
            colors.pressedColor = new Color(0.5f, 0.34f, 0.12f, 1f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0f;
            button.colors = colors;
            var id = spec.Id;
            button.onClick.AddListener(() =>
            {
                _moduleSelection = id;
                MarkDirty();
            });
            if (selected)
            {
                UiKit.Border(bg.transform, UiKit.Accent, 1f);
            }

            var iconBg = UiKit.Image(bg.transform, "IconBg", UiKit.CellBg, false);
            UiKit.TopLeft(iconBg.rectTransform, 6f, 6f, h - 12f, h - 12f);
            var sprite = IconOf(ModuleIcons[(int)spec.Id]);
            if (sprite != null)
            {
                var icon = UiKit.Image(iconBg.transform, "Icon", Color.white, false);
                icon.sprite = sprite;
                icon.preserveAspect = true;
                UiKit.Stretch(icon.rectTransform, 4f);
            }

            int level = bench.Level(spec.Id);
            var name = UiKit.Text(bg.transform, "Name", ModuleName(spec.Id).ToUpperInvariant(), 17, Color.white, TextAnchor.UpperLeft);
            UiKit.TopLeft(name.rectTransform, h + 6f, 8f, w - h - 190f, 24f);

            // Level pips.
            for (int i = 0; i < spec.Max; i++)
            {
                var pip = UiKit.Image(bg.transform, "Pip", i < level ? UiKit.Accent : new Color(0.25f, 0.25f, 0.24f, 1f), false);
                UiKit.TopLeft(pip.rectTransform, h + 6f + i * 26f, 38f, 22f, 8f);
            }

            string state;
            Color stateColor;
            var next = HideoutModules.Next(bench, spec);
            if (next == null)
            {
                state = UiKit.L("$tv_ui_module_max");
                stateColor = UiKit.Muted;
            }
            else if (!HideoutModules.NeedsMet(bench, next))
            {
                state = UiKit.L("$tv_ui_module_locked");
                stateColor = UiKit.Accent;
            }
            else if (HideoutModules.HaveCost(player, next))
            {
                state = level == 0 ? UiKit.L("$tv_ui_module_can_build") : UiKit.L("$tv_ui_module_can_upgrade");
                stateColor = new Color(0.5f, 0.83f, 0.42f);
            }
            else
            {
                state = UiKit.L("$tv_ui_missing");
                stateColor = new Color(0.75f, 0.35f, 0.3f);
            }
            var st = UiKit.Text(bg.transform, "State", state, 13, stateColor, TextAnchor.MiddleRight);
            UiKit.TopLeft(st.rectTransform, w - 180f, 0f, 170f, h);
        }

        private void BuildModuleDetail(Player player, HideoutMarker bench, float x, float w)
        {
            var spec = HideoutModules.Find(_moduleSelection) ?? HideoutModules.All[0];
            var col = UiKit.Rect("ModuleDetail", _frame);
            float h = FrameH - BodyTop - 30f;
            UiKit.TopLeft(col, x, BodyTop, w, h);
            var panel = UiKit.Image(col, "Panel", UiKit.PanelBg, false);
            UiKit.Stretch(panel.rectTransform);

            int level = bench.Level(spec.Id);
            UiKit.Header(col, UiKit.L("$tv_ui_module"), $"{UiKit.L("$tv_ui_level")} {level}/{spec.Max}", 0f, 0f, w);

            var title = UiKit.Text(col, "Title", ModuleName(spec.Id), 26, Color.white);
            UiKit.TopLeft(title.rectTransform, 20f, 40f, w - 40f, 34f);
            var desc = UiKit.Text(col, "Desc", UiKit.L("$tv_module_" + spec.Id.ToString().ToLowerInvariant() + "_desc"), 14, UiKit.Label);
            UiKit.TopLeft(desc.rectTransform, 20f, 80f, w - 40f, 60f);

            // Every level: done ones ticked, the next one highlighted.
            float y = 150f;
            UiKit.Header(col, UiKit.L("$tv_ui_module_levels"), "", 20f, y, w / 2f - 30f);
            y += 28f;
            for (int i = 0; i < spec.Max; i++)
            {
                string mark = i < level ? "<color=#7fd36b>OK</color>" : i == level ? "<color=#e39b3b>></color>" : "<color=#555550>-</color>";
                var line = UiKit.Text(col, "Level", $"{mark}  {UiKit.L("$tv_ui_level")} {i + 1}: {UiKit.L(spec.Levels[i].Effect)}", 14,
                    i < level ? UiKit.Label : i == level ? Color.white : UiKit.Muted);
                UiKit.TopLeft(line.rectTransform, 20f, y, w / 2f - 30f, 60f);
                y += Mathf.Max(24f, line.preferredHeight + 6f);
            }

            var next = HideoutModules.Next(bench, spec);
            float rx = w / 2f;
            float rw = w / 2f - 20f;
            if (next == null)
            {
                var done = UiKit.Text(col, "Max", UiKit.L("$tv_ui_module_maxed"), 15, UiKit.Muted);
                UiKit.TopLeft(done.rectTransform, rx, 150f, rw, 40f);
                return;
            }

            float ry = 150f;
            UiKit.Header(col, UiKit.L("$tv_ui_requirements"), "", rx, ry, rw);
            ry += 30f;
            foreach (var need in next.Needs)
            {
                bool ok = bench.Level(need.Key) >= need.Value;
                string text = $"{ModuleName(need.Key)} {UiKit.L("$tv_ui_level")} {need.Value}";
                var n = UiKit.Text(col, "Need", ok ? text : $"<color=#e39b3b>{text}</color>", 15, UiKit.Label);
                UiKit.TopLeft(n.rectTransform, rx, ry, rw, 24f);
                ry += 28f;
            }
            foreach (var c in next.Cost)
            {
                int have = CraftService.CountPrefab(player, c.Key);
                var row = UiKit.Image(col, "Req", new Color(0.1f, 0.105f, 0.1f, 0.95f), false);
                UiKit.TopLeft(row.rectTransform, rx, ry, rw, 44f);
                var sprite = IconOf(c.Key);
                if (sprite != null)
                {
                    var ri = UiKit.Image(row.transform, "Icon", Color.white, false);
                    ri.sprite = sprite;
                    ri.preserveAspect = true;
                    UiKit.TopLeft(ri.rectTransform, 6f, 4f, 36f, 36f);
                }
                var go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(c.Key) : null;
                var drop = go != null ? go.GetComponent<ItemDrop>() : null;
                var rn = UiKit.Text(row.transform, "Name", drop != null ? UiKit.L(drop.m_itemData.m_shared.m_name) : c.Key, 15,
                    Color.white, TextAnchor.MiddleLeft);
                UiKit.TopLeft(rn.rectTransform, 50f, 0f, rw - 160f, 44f);
                var rc = UiKit.Text(row.transform, "Count", $"{have} / {c.Value}", 16,
                    have >= c.Value ? new Color(0.5f, 0.83f, 0.42f) : new Color(0.85f, 0.35f, 0.3f), TextAnchor.MiddleRight);
                UiKit.TopLeft(rc.rectTransform, rw - 110f, 0f, 100f, 44f);
                ry += 48f;
            }

            bool busy = _crafting != null || _buildModule != null;
            bool can = HideoutModules.CanBuild(player, bench, spec) && !busy;
            string action = UiKit.L(busy ? "$tv_ui_crafting_busy" : level == 0 ? "$tv_ui_module_build" : "$tv_ui_module_upgrade");
            var build = UiKit.Button(col, "Build", action, () => StartModule(player, bench, spec), can, 20);
            ((Button)build).interactable = can;
            UiKit.TopLeft((RectTransform)build.transform, w - 300f, h - 76f, 280f, 56f);
        }

        // Building takes longer than crafting: 4x the craft time per level.
        private void StartModule(Player player, HideoutMarker bench, ModuleSpec spec)
        {
            if (_crafting != null || _buildModule != null || !HideoutModules.CanBuild(player, bench, spec))
            {
                return;
            }
            int level = bench.Level(spec.Id);
            float seconds = Mathf.Max(3f, Plugin.CraftSeconds.Value * 4f * (level + 1)) * WorkshopSpeed(player);
            _buildModule = spec;
            _buildBench = bench;
            _craftingAt = player.GetCurrentCraftingStation();
            _craftStart = Time.time;
            _craftDuration = seconds;
            BuildCraftPopup(IconOf(ModuleIcons[(int)spec.Id]), UiKit.L(level == 0 ? "$tv_ui_building" : "$tv_ui_upgrading"),
                $"{ModuleName(spec.Id)}  <color=#8a8a84>{level} > {level + 1}</color>");
            MarkDirty();
        }

        private void FinishModule(Player player, HideoutMarker bench, ModuleSpec spec)
        {
            if (bench == null || !HideoutModules.Build(player, bench, spec))
            {
                ReportError();
            }
            else
            {
                player.Message(MessageHud.MessageType.Center,
                    $"{ModuleName(spec.Id)} {UiKit.L("$tv_ui_level")} {bench.Level(spec.Id)}", 0, null, false);
            }
            MarkDirty();
        }
    }
}
