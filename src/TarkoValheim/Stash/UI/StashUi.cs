using System.Collections.Generic;
using System;
using TarkoValheim.Content;
using TarkoValheim.Guns;
using TarkoValheim.Util;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine;

namespace TarkoValheim.Stash.UI
{
    /// <summary>
    /// The Tarkov-style inventory screen. Shown whenever Valheim's inventory
    /// is open; Valheim's own player and container grids are shrunk to zero
    /// while it is up, and its crafting panel is left alone.
    ///
    /// Controls: drag to move, R while dragging to rotate, Shift+drag to
    /// split a stack, Ctrl+click to send to/from the open container,
    /// double-click to use, right-click for more, drag off the panels to drop.
    /// </summary>
    internal class StashUi : MonoBehaviour
    {
        public static StashUi Instance { get; private set; }

        private const float Pad = 12f;
        private const float Gap = 6f;

        private GameObject _canvasGo;
        private Canvas _canvas;
        private RectTransform _root;
        private RectTransform _content;
        private RectTransform _main;
        private RectTransform _containerPanel;
        private RectTransform _scrollContent;
        private float _savedScroll;

        private RectTransform _tooltip;
        private Text _tooltipText;
        private RectTransform _menu;

        private readonly List<DropTarget> _targets = new List<DropTarget>();
        private readonly Dictionary<Transform, Vector3> _hiddenVanilla = new Dictionary<Transform, Vector3>();

        private int _builtVersion = -1;
        private int _builtContainerSig;
        private Container _builtContainer;
        private float _nextCheck;
        private bool _wasVisible;
        private static bool _dirty;
        private Vector2 _mainOffset = new Vector2(-24f, 0f);
        private float _mainWidth;

        private ItemView _dragView;
        private int _dragAmount;
        private bool _dragRot;
        private Vector2 _grab;
        private RectTransform _ghost;
        private Image _ghostTint;

        public static void MarkDirty()
        {
            _dirty = true;
        }

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            try
            {
                var player = Player.m_localPlayer;
                bool visible = Reconciler.Active && player != null && InventoryGui.instance != null && InventoryGui.IsVisible();
                HideVanilla(Reconciler.Active);

                if (!visible)
                {
                    if (_canvasGo != null)
                    {
                        _canvasGo.SetActive(false);
                    }
                    CancelDrag();
                    CloseMenu();
                    _wasVisible = false;
                    return;
                }

                EnsureCanvas();
                _canvasGo.SetActive(true);

                if (!_wasVisible)
                {
                    _wasVisible = true;
                    Reconciler.Run(player);
                    _dirty = true;
                }

                if (_dragView != null)
                {
                    if (ZInput.GetKeyDown(KeyCode.R))
                    {
                        RotateDrag();
                    }
                    return;
                }

                var container = CurrentContainer();
                if (Time.time >= _nextCheck)
                {
                    _nextCheck = Time.time + 0.5f;
                    Reconciler.Run(player);
                    if (container != null && ContainerSignature(container) != _builtContainerSig)
                    {
                        _dirty = true;
                    }
                }

                if (_dirty || _builtVersion != Reconciler.Version || container != _builtContainer)
                {
                    Rebuild(player, container);
                }
            }
            catch (System.Exception e)
            {
                GameAccess.WarnOnce("StashUi.Update", e);
            }
        }

        private static Container CurrentContainer()
        {
            return InventoryGui.instance != null ? GameAccess.CurrentContainer(InventoryGui.instance) : null;
        }

        private static int ContainerSignature(Container container)
        {
            var inventory = container.GetInventory();
            if (inventory == null)
            {
                return 0;
            }

            int sig = inventory.GetAllItems().Count;
            foreach (var item in inventory.GetAllItems())
            {
                sig = sig * 31 + item.m_stack;
                sig = sig * 31 + (item.m_customData.TryGetValue("tv_loc", out var loc) ? loc.GetHashCode() : 0);
            }
            return sig;
        }

        // Valheim's own grids are scaled to zero rather than disabled, so its
        // code that reads them keeps working.
        private void HideVanilla(bool hide)
        {
            var gui = InventoryGui.instance;
            if (gui == null)
            {
                _hiddenVanilla.Clear();
                return;
            }

            foreach (var rt in new Transform[] { GameAccess.PlayerPanel(gui), GameAccess.ContainerPanel(gui) })
            {
                if (rt == null)
                {
                    continue;
                }

                if (hide)
                {
                    if (!_hiddenVanilla.ContainsKey(rt))
                    {
                        _hiddenVanilla[rt] = rt.localScale;
                    }
                    rt.localScale = Vector3.zero;
                }
                else if (_hiddenVanilla.TryGetValue(rt, out var scale))
                {
                    rt.localScale = scale;
                    _hiddenVanilla.Remove(rt);
                }
            }
        }

