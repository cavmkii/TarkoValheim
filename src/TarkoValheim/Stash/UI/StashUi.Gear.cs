using System.Collections.Generic;
using TarkoValheim.Content;
using TarkoValheim.Guns;
using TarkoValheim.Util;
using UnityEngine.UI;
using UnityEngine;

namespace TarkoValheim.Stash.UI
{
    internal enum ItemFilter
    {
        All,
        Weapons,
        Ammo,
        Mods,
        Gear,
        Armor,
        Food,
        Other
    }

    /// <summary>
    /// GEAR tab, laid out like Tarkov's: equipment slots and vitals on the
    /// left, rig / pockets / backpack / pouch in the middle, the open
    /// container (or the weapon modding panel) on the right, quick-use bar
    /// along the bottom.
    /// </summary>
    internal partial class StashUi
    {
        private const float ColumnTop = BodyTop;
        private const float QuickBarY = 1000f;
        private const float ColumnH = QuickBarY - BodyTop - 12f;

        private ItemFilter _filter = ItemFilter.All;

        private void BuildGear(Player player, Container container)
        {
            var inventory = player.GetInventory();
            var lo = Reconciler.Current ?? Loadout.Build(player);

            BuildEquipmentColumn(player, lo, inventory, 40f, 560f);
            BuildCarryColumn(lo, inventory, 640f, 600f);
            BuildRightColumn(player, container, 1280f, 600f);
            BuildQuickBar(lo, inventory);
        }

        private RectTransform Column(string name, float x, float w)
        {
            var col = UiKit.Rect(name, _frame);
            UiKit.TopLeft(col, x, ColumnTop, w, ColumnH);
            return col;
        }

        // ------------------------------------------------------------------
        // Left: equipment and vitals

        private void BuildEquipmentColumn(Player player, Loadout lo, Inventory inventory, float x, float w)
        {
            var col = Column("Equipment", x, w);
            float cell = Plugin.CellSize.Value;
            float box = Mathf.Round(cell * 2.4f);
            float y = 0f;

            UiKit.Header(col, UiKit.L("$tv_ui_gear"), "", 0f, y, w);
            y += 30f;

            float quarter = (w - 3 * 10f) / 4f;
            var row1 = new[] { SlotId.Head, SlotId.Body, SlotId.Legs, SlotId.Cape };
            for (int i = 0; i < row1.Length; i++)
            {
                SlotBox(col, lo, inventory, row1[i], i * (quarter + 10f), y, quarter, box);
            }
            y += box + 10f;
            SlotBox(col, lo, inventory, SlotId.Belt, 0f, y, quarter, box);
            SlotBox(col, lo, inventory, SlotId.Trinket, quarter + 10f, y, quarter, box);
            y += box + 10f;

            SlotBox(col, lo, inventory, SlotId.Primary, 0f, y, w, box);
            y += box + 10f;
            SlotBox(col, lo, inventory, SlotId.Secondary, 0f, y, w, box);
            y += box + 10f;
            float half = (w - 10f) / 2f;
            SlotBox(col, lo, inventory, SlotId.Holster, 0f, y, half, box);
            SlotBox(col, lo, inventory, SlotId.Melee, half + 10f, y, half, box);
            y += box + 16f;

            UiKit.Header(col, UiKit.L("$tv_ui_vitals"), "", 0f, y, w);
            y += 28f;
            UiKit.Bar(col, UiKit.L("$tv_ui_health"), player.GetHealth(), player.GetMaxHealth(),
                new Color(0.6f, 0.16f, 0.14f, 1f), 0f, y, w);
            y += 26f;
            UiKit.Bar(col, UiKit.L("$tv_ui_stamina"), player.GetStamina(), player.GetMaxStamina(),
                new Color(0.62f, 0.52f, 0.16f, 1f), 0f, y, w);
            y += 26f;
            float weight = inventory.GetTotalWeight();
            float max = GameAccess.MaxCarryWeight(player);
            UiKit.Bar(col, UiKit.L("$tv_ui_weight"), weight, max,
                weight > max ? new Color(0.7f, 0.2f, 0.15f, 1f) : new Color(0.32f, 0.36f, 0.3f, 1f), 0f, y, w);
            y += 26f;
            var armor = UiKit.Text(col, "Armor", $"{UiKit.L("$tv_ui_armor")}: {GameAccess.BodyArmor(player):0}", 14, UiKit.Label);
            UiKit.TopLeft(armor.rectTransform, 0f, y, w, 20f);
            y += 22f;

            FitInto(col, w, y, w, ColumnH);
        }

