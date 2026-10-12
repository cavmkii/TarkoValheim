using System;
using Jotunn.Managers;
using UnityEngine.UI;
using UnityEngine;

namespace TarkoValheim.Stash.UI
{
    /// <summary>Small uGUI builders so the layout code stays readable.</summary>
    internal static class UiKit
    {
        // Tarkov palette: near-black screen, charcoal panels, grey text, one
        // light-orange accent for everything you can press.
        // Fully opaque: Valheim renders UI in linear colour space, where even
        // 3% transparency lets its own HUD and crafting panel show through.
        public static readonly Color ScreenBg = new Color(0.035f, 0.04f, 0.04f, 1f);
        public static readonly Color PanelBg = new Color(0.07f, 0.075f, 0.075f, 0.96f);
        public static readonly Color HeaderBg = new Color(0.15f, 0.155f, 0.15f, 0.95f);
        public static readonly Color Accent = new Color(0.89f, 0.61f, 0.23f, 1f);
        public static readonly Color AccentDim = new Color(0.89f, 0.61f, 0.23f, 0.35f);
        public static readonly Color Muted = new Color(0.55f, 0.55f, 0.52f, 1f);
        public static readonly Color CellBg = new Color(0.16f, 0.165f, 0.16f, 0.85f);
        public static readonly Color SecureCellBg = new Color(0.62f, 0.48f, 0.12f, 0.55f);
        public static readonly Color SecureText = new Color(1f, 0.78f, 0.25f, 1f);
        public static readonly Color SlotBg = new Color(0.11f, 0.115f, 0.11f, 0.95f);
        public static readonly Color Label = new Color(0.78f, 0.76f, 0.7f, 1f);
        public static readonly Color Good = new Color(0.25f, 0.8f, 0.3f, 0.45f);
        public static readonly Color Bad = new Color(0.85f, 0.2f, 0.2f, 0.45f);
        public static readonly Color Swap = new Color(0.3f, 0.55f, 0.9f, 0.45f);
        public static readonly Color Warning = new Color(1f, 0.4f, 0.3f, 1f);

        private static Font _font;