        private void EnsureCanvas()
        {
            if (_canvasGo != null)
            {
                return;
            }

            _canvasGo = new GameObject("TarkoValheim_Inventory");
            _canvasGo.layer = 5;
            DontDestroyOnLoad(_canvasGo);
            _canvas = _canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 30;
            var scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            _canvasGo.AddComponent<GraphicRaycaster>();
            _root = (RectTransform)_canvasGo.transform;

            BuildTooltip();
        }

        // ------------------------------------------------------------------
        // Layout

        private void Rebuild(Player player, Container container)
        {
            _dirty = false;
            _builtVersion = Reconciler.Version;
            _builtContainer = container;
            _builtContainerSig = container != null ? ContainerSignature(container) : 0;

            if (_scrollContent != null)
            {
                _savedScroll = _scrollContent.anchoredPosition.y;
            }

            if (_content != null)
            {
                Destroy(_content.gameObject);
            }
            _targets.Clear();
            HideTooltip(null);

            _content = UiKit.Rect("Content", _root);
            UiKit.Stretch(_content);
            _content.SetAsFirstSibling();

            var lo = Reconciler.Current ?? Loadout.Build(player);
            BuildMain(player, lo);

            _containerPanel = null;
            _scrollContent = null;
            if (container != null && container.GetInventory() != null)
            {
                BuildContainer(container);
            }
        }

        private void BuildMain(Player player, Loadout lo)
        {
            float cell = Plugin.CellSize.Value;
            float box = Mathf.Round(cell * 1.5f);
            var inventory = player.GetInventory();

            var bg = UiKit.Image(_content, "Main", UiKit.PanelBg);
            _main = bg.rectTransform;
            _main.anchorMin = _main.anchorMax = new Vector2(1f, 0.5f);
            _main.pivot = new Vector2(1f, 0.5f);

            // Header (drag to move the panel).
            var header = UiKit.Image(_main, "Header", new Color(0f, 0f, 0f, 0.35f));
            header.gameObject.AddComponent<PanelMover>().Owner = this;
            var title = UiKit.Text(header.transform, "Title", UiKit.L("$tv_ui_inventory"), 20, UiKit.Label, TextAnchor.MiddleLeft);
            UiKit.Stretch(title.rectTransform, 6f);
            var weight = UiKit.Text(header.transform, "Weight",
                $"{UiKit.L("$tv_ui_weight")} {inventory.GetTotalWeight():0}/{GameAccess.MaxCarryWeight(player):0}",
                16, UiKit.Label, TextAnchor.MiddleRight);
            UiKit.Stretch(weight.rectTransform, 6f);

            // Left column: equipment.
            float leftW = 4 * box + 3 * Gap;
            float x = Pad;
            float y = Pad + 36f;
            Label(_main, UiKit.L("$tv_ui_gear"), x, y, leftW, UiKit.Label);
            y += 20f;

            var row1 = new[] { SlotId.Head, SlotId.Body, SlotId.Legs, SlotId.Cape };
            var row2 = new[] { SlotId.Belt, SlotId.Trinket, SlotId.Rig, SlotId.Backpack };
            for (int i = 0; i < 4; i++)
            {
                SlotBox(lo, inventory, row1[i], x + i * (box + Gap), y, box, box);
                SlotBox(lo, inventory, row2[i], x + i * (box + Gap), y + box + Gap, box, box);
            }
            y += 2 * (box + Gap);

            SlotBox(lo, inventory, SlotId.Primary, x, y, leftW, box);
            y += box + Gap;
            SlotBox(lo, inventory, SlotId.Secondary, x, y, leftW, box);
            y += box + Gap;
            float half = (leftW - Gap) / 2f;
            SlotBox(lo, inventory, SlotId.Holster, x, y, half, box);
            SlotBox(lo, inventory, SlotId.Melee, x + half + Gap, y, half, box);
            y += box + Gap;

            SlotBox(lo, inventory, SlotId.Secure, x, y, box, box);
            var secureNote = UiKit.Text(_main, "SecureNote", UiKit.L("$tv_ui_secure_note"), 13, UiKit.SecureText);
            UiKit.TopLeft(secureNote.rectTransform, x + box + Gap, y + 4f, leftW - box - Gap, box);
            y += box + Gap + 6f;

            Label(_main, UiKit.L("$tv_ui_quick"), x, y, leftW, UiKit.Label);
            y += 20f;
            float qbox = Mathf.Round(cell * 1.2f);
            for (int i = 0; i < 4; i++)
            {
                QuickBox(inventory, Loadout.QuickFirst + i, x + i * (qbox + Gap), y, qbox);
            }
            y += qbox + Gap;
            float leftBottom = y;

            // Right column: grids.
            float rx = Pad + leftW + 20f;
            float ry = Pad + 36f;
            float rightW = Mathf.Max(4 * cell, 330f);

            ry = GridSection(lo, Loadout.Pockets, UiKit.L("$tv_ui_pockets"), UiKit.Label, UiKit.CellBg, inventory, rx, ry, ref rightW);
            ry = GridSection(lo, Loadout.RigGrid, UiKit.L("$tv_ui_rig") + SlotName(lo, SlotId.Rig), UiKit.Label, UiKit.CellBg, inventory, rx, ry, ref rightW);
            ry = GridSection(lo, Loadout.SecureGrid, UiKit.L("$tv_ui_secure"), UiKit.SecureText, UiKit.SecureCellBg, inventory, rx, ry, ref rightW);
            ry = GridSection(lo, Loadout.PackGrid, UiKit.L("$tv_ui_backpack") + SlotName(lo, SlotId.Backpack), UiKit.Label, UiKit.CellBg, inventory, rx, ry, ref rightW);

            if (!lo.Grids.ContainsKey(Loadout.RigGrid) || !lo.Grids.ContainsKey(Loadout.PackGrid))
            {
                var hint = UiKit.Text(_main, "Hint", UiKit.L("$tv_ui_no_rig_hint"), 13, UiKit.Label);
                UiKit.TopLeft(hint.rectTransform, rx, ry, rightW, 40f);
                ry += 44f;
            }

            float width = rx + rightW + Pad;
            float bottom = Mathf.Max(leftBottom, ry);

            if (lo.Unplaced.Count > 0)
            {
                bottom = UnsortedStrip(lo.Unplaced, inventory, Pad, bottom + 4f, width - 2 * Pad, cell);
            }

            _mainWidth = width;
            _main.sizeDelta = new Vector2(width, bottom + Pad);
            _main.anchoredPosition = _mainOffset;
            UiKit.TopLeft((RectTransform)header.transform, 0f, 0f, width, 32f);
        }

