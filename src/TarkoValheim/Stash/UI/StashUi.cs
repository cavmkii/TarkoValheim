using System.Collections.Generic;
using System;
using TarkoValheim.Content;
using TarkoValheim.Modding;
using TarkoValheim.Raid;
using TarkoValheim.Util;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine;

namespace TarkoValheim.Stash.UI
{
    internal enum Tab
    {
        Gear,
        Crafting,
        Skills,
        Status
    }

    /// <summary>
    /// The Tarkov-style screen that replaces Valheim's whole inventory screen:
    /// GEAR (equipment, rig, pockets, backpack, pouch, and the open container),
    /// CRAFTING, SKILLS and HEALTH, under one top bar. Shown whenever Valheim's
    /// inventory is open; it covers Valheim's screen completely and takes every
    /// click, so nothing underneath can be touched by accident.
    ///
    /// Item controls: drag to move (drop on a same-size item to swap), R while
    /// dragging to rotate, Shift+drag to split, Ctrl+click to send to/from the
    /// open container, double-click to use, middle-click to lock, right-click
    /// for more.
    /// </summary>
    internal partial class StashUi : MonoBehaviour
    {
        public static StashUi Instance { get; private set; }

        // Layout is done on a fixed 1920x1080 frame, scaled to fit the screen.
        private const float FrameW = 1920f;
        private const float FrameH = 1080f;
        private const float TopH = 60f;
        private const float BodyTop = 72f;
        private const float Pad = 12f;
        private const float Gap = 6f;

        private GameObject _canvasGo;
        private Canvas _canvas;
        private RectTransform _root;
        private RectTransform _content;
        private RectTransform _frame;

        private RectTransform _tooltip;
        private Text _tooltipText;
        private RectTransform _menu;

        private readonly List<DropTarget> _targets = new List<DropTarget>();
        private readonly Dictionary<string, RectTransform> _scrollAreas = new Dictionary<string, RectTransform>();
        private readonly Dictionary<string, float> _savedScroll = new Dictionary<string, float>();

        private Tab _tab = Tab.Gear;
        private int _builtVersion = -1;
        private int _builtPlayerSig;
        private float _nextInfoRefresh;
        private Text _statusText;
        private int _builtContainerSig;
        private Container _builtContainer;
        private CraftingStation _builtStation;
        private float _nextCheck;
        private bool _wasVisible;
        private static bool _dirty;
        private float _fitScale = 1f;

        private ItemDrop.ItemData _modWeapon;
        private Inventory _modInventory;

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
                    _modWeapon = null;
                    _wasVisible = false;
                    return;
                }

                EnsureCanvas();
                _canvasGo.SetActive(true);

