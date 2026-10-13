using System;
using System.Collections.Generic;
using TarkoValheim.Content;
using TarkoValheim.Crafting;
using TarkoValheim.Util;
using UnityEngine.UI;
using UnityEngine;

namespace TarkoValheim.Stash.UI
{
    /// <summary>Groups for the crafting lists.</summary>
    internal enum CraftCategory
    {
        All,
        Weapons,
        Tools,
        Armor,
        Gear,
        Parts,
        Ammo,
        Consumables,
        Materials,
        Other
    }

    internal enum CraftMode
    {
        Craft,
        Upgrade,
        Hideout
    }

    /// <summary>
    /// CRAFTING tab, with CRAFT and UPGRADE sub-tabs: the list on the left
    /// (what you can do right now at the top), the selected recipe on the
    /// right with what it needs and what you have. Works at any station, or
    /// by hand with none. Crafting takes a moment, shown by a progress bar.
    /// </summary>
    internal partial class StashUi
    {
        private Recipe _craftSelection;
        private ItemDrop.ItemData _craftUpgrade;
        private CraftMode _craftMode = CraftMode.Craft;
        private CraftCategory _craftCategory = CraftCategory.All;

        // The craft in progress, if any.
        private CraftEntry _crafting;
        private CraftingStation _craftingAt;
        private float _craftStart;
        private float _craftDuration;
        private RectTransform _craftPopup;
        private RectTransform _craftFill;
        private Text _craftPercent;

        // A hideout module being built, if any (shares the progress popup).
        private ModuleSpec _buildModule;
        private HideoutMarker _buildBench;

        private void BuildCrafting(Player player, CraftingStation station)
        {
            var bench = HideoutModules.BenchOf(station);
            if (_craftMode == CraftMode.Hideout && bench == null)
            {
                _craftMode = CraftMode.Craft;
            }
            int readyModules = 0;
            if (bench != null)
            {
                foreach (var m in HideoutModules.All)
                {
                    if (HideoutModules.CanBuild(player, bench, m))
                    {
                        readyModules++;
                    }
                }
            }

            var all = CraftService.Entries(player);
            var modeEntries = all.FindAll(e => (e.Upgrade != null) == (_craftMode == CraftMode.Upgrade));

            // Category: counts for the strip, then only the chosen one (in
            // category order under ALL, keeping "ready first" within each).
            var categoryCounts = new int[System.Enum.GetValues(typeof(CraftCategory)).Length];
            foreach (var e in modeEntries)
            {
                categoryCounts[(int)CategoryOf(e)]++;
            }
            categoryCounts[0] = modeEntries.Count;
            if (_craftCategory != CraftCategory.All && categoryCounts[(int)_craftCategory] == 0)
            {
                _craftCategory = CraftCategory.All;
            }
            var entries = new List<CraftEntry>();
            foreach (CraftCategory cat in System.Enum.GetValues(typeof(CraftCategory)))
            {
                if (cat == CraftCategory.All || (_craftCategory != CraftCategory.All && cat != _craftCategory))
                {
                    continue;
                }
                foreach (var e in modeEntries)
                {
                    if (CategoryOf(e) == cat)
                    {
                        entries.Add(e);
                    }
                }
            }
            int readyCraft = all.FindAll(e => e.Upgrade == null && e.CanCraft).Count;
            int readyUpgrade = all.FindAll(e => e.Upgrade != null && e.CanCraft).Count;
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

            var counts = new[] { readyCraft, readyUpgrade, readyModules };
            if (_craftMode == CraftMode.Hideout)
            {
                var col = CraftHeader(player, station, bench, counts, 40f, 760f, out float top);
                BuildModuleList(player, bench, col, top, 760f);
                BuildModuleDetail(player, bench, 840f, 1040f);
                return;
            }
            BuildRecipeList(player, station, bench, entries, selected, counts, categoryCounts, 40f, 760f);
            BuildRecipeDetail(player, station, selected, 840f, 1040f);
        }