        private static string SlotName(Loadout lo, SlotId slot)
        {
            return lo.Slots.TryGetValue(slot, out var item) ? " - " + UiKit.L(item.m_shared.m_name) : "";
        }

        private static void Label(RectTransform parent, string text, float x, float y, float w, Color color)
        {
            var t = UiKit.Text(parent, "Label", text, 14, color);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.TopLeft(t.rectTransform, x, y, w, 18f);
        }

        private void SlotBox(Loadout lo, Inventory inventory, SlotId slot, float x, float y, float w, float h)
        {
            var bg = UiKit.Image(_main, "Slot_" + slot, slot == SlotId.Secure ? UiKit.SecureCellBg : UiKit.SlotBg);
            UiKit.TopLeft(bg.rectTransform, x, y, w, h);
            var label = UiKit.Text(bg.transform, "Label", UiKit.L("$tv_slot_" + slot.ToString().ToLowerInvariant()),
                11, new Color(1f, 1f, 1f, 0.45f));
            UiKit.Stretch(label.rectTransform, 3f);

            _targets.Add(new DropTarget
            {
                Rect = bg.rectTransform, Inventory = inventory, IsSlot = true, Slot = slot,
                Cell = Plugin.CellSize.Value
            });

            if (lo.Slots.TryGetValue(slot, out var item))
            {
                AddItemView(bg.rectTransform, item, inventory, 0f, 0f, w, h, false);
            }
        }

        private void QuickBox(Inventory inventory, int index, float x, float y, float size)
        {
            var bg = UiKit.Image(_main, "Quick_" + index, UiKit.SlotBg);
            UiKit.TopLeft(bg.rectTransform, x, y, size, size);
            var number = UiKit.Text(bg.transform, "Key", (index + 1).ToString(), 13, UiKit.Label);
            UiKit.Stretch(number.rectTransform, 3f);

            foreach (var item in inventory.GetAllItems())
            {
                if (Loc.QuickSlot(item) == index)
                {
                    var icon = UiKit.Image(bg.transform, "Icon", Color.white, false);
                    icon.sprite = item.GetIcon();
                    icon.preserveAspect = true;
                    UiKit.Stretch(icon.rectTransform, 6f);
                    var view = bg.gameObject.AddComponent<QuickSlotView>();
                    view.Item = item;
                    break;
                }
            }

            _targets.Add(new DropTarget { Rect = bg.rectTransform, Inventory = inventory, Quick = index });
        }