                var container = CurrentContainer();
                var station = player.GetCurrentCraftingStation();
                if (!_wasVisible)
                {
                    _wasVisible = true;
                    // Opened at a workbench: straight to crafting. Otherwise gear.
                    _tab = container == null && station != null ? Tab.Crafting : Tab.Gear;
                    _craftSelection = null;
                    _craftUpgrade = null;
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

                if (Time.time >= _nextCheck)
                {
                    _nextCheck = Time.time + 0.5f;
                    Reconciler.Run(player);
                    if (WorldSignature(player, container) != _builtContainerSig)
                    {
                        _dirty = true;
                    }
                    if (_statusText != null)
                    {
                        _statusText.text = StatusLine(player);
                    }
                }

                // Health, food and skill numbers move on their own.
                if ((_tab == Tab.Status || _tab == Tab.Skills) && Time.time >= _nextInfoRefresh)
                {
                    _dirty = true;
                }

                // The reconciler bumps its version on every pass, changed or
                // not; only rebuild when what's drawn would actually differ.
                // Rebuilding resets hover states and tooltips.
                if (!_dirty && _builtVersion != Reconciler.Version)
                {
                    _builtVersion = Reconciler.Version;
            _builtPlayerSig = PlayerSignature(player);
            _nextInfoRefresh = Time.time + 1f;
                    _dirty = PlayerSignature(player) != _builtPlayerSig;
                }

                if (_dirty || container != _builtContainer || station != _builtStation)
                {
                    Rebuild(player, container, station);
                }
            }
            catch (Exception e)
            {
                GameAccess.WarnOnce("StashUi.Update", e);
            }
        }

        private static Container CurrentContainer()
        {
            return InventoryGui.instance != null ? GameAccess.CurrentContainer(InventoryGui.instance) : null;
        }

        // Everything about the player's items that the screen draws.
        private static int PlayerSignature(Player player)
        {
            unchecked
            {
                var items = player.GetInventory().GetAllItems();
                int sig = items.Count;
                foreach (var item in items)
                {
                    sig = sig * 31 + (item.m_shared.m_name ?? "").GetHashCode();
                    sig = sig * 31 + item.m_stack;
                    sig = sig * 31 + item.m_quality;
                    sig = sig * 31 + (item.m_equipped ? 1 : 0);
                    sig = sig * 31 + Mathf.FloorToInt(item.m_durability);
                    if (item.m_customData != null)
                    {
                        foreach (var kv in item.m_customData)
                        {
                            sig += kv.Key.GetHashCode() ^ (kv.Value ?? "").GetHashCode();
                        }
                    }
                }
                sig = sig * 31 + Mathf.FloorToInt(player.GetHealth());
                return sig;
            }
        }

        // The open chest, plus every hideout stash chest when in a hideout.
        private static int WorldSignature(Player player, Container open)
        {
            unchecked
            {
                int sig = open != null ? ContainerSignature(open) : 0;
                foreach (var c in HideoutStash.Containers(player, open))
                {
                    sig = sig * 31 + (c != open ? ContainerSignature(c) : 1);
                }
                return sig;
            }
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
        // code that reads them keeps working. Everything else of Valheim's
        // screen is simply covered.
        private readonly Dictionary<Transform, Vector3> _hiddenVanilla = new Dictionary<Transform, Vector3>();

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
            // Above Valheim's own GUI canvas. Its inventory screen has a
            // full-screen click catcher; anything drawn under it gets no clicks.
            _canvas.sortingOrder = 10000;
            var scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(FrameW, FrameH);
            scaler.matchWidthOrHeight = 1f;
            _canvasGo.AddComponent<GraphicRaycaster>();
            _root = (RectTransform)_canvasGo.transform;

            BuildTooltip();
        }

        // ------------------------------------------------------------------
        // Shell

        private void Rebuild(Player player, Container container, CraftingStation station)
        {
            _dirty = false;
            _builtVersion = Reconciler.Version;
            _builtContainer = container;
            _builtStation = station;
            _builtContainerSig = WorldSignature(player, container);

            foreach (var kv in _scrollAreas)
            {
                if (kv.Value != null)
                {
                    _savedScroll[kv.Key] = kv.Value.anchoredPosition.y;
                }
            }
            _scrollAreas.Clear();

            if (_content != null)
            {
                Destroy(_content.gameObject);
            }
            _targets.Clear();
            HideTooltip(null);

            _content = UiKit.Rect("Content", _root);
            UiKit.Stretch(_content);
            _content.SetAsFirstSibling();

            // Covers Valheim's screen and swallows its clicks.
            var screen = UiKit.Image(_content, "Screen", UiKit.ScreenBg);
            UiKit.Stretch(screen.rectTransform);

            _frame = UiKit.Rect("Frame", _content);
            _frame.anchorMin = _frame.anchorMax = _frame.pivot = new Vector2(0.5f, 0.5f);
            _frame.sizeDelta = new Vector2(FrameW, FrameH);
            var size = _root.rect.size;
            _fitScale = Mathf.Clamp(Mathf.Min(size.x / FrameW, size.y / FrameH), 0.4f, 1f);
            _frame.localScale = Vector3.one * _fitScale;
            _frame.anchoredPosition = Vector2.zero;

            if (_modWeapon != null && (_modInventory == null || !_modInventory.GetAllItems().Contains(_modWeapon)))
            {
                _modWeapon = null;
            }

            BuildTopBar(player, station);
            switch (_tab)
            {
                case Tab.Crafting:
                    BuildCrafting(player, station);
                    break;
                case Tab.Skills:
                    BuildSkills(player);
                    break;
                case Tab.Status:
                    BuildStatus(player);
                    break;
                default:
                    BuildGear(player, container);
                    break;
            }
        }

        private void BuildTopBar(Player player, CraftingStation station)
        {
            var bar = UiKit.Image(_frame, "TopBar", new Color(0.02f, 0.02f, 0.02f, 0.9f), false);
            UiKit.TopLeft(bar.rectTransform, 0f, 0f, FrameW, TopH);
            var line = UiKit.Image(_frame, "TopLine", UiKit.AccentDim, false);
            UiKit.TopLeft(line.rectTransform, 0f, TopH, FrameW, 1f);

            var back = UiKit.Button(_frame, "Back", "<  " + UiKit.L("$tv_ui_back"), () =>
            {
                if (InventoryGui.instance != null)
                {
                    GameAccess.HideInventory(InventoryGui.instance);
                }
            }, false, 16);
            UiKit.TopLeft((RectTransform)back.transform, 20f, 12f, 130f, 36f);

            var tabs = new[] { Tab.Gear, Tab.Crafting, Tab.Skills, Tab.Status };
            float tabW = 190f;
            float x = (FrameW - tabs.Length * tabW - (tabs.Length - 1) * 4f) / 2f;
            foreach (var tab in tabs)
            {
                var t = tab;
                string label = UiKit.L("$tv_tab_" + tab.ToString().ToLowerInvariant());
                if (tab == Tab.Crafting && station != null)
                {
                    label += " *";
                }
                var button = UiKit.Button(_frame, "Tab_" + tab, label, () =>
                {
                    _tab = t;
                    CloseMenu();
                    MarkDirty();
                }, _tab == tab, 17);
                UiKit.TopLeft((RectTransform)button.transform, x, 10f, tabW, 40f);
                x += tabW + 4f;
            }

            // Right: who and where. Refreshed in place, without a rebuild.
            _statusText = UiKit.Text(_frame, "Who", StatusLine(player), 14, UiKit.Label, TextAnchor.MiddleRight);
            UiKit.TopLeft(_statusText.rectTransform, FrameW - 420f, 6f, 400f, 48f);
        }

        private static string StatusLine(Player player)
        {
            string status;
            var raid = RaidTracker.InRaid;
            if (raid == true)
            {
                int s = Mathf.FloorToInt(RaidTracker.RaidSeconds);
                status = $"<color=#e0604f>{UiKit.L("$tv_ui_in_raid")}</color>  {s / 60:00}:{s % 60:00}";
            }
            else if (raid == false)
            {
                status = $"<color=#7fd36b>{UiKit.L("$tv_ui_in_hideout")}</color>";
            }
            else
            {
                status = "";
            }
            return $"{player.GetPlayerName()}\n{status}";
        }

        /// <summary>A vertical scroll view; scroll position survives rebuilds.</summary>
        private RectTransform ScrollArea(RectTransform parent, string key, float x, float y, float w, float h, float contentH,
            out RectTransform viewport)
        {
            var vp = UiKit.Image(parent, "Viewport_" + key, new Color(0f, 0f, 0f, 0.01f));
            UiKit.TopLeft(vp.rectTransform, x, y, w, h);
            vp.gameObject.AddComponent<RectMask2D>();
            viewport = vp.rectTransform;

            var content = UiKit.Rect("ScrollContent", vp.rectTransform);
            UiKit.TopLeft(content, 0f, 0f, w, contentH);

            var scroll = vp.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = vp.rectTransform;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            _savedScroll.TryGetValue(key, out var saved);
            content.anchoredPosition = new Vector2(0f, Mathf.Clamp(saved, 0f, Mathf.Max(0f, contentH - h)));
            _scrollAreas[key] = content;
            return content;
        }

        /// <summary>Scales a column down (never up) so it fits its space.</summary>
        private static void FitInto(RectTransform rt, float naturalW, float naturalH, float maxW, float maxH)
        {
            float s = Mathf.Min(1f, maxW / Mathf.Max(1f, naturalW), maxH / Mathf.Max(1f, naturalH));
            rt.localScale = Vector3.one * s;
        }

        // ------------------------------------------------------------------
        // Drag and drop

        public void BeginDrag(ItemView view, PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || _dragView != null)
            {
                return;
            }
            if (Loc.IsLocked(view.Item))
            {
                StashOps.LastError = "$tv_err_locked";
                ReportError();
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

            var group = view.GetComponent<CanvasGroup>() ?? view.gameObject.AddComponent<CanvasGroup>();
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
            bool turned = _dragRot && ItemSizes.Of(_dragView.Item).x != ItemSizes.Of(_dragView.Item).y;
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
            _ghost.localScale = Vector3.one * _fitScale;
            _ghost.anchoredPosition = local + new Vector2(-_grab.x, _grab.y) * _fitScale;
            _ghost.SetAsLastSibling();

            var target = FindTarget(e.position, out var dest);
            var weaponUnder = PartOntoWeapon(_dragView.Item, target, e.position);
            var draggedPart = WeaponParts.Find(Gear.PrefabName(_dragView.Item));
            if (weaponUnder != null)
            {
                _ghostTint.color = WeaponParts.Fits(draggedPart, WeaponMods.FamilyOf(weaponUnder)) ? UiKit.Good : UiKit.Bad;
            }
            else if (target != null && target.ModSlot >= 0)
            {
                bool ok = draggedPart != null && (int)draggedPart.Slot == target.ModSlot
                    && WeaponParts.Fits(draggedPart, WeaponMods.FamilyOf(target.ModWeapon));
                _ghostTint.color = ok ? UiKit.Good : UiKit.Bad;
            }
            else if (target == null)
            {
                _ghostTint.color = new Color(0.5f, 0.5f, 0.5f, 0.35f);
            }
            else if (target.Ground)
            {
                _ghostTint.color = UiKit.AccentDim;
            }
            else if (target.Quick >= 0)
            {
                _ghostTint.color = StashOps.IsPlayerInventory(_dragView.Inventory) ? UiKit.Good : UiKit.Bad;
            }
            else if (StashOps.CanMove(_dragView.Item, _dragView.Inventory, dest, _dragAmount))
            {
                _ghostTint.color = UiKit.Good;
            }
            else
            {
                _ghostTint.color = StashOps.CanSwap(_dragView.Item, _dragView.Inventory, dest) ? UiKit.Swap : UiKit.Bad;
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
            var weaponUnder = PartOntoWeapon(item, target, e.position);
            CancelDrag();

            if (weaponUnder != null || (target != null && target.ModSlot >= 0))
            {
                var weapon = weaponUnder ?? target.ModWeapon;
                var part = WeaponParts.Find(Gear.PrefabName(item));
                string error = "$tv_err_part_doesnt_fit";
                if (part != null && (weaponUnder != null || (int)part.Slot == target.ModSlot)
                    && WeaponMods.Install(Player.m_localPlayer, weapon, target.Inventory, item, from, out error))
                {
                    GameAccess.Changed(target.Inventory);
                }
                else
                {
                    StashOps.LastError = error ?? "$tv_err_part_doesnt_fit";
                    ReportError();
                }
            }
            else if (target != null && target.Ground)
            {
                StashOps.Drop(item, from, amount);
                ReportError();
            }
            else if (target != null && target.Quick >= 0)
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
                    string error = StashOps.LastError;
                    if (amount != item.m_stack || !StashOps.Swap(item, from, dest))
                    {
                        StashOps.LastError = error;
                        ReportError();
                    }
                }
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

        /// <summary>The modular weapon under the pointer when a part is
        /// dropped on it, or null.</summary>
        private ItemDrop.ItemData PartOntoWeapon(ItemDrop.ItemData dragged, DropTarget target, Vector2 screen)
        {
            if (target == null || target.Quick >= 0 || target.ModSlot >= 0 || target.Ground
                || WeaponParts.Find(Gear.PrefabName(dragged)) == null)
            {
                return null;
            }

            ItemDrop.ItemData under = null;
            if (target.IsSlot)
            {
                if (StashOps.IsPlayerInventory(target.Inventory) && Reconciler.Current != null)
                {
                    Reconciler.Current.Slots.TryGetValue(target.Slot, out under);
                }
            }
            else if (target.Model != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(target.Rect, screen, null, out var lp);
                under = target.Model.At(Mathf.FloorToInt(lp.x / target.Cell), Mathf.FloorToInt(-lp.y / target.Cell));
            }

            return under != null && under != dragged && WeaponMods.IsModular(under) ? under : null;
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

                if (t.Quick >= 0 || t.ModSlot >= 0 || t.Ground)
                {
                    return t;
                }

                if (t.IsSlot)
                {
                    dest = new Dest { Inventory = t.Inventory, Loc = Loc.Slot(t.Slot) };
                    return t;
                }

                // Grid coordinates: the target rect may be scaled with its column.
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

        /// <summary>Where Ctrl+click sends a carried item: the open chest, or in a
        /// hideout the first stash chest with room for it. Null for items that
        /// aren't the player's (those go to the player).</summary>
        private Inventory SendTarget(ItemDrop.ItemData item)
        {
            var player = Player.m_localPlayer;
            var open = CurrentContainer();
            var stash = HideoutStash.Containers(player, open);
            if (stash.Count == 0)
            {
                return open != null ? open.GetInventory() : null;
            }
            foreach (var c in stash)
            {
                var inventory = c.GetInventory();
                if (ContainerLayout.Build(inventory).Grid.FindSpot(item, out _, out _, out _))
                {
                    return inventory;
                }
            }
            return stash[0].GetInventory();
        }

        public void Click(ItemView view, PointerEventData e)
        {
            var containerInventory = SendTarget(view.Item);

            if (e.button == PointerEventData.InputButton.Right)
            {
                OpenMenu(view, e.position, containerInventory);
                return;
            }

            if (e.button == PointerEventData.InputButton.Middle)
            {
                Loc.SetLocked(view.Item, !Loc.IsLocked(view.Item));
                GameAccess.Changed(view.Inventory);
                MarkDirty();
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
            bool locked = Loc.IsLocked(item);
            var entries = new List<KeyValuePair<string, Action>>();

            void Add(string token, string key, Action action)
            {
                string label = UiKit.L(token);
                if (key != null)
                {
                    label += $"  <color=#8a8a84>{key}</color>";
                }
                entries.Add(new KeyValuePair<string, Action>(label, action));
            }

            Add(own ? "$tv_menu_use" : "$tv_menu_take", own ? UiKit.L("$tv_key_dblclick") : "Ctrl+LMB",
                () => Use(item, from, container));

            if (WeaponMods.IsModular(item))
            {
                Add("$tv_menu_modify", null, () => OpenModPanel(item, from));
            }

            // Turn it round where it lies, if there's room.
            if (!locked && Loc.TryRead(item, out var loc) && !loc.IsSlot(out _))
            {
                var size = ItemSizes.Of(item);
                var turned = new Dest { Inventory = from, Loc = new Loc { Grid = loc.Grid, X = loc.X, Y = loc.Y, Rot = !loc.Rot } };
                if (size.x != size.y && StashOps.CanMove(item, from, turned, item.m_stack))
                {
                    Add("$tv_menu_rotate", null, () => StashOps.Move(item, from, turned, item.m_stack));
                }
            }

            if (own && container != null && !locked)
            {
                Add("$tv_menu_to_container", "Ctrl+LMB", () => StashOps.QuickMove(item, from, container));
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
                    Add("$tv_menu_unbind", null, () => StashOps.BindQuickSlot(item, -1));
                }
            }

            Add(locked ? "$tv_menu_unlock" : "$tv_menu_lock", "MMB", () =>
            {
                Loc.SetLocked(item, !locked);
                GameAccess.Changed(from);
            });

            if (!locked)
            {
                Add("$tv_menu_drop", null, () => StashOps.Drop(item, from, item.m_stack));
            }

            // Full-screen blocker: clicking anywhere else closes the menu.
            var blocker = UiKit.Image(_root, "MenuBlocker", new Color(0f, 0f, 0f, 0.001f));
            UiKit.Stretch(blocker.rectTransform);
            var blockerButton = blocker.gameObject.AddComponent<Button>();
            blockerButton.onClick.AddListener(CloseMenu);
            _menu = blocker.rectTransform;

            float w = 240f;
            float rowH = 28f;
            float h = entries.Count * rowH + 8f;
            var panel = UiKit.Image(_menu, "Menu", UiKit.AccentDim);
            var fill = UiKit.Image(panel.transform, "Fill", UiKit.PanelBg, false);
            UiKit.Stretch(fill.rectTransform, 1f);
            var prt = panel.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0f, 1f);
            prt.localScale = Vector3.one * _fitScale;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out var local);
            var half = _root.rect.size / 2f;
            local.x = Mathf.Min(local.x, half.x - w * _fitScale);
            local.y = Mathf.Max(local.y, -half.y + h * _fitScale);
            prt.anchoredPosition = local;
            prt.sizeDelta = new Vector2(w, h);

            for (int i = 0; i < entries.Count; i++)
            {
                var action = entries[i].Value;
                var button = UiKit.Button(prt, "Entry", entries[i].Key, () =>
                {
                    CloseMenu();
                    action();
                    ReportError();
                    MarkDirty();
                }, false, 14);
                UiKit.TopLeft((RectTransform)button.transform, 4f, 4f + i * rowH, w - 8f, rowH - 2f);
                var label = button.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.alignment = TextAnchor.MiddleLeft;
                    UiKit.Stretch(label.rectTransform, 8f);
                }
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
            var bg = UiKit.Image(_root, "Tooltip", UiKit.AccentDim, false);
            _tooltip = bg.rectTransform;
            _tooltip.anchorMin = _tooltip.anchorMax = new Vector2(0.5f, 0.5f);
            _tooltip.pivot = new Vector2(0f, 1f);
            var fill = UiKit.Image(_tooltip, "Fill", new Color(0.05f, 0.05f, 0.05f, 0.97f), false);
            UiKit.Stretch(fill.rectTransform, 1f);
            _tooltipText = UiKit.Text(_tooltip, "Text", "", 15, Color.white);
            UiKit.Stretch(_tooltipText.rectTransform, 10f);
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
            string text = "<size=18><b>" + UiKit.L(item.m_shared.m_name) + "</b></size>";
            if (item.m_shared.m_maxQuality > 1)
            {
                text += $"  <color=#8a8a84>{UiKit.L("$tv_ui_quality")} {item.m_quality}/{item.m_shared.m_maxQuality}</color>";
            }
            if (Loc.IsLocked(item))
            {
                text += $"  <color=#e39b3b>{UiKit.L("$tv_ui_locked")}</color>";
            }
            text += "\n";
            try
            {
                text += UiKit.L(item.GetTooltip());
            }
            catch (Exception)
            {
                // Valheim's tooltip builder can fail on modded items; the name is enough.
            }

            text += $"\n\n{UiKit.L("$tv_ui_size")}: {size.x}x{size.y}";
            if (WeaponMods.IsModular(item))
            {
                foreach (var inst in WeaponMods.AllInstalled(item))
                {
                    text += $"\n  + {UiKit.L("$" + inst.Part.Id.ToLowerInvariant())}  Lv{inst.Level}";
                }
                if (WeaponMods.Totals(item).MissingHead)
                {
                    text += "\n<color=#e0604f>" + UiKit.L("$tv_ui_missing_head") + "</color>";
                }
                else if (WeaponMods.Totals(item).Count == 0)
                {
                    text += "\n" + UiKit.L("$tv_ui_no_parts_short");
                }
            }
            var gear = Gear.Find(item);
            if (gear != null)
            {
                text += $"\n{UiKit.L("$tv_ui_capacity")}: {gear.GridW}x{gear.GridH}";
            }
            var partSpec = WeaponParts.Find(Gear.PrefabName(item));
            if (partSpec != null)
            {
                text += "\n" + ModPanel.PartSummary(partSpec, Mathf.Max(1, item.m_quality));
            }
            var med = Health.Meds.Find(Gear.PrefabName(item));
            if (med != null)
            {
                text += "\n" + UiKit.L("$tv_ui_med_hint");
            }

            // Keyboard and mouse shortcuts, so nobody has to guess.
            text += "\n\n<size=13><color=#8a8a84>" + UiKit.L("$tv_ui_shortcuts") + "</color></size>";

            _tooltipText.text = text;
            _tooltip.localScale = Vector3.one * _fitScale;
            _tooltip.sizeDelta = new Vector2(380f, 20f + _tooltipText.preferredHeight);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out var local);
            var half = _root.rect.size / 2f;
            float x = Mathf.Min(local.x + 18f, half.x - _tooltip.sizeDelta.x * _fitScale);
            float y = Mathf.Max(local.y - 18f, -half.y + _tooltip.sizeDelta.y * _fitScale);
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

        public void OpenModPanel(ItemDrop.ItemData weapon, Inventory inventory)
        {
            _modWeapon = weapon;
            _modInventory = inventory;
            _tab = Tab.Gear;
            MarkDirty();
        }

        public void CloseModPanel()
        {
            _modWeapon = null;
            _modInventory = null;
            MarkDirty();
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