        private void BuildRecipeList(Player player, CraftingStation station, HideoutMarker bench, List<CraftEntry> entries,
            CraftEntry selected, int[] counts, int[] categoryCounts, float x, float w)
        {
            var col = CraftHeader(player, station, bench, counts, x, w, out float y);
            y = CategoryStrip(col, categoryCounts, y, w);
            float h = FrameH - BodyTop - 30f;
            int craftable = 0;
            foreach (var e in entries)
            {
                if (e.CanCraft)
                {
                    craftable++;
                }
            }

            var summary = UiKit.Text(col, "Summary",
                string.Format(UiKit.L(_craftMode == CraftMode.Craft ? "$tv_ui_recipes_summary" : "$tv_ui_upgrades_summary"), entries.Count, craftable),
                13, UiKit.Muted, TextAnchor.MiddleLeft);
            UiKit.TopLeft(summary.rectTransform, 0f, y, w, 22f);
            y += 26f;

            if (entries.Count == 0)
            {
                var none = UiKit.Text(col, "None", UiKit.L(_craftMode == CraftMode.Craft ? "$tv_ui_no_recipes" : "$tv_ui_no_upgrades"), 15, UiKit.Muted);
                UiKit.TopLeft(none.rectTransform, 0f, y, w, 60f);
                return;
            }

            // Under ALL, a heading starts each category.
            const float rowH = 58f;
            const float headH = 28f;
            bool headings = _craftCategory == CraftCategory.All;
            float contentH = 0f;
            var last = (CraftCategory)(-1);
            foreach (var e in entries)
            {
                var cat = CategoryOf(e);
                if (headings && cat != last)
                {
                    contentH += headH;
                    last = cat;
                }
                contentH += rowH + 4f;
            }

            var content = ScrollArea(col, "recipes", 0f, y, w, h - y, contentH, out _);
            float ry = 0f;
            last = (CraftCategory)(-1);
            foreach (var e in entries)
            {
                var cat = CategoryOf(e);
                if (headings && cat != last)
                {
                    UiKit.Header(content, UiKit.L("$tv_craftcat_" + cat.ToString().ToLowerInvariant()), categoryCounts[(int)cat].ToString(),
                        0f, ry + 2f, w);
                    ry += headH;
                    last = cat;
                }
                RecipeRow(content, e, e == selected, ry, w, rowH);
                ry += rowH + 4f;
            }
        }

        /// <summary>ALL plus every category that has something in it, with
        /// counts. Returns where the list starts.</summary>
        private float CategoryStrip(RectTransform col, int[] counts, float y, float w)
        {
            var shown = new List<CraftCategory>();
            foreach (CraftCategory cat in System.Enum.GetValues(typeof(CraftCategory)))
            {
                if (cat == CraftCategory.All || counts[(int)cat] > 0)
                {
                    shown.Add(cat);
                }
            }
            const float gap = 4f;
            const float chipH = 26f;
            int perRow = Mathf.Min(shown.Count, 6);
            float chipW = Mathf.Floor((w - (perRow - 1) * gap) / perRow);
            for (int i = 0; i < shown.Count; i++)
            {
                var cat = shown[i];
                string label = UiKit.L("$tv_craftcat_" + cat.ToString().ToLowerInvariant()) + "  " + counts[(int)cat];
                var chip = UiKit.Button(col, "Cat_" + cat, label, () =>
                {
                    if (_craftCategory != cat)
                    {
                        _craftCategory = cat;
                        _craftSelection = null;
                        _craftUpgrade = null;
                        _savedScroll.Remove("recipes");
                        MarkDirty();
                    }
                }, _craftCategory == cat, 11);
                UiKit.TopLeft((RectTransform)chip.transform, (i % perRow) * (chipW + gap), y + (i / perRow) * (chipH + gap), chipW, chipH);
            }
            int rows = Mathf.CeilToInt(shown.Count / (float)perRow);
            return y + rows * (chipH + gap) + 4f;
        }

        private static CraftCategory CategoryOf(CraftEntry e) => CategoryOf(e.Item, e.Recipe.m_item.name);

        /// <summary>The group an item is listed under. Recipe items are prefab
        /// data with no drop prefab set, so the prefab name is passed in.</summary>
        public static CraftCategory CategoryOf(ItemDrop.ItemData item, string prefab)
        {
            var type = item.m_shared.m_itemType;
            string name = prefab ?? Content.Gear.PrefabName(item);
            foreach (var gear in Content.Gear.All)
            {
                if (gear.Id == name)
                {
                    return CraftCategory.Gear;
                }
            }
            if (Content.WeaponParts.Find(name) != null)
            {
                return CraftCategory.Parts;
            }
            if (Loadout.IsHandItem(item))
            {
                return Loadout.IsTool(item) ? CraftCategory.Tools : CraftCategory.Weapons;
            }
            if (Loadout.ArmorSlotFor(item, out _))
            {
                return CraftCategory.Armor;
            }
            if (type == ItemDrop.ItemData.ItemType.Ammo || type == ItemDrop.ItemData.ItemType.AmmoNonEquipable)
            {
                return CraftCategory.Ammo;
            }
            if (type == ItemDrop.ItemData.ItemType.Consumable)
            {
                return CraftCategory.Consumables;
            }
            if (type == ItemDrop.ItemData.ItemType.Material)
            {
                return CraftCategory.Materials;
            }
            return CraftCategory.Other;
        }