        private void SlotBox(RectTransform parent, Loadout lo, Inventory inventory, SlotId slot, float x, float y, float w, float h)
        {
            bool secure = slot == SlotId.Secure;
            var bg = UiKit.Image(parent, "Slot_" + slot, secure ? UiKit.SecureCellBg : UiKit.SlotBg);
            UiKit.TopLeft(bg.rectTransform, x, y, w, h);
            UiKit.Border(bg.transform, new Color(1f, 1f, 1f, 0.08f), 1f);

            var caption = UiKit.Text(bg.transform, "Caption", UiKit.L("$tv_slot_" + slot.ToString().ToLowerInvariant()),
                11, secure ? UiKit.SecureText : UiKit.Muted, TextAnchor.UpperLeft);
            caption.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Stretch(caption.rectTransform, 4f);

            _targets.Add(new DropTarget
            {
                Rect = bg.rectTransform, Inventory = inventory, IsSlot = true, Slot = slot,
                Cell = Plugin.CellSize.Value
            });

            if (lo.Slots.TryGetValue(slot, out var item))
            {
                AddItemView(bg.rectTransform, item, inventory, 0f, 16f, w, h - 16f, false);
            }
        }

        // ------------------------------------------------------------------
        // Middle: what you carry things in

        private void BuildCarryColumn(Loadout lo, Inventory inventory, float x, float w)
        {
            var col = Column("Carry", x, w);
            float cell = Plugin.CellSize.Value;
            float box = Mathf.Round(cell * 2.4f);
            float y = 0f;

            y = CarrySection(col, lo, inventory, SlotId.Rig, Loadout.RigGrid, "$tv_ui_rig", UiKit.Label, UiKit.CellBg, box, w, y);

            lo.Grids.TryGetValue(Loadout.Pockets, out var pockets);
            UiKit.Header(col, UiKit.L("$tv_ui_pockets"), pockets != null ? Capacity(pockets) : "", 0f, y, w);
            y += 28f;
            if (pockets != null)
            {
                DrawGrid(col, pockets, inventory, 0f, y, UiKit.CellBg, null);
                y += pockets.H * cell;
            }
            y += 14f;

            y = CarrySection(col, lo, inventory, SlotId.Backpack, Loadout.PackGrid, "$tv_ui_backpack", UiKit.Label, UiKit.CellBg, box, w, y);
            y = CarrySection(col, lo, inventory, SlotId.Secure, Loadout.SecureGrid, "$tv_ui_secure", UiKit.SecureText, UiKit.SecureCellBg, box, w, y);

            if (lo.Unplaced.Count > 0)
            {
                UiKit.Header(col, UiKit.L("$tv_ui_unsorted"), lo.Unplaced.Count.ToString(), 0f, y, w, UiKit.Warning);
                y += 28f;
                int perRow = Mathf.Max(1, Mathf.FloorToInt(w / cell));
                for (int i = 0; i < lo.Unplaced.Count; i++)
                {
                    AddItemView(col, lo.Unplaced[i], inventory, (i % perRow) * cell, y + (i / perRow) * cell, cell, cell, false);
                }
                y += Mathf.CeilToInt(lo.Unplaced.Count / (float)perRow) * cell + 8f;
            }

            FitInto(col, w, y, w, ColumnH);
        }