        private float GridSection(Loadout lo, string gridId, string title, Color titleColor, Color cellColor,
            Inventory inventory, float x, float y, ref float maxWidth)
        {
            if (!lo.Grids.TryGetValue(gridId, out var grid))
            {
                return y;
            }

            float cell = Plugin.CellSize.Value;
            Label(_main, title, x, y, Mathf.Max(grid.W * cell, 240f), titleColor);
            y += 20f;

            var rt = DrawGrid(_main, grid, inventory, x, y, cellColor, null);
            maxWidth = Mathf.Max(maxWidth, grid.W * cell);
            return y + rt.sizeDelta.y + 10f;
        }

        private RectTransform DrawGrid(RectTransform parent, TGrid grid, Inventory inventory, float x, float y,
            Color cellColor, RectTransform clip)
        {
            float cell = Plugin.CellSize.Value;
            var rt = UiKit.Rect("Grid_" + grid.Id, parent);
            UiKit.TopLeft(rt, x, y, grid.W * cell, grid.H * cell);

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
                    AddItemView(rt, item, inventory, loc.X * cell, loc.Y * cell, size.x * cell, size.y * cell, loc.Rot);
                }
            }

            _targets.Add(new DropTarget
            {
                Rect = rt, Inventory = inventory, Grid = grid.Id, Model = grid, Cell = cell, Clip = clip
            });
            return rt;
        }

        private float UnsortedStrip(List<ItemDrop.ItemData> items, Inventory inventory, float x, float y, float width, float cell)
        {
            Label(_main, UiKit.L("$tv_ui_unsorted"), x, y, width, UiKit.Warning);
            y += 20f;
            int perRow = Mathf.Max(1, Mathf.FloorToInt(width / cell));
            for (int i = 0; i < items.Count; i++)
            {
                AddItemView(_main, items[i], inventory, x + (i % perRow) * cell, y + (i / perRow) * cell, cell, cell, false);
            }
            return y + Mathf.CeilToInt(items.Count / (float)perRow) * cell + 4f;
        }

        private void BuildContainer(Container container)
        {
            float cell = Plugin.CellSize.Value;
            var inventory = container.GetInventory();
            var cl = ContainerLayout.Build(inventory);

            float gridW = cl.Grid.W * cell;
            float gridH = cl.Grid.H * cell;
            float viewH = Mathf.Min(gridH, 720f);
            float width = Mathf.Max(gridW + 2 * Pad, 260f);

            var bg = UiKit.Image(_content, "Container", UiKit.PanelBg);
            _containerPanel = bg.rectTransform;
            _containerPanel.anchorMin = _containerPanel.anchorMax = new Vector2(1f, 0.5f);
            _containerPanel.pivot = new Vector2(1f, 0.5f);
            _containerPanel.anchoredPosition = new Vector2(_mainOffset.x - _mainWidth - 16f, _mainOffset.y);

            var title = UiKit.Text(_containerPanel, "Title", UiKit.L(GameAccess.ContainerTitle(container)), 18, UiKit.Label, TextAnchor.MiddleLeft);
            UiKit.TopLeft(title.rectTransform, Pad, 4f, width - 120f, 28f);
            var takeAll = UiKit.Button(_containerPanel, "TakeAll", UiKit.L("$tv_ui_take_all"), () =>
            {
                StashOps.TakeAll(inventory);
                ReportError();
                MarkDirty();
            });
            UiKit.TopLeft((RectTransform)takeAll.transform, width - Pad - 100f, 6f, 100f, 24f);

            var viewport = UiKit.Image(_containerPanel, "Viewport", new Color(0f, 0f, 0f, 0.01f));
            UiKit.TopLeft(viewport.rectTransform, Pad, 38f, gridW, viewH);
            viewport.gameObject.AddComponent<RectMask2D>();

            var scrollContent = UiKit.Rect("ScrollContent", viewport.rectTransform);
            UiKit.TopLeft(scrollContent, 0f, 0f, gridW, gridH);
            _scrollContent = scrollContent;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = scrollContent;
            scroll.viewport = viewport.rectTransform;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = cell;

            DrawGrid(scrollContent, cl.Grid, inventory, 0f, 0f, UiKit.CellBg, viewport.rectTransform);
            scrollContent.anchoredPosition = new Vector2(0f, Mathf.Clamp(_savedScroll, 0f, Mathf.Max(0f, gridH - viewH)));

            float bottom = 38f + viewH + 8f;
            if (cl.Unplaced.Count > 0)
            {
                var note = UiKit.Text(_containerPanel, "Overflow", UiKit.L("$tv_ui_container_overflow"), 13, UiKit.Warning);
                UiKit.TopLeft(note.rectTransform, Pad, bottom, width - 2 * Pad, 18f);
                bottom += 20f;
                int perRow = Mathf.Max(1, Mathf.FloorToInt(gridW / cell));
                for (int i = 0; i < cl.Unplaced.Count; i++)
                {
                    AddItemView(_containerPanel, cl.Unplaced[i], inventory,
                        Pad + (i % perRow) * cell, bottom + (i / perRow) * cell, cell, cell, false);
                }
                bottom += Mathf.CeilToInt(cl.Unplaced.Count / (float)perRow) * cell + 4f;
            }

            _containerPanel.sizeDelta = new Vector2(width, bottom + Pad);
        }

        // ------------------------------------------------------------------
        // Item drawing

        private void AddItemView(RectTransform parent, ItemDrop.ItemData item, Inventory inventory,
            float x, float y, float w, float h, bool rotated)
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

            if (item.m_shared.m_maxStackSize > 1)
            {
                var stack = UiKit.Text(bg.transform, "Stack", item.m_stack.ToString(), 13, Color.white, TextAnchor.LowerRight);
                UiKit.Stretch(stack.rectTransform, 3f);
            }

            var gun = Firearms.FindGun(Gear.PrefabName(item));
            if (gun != null)
            {
                var mag = UiKit.Text(bg.transform, "Mag", $"{GunState.Mag(item)}/{gun.MagSize}", 12,
                    GunState.Jammed(item) ? UiKit.Warning : new Color(0.9f, 0.85f, 0.6f), TextAnchor.UpperLeft);
                UiKit.Stretch(mag.rectTransform, 3f);
            }

            int quick = Loc.QuickSlot(item);
            if (quick >= 0)
            {
                var q = UiKit.Text(bg.transform, "Quick", (quick + 1).ToString(), 12, UiKit.SecureText, TextAnchor.UpperRight);
                UiKit.Stretch(q.rectTransform, 3f);
            }

            if (item.m_equipped)
            {
                var e = UiKit.Text(bg.transform, "Equipped", "E", 12, new Color(0.5f, 0.9f, 0.5f), TextAnchor.LowerLeft);
                UiKit.Stretch(e.rectTransform, 3f);
            }
            else if (item.m_shared.m_useDurability && item.m_durability < item.GetMaxDurability() * 0.25f)
            {
                var d = UiKit.Text(bg.transform, "Worn", "!", 14, UiKit.Warning, TextAnchor.LowerLeft);
                UiKit.Stretch(d.rectTransform, 3f);
            }

            var view = bg.gameObject.AddComponent<ItemView>();
            view.Item = item;
            view.Inventory = inventory;
            view.Rotated = rotated;
            view.Cell = Plugin.CellSize.Value;
        }

        private static Color CategoryColor(ItemDrop.ItemData item)
        {
            var type = item.m_shared.m_itemType;
            if (Gear.Find(item) != null)
            {
                return new Color(0.2f, 0.28f, 0.18f, 0.92f);
            }
            if (type == ItemDrop.ItemData.ItemType.Ammo || type == ItemDrop.ItemData.ItemType.AmmoNonEquipable)
            {
                return new Color(0.34f, 0.28f, 0.12f, 0.92f);
            }
            if (item.IsWeapon() || type == ItemDrop.ItemData.ItemType.Shield || type == ItemDrop.ItemData.ItemType.Tool)
            {
                return new Color(0.32f, 0.18f, 0.16f, 0.92f);
            }
            if (Loadout.ArmorSlotFor(item, out _))
            {
                return new Color(0.16f, 0.22f, 0.32f, 0.92f);
            }
            if (type == ItemDrop.ItemData.ItemType.Consumable)
            {
                return new Color(0.18f, 0.3f, 0.24f, 0.92f);
            }
            return new Color(0.26f, 0.26f, 0.26f, 0.92f);
        }

        // ------------------------------------------------------------------
        // Drag and drop

        public void BeginDrag(ItemView view, PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || _dragView != null)
            {
                return;
            }

            CloseMenu();
            HideTooltip(null);

            _dragView = view;
            _dragRot = view.Rotated;
            bool shift = ZInput.GetKey(KeyCode.LeftShift) || ZInput.GetKey(KeyCode.RightShift);
            _dragAmount = shift && view.Item.m_stack > 1 ? Mathf.CeilToInt(view.Item.m_stack / 2f) : view.Item.m_stack;

            var size = ItemSizes.Of(view.Item, _dragRot);
            float cell = Plugin.CellSize.Value;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)view.transform, e.position, null, out var local);
            var viewRt = (RectTransform)view.transform;
            bool inGrid = Mathf.Abs(viewRt.sizeDelta.x - (size.x * cell - 2f)) < 1f && Mathf.Abs(viewRt.sizeDelta.y - (size.y * cell - 2f)) < 1f;
            _grab = inGrid
                ? new Vector2(Mathf.Clamp(local.x, 0f, size.x * cell), Mathf.Clamp(-local.y, 0f, size.y * cell))
                : new Vector2(size.x * cell / 2f, size.y * cell / 2f);

            var ghost = UiKit.Image(_root, "Ghost", UiKit.Good, false);
            _ghostTint = ghost;
            _ghost = ghost.rectTransform;
            _ghost.anchorMin = _ghost.anchorMax = new Vector2(0.5f, 0.5f);
            _ghost.pivot = new Vector2(0f, 1f);
            var icon = UiKit.Image(_ghost, "Icon", Color.white, false);
            icon.sprite = view.Item.GetIcon();
            icon.preserveAspect = true;
            SizeGhost();

            var group = view.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0.35f;
            group.blocksRaycasts = false;

            Drag(e);
        }

        private void SizeGhost()
        {
            float cell = Plugin.CellSize.Value;
            var size = ItemSizes.Of(_dragView.Item, _dragRot);
            _ghost.sizeDelta = new Vector2(size.x * cell, size.y * cell);
            var icon = (RectTransform)_ghost.GetChild(0);
            icon.anchorMin = icon.anchorMax = icon.pivot = new Vector2(0.5f, 0.5f);
            icon.anchoredPosition = Vector2.zero;
            bool turned = _dragRot != false && ItemSizes.Of(_dragView.Item).x != ItemSizes.Of(_dragView.Item).y;
            icon.sizeDelta = turned ? new Vector2(size.y * cell - 8f, size.x * cell - 8f) : new Vector2(size.x * cell - 8f, size.y * cell - 8f);
            icon.localRotation = turned ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
        }

        private void RotateDrag()
        {
            _dragRot = !_dragRot;
            var size = ItemSizes.Of(_dragView.Item, _dragRot);
            float cell = Plugin.CellSize.Value;
            _grab = new Vector2(Mathf.Min(_grab.y, size.x * cell - cell / 2f), Mathf.Min(_grab.x, size.y * cell - cell / 2f));
            SizeGhost();
        }

        public void Drag(PointerEventData e)
        {
            if (_dragView == null)
            {
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, e.position, null, out var local);
            _ghost.anchoredPosition = local + new Vector2(-_grab.x, _grab.y);
            _ghost.SetAsLastSibling();

            var target = FindTarget(e.position, out var dest);
            if (target == null)
            {
                _ghostTint.color = new Color(0.5f, 0.5f, 0.5f, 0.35f);
            }
            else if (target.Quick >= 0)
            {
                _ghostTint.color = StashOps.IsPlayerInventory(_dragView.Inventory) ? UiKit.Good : UiKit.Bad;
            }
            else
            {
                _ghostTint.color = StashOps.CanMove(_dragView.Item, _dragView.Inventory, dest, _dragAmount) ? UiKit.Good : UiKit.Bad;
            }
        }

        public void EndDrag(PointerEventData e)
        {
            if (_dragView == null)
            {
                return;
            }

            var item = _dragView.Item;
            var from = _dragView.Inventory;
            int amount = _dragAmount;
            var target = FindTarget(e.position, out var dest);
            bool overPanels = Contains(_main, e.position) || Contains(_containerPanel, e.position);
            CancelDrag();

            if (target != null && target.Quick >= 0)
            {
                if (StashOps.IsPlayerInventory(from))
                {
                    StashOps.BindQuickSlot(item, target.Quick);
                }
            }
            else if (target != null)
            {
                if (!StashOps.Move(item, from, dest, amount))
                {
                    ReportError();
                }
            }
            else if (!overPanels && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            {
                StashOps.Drop(item, from, amount);
                ReportError();
            }

            MarkDirty();
        }

        private void CancelDrag()
        {
            if (_ghost != null)
            {
                Destroy(_ghost.gameObject);
            }
            if (_dragView != null)
            {
                var group = _dragView.GetComponent<CanvasGroup>();
                if (group != null)
                {
                    Destroy(group);
                }
            }
            _ghost = null;
            _dragView = null;
        }

        private DropTarget FindTarget(Vector2 screen, out Dest dest)
        {
            dest = default(Dest);
            foreach (var t in _targets)
            {
                if (t.Clip != null && !Contains(t.Clip, screen))
                {
                    continue;
                }
                if (!Contains(t.Rect, screen))
                {
                    continue;
                }

                if (t.Quick >= 0)
                {
                    return t;
                }

                if (t.IsSlot)
                {
                    dest = new Dest { Inventory = t.Inventory, Loc = Loc.Slot(t.Slot) };
                    return t;
                }

                RectTransformUtility.ScreenPointToLocalPointInRectangle(t.Rect, screen, null, out var lp);
                float left = lp.x - _grab.x;
                float top = -lp.y - _grab.y;
                dest = new Dest
                {
                    Inventory = t.Inventory,
                    Loc = new Loc
                    {
                        Grid = t.Grid,
                        X = Mathf.RoundToInt(left / t.Cell),
                        Y = Mathf.RoundToInt(top / t.Cell),
                        Rot = _dragRot
                    }
                };
                return t;
            }
            return null;
        }

        private static bool Contains(RectTransform rt, Vector2 screen)
        {
            return rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, screen, null);
        }

        // ------------------------------------------------------------------
        // Clicks and context menu

        public void Click(ItemView view, PointerEventData e)
        {
            var container = CurrentContainer();
            var containerInventory = container != null ? container.GetInventory() : null;

            if (e.button == PointerEventData.InputButton.Right)
            {
                OpenMenu(view, e.position, containerInventory);
                return;
            }

            if (e.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            bool ctrl = ZInput.GetKey(KeyCode.LeftControl) || ZInput.GetKey(KeyCode.RightControl);
            if (ctrl)
            {
                if (!StashOps.QuickMove(view.Item, view.Inventory, containerInventory))
                {
                    ReportError();
                }
                MarkDirty();
            }
            else if (e.clickCount == 2)
            {
                Use(view.Item, view.Inventory, containerInventory);
            }
        }

        private void Use(ItemDrop.ItemData item, Inventory from, Inventory container)
        {
            var player = Player.m_localPlayer;
            if (player == null)
            {
                return;
            }

            if (StashOps.IsPlayerInventory(from))
            {
                player.UseItem(from, item, true);
            }
            else
            {
                StashOps.QuickMove(item, from, container);
                ReportError();
            }
            MarkDirty();
        }

        private void OpenMenu(ItemView view, Vector2 screen, Inventory container)
        {
            CloseMenu();
            HideTooltip(null);

            var item = view.Item;
            var from = view.Inventory;
            bool own = StashOps.IsPlayerInventory(from);
            var entries = new List<KeyValuePair<string, Action>>();

            entries.Add(new KeyValuePair<string, Action>(UiKit.L(own ? "$tv_menu_use" : "$tv_menu_take"),
                () => Use(item, from, container)));

            var gun = Firearms.FindGun(Gear.PrefabName(item));
            if (gun != null && own && GunState.Mag(item) > 0)
            {
                entries.Add(new KeyValuePair<string, Action>(UiKit.L("$tv_menu_unload"),
                    () => GunState.Unload(Player.m_localPlayer, item)));
            }

            if (own && container != null)
            {
                entries.Add(new KeyValuePair<string, Action>(UiKit.L("$tv_menu_to_container"),
                    () => StashOps.QuickMove(item, from, container)));
            }

            if (own && Reconciler.Current != null && Reconciler.Current.IsPlaced(item))
            {
                for (int i = Loadout.QuickFirst; i <= Loadout.QuickLast; i++)
                {
                    int index = i;
                    entries.Add(new KeyValuePair<string, Action>(UiKit.L("$tv_menu_bind") + " " + (index + 1),
                        () => StashOps.BindQuickSlot(item, index)));
                }
                if (Loc.QuickSlot(item) >= 0)
                {
                    entries.Add(new KeyValuePair<string, Action>(UiKit.L("$tv_menu_unbind"),
                        () => StashOps.BindQuickSlot(item, -1)));
                }
            }

            entries.Add(new KeyValuePair<string, Action>(UiKit.L("$tv_menu_drop"),
                () => StashOps.Drop(item, from, item.m_stack)));

            // Full-screen blocker: clicking anywhere else closes the menu.
            var blocker = UiKit.Image(_root, "MenuBlocker", new Color(0f, 0f, 0f, 0.001f));
            UiKit.Stretch(blocker.rectTransform);
            var blockerButton = blocker.gameObject.AddComponent<Button>();
            blockerButton.onClick.AddListener(CloseMenu);
            _menu = blocker.rectTransform;

            var panel = UiKit.Image(_menu, "Menu", UiKit.PanelBg);
            var prt = panel.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0f, 1f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out var local);
            prt.anchoredPosition = local;
            float w = 180f;
            float rowH = 26f;
            prt.sizeDelta = new Vector2(w, entries.Count * rowH + 8f);

            for (int i = 0; i < entries.Count; i++)
            {
                var action = entries[i].Value;
                var button = UiKit.Button(prt, "Entry", entries[i].Key, () =>
                {
                    CloseMenu();
                    action();
                    ReportError();
                    MarkDirty();
                });
                UiKit.TopLeft((RectTransform)button.transform, 4f, 4f + i * rowH, w - 8f, rowH - 2f);
            }
        }

        private void CloseMenu()
        {
            if (_menu != null)
            {
                Destroy(_menu.gameObject);
                _menu = null;
            }
        }

        private static void ReportError()
        {
            if (StashOps.LastError != null && Player.m_localPlayer != null)
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, UiKit.L(StashOps.LastError), 0, null, false);
                StashOps.LastError = null;
            }
        }

        // ------------------------------------------------------------------
        // Tooltip

        private void BuildTooltip()
        {
            var bg = UiKit.Image(_root, "Tooltip", new Color(0.05f, 0.05f, 0.05f, 0.96f), false);
            _tooltip = bg.rectTransform;
            _tooltip.anchorMin = _tooltip.anchorMax = new Vector2(0.5f, 0.5f);
            _tooltip.pivot = new Vector2(0f, 1f);
            _tooltipText = UiKit.Text(_tooltip, "Text", "", 15, Color.white);
            UiKit.Stretch(_tooltipText.rectTransform, 8f);
            _tooltip.gameObject.SetActive(false);
        }

        public void ShowTooltip(ItemView view, Vector2 screen)
        {
            if (_dragView != null || _menu != null || _tooltip == null)
            {
                return;
            }

            var item = view.Item;
            var size = ItemSizes.Of(item);
            string text = "<size=18><b>" + UiKit.L(item.m_shared.m_name) + "</b></size>\n";
            try
            {
                text += UiKit.L(item.GetTooltip());
            }
            catch (Exception)
            {
                // Valheim's tooltip builder can fail on modded items; the name is enough.
            }

            text += $"\n\n{UiKit.L("$tv_ui_size")}: {size.x}x{size.y}";
            var gun = Firearms.FindGun(Gear.PrefabName(item));
            if (gun != null)
            {
                text += $"\n{UiKit.L("$tv_ui_mag")}: {GunState.Mag(item)}/{gun.MagSize} {UiKit.L(GunState.LoadedAmmoName(item))}";
                text += $"\n{UiKit.L(gun.Caliber)}, {gun.Rpm} RPM, {GunState.ModeNames(gun)}";
            }
            var gear = Gear.Find(item);
            if (gear != null)
            {
                text += $"\n{UiKit.L("$tv_ui_capacity")}: {gear.GridW}x{gear.GridH}";
            }

            _tooltipText.text = text;
            _tooltip.sizeDelta = new Vector2(360f, 16f + _tooltipText.preferredHeight);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out var local);
            var half = _root.rect.size / 2f;
            float x = Mathf.Min(local.x + 18f, half.x - _tooltip.sizeDelta.x);
            float y = Mathf.Max(local.y - 18f, -half.y + _tooltip.sizeDelta.y);
            _tooltip.anchoredPosition = new Vector2(x, y);
            _tooltip.SetAsLastSibling();
            _tooltip.gameObject.SetActive(true);
        }

        public void HideTooltip(ItemView view)
        {
            if (_tooltip != null)
            {
                _tooltip.gameObject.SetActive(false);
            }
        }

        public void MovePanel(Vector2 delta)
        {
            float scale = _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
            _mainOffset += delta / scale;
            if (_main != null)
            {
                _main.anchoredPosition = _mainOffset;
            }
            if (_containerPanel != null)
            {
                _containerPanel.anchoredPosition = new Vector2(_mainOffset.x - _mainWidth - 16f, _mainOffset.y);
            }
        }

        private void OnDestroy()
        {
            HideVanilla(false);
            if (_canvasGo != null)
            {
                Destroy(_canvasGo);
            }
        }
    }

    /// <summary>Lets the inventory header drag the panel around.</summary>
    internal class PanelMover : MonoBehaviour, IDragHandler
    {
        public StashUi Owner;

        public void OnDrag(PointerEventData e)
        {
            Owner?.MovePanel(e.delta);
        }
    }

    /// <summary>Quick-slot box: right-click clears the binding.</summary>
    internal class QuickSlotView : MonoBehaviour, IPointerClickHandler
    {
        public ItemDrop.ItemData Item;

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Right && Item != null)
            {
                StashOps.BindQuickSlot(Item, -1);
                StashUi.MarkDirty();
            }
        }
    }
}