        /// <summary>Station header, the sub-tabs and REPAIR ALL. Returns the
        /// column and where the content below starts.</summary>
        private RectTransform CraftHeader(Player player, CraftingStation station, HideoutMarker bench, int[] counts,
            float x, float w, out float contentTop)
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

            // CRAFT | UPGRADE (| HIDEOUT at a Hideout bench), with how many are ready.
            var modes = bench != null
                ? new[] { CraftMode.Craft, CraftMode.Upgrade, CraftMode.Hideout }
                : new[] { CraftMode.Craft, CraftMode.Upgrade };
            for (int i = 0; i < modes.Length; i++)
            {
                var mode = modes[i];
                int ready = counts[(int)mode];
                string label = UiKit.L("$tv_ui_sub_" + mode.ToString().ToLowerInvariant()) + (ready > 0 ? $"  ({ready})" : "");
                var tab = UiKit.Button(col, "Mode_" + mode, label, () =>
                {
                    if (_craftMode != mode)
                    {
                        _craftMode = mode;
                        _craftSelection = null;
                        _craftUpgrade = null;
                        _savedScroll.Remove("recipes");
                        MarkDirty();
                    }
                }, _craftMode == mode, 15);
                UiKit.TopLeft((RectTransform)tab.transform, i * 164f, y, 160f, 32f);
            }