        private float CarrySection(RectTransform col, Loadout lo, Inventory inventory, SlotId slot, string gridId, string title,
            Color titleColor, Color cellColor, float box, float w, float y)
        {
            lo.Grids.TryGetValue(gridId, out var grid);
            string caption = UiKit.L(title);
            if (lo.Slots.TryGetValue(slot, out var item))
            {
                caption += "  <color=#8a8a84>" + UiKit.L(item.m_shared.m_name) + "</color>";
            }
            UiKit.Header(col, caption, grid != null ? Capacity(grid) : "", 0f, y, w, titleColor);
            y += 28f;

            SlotBox(col, lo, inventory, slot, 0f, y, box, box);
            float gridH = 0f;
            if (grid != null)
            {
                DrawGrid(col, grid, inventory, box + 12f, y, cellColor, null);
                gridH = grid.H * Plugin.CellSize.Value;
            }
            else
            {
                var hint = UiKit.Text(col, "Empty", UiKit.L(slot == SlotId.Secure ? "$tv_ui_no_secure" : "$tv_ui_no_rig_hint"),
                    13, UiKit.Muted, TextAnchor.MiddleLeft);
                UiKit.TopLeft(hint.rectTransform, box + 12f, y, w - box - 12f, box);
            }
            return y + Mathf.Max(box, gridH) + 14f;
        }

        private static string Capacity(TGrid grid)
        {
            int used = 0;
            for (int i = 0; i < grid.W; i++)
            {
                for (int j = 0; j < grid.H; j++)
                {
                    if (grid.At(i, j) != null)
                    {
                        used++;
                    }
                }
            }
            return $"{used}/{grid.W * grid.H}";
        }

        private RectTransform DrawGrid(RectTransform parent, TGrid grid, Inventory inventory, float x, float y,
            Color cellColor, RectTransform clip, bool filtered = false)
        {
            float cell = Plugin.CellSize.Value;
            var rt = UiKit.Rect("Grid_" + grid.Id, parent);
            UiKit.TopLeft(rt, x, y, grid.W * cell, grid.H * cell);
            var back = UiKit.Image(rt, "Lines", new Color(0f, 0f, 0f, 0.55f), false);
            UiKit.Stretch(back.rectTransform);

            for (int i = 0; i < grid.W; i++)
            {
                for (int j = 0; j < grid.H; j++)
                {
                    var c = UiKit.Image(rt, "Cell", cellColor, false);
                    UiKit.TopLeft(c.rectTransform, i * cell + 1f, j * cell + 1f, cell - 2f, cell - 2f);
                }
            }

            var drawn = new HashSet<ItemDrop.ItemData>();
            for (int j = 0; j < grid.H; j++)
            {
                for (int i = 0; i < grid.W; i++)
                {
                    var item = grid.At(i, j);
                    if (item == null || !drawn.Add(item) || !Loc.TryRead(item, out var loc))
                    {
                        continue;
                    }

                    var size = ItemSizes.Of(item, loc.Rot);
                    bool dim = filtered && _filter != ItemFilter.All && Category(item) != _filter;
                    AddItemView(rt, item, inventory, loc.X * cell, loc.Y * cell, size.x * cell, size.y * cell, loc.Rot, dim);
                }
            }

            _targets.Add(new DropTarget
            {
                Rect = rt, Inventory = inventory, Grid = grid.Id, Model = grid, Cell = cell, Clip = clip
            });
            return rt;
        }

        // ------------------------------------------------------------------
        // Right: container, modding, or nothing open

