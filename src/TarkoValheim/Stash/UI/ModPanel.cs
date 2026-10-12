using System.Collections.Generic;
using System.Text;
using TarkoValheim.Content;
using TarkoValheim.Modding;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TarkoValheim.Stash.UI
{
    /// <summary>
    /// Weapon modding screen: one box per part slot the weapon has (blade,
    /// grip, guard, wrap...), and its stats with and without its parts. Drag
    /// a part onto a slot (or onto the weapon in any grid) to install it;
    /// right-click a slot to take the part off.
    /// </summary>
    internal static class ModPanel
    {
        private const float Pad = 12f;

        public static RectTransform Build(StashUi owner, RectTransform parent, Player player,
            ItemDrop.ItemData weapon, Inventory weaponInventory, List<DropTarget> targets)
        {
            var family = WeaponMods.FamilyOf(weapon);
            float cell = Plugin.CellSize.Value;
            float box = Mathf.Round(cell * 1.9f);
            float width = 600f;

            var bg = UiKit.Image(parent, "Mods", UiKit.PanelBg);
            var rt = bg.rectTransform;

            UiKit.Header(rt, UiKit.L("$tv_ui_modding") + "  <color=#8a8a84>" + UiKit.L(weapon.m_shared.m_name) + "</color>", "", 0f, 0f, width);
            var close = UiKit.Button(rt, "Close", "X", owner.CloseModPanel, false, 13);
            UiKit.TopLeft((RectTransform)close.transform, width - 26f, 1f, 24f, 20f);

            // The weapon itself.
            var weaponBox = UiKit.Image(rt, "Weapon", UiKit.SlotBg);
            float weaponW = width - 2 * Pad;
            UiKit.TopLeft(weaponBox.rectTransform, Pad, 32f, weaponW, box);
            UiKit.Border(weaponBox.transform, new Color(1f, 1f, 1f, 0.08f), 1f);
            var icon = UiKit.Image(weaponBox.transform, "Icon", Color.white, false);
            icon.sprite = weapon.GetIcon();
            icon.preserveAspect = true;
            UiKit.Stretch(icon.rectTransform, 6f);

            // One box per slot, in a row.
            var slots = new List<PartSlot>(WeaponParts.SlotsOf(family));
            float slotW = Mathf.Min(box * 1.4f, (weaponW - (slots.Count - 1) * 8f) / Mathf.Max(1, slots.Count));
            float slotsTop = 32f + box + 10f;
            for (int i = 0; i < slots.Count; i++)
            {
                SlotBox(rt, weapon, weaponInventory, family, slots[i], Pad + i * (slotW + 8f), slotsTop, slotW, box, targets);
            }
            float y = slotsTop + box + 12f;

            var stats = UiKit.Text(rt, "Stats", Describe(weapon), 14, UiKit.Label);
            UiKit.TopLeft(stats.rectTransform, Pad, y, width - 2 * Pad, 220f);
            y += stats.preferredHeight + 10f;

            var hint = UiKit.Text(rt, "Hint", UiKit.L("$tv_ui_mod_hint"), 12, UiKit.Muted);
            UiKit.TopLeft(hint.rectTransform, Pad, y, width - 2 * Pad, 36f);

            rt.sizeDelta = new Vector2(width, y + 44f);
            return rt;
        }

        private static void SlotBox(RectTransform parent, ItemDrop.ItemData weapon, Inventory weaponInventory, WeaponFamily family,
            PartSlot slot, float x, float y, float w, float h, List<DropTarget> targets)
        {
            var bg = UiKit.Image(parent, "Slot_" + slot, UiKit.SlotBg);
            UiKit.TopLeft(bg.rectTransform, x, y, w, h);
            UiKit.Border(bg.transform, UiKit.AccentDim, 1f);
            var label = UiKit.Text(bg.transform, "Label", UiKit.L(WeaponParts.SlotToken(slot, family)).ToUpperInvariant(),
                11, UiKit.Muted);
            UiKit.Stretch(label.rectTransform, 4f);

            if (WeaponMods.TryGet(weapon, slot, out var installed))
            {
                bg.color = new Color(0.22f, 0.17f, 0.11f, 0.95f);
                var prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(installed.Part.Id) : null;
                var drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (drop != null)
                {
                    var icon = UiKit.Image(bg.transform, "Icon", Color.white, false);
                    icon.sprite = drop.m_itemData.GetIcon();
                    icon.preserveAspect = true;
                    UiKit.Stretch(icon.rectTransform, 14f);
                }
                var name = UiKit.Text(bg.transform, "Part", $"{UiKit.L("$" + installed.Part.Id.ToLowerInvariant())}  Lv{installed.Level}",
                    11, Color.white, TextAnchor.LowerCenter);
                UiKit.Stretch(name.rectTransform, 3f);

                var view = bg.gameObject.AddComponent<ModSlotView>();
                view.Weapon = weapon;
                view.Inventory = weaponInventory;
                view.Slot = slot;
            }

            targets.Add(new DropTarget
            {
                Rect = bg.rectTransform, Inventory = weaponInventory, ModSlot = (int)slot, ModWeapon = weapon
            });
        }

        /// <summary>The weapon's numbers with its parts, and what the parts add.</summary>
        public static string Describe(ItemDrop.ItemData weapon)
        {
            var t = WeaponMods.Totals(weapon);
            var sb = new StringBuilder();
            sb.AppendLine("<b>" + UiKit.L("$tv_ui_part_effects") + "</b>");
            if (t.Count == 0)
            {
                sb.AppendLine(UiKit.L("$tv_ui_no_parts"));
                return sb.ToString();
            }
            Line(sb, "$tv_stat_damage", t.Damage, false);
            Line(sb, "$tv_stat_durability", t.Durability, false);
            Line(sb, "$tv_stat_stamina", t.Stamina, true);
            Line(sb, "$tv_stat_block", t.Block, false);
            Line(sb, "$tv_stat_parry", t.Parry, false);
            if (t.Spirit > 0f)
            {
                sb.AppendLine($"<color=#7fd36b>{UiKit.L("$tv_stat_spirit")} +{t.Spirit * 100f:0}%</color>");
            }
            if (t.Fire > 0f)
            {
                sb.AppendLine($"<color=#7fd36b>{UiKit.L("$tv_stat_fire")} +{t.Fire * 100f:0}%</color>");
            }
            return sb.ToString();
        }

        private static void Line(StringBuilder sb, string token, float v, bool lowerIsBetter)
        {
            if (Mathf.Abs(v) < 0.0001f)
            {
                return;
            }
            bool good = lowerIsBetter ? v < 0f : v > 0f;
            sb.AppendLine($"<color={(good ? "#7fd36b" : "#e0604f")}>{UiKit.L(token)} {(v > 0 ? "+" : "")}{v * 100f:0}%</color>");
        }

        /// <summary>One part's effect at its level, for tooltips.</summary>
        public static string PartSummary(PartSpec part, int level)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{UiKit.L("$tv_ui_part_slot_" + part.Slot.ToString().ToLowerInvariant())}  <color=#8a8a84>{UiKit.L("$tv_ui_level")} {level}/{WeaponParts.MaxLevel}</color>");
            Line(sb, "$tv_stat_damage", part.At(part.Damage, part.DamageStep, level), false);
            Line(sb, "$tv_stat_durability", part.At(part.Durability, part.DurabilityStep, level), false);
            Line(sb, "$tv_stat_stamina", part.At(part.Stamina, part.StaminaStep, level), true);
            Line(sb, "$tv_stat_block", part.At(part.Block, part.BlockStep, level), false);
            Line(sb, "$tv_stat_parry", part.At(part.Parry, part.ParryStep, level), false);
            if (part.Spirit > 0f)
            {
                sb.AppendLine($"<color=#7fd36b>{UiKit.L("$tv_stat_spirit")} +{part.At(part.Spirit, part.SpiritStep, level) * 100f:0}%</color>");
            }
            if (part.Fire > 0f)
            {
                sb.AppendLine($"<color=#7fd36b>{UiKit.L("$tv_stat_fire")} +{part.At(part.Fire, part.FireStep, level) * 100f:0}%</color>");
            }

            var fits = new List<string>();
            foreach (WeaponFamily f in System.Enum.GetValues(typeof(WeaponFamily)))
            {
                if (f != WeaponFamily.None && WeaponParts.Fits(part, f))
                {
                    fits.Add(UiKit.L("$tv_family_" + f.ToString().ToLowerInvariant()));
                }
            }
            sb.Append(UiKit.L("$tv_ui_fits") + ": " + string.Join(", ", fits.ToArray()));
            return sb.ToString();
        }
    }

    /// <summary>Installed-part box: right-click takes the part off.</summary>
    internal class ModSlotView : MonoBehaviour, IPointerClickHandler
    {
        public ItemDrop.ItemData Weapon;
        public Inventory Inventory;
        public PartSlot Slot;

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Right)
            {
                WeaponMods.Remove(Player.m_localPlayer, Weapon, Inventory, Slot);
                StashUi.MarkDirty();
            }
        }
    }
}
