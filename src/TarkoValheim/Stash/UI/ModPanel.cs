using System.Text;
using TarkoValheim.Content;
using TarkoValheim.Guns;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TarkoValheim.Stash.UI
{
    /// <summary>
    /// Weapon modding screen: one box per part slot the gun has, and the
    /// gun's stats with and without its parts. Drag a part onto a slot (or
    /// onto the gun in any grid) to install it; right-click a slot to take
    /// the part off.
    /// </summary>
    internal static class ModPanel
    {
        private const float Pad = 12f;

        public static RectTransform Build(StashUi owner, RectTransform parent, Player player,
            ItemDrop.ItemData gunItem, Inventory gunInventory, System.Collections.Generic.List<DropTarget> targets)
        {
            var gun = Firearms.FindGun(Gear.PrefabName(gunItem));
            float cell = Plugin.CellSize.Value;
            float box = Mathf.Round(cell * 1.6f);
            float width = 3 * box + 2 * 8f + 2 * Pad + 250f;

            var bg = UiKit.Image(parent, "Mods", UiKit.PanelBg);
            var rt = bg.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0.5f);

            var title = UiKit.Text(rt, "Title", UiKit.L(gunItem.m_shared.m_name) + "  -  " + UiKit.L("$tv_ui_modding"),
                18, UiKit.Label, TextAnchor.MiddleLeft);
            UiKit.TopLeft(title.rectTransform, Pad, 4f, width - 80f, 28f);
            var close = UiKit.Button(rt, "Close", "X", owner.CloseModPanel);
            UiKit.TopLeft((RectTransform)close.transform, width - Pad - 28f, 6f, 28f, 24f);

            // The gun itself.
            var gunBox = UiKit.Image(rt, "Gun", UiKit.SlotBg);
            float gunW = 3 * box + 16f;
            UiKit.TopLeft(gunBox.rectTransform, Pad, 40f, gunW, box);
            var icon = UiKit.Image(gunBox.transform, "Icon", Color.white, false);
            icon.sprite = gunItem.GetIcon();
            icon.preserveAspect = true;
            UiKit.Stretch(icon.rectTransform, 6f);

            // Slots, three per row.
            int i = 0;
            float slotsTop = 40f + box + 10f;
            foreach (var slot in GunParts.OpenSlots(gun))
            {
                float x = Pad + (i % 3) * (box + 8f);
                float y = slotsTop + (i / 3) * (box + 8f);
                SlotBox(rt, gunItem, gunInventory, slot, x, y, box, targets);
                i++;
            }
            float slotsBottom = slotsTop + Mathf.CeilToInt(i / 3f) * (box + 8f);

            // Stats: base (no parts) against current.
            var now = GunStats.Of(gun, gunItem, player);
            var bare = GunStats.Of(gun, null, player);
            var stats = UiKit.Text(rt, "Stats", Describe(gun, bare, now), 14, UiKit.Label);
            UiKit.TopLeft(stats.rectTransform, Pad + gunW + 14f, 40f, 240f, 300f);

            var hint = UiKit.Text(rt, "Hint", UiKit.L("$tv_ui_mod_hint"), 12, new Color(1f, 1f, 1f, 0.5f));
            UiKit.TopLeft(hint.rectTransform, Pad, slotsBottom + 2f, width - 2 * Pad, 36f);

            rt.sizeDelta = new Vector2(width, Mathf.Max(slotsBottom + 44f, 340f));
            return rt;
        }

        private static void SlotBox(RectTransform parent, ItemDrop.ItemData gunItem, Inventory gunInventory, PartSlot slot,
            float x, float y, float size, System.Collections.Generic.List<DropTarget> targets)
        {
            var bg = UiKit.Image(parent, "Slot_" + slot, UiKit.SlotBg);
            UiKit.TopLeft(bg.rectTransform, x, y, size, size);
            var label = UiKit.Text(bg.transform, "Label", UiKit.L("$tv_partslot_" + slot.ToString().ToLowerInvariant()),
                11, new Color(1f, 1f, 1f, 0.45f));
            UiKit.Stretch(label.rectTransform, 3f);

            var part = GunMods.Installed(gunItem, slot);
            if (part != null)
            {
                bg.color = new Color(0.3f, 0.22f, 0.16f, 0.95f);
                var prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(part.Id) : null;
                var drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (drop != null)
                {
                    var icon = UiKit.Image(bg.transform, "Icon", Color.white, false);
                    icon.sprite = drop.m_itemData.GetIcon();
                    icon.preserveAspect = true;
                    UiKit.Stretch(icon.rectTransform, 10f);
                }
                var name = UiKit.Text(bg.transform, "Part", UiKit.L("$" + part.Id.ToLowerInvariant()), 11,
                    Color.white, TextAnchor.LowerCenter);
                UiKit.Stretch(name.rectTransform, 3f);

                var view = bg.gameObject.AddComponent<ModSlotView>();
                view.Gun = gunItem;
                view.Inventory = gunInventory;
                view.Slot = slot;
            }

            targets.Add(new DropTarget
            {
                Rect = bg.rectTransform, Inventory = gunInventory, ModSlot = (int)slot, ModGun = gunItem
            });
        }

        private static string Describe(GunSpec gun, GunStats bare, GunStats now)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<b>" + UiKit.L("$tv_ui_stats") + "</b>");
            Line(sb, "$tv_stat_damage", bare.DamageMult * 100f, now.DamageMult * 100f, "0", "%", true);
            Line(sb, "$tv_stat_velocity", bare.Velocity, now.Velocity, "0", " m/s", true);
            Line(sb, "$tv_stat_hip", bare.HipSpread, now.HipSpread, "0.00", "°", false);
            Line(sb, "$tv_stat_ads", bare.AdsSpread, now.AdsSpread, "0.00", "°", false);
            Line(sb, "$tv_stat_recoil", bare.RecoilUp, now.RecoilUp, "0.00", "", false);
            Line(sb, "$tv_stat_reload", bare.ReloadTime, now.ReloadTime, "0.0", " s", false);
            Line(sb, "$tv_stat_mag", bare.MagSize, now.MagSize, "0", "", true);
            Line(sb, "$tv_stat_zoom", Zoom(bare.AdsFov), Zoom(now.AdsFov), "0.0", "x", true);
            sb.AppendLine();
            sb.Append($"{UiKit.L(gun.Caliber)}, {gun.Rpm} RPM\n{GunState.ModeNames(gun)}");
            return sb.ToString();
        }

        private static void Line(StringBuilder sb, string token, float before, float after, string format, string unit, bool higherIsBetter)
        {
            string value = after.ToString(format) + unit;
            if (Mathf.Abs(after - before) > 0.0001f)
            {
                bool better = higherIsBetter ? after > before : after < before;
                value = $"<color={(better ? "#7fd36b" : "#e0604f")}>{value}</color>";
            }
            sb.AppendLine($"{UiKit.L(token)}: {value}");
        }

        // Magnification relative to Valheim's default ~65° field of view.
        private static float Zoom(float fov)
        {
            return Mathf.Tan(65f * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(Mathf.Max(fov, 1f) * 0.5f * Mathf.Deg2Rad);
        }
    }

    /// <summary>Installed-part box: right-click takes the part off.</summary>
    internal class ModSlotView : MonoBehaviour, IPointerClickHandler
    {
        public ItemDrop.ItemData Gun;
        public Inventory Inventory;
        public PartSlot Slot;

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Right)
            {
                GunMods.Remove(Player.m_localPlayer, Gun, Inventory, Slot);
                StashUi.MarkDirty();
            }
        }
    }
}