        private void BuildRightColumn(Player player, Container container, float x, float w)
        {
            var col = Column("Right", x, w);

            if (_modGun != null)
            {
                var panel = ModPanel.Build(this, col, player, _modGun, _modInventory, _targets);
                var size = panel.sizeDelta;
                UiKit.TopLeft(panel, 0f, 0f, size.x, size.y);
                FitInto(col, size.x, size.y, w, ColumnH);
                return;
            }

            var inventory = container != null ? container.GetInventory() : null;
            if (inventory == null)
            {
                UiKit.Header(col, UiKit.L("$tv_ui_stash"), "", 0f, 0f, w);
                var hint = UiKit.Text(col, "Hint", UiKit.L("$tv_ui_stash_hint"), 15, UiKit.Muted, TextAnchor.UpperLeft);
                UiKit.TopLeft(hint.rectTransform, 0f, 34f, w, 200f);
                return;
            }

            float naturalW;
            float naturalH = BuildContainer(col, container, inventory, w, out naturalW);
            FitInto(col, naturalW, naturalH, w, ColumnH);
        }

        private float BuildContainer(RectTransform col, Container container, Inventory inventory, float w, out float naturalW)
        {
            float cell = Plugin.CellSize.Value;
            var cl = ContainerLayout.Build(inventory);
            float gridW = cl.Grid.W * cell;
            float gridH = cl.Grid.H * cell;
            naturalW = Mathf.Max(w, gridW);

            UiKit.Header(col, UiKit.L(GameAccess.ContainerTitle(container)).ToUpperInvariant(), Capacity(cl.Grid), 0f, 0f, naturalW);

            // Category filter strip: non-matching items are dimmed, not hidden,
            // so the grid keeps its shape.
            var filters = (ItemFilter[])System.Enum.GetValues(typeof(ItemFilter));
            float takeW = 110f;
            float fw = Mathf.Floor((naturalW - takeW - 10f - (filters.Length - 1) * 3f) / filters.Length);
            for (int i = 0; i < filters.Length; i++)
            {
                var f = filters[i];
                var b = UiKit.Button(col, "Filter_" + f, UiKit.L("$tv_filter_" + f.ToString().ToLowerInvariant()), () =>
                {
                    _filter = f;
                    MarkDirty();
                }, _filter == f, 11);
                UiKit.TopLeft((RectTransform)b.transform, i * (fw + 3f), 28f, fw, 24f);
            }
            var takeAll = UiKit.Button(col, "TakeAll", UiKit.L("$tv_ui_take_all"), () =>
            {
                StashOps.TakeAll(inventory);
                ReportError();
                MarkDirty();
            }, false, 13);
            UiKit.TopLeft((RectTransform)takeAll.transform, naturalW - takeW, 28f, takeW, 24f);

            float top = 60f;
            float overflowH = cl.Unplaced.Count > 0 ? 28f + Mathf.CeilToInt(cl.Unplaced.Count / Mathf.Max(1f, Mathf.Floor(gridW / cell))) * cell : 0f;
            float viewH = Mathf.Min(gridH, Mathf.Max(cell * 3f, ColumnH - top - overflowH - 8f));

            var content = ScrollArea(col, "container", 0f, top, gridW, viewH, gridH, out var viewport);
            DrawGrid(content, cl.Grid, inventory, 0f, 0f, UiKit.CellBg, viewport, true);

            float bottom = top + viewH + 8f;
            if (cl.Unplaced.Count > 0)
            {
                UiKit.Header(col, UiKit.L("$tv_ui_container_overflow"), cl.Unplaced.Count.ToString(), 0f, bottom, naturalW, UiKit.Warning);
                bottom += 28f;
                int perRow = Mathf.Max(1, Mathf.FloorToInt(gridW / cell));
                for (int i = 0; i < cl.Unplaced.Count; i++)
                {
                    AddItemView(col, cl.Unplaced[i], inventory, (i % perRow) * cell, bottom + (i / perRow) * cell, cell, cell, false);
                }
                bottom += Mathf.CeilToInt(cl.Unplaced.Count / (float)perRow) * cell + 4f;
            }
            return bottom;
        }

