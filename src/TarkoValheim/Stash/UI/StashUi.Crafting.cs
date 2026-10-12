using System;
using System.Collections.Generic;
using TarkoValheim.Crafting;
using TarkoValheim.Util;
using UnityEngine.UI;
using UnityEngine;

namespace TarkoValheim.Stash.UI
{
    /// <summary>
    /// CRAFTING tab: the recipe list on the left (what you can make right now
    /// at the top), the selected recipe on the right with what it needs and
    /// what you have. Works at any station, or by hand with none.
    /// </summary>
    internal partial class StashUi
    {
        private Recipe _craftSelection;
        private ItemDrop.ItemData _craftUpgrade;

        private void BuildCrafting(Player player, CraftingStation station)
        {
            var entries = CraftService.Entries(player);
            CraftEntry selected = null;
            foreach (var e in entries)
            {
                if (e.Recipe == _craftSelection && e.Upgrade == _craftUpgrade)
                {
                    selected = e;
                    break;
                }
            }
            if (selected == null && entries.Count > 0)
            {
                selected = entries[0];
                _craftSelection = selected.Recipe;
                _craftUpgrade = selected.Upgrade;
            }

            BuildRecipeList(player, station, entries, selected, 40f, 760f);
            BuildRecipeDetail(player, station, selected, 840f, 1040f);
        }

        private void BuildRecipeList(Player player, CraftingStation station, List<CraftEntry> entries, CraftEntry selected,
            float x, float w)
        {
            var col = UiKit.Rect("Recipes", _frame);
            float h = FrameH - BodyTop - 30f;
            UiKit.TopLeft(col, x, BodyTop, w, h);

            string title = station != null
                ? UiKit.L(GameAccess.GetStationName(station)).ToUpperInvariant()
                : UiKit.L("$tv_ui_hand_crafting");
            string level = station != null ? $"{UiKit.L("$tv_ui_level")} {GameAccess.StationLevel(station)}" : "";
            UiKit.Header(col, title, level, 0f, 0f, w);

            float y = 30f;
            int craftable = 0;
            foreach (var e in entries)
            {
                if (e.CanCraft)
                {
                    craftable++;
                }
            }
            var summary = UiKit.Text(col, "Summary", string.Format(UiKit.L("$tv_ui_recipes_summary"), entries.Count, craftable),
                13, UiKit.Muted, TextAnchor.MiddleLeft);
            UiKit.TopLeft(summary.rectTransform, 0f, y, w - 220f, 28f);

            if (station != null)
            {
                int broken = CraftService.Repairable(player).Count;
                var repair = UiKit.Button(col, "RepairAll", string.Format(UiKit.L("$tv_ui_repair_all"), broken), () =>
                {
                    int n = CraftService.RepairAll(player);
                    if (n > 0)
                    {
                        player.Message(MessageHud.MessageType.Center, string.Format(UiKit.L("$tv_msg_repaired"), n), 0, null, false);
                    }
                    MarkDirty();
                }, broken > 0, 14);
                ((Button)repair).interactable = broken > 0;
                UiKit.TopLeft((RectTransform)repair.transform, w - 210f, y, 210f, 28f);
            }
            y += 36f;

            if (entries.Count == 0)
            {
                var none = UiKit.Text(col, "None", UiKit.L("$tv_ui_no_recipes"), 15, UiKit.Muted);
                UiKit.TopLeft(none.rectTransform, 0f, y, w, 60f);
                return;
            }

            const float rowH = 58f;
            var content = ScrollArea(col, "recipes", 0f, y, w, h - y, entries.Count * (rowH + 4f), out _);
            for (int i = 0; i < entries.Count; i++)
            {
                RecipeRow(content, entries[i], entries[i] == selected, i * (rowH + 4f), w, rowH);
            }
        }

