using TarkoValheim.Content;
using TarkoValheim.Stash.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TarkoValheim.Guns
{
    /// <summary>
    /// Flat sight pictures drawn over the screen while aiming, in place of
    /// gun models (which need a Unity asset bundle). Iron sights are a front
    /// post sitting in a rear notch; the scope is a circular field of view
    /// with a PSO-style reticle. All shapes are built in code.
    /// </summary>
    internal sealed class SightOverlay
    {
        private static readonly Color Metal = new Color(0.07f, 0.07f, 0.07f, 0.97f);
        private static readonly Color Reticle = new Color(0f, 0f, 0f, 0.95f);

        private readonly RectTransform _irons;
        private readonly RectTransform _reflex;
        private readonly RectTransform _scope;
        private readonly CanvasGroup _ironsGroup;
        private readonly CanvasGroup _reflexGroup;
        private readonly CanvasGroup _scopeGroup;

        public SightOverlay(RectTransform root)
        {
            _irons = Layer(root, "Irons", out _ironsGroup);
            _reflex = Layer(root, "Reflex", out _reflexGroup);
            _scope = Layer(root, "Scope", out _scopeGroup);
            BuildIrons(_irons);
            BuildReflex(_reflex);
            BuildScope(_scope);
        }

        /// <summary>Returns true while a sight picture is covering the crosshair.</summary>
        public bool Update(GunStats stats, float ads)
        {
            // Snap in over the last part of the aim transition.
            float alpha = stats != null ? Mathf.Clamp01((ads - 0.6f) / 0.3f) : 0f;
            var sight = stats != null ? stats.Sight : SightType.Irons;

            Show(_irons, _ironsGroup, sight == SightType.Irons, alpha);
            Show(_reflex, _reflexGroup, sight == SightType.Reflex, alpha);
            Show(_scope, _scopeGroup, sight == SightType.Scope, alpha);
            return alpha > 0.5f;
        }

        private static void Show(RectTransform layer, CanvasGroup group, bool selected, float alpha)
        {
            group.alpha = selected ? alpha : 0f;
            layer.gameObject.SetActive(selected && alpha > 0f);
        }

        // Red dot: a thin housing frame low in view, and a dot at the point of aim.
        private static void BuildReflex(RectTransform root)
        {
            var frame = new Color(0.08f, 0.08f, 0.08f, 0.9f);
            var center = new Vector2(0.5f, 0.5f);
            Block(root, "FrameL", frame, center, -150f, -10f, 14f, 200f);
            Block(root, "FrameR", frame, center, 150f, -10f, 14f, 200f);
            Block(root, "FrameT", frame, center, 0f, 90f, 314f, 10f);
            Block(root, "FrameB", frame, center, 0f, -112f, 314f, 18f);
            Block(root, "Mount", frame, new Vector2(0.5f, 1f), 0f, -120f, 120f, 400f);
            Block(root, "Glass", new Color(0.4f, 0.6f, 0.7f, 0.06f), center, 0f, -10f, 286f, 190f);
            var dot = Block(root, "Dot", new Color(1f, 0.15f, 0.1f, 0.95f), center, 0f, 0f, 5f, 5f);
            dot.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        private static RectTransform Layer(RectTransform root, string name, out CanvasGroup group)
        {
            var rt = UiKit.Rect(name, root);
            UiKit.Stretch(rt);
            group = rt.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            rt.gameObject.SetActive(false);
            return rt;
        }

        // Centre-anchored rect: x/y is the offset of its pivot from screen centre.
        private static Image Block(RectTransform parent, string name, Color color, Vector2 pivot, float x, float y, float w, float h)
        {
            var img = UiKit.Image(parent, name, color, false);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = pivot;
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return img;
        }

        private static void BuildIrons(RectTransform root)
        {
            var top = new Vector2(0.5f, 1f);
            var near = new Color(0.13f, 0.12f, 0.11f, 0.93f);

            // Front sight, far away and sharp: a thin post whose tip is the
            // point of aim (screen centre), inside a pair of hood ears.
            Block(root, "Post", Metal, top, 0f, 0f, 6f, 120f);
            Block(root, "EarL", Metal, top, -38f, 34f, 8f, 150f);
            Block(root, "EarR", Metal, top, 38f, 34f, 8f, 150f);
            Block(root, "FrontBase", Metal, top, 0f, -110f, 90f, 40f);

            // Rear sight, close and soft: a leaf with a notch, top edge level
            // with the post tip, over the receiver running down out of view.
            Block(root, "Receiver", near, top, 0f, -100f, 300f, 700f);
            Block(root, "RearL", near, new Vector2(1f, 1f), -18f, 0f, 240f, 110f);
            Block(root, "RearR", near, new Vector2(0f, 1f), 18f, 0f, 240f, 110f);
            Block(root, "NotchFloor", near, top, 0f, -62f, 36f, 50f);
        }

        private static void BuildScope(RectTransform root)
        {
            const float d = 1000f;

            var lens = UiKit.Image(root, "Vignette", Color.white, false);
            lens.sprite = Vignette();
            var lrt = lens.rectTransform;
            lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.sizeDelta = new Vector2(d, d);

            // Black beyond the lens on all sides.
            Block(root, "Left", Color.black, new Vector2(1f, 0.5f), -d / 2f + 1f, 0f, 3000f, 3000f);
            Block(root, "Right", Color.black, new Vector2(0f, 0.5f), d / 2f - 1f, 0f, 3000f, 3000f);
            Block(root, "Top", Color.black, new Vector2(0.5f, 0f), 0f, d / 2f - 1f, d, 2000f);
            Block(root, "Bottom", Color.black, new Vector2(0.5f, 1f), 0f, -d / 2f + 1f, d, 2000f);

            // PSO-style reticle: chevron at the point of aim, a thick post
            // below, and fine horizontal lines left and right.
            var chevron = Block(root, "Chevron", Reticle, new Vector2(0.5f, 0.5f), 0f, -7f, 13f, 13f);
            chevron.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Block(root, "Post", Reticle, new Vector2(0.5f, 1f), 0f, -60f, 5f, 440f);
            Block(root, "LineL", Reticle, new Vector2(1f, 0.5f), -40f, 0f, 420f, 2f);
            Block(root, "LineR", Reticle, new Vector2(0f, 0.5f), 40f, 0f, 420f, 2f);

            for (int i = 1; i <= 4; i++)
            {
                Block(root, "TickL" + i, Reticle, new Vector2(0.5f, 0.5f), -40f - i * 70f, 0f, 2f, i % 2 == 0 ? 22f : 12f);
                Block(root, "TickR" + i, Reticle, new Vector2(0.5f, 0.5f), 40f + i * 70f, 0f, 2f, i % 2 == 0 ? 22f : 12f);
            }
        }

        // A black square with a soft-edged transparent circle in the middle.
        private static Sprite Vignette()
        {
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[n * n];
            float c = (n - 1) / 2f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / (n / 2f);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((r - 0.88f) / 0.1f));
                    pixels[y * n + x] = new Color32(0, 0, 0, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f));
        }
    }
}