        // ------------------------------------------------------------------
        // Bottom: quick use

        private void BuildQuickBar(Loadout lo, Inventory inventory)
        {
            float size = 64f;
            float x = 40f;
            var label = UiKit.Text(_frame, "QuickLabel", UiKit.L("$tv_ui_quick"), 12, UiKit.Muted);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.TopLeft(label.rectTransform, x, QuickBarY - 16f, 400f, 16f);

            var weapons = new[] { SlotId.Primary, SlotId.Secondary, SlotId.Holster, SlotId.Melee };
            for (int i = 0; i < 8; i++)
            {
                float bx = x + i * (size + 6f);
                if (i < weapons.Length)
                {
                    WeaponQuickBox(lo, weapons[i], i, bx, QuickBarY, size);
                }
                else
                {
                    QuickBox(inventory, i, bx, QuickBarY, size);
                }
            }

            var help = UiKit.Text(_frame, "Help", UiKit.L("$tv_ui_controls"), 13, UiKit.Muted, TextAnchor.MiddleCenter);
            UiKit.TopLeft(help.rectTransform, x + 8 * (size + 6f) + 20f, QuickBarY, 1440f - (x + 8 * (size + 6f) + 20f), size);

            var ground = UiKit.Image(_frame, "Ground", new Color(0.1f, 0.08f, 0.05f, 0.9f));
            UiKit.TopLeft(ground.rectTransform, 1480f, QuickBarY, 400f, size);
            UiKit.Border(ground.transform, UiKit.AccentDim, 1f);
            var gt = UiKit.Text(ground.transform, "Label", UiKit.L("$tv_ui_ground"), 14, UiKit.Accent, TextAnchor.MiddleCenter);
            UiKit.Stretch(gt.rectTransform, 4f);
            _targets.Add(new DropTarget { Rect = ground.rectTransform, Ground = true });
        }

        private void WeaponQuickBox(Loadout lo, SlotId slot, int index, float x, float y, float size)
        {
            var bg = UiKit.Image(_frame, "Hotbar_" + index, UiKit.SlotBg, false);
            UiKit.TopLeft(bg.rectTransform, x, y, size, size);
            UiKit.Border(bg.transform, new Color(1f, 1f, 1f, 0.08f), 1f);
            if (lo.Slots.TryGetValue(slot, out var item))
            {
                var icon = UiKit.Image(bg.transform, "Icon", Color.white, false);
                icon.sprite = item.GetIcon();
                icon.preserveAspect = true;
                UiKit.Stretch(icon.rectTransform, 6f);
            }
            var key = UiKit.Text(bg.transform, "Key", (index + 1).ToString(), 13, UiKit.Accent);
            UiKit.Stretch(key.rectTransform, 3f);
        }

        private void QuickBox(Inventory inventory, int index, float x, float y, float size)
        {
            var bg = UiKit.Image(_frame, "Quick_" + index, UiKit.SlotBg);
            UiKit.TopLeft(bg.rectTransform, x, y, size, size);
            UiKit.Border(bg.transform, UiKit.AccentDim, 1f);

            foreach (var item in inventory.GetAllItems())
            {
                if (Loc.QuickSlot(item) == index)
                {
                    var icon = UiKit.Image(bg.transform, "Icon", Color.white, false);
                    icon.sprite = item.GetIcon();
                    icon.preserveAspect = true;
                    UiKit.Stretch(icon.rectTransform, 6f);
                    if (item.m_shared.m_maxStackSize > 1)
                    {
                        var n = UiKit.Text(bg.transform, "Count", CountOf(inventory, item).ToString(), 12, Color.white, TextAnchor.LowerRight);
                        UiKit.Stretch(n.rectTransform, 3f);
                    }
                    var view = bg.gameObject.AddComponent<QuickSlotView>();
                    view.Item = item;
                    break;
                }
            }

            var number = UiKit.Text(bg.transform, "Key", (index + 1).ToString(), 13, UiKit.Accent);
            UiKit.Stretch(number.rectTransform, 3f);
            _targets.Add(new DropTarget { Rect = bg.rectTransform, Inventory = inventory, Quick = index });
        }