        private void RecipeRow(RectTransform parent, CraftEntry entry, bool selected, float y, float w, float h)
        {
            var bg = UiKit.Image(parent, "Row", Color.white);
            UiKit.TopLeft(bg.rectTransform, 0f, y, w, h);
            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            var colors = button.colors;
            colors.normalColor = selected ? new Color(0.3f, 0.21f, 0.09f, 1f) : new Color(0.1f, 0.105f, 0.1f, 0.95f);
            colors.highlightedColor = new Color(0.36f, 0.25f, 0.1f, 1f);
            colors.selectedColor = colors.normalColor;
            colors.pressedColor = new Color(0.5f, 0.34f, 0.12f, 1f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                _craftSelection = entry.Recipe;
                _craftUpgrade = entry.Upgrade;
                MarkDirty();
            });
            if (selected)
            {
                UiKit.Border(bg.transform, UiKit.Accent, 1f);
            }

            var iconBg = UiKit.Image(bg.transform, "IconBg", UiKit.CellBg, false);
            UiKit.TopLeft(iconBg.rectTransform, 5f, 5f, h - 10f, h - 10f);
            var icon = UiKit.Image(iconBg.transform, "Icon", Color.white, false);
            icon.sprite = entry.Item.GetIcon();
            icon.preserveAspect = true;
            UiKit.Stretch(icon.rectTransform, 3f);

            string name = UiKit.L(entry.Item.m_shared.m_name);
            if (entry.Upgrade != null)
            {
                name = $"{UiKit.L("$tv_ui_upgrade")}: {name}  <color=#8a8a84>{entry.Quality - 1} > {entry.Quality}</color>";
            }
            else if (entry.Recipe.m_amount > 1)
            {
                name += $"  <color=#8a8a84>x{entry.Recipe.m_amount}</color>";
            }
            var label = UiKit.Text(bg.transform, "Name", name, 16, entry.CanCraft ? Color.white : UiKit.Muted, TextAnchor.MiddleLeft);
            UiKit.TopLeft(label.rectTransform, h + 6f, 0f, w - h - 180f, h);

            string state;
            Color stateColor;
            if (entry.CanCraft)
            {
                state = UiKit.L("$tv_ui_can_craft");
                stateColor = new Color(0.5f, 0.83f, 0.42f);
            }
            else if (!entry.StationOk)
            {
                state = $"{UiKit.L("$tv_ui_needs_level")} {entry.StationLevelNeeded}";
                stateColor = UiKit.Accent;
            }
            else
            {
                state = UiKit.L("$tv_ui_missing");
                stateColor = new Color(0.75f, 0.35f, 0.3f);
            }
            var st = UiKit.Text(bg.transform, "State", state, 13, stateColor, TextAnchor.MiddleRight);
            UiKit.TopLeft(st.rectTransform, w - 170f, 0f, 160f, h);
        }