        public static Font Font
        {
            get
            {
                if (_font == null)
                {
                    // A plain sans reads closer to Tarkov than Valheim's serif.
                    try
                    {
                        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    }
                    catch (Exception)
                    {
                        _font = null;
                    }

                    if (_font == null)
                    {
                        try
                        {
                            _font = GUIManager.Instance.AveriaSerifBold;
                        }
                        catch (Exception)
                        {
                            _font = null;
                        }
                    }
                }
                return _font;
            }
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Places a rect by its top-left corner, in pixels from the
        /// parent's top-left corner (y grows downwards).</summary>
        public static RectTransform TopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        public static Image Image(Transform parent, string name, Color color, bool raycast = true)
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static Text Text(Transform parent, string name, string value, int size, Color color,
            TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var text = Rect(name, parent).gameObject.AddComponent<Text>();
            text.font = Font;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.text = value;
            text.raycastTarget = false;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(1f, -1f);
            return text;
        }

        /// <summary>A bordered button: orange outline, dark fill that lights
        /// up orange on hover. <paramref name="primary"/> fills it orange.</summary>
        public static Button Button(Transform parent, string name, string label, Action onClick, bool primary = false,
            int fontSize = 15)
        {
            var border = Image(parent, name, primary ? Accent : AccentDim);
            // White fill tinted by the button's colour states.
            var fill = Image(border.transform, "Fill", Color.white);
            Stretch(fill.rectTransform, 1f);

            var button = border.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            var colors = button.colors;
            colors.normalColor = primary ? new Color(0.82f, 0.55f, 0.2f, 1f) : new Color(0.12f, 0.12f, 0.11f, 1f);
            colors.highlightedColor = primary ? new Color(0.96f, 0.7f, 0.32f, 1f) : new Color(0.42f, 0.28f, 0.1f, 1f);
            colors.selectedColor = colors.normalColor;
            colors.pressedColor = primary ? new Color(0.6f, 0.4f, 0.14f, 1f) : new Color(0.6f, 0.4f, 0.14f, 1f);
            colors.disabledColor = new Color(0.1f, 0.1f, 0.1f, 1f);
            colors.colorMultiplier = 1f;
            // No fade: a fresh button would otherwise fade in from white,
            // which flashes every time the screen is rebuilt.
            colors.fadeDuration = 0f;
            button.colors = colors;
            button.onClick.AddListener(() => onClick());

            var text = Text(fill.transform, "Label", label, fontSize, primary ? new Color(0.08f, 0.06f, 0.03f) : new Color(0.92f, 0.9f, 0.84f),
                TextAnchor.MiddleCenter);
            if (primary)
            {
                UnityEngine.Object.Destroy(text.GetComponent<Outline>());
            }
            Stretch(text.rectTransform, 2f);
            return button;
        }

        /// <summary>Tarkov's grey strip with a caption on the left and an
        /// optional figure (capacity, level) on the right.</summary>
        public static RectTransform Header(RectTransform parent, string caption, string right, float x, float y, float w,
            Color? textColor = null)
        {
            var strip = Image(parent, "Header", HeaderBg, false);
            TopLeft(strip.rectTransform, x, y, w, 22f);
            var left = Text(strip.transform, "Caption", caption, 13, textColor ?? Label, TextAnchor.MiddleLeft);
            left.horizontalOverflow = HorizontalWrapMode.Overflow;
            Stretch(left.rectTransform, 6f);
            if (!string.IsNullOrEmpty(right))
            {
                var r = Text(strip.transform, "Right", right, 13, Muted, TextAnchor.MiddleRight);
                Stretch(r.rectTransform, 6f);
            }
            return strip.rectTransform;
        }

        /// <summary>A labelled fill bar: "Health  83 / 120".</summary>
        public static void Bar(RectTransform parent, string caption, float value, float max, Color color,
            float x, float y, float w, float h = 20f, string format = "0")
        {
            var bg = Image(parent, "Bar", new Color(0f, 0f, 0f, 0.6f), false);
            TopLeft(bg.rectTransform, x, y, w, h);
            float f = max > 0f ? Mathf.Clamp01(value / max) : 0f;
            var fill = Image(bg.transform, "Fill", color, false);
            var frt = fill.rectTransform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = new Vector2(f, 1f);
            frt.offsetMin = new Vector2(1f, 1f);
            frt.offsetMax = new Vector2(f > 0.999f ? -1f : 0f, -1f);
            if (h < 14f)
            {
                return;
            }
            var left = Text(bg.transform, "Caption", caption, 13, Color.white, TextAnchor.MiddleLeft);
            Stretch(left.rectTransform, 6f);
            var right = Text(bg.transform, "Value", value.ToString(format) + " / " + max.ToString(format), 13, Color.white,
                TextAnchor.MiddleRight);
            Stretch(right.rectTransform, 6f);
        }

        /// <summary>A thin coloured frame drawn inside a rect.</summary>
        public static void Border(Transform parent, Color color, float t = 2f)
        {
            var top = Image(parent, "BorderT", color, false).rectTransform;
            top.anchorMin = new Vector2(0f, 1f); top.anchorMax = Vector2.one; top.offsetMin = new Vector2(0f, -t); top.offsetMax = Vector2.zero;
            var bottom = Image(parent, "BorderB", color, false).rectTransform;
            bottom.anchorMin = Vector2.zero; bottom.anchorMax = new Vector2(1f, 0f); bottom.offsetMin = Vector2.zero; bottom.offsetMax = new Vector2(0f, t);
            var left = Image(parent, "BorderL", color, false).rectTransform;
            left.anchorMin = Vector2.zero; left.anchorMax = new Vector2(0f, 1f); left.offsetMin = Vector2.zero; left.offsetMax = new Vector2(t, 0f);
            var right = Image(parent, "BorderR", color, false).rectTransform;
            right.anchorMin = new Vector2(1f, 0f); right.anchorMax = Vector2.one; right.offsetMin = new Vector2(-t, 0f); right.offsetMax = Vector2.zero;
        }

        public static string L(string token)
        {
            return Localization.instance != null ? Localization.instance.Localize(token) : token;
        }
    }
}