        private static int CountOf(Inventory inventory, ItemDrop.ItemData like)
        {
            int n = 0;
            foreach (var item in inventory.GetAllItems())
            {
                if (item.m_shared.m_name == like.m_shared.m_name)
                {
                    n += item.m_stack;
                }
            }
            return n;
        }

        // ------------------------------------------------------------------
        // Item drawing

        private void AddItemView(RectTransform parent, ItemDrop.ItemData item, Inventory inventory,
            float x, float y, float w, float h, bool rotated, bool dim = false)
        {
            var bg = UiKit.Image(parent, "Item", CategoryColor(item));
            UiKit.TopLeft(bg.rectTransform, x + 1f, y + 1f, w - 2f, h - 2f);
            var icon = UiKit.Image(bg.transform, "Icon", Color.white, false);
            icon.sprite = item.GetIcon();
            icon.preserveAspect = true;
            var irt = icon.rectTransform;
            irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0.5f, 0.5f);
            irt.anchoredPosition = Vector2.zero;
            if (rotated)
            {
                irt.sizeDelta = new Vector2(h - 8f, w - 8f);
                irt.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            else
            {
                irt.sizeDelta = new Vector2(w - 8f, h - 8f);
            }

            var gold = new Color(0.9f, 0.85f, 0.6f);
            string name = Gear.PrefabName(item);

            if (item.m_shared.m_maxStackSize > 1)
            {
                Corner(bg, item.m_stack.ToString(), 13, Color.white, TextAnchor.LowerRight);
            }

            var gun = Firearms.FindGun(name);
            if (gun != null)
            {
                Corner(bg, $"{GunState.Mag(item)}/{GunStats.Of(gun, item).MagSize}", 12,
                    GunState.Jammed(item) ? UiKit.Warning : gold, TextAnchor.UpperLeft);
                Corner(bg, ShortCaliber(gun.Caliber), 10, UiKit.Muted, TextAnchor.LowerRight);
            }

            var ammo = Firearms.FindAmmo(name);
            if (ammo != null)
            {
                Corner(bg, ShortCaliber(ammo.Caliber), 10, gold, TextAnchor.UpperLeft);
            }

            var magSpec = Magazines.Find(name);
            if (magSpec != null)
            {
                Corner(bg, $"{GunState.Mag(item)}/{magSpec.Capacity}", 12, gold, TextAnchor.UpperLeft);
                Corner(bg, ShortCaliber(magSpec.Caliber), 10, UiKit.Muted, TextAnchor.LowerRight);
            }

            var partSpec = GunParts.Find(name);
            if (partSpec != null)
            {
                string tier = partSpec.Tier > 0 ? " " + new[] { "", "I", "II", "III", "IV", "V" }[partSpec.Tier] : "";
                Corner(bg, partSpec.Label + tier, 11, new Color(0.95f, 0.8f, 0.55f), TextAnchor.UpperLeft);
            }

            // Armour value, like Tarkov's armour class badge.
            if (Loadout.ArmorSlotFor(item, out _) && item.m_shared.m_armor > 0f)
            {
                float armor = item.m_shared.m_armor + Mathf.Max(0, item.m_quality - 1) * item.m_shared.m_armorPerLevel;
                Corner(bg, $"AR {armor:0}", 11, new Color(0.65f, 0.8f, 1f), TextAnchor.UpperLeft);
            }

            // Rigs, packs and pouches: how full, when worn; otherwise their size.
            var gear = Gear.Find(item);
            if (gear != null)
            {
                string cap = $"{gear.GridW}x{gear.GridH}";
                if (StashOps.IsPlayerInventory(inventory) && Reconciler.Current != null
                    && Reconciler.Current.TryGetSlotOf(item, out var worn) && Loadout.GridOf(worn) != null
                    && Reconciler.Current.Grids.TryGetValue(Loadout.GridOf(worn), out var g))
                {
                    cap = Capacity(g);
                }
                Corner(bg, cap, 11, gold, TextAnchor.UpperLeft);
            }

            int quick = Loc.QuickSlot(item);
            if (quick >= 0)
            {
                Corner(bg, (quick + 1).ToString(), 12, UiKit.Accent, TextAnchor.UpperRight);
            }

            if (item.m_equipped)
            {
                Corner(bg, "E", 12, new Color(0.5f, 0.9f, 0.5f), TextAnchor.LowerLeft);
            }
            else if (item.m_shared.m_useDurability && item.m_durability < item.GetMaxDurability() * 0.25f)
            {
                Corner(bg, "!", 14, UiKit.Warning, TextAnchor.LowerLeft);
            }

            if (Loc.IsLocked(item))
            {
                UiKit.Border(bg.transform, UiKit.Accent, 2f);
            }

            if (dim)
            {
                bg.gameObject.AddComponent<CanvasGroup>().alpha = 0.2f;
            }

            var view = bg.gameObject.AddComponent<ItemView>();
            view.Item = item;
            view.Inventory = inventory;
            view.Rotated = rotated;
            view.Cell = Plugin.CellSize.Value;
        }