        private void BuildRecipeDetail(Player player, CraftingStation station, CraftEntry entry, float x, float w)
        {
            var col = UiKit.Rect("Detail", _frame);
            float h = FrameH - BodyTop - 30f;
            UiKit.TopLeft(col, x, BodyTop, w, h);
            var panel = UiKit.Image(col, "Panel", UiKit.PanelBg, false);
            UiKit.Stretch(panel.rectTransform);
            UiKit.Header(col, UiKit.L("$tv_ui_recipe"), "", 0f, 0f, w);
            if (entry == null)
            {
                return;
            }

            // The item as it will come out: the next quality for upgrades.
            var preview = entry.Upgrade != null ? entry.Upgrade.Clone() : entry.Item.Clone();
            preview.m_quality = entry.Quality;
            preview.m_durability = preview.GetMaxDurability();

            var iconBg = UiKit.Image(col, "IconBg", UiKit.CellBg, false);
            UiKit.TopLeft(iconBg.rectTransform, 20f, 40f, 150f, 150f);
            UiKit.Border(iconBg.transform, new Color(1f, 1f, 1f, 0.08f), 1f);
            var icon = UiKit.Image(iconBg.transform, "Icon", Color.white, false);
            icon.sprite = preview.GetIcon();
            icon.preserveAspect = true;
            UiKit.Stretch(icon.rectTransform, 10f);

            var title = UiKit.Text(col, "Title", UiKit.L(preview.m_shared.m_name), 26, Color.white);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.TopLeft(title.rectTransform, 190f, 40f, w - 210f, 34f);

            string sub = entry.Upgrade != null
                ? $"{UiKit.L("$tv_ui_upgrade")} {UiKit.L("$tv_ui_quality")} {entry.Quality - 1} > {entry.Quality}"
                : entry.Recipe.m_amount > 1 ? $"{UiKit.L("$tv_ui_makes")} {entry.Recipe.m_amount}" : "";
            if (entry.Recipe.m_craftingStation != null)
            {
                string stationName = UiKit.L(GameAccess.GetStationName(entry.Recipe.m_craftingStation));
                string need = $"{UiKit.L("$tv_ui_requires")} {stationName} {UiKit.L("$tv_ui_level")} {entry.StationLevelNeeded}";
                sub += (sub.Length > 0 ? "\n" : "") + (entry.StationOk ? need : $"<color=#e39b3b>{need}</color>");
            }
            var subText = UiKit.Text(col, "Sub", sub, 15, UiKit.Label);
            UiKit.TopLeft(subText.rectTransform, 190f, 80f, w - 210f, 60f);

            string description;
            try
            {
                description = UiKit.L(preview.GetTooltip());
            }
            catch (Exception)
            {
                description = UiKit.L(preview.m_shared.m_description);
            }
            var desc = UiKit.Text(col, "Description", description, 14, UiKit.Label);
            UiKit.TopLeft(desc.rectTransform, 20f, 210f, w / 2f - 40f, h - 300f);

            // Requirements: icon, name, have / need.
            float rx = w / 2f;
            float rw = w / 2f - 20f;
            UiKit.Header(col, UiKit.L("$tv_ui_requirements"), "", rx, 210f, rw);
            float ry = 240f;
            bool onlyOne = GameAccess.RequireOnlyOneIngredient(entry.Recipe);
            if (onlyOne)
            {
                var note = UiKit.Text(col, "OnlyOne", UiKit.L("$tv_ui_any_one"), 13, UiKit.Muted);
                UiKit.TopLeft(note.rectTransform, rx, ry, rw, 20f);
                ry += 22f;
            }
            foreach (var req in entry.Recipe.m_resources ?? new Piece.Requirement[0])
            {
                int need = req.GetAmount(entry.Quality);
                if (req.m_resItem == null || need <= 0)
                {
                    continue;
                }
                int have = CraftService.Count(player, req.m_resItem);
                var row = UiKit.Image(col, "Req", new Color(0.1f, 0.105f, 0.1f, 0.95f), false);
                UiKit.TopLeft(row.rectTransform, rx, ry, rw, 44f);
                var ri = UiKit.Image(row.transform, "Icon", Color.white, false);
                ri.sprite = req.m_resItem.m_itemData.GetIcon();
                ri.preserveAspect = true;
                UiKit.TopLeft(ri.rectTransform, 6f, 4f, 36f, 36f);
                var rn = UiKit.Text(row.transform, "Name", UiKit.L(req.m_resItem.m_itemData.m_shared.m_name), 15, Color.white,
                    TextAnchor.MiddleLeft);
                UiKit.TopLeft(rn.rectTransform, 50f, 0f, rw - 160f, 44f);
                var rc = UiKit.Text(row.transform, "Count", $"{have} / {need}", 16,
                    have >= need ? new Color(0.5f, 0.83f, 0.42f) : new Color(0.85f, 0.35f, 0.3f), TextAnchor.MiddleRight);
                UiKit.TopLeft(rc.rectTransform, rw - 110f, 0f, 100f, 44f);
                ry += 48f;
            }

            string action = UiKit.L(entry.Upgrade != null ? "$tv_ui_upgrade_button" : "$tv_ui_craft_button");
            var craft = UiKit.Button(col, "Craft", action, () =>
            {
                if (!CraftService.Craft(player, entry))
                {
                    ReportError();
                }
                else
                {
                    if (StashOps.LastError != null)
                    {
                        ReportError();
                    }
                    else
                    {
                        player.Message(MessageHud.MessageType.TopLeft,
                            $"{UiKit.L("$tv_msg_crafted")} {UiKit.L(entry.Item.m_shared.m_name)}", 0, null, false);
                    }
                }
                MarkDirty();
            }, entry.CanCraft, 20);
            ((Button)craft).interactable = entry.CanCraft;
            UiKit.TopLeft((RectTransform)craft.transform, w - 300f, h - 76f, 280f, 56f);
        }
    }
}
