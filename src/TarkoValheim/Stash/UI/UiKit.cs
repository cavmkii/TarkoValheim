using System;
using Jotunn.Managers;
using UnityEngine.UI;
using UnityEngine;

namespace TarkoValheim.Stash.UI
{
    /// <summary>Small uGUI builders so the layout code stays readable.</summary>
    internal static class UiKit
    {
        public static readonly Color PanelBg = new Color(0.06f, 0.06f, 0.06f, 0.94f);
        public static readonly Color CellBg = new Color(0.22f, 0.22f, 0.22f, 0.55f);
        public static readonly Color SecureCellBg = new Color(0.62f, 0.48f, 0.12f, 0.55f);
        public static readonly Color SecureText = new Color(1f, 0.78f, 0.25f, 1f);
        public static readonly Color SlotBg = new Color(0.16f, 0.16f, 0.16f, 0.9f);
        public static readonly Color Label = new Color(0.78f, 0.76f, 0.7f, 1f);
        public static readonly Color Good = new Color(0.25f, 0.8f, 0.3f, 0.45f);
        public static readonly Color Bad = new Color(0.85f, 0.2f, 0.2f, 0.45f);
        public static readonly Color Warning = new Color(1f, 0.4f, 0.3f, 1f);

        private static Font _font;

        public static Font Font
        {
            get
            {
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

                    if (_font == null)
                    {
                        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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

        public static Button Button(Transform parent, string name, string label, Action onClick)
        {
            var img = Image(parent, name, new Color(0.25f, 0.22f, 0.18f, 0.95f));
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.3f, 1.2f, 1f, 1f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick());

            var text = Text(img.transform, "Label", label, 15, Color.white, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 2f);
            return button;
        }

        public static string L(string token)
        {
            return Localization.instance != null ? Localization.instance.Localize(token) : token;
        }
    }
}