        private static void Corner(Image bg, string text, int size, Color color, TextAnchor anchor)
        {
            var t = UiKit.Text(bg.transform, "Badge", text, size, color, anchor);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Stretch(t.rectTransform, 3f);
        }

        private static string ShortCaliber(string token)
        {
            switch (token)
            {
                case "$tv_cal_9x18": return "9x18";
                case "$tv_cal_12ga": return "12ga";
                case "$tv_cal_762x54r": return "7.62R";
                case "$tv_cal_545x39": return "5.45";
            }
            var s = UiKit.L(token);
            int space = s.IndexOf(' ');
            return space > 0 ? s.Substring(0, space) : s;
        }

        private static ItemFilter Category(ItemDrop.ItemData item)
        {
            var name = Gear.PrefabName(item);
            var type = item.m_shared.m_itemType;
            if (Gear.Find(item) != null)
            {
                return ItemFilter.Gear;
            }
            if (GunParts.Find(name) != null)
            {
                return ItemFilter.Mods;
            }
            if (Magazines.Find(name) != null || type == ItemDrop.ItemData.ItemType.Ammo
                || type == ItemDrop.ItemData.ItemType.AmmoNonEquipable)
            {
                return ItemFilter.Ammo;
            }
            if (Loadout.IsHandItem(item))
            {
                return ItemFilter.Weapons;
            }
            if (Loadout.ArmorSlotFor(item, out _))
            {
                return ItemFilter.Armor;
            }
            if (type == ItemDrop.ItemData.ItemType.Consumable)
            {
                return ItemFilter.Food;
            }
            return ItemFilter.Other;
        }

        private static Color CategoryColor(ItemDrop.ItemData item)
        {
            switch (Category(item))
            {
                case ItemFilter.Gear: return new Color(0.2f, 0.26f, 0.18f, 0.92f);
                case ItemFilter.Mods: return new Color(0.28f, 0.23f, 0.19f, 0.92f);
                case ItemFilter.Ammo: return new Color(0.3f, 0.26f, 0.13f, 0.92f);
                case ItemFilter.Weapons: return new Color(0.27f, 0.17f, 0.15f, 0.92f);
                case ItemFilter.Armor: return new Color(0.15f, 0.2f, 0.28f, 0.92f);
                case ItemFilter.Food: return new Color(0.16f, 0.27f, 0.22f, 0.92f);
                default: return new Color(0.22f, 0.22f, 0.21f, 0.92f);
            }
        }
    }
}