            if (station != null && _craftMode != CraftMode.Hideout)
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
                UiKit.TopLeft((RectTransform)repair.transform, w - 210f, y + 2f, 210f, 28f);
            }
            contentTop = y + 40f;
            return col;
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
            // No fade: a fresh button would otherwise fade in from white,
            // which flashes every time the screen is rebuilt.
            colors.fadeDuration = 0f;
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
            else if (!entry.GateOk)
            {
                state = $"{UiKit.L("$tv_ui_needs")} {UiKit.L("$tv_module_" + entry.GateModule.ToString().ToLowerInvariant()).ToUpperInvariant()} {entry.GateLevel}";
                stateColor = UiKit.Accent;
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
            if (entry.Gated)
            {
                string gate = $"{UiKit.L("$tv_ui_requires")} {UiKit.L("$tv_module_" + entry.GateModule.ToString().ToLowerInvariant())} {UiKit.L("$tv_ui_level")} {entry.GateLevel}";
                sub += (sub.Length > 0 ? "\n" : "") + (entry.GateOk ? gate : $"<color=#e39b3b>{gate}</color>");
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
            foreach (var req in CraftService.Requirements(player, entry.Recipe))
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

            bool busy = _crafting != null || _buildModule != null;
            string action = UiKit.L(busy ? "$tv_ui_crafting_busy" : entry.Upgrade != null ? "$tv_ui_upgrade_button" : "$tv_ui_craft_button");
            var craft = UiKit.Button(col, "Craft", action, () => StartCraft(player, entry, station), entry.CanCraft && !busy, 20);
            ((Button)craft).interactable = entry.CanCraft && !busy;
            UiKit.TopLeft((RectTransform)craft.transform, w - 300f, h - 76f, 280f, 56f);
        }
    
        // ------------------------------------------------------------------
        // Crafting in progress

        private void StartCraft(Player player, CraftEntry entry, CraftingStation station)
        {
            if (_crafting != null || _buildModule != null || entry == null || !entry.CanCraft)
            {
                return;
            }

            float seconds = Plugin.CraftSeconds.Value * (entry.Upgrade != null ? 1.5f : 1f) * WorkshopSpeed(player);
            if (seconds <= 0.01f)
            {
                FinishCraft(player, entry);
                return;
            }

            _crafting = entry;
            _craftingAt = station;
            _craftStart = Time.time;
            _craftDuration = seconds;
            string name = UiKit.L(entry.Item.m_shared.m_name);
            if (entry.Upgrade != null)
            {
                name += $"  <color=#8a8a84>{entry.Quality - 1} > {entry.Quality}</color>";
            }
            BuildCraftPopup((entry.Upgrade ?? entry.Item).GetIcon(),
                UiKit.L(entry.Upgrade != null ? "$tv_ui_upgrading" : "$tv_ui_crafting_now"), name);
            MarkDirty();
        }

        /// <summary>Called every frame from Update while the screen is open.</summary>
        private void UpdateCraft(Player player, CraftingStation station)
        {
            if (_crafting == null && _buildModule == null)
            {
                return;
            }
            // Walking away from the station (or to another one) stops it.
            if (station != _craftingAt || player.IsDead())
            {
                CancelCraft();
                return;
            }

            float p = Mathf.Clamp01((Time.time - _craftStart) / _craftDuration);
            if (_craftFill != null)
            {
                _craftFill.anchorMax = new Vector2(p, 1f);
            }
            if (_craftPercent != null)
            {
                _craftPercent.text = Mathf.FloorToInt(p * 100f) + "%";
            }
            if (p >= 1f)
            {
                var entry = _crafting;
                var module = _buildModule;
                var bench = _buildBench;
                CancelCraft();
                if (module != null)
                {
                    FinishModule(player, bench, module);
                }
                else
                {
                    FinishCraft(player, entry);
                }
            }
        }

        private void FinishCraft(Player player, CraftEntry entry)
        {
            // Rechecked by Craft: something may have moved during the wait.
            if (!CraftService.Craft(player, entry))
            {
                ReportError();
            }
            else if (StashOps.LastError != null)
            {
                ReportError();
            }
            else
            {
                player.Message(MessageHud.MessageType.TopLeft,
                    $"{UiKit.L(entry.Upgrade != null ? "$tv_msg_upgraded" : "$tv_msg_crafted")} {UiKit.L(entry.Item.m_shared.m_name)}", 0, null, false);
            }
            MarkDirty();
        }

        private void CancelCraft()
        {
            if (_craftPopup != null)
            {
                Destroy(_craftPopup.gameObject);
            }
            _craftPopup = null;
            _craftFill = null;
            _craftPercent = null;
            if (_crafting != null || _buildModule != null)
            {
                _crafting = null;
                _buildModule = null;
                _buildBench = null;
                MarkDirty();
            }
        }

        // Lives on the canvas root, not the rebuilt content, so screen
        // rebuilds during the wait don't touch it.
        private void BuildCraftPopup(Sprite iconSprite, string verb, string name)
        {
            const float w = 560f;
            const float h = 150f;
            var border = UiKit.Image(_root, "CraftProgress", UiKit.Accent);
            _craftPopup = border.rectTransform;
            _craftPopup.anchorMin = _craftPopup.anchorMax = _craftPopup.pivot = new Vector2(0.5f, 0.5f);
            _craftPopup.sizeDelta = new Vector2(w, h);
            _craftPopup.anchoredPosition = new Vector2(0f, -40f * _fitScale);
            _craftPopup.localScale = Vector3.one * _fitScale;
            var fill = UiKit.Image(_craftPopup, "Fill", new Color(0.06f, 0.06f, 0.06f, 0.98f), false);
            UiKit.Stretch(fill.rectTransform, 1f);

            var iconBg = UiKit.Image(_craftPopup, "IconBg", UiKit.CellBg, false);
            UiKit.TopLeft(iconBg.rectTransform, 16f, 16f, 90f, 90f);
            var icon = UiKit.Image(iconBg.transform, "Icon", Color.white, false);
            icon.sprite = iconSprite;
            icon.preserveAspect = true;
            UiKit.Stretch(icon.rectTransform, 8f);

            var title = UiKit.Text(_craftPopup, "Title", $"<color=#e39b3b>{verb}</color>  {name}", 18, Color.white, TextAnchor.MiddleLeft);
            UiKit.TopLeft(title.rectTransform, 122f, 18f, w - 140f, 28f);

            var barBg = UiKit.Image(_craftPopup, "Bar", new Color(0f, 0f, 0f, 0.7f), false);
            UiKit.TopLeft(barBg.rectTransform, 122f, 56f, w - 140f, 20f);
            UiKit.Border(barBg.transform, new Color(1f, 1f, 1f, 0.08f), 1f);
            var barFill = UiKit.Image(barBg.transform, "Fill", UiKit.Accent, false);
            _craftFill = barFill.rectTransform;
            _craftFill.anchorMin = Vector2.zero;
            _craftFill.anchorMax = new Vector2(0f, 1f);
            _craftFill.offsetMin = new Vector2(1f, 1f);
            _craftFill.offsetMax = new Vector2(0f, -1f);
            _craftPercent = UiKit.Text(barBg.transform, "Percent", "0%", 13, Color.white, TextAnchor.MiddleCenter);
            UiKit.Stretch(_craftPercent.rectTransform);

            var cancel = UiKit.Button(_craftPopup, "Cancel", UiKit.L("$tv_ui_cancel"), CancelCraft, false, 14);
            UiKit.TopLeft((RectTransform)cancel.transform, w - 138f, 94f, 120f, 36f);
        }
    
        /// <summary>Workshop: crafting in the hideout is 25% faster per level.</summary>
        private static float WorkshopSpeed(Player player)
        {
            int level = HideoutModules.LevelAt(player.transform.position, ModuleId.Workshop);
            return Mathf.Max(0.25f, 1f - 0.25f * level);
        }
    }
}
