using System;
using UnityEngine;

namespace UnityEngine.UI
{
    public abstract class Graphic : MonoBehaviour
    {
        public virtual Color color { get; set; }
        public bool raycastTarget { get; set; }
        public RectTransform rectTransform { get { throw null; } }
    }

    public abstract class MaskableGraphic : Graphic { }

    public class Image : MaskableGraphic
    {
        public Sprite sprite { get; set; }
        public bool preserveAspect { get; set; }
    }

    public class Text : MaskableGraphic
    {
        public Font font { get; set; }
        public int fontSize { get; set; }
        public TextAnchor alignment { get; set; }
        public string text { get; set; }
        public bool supportRichText { get; set; }
        public HorizontalWrapMode horizontalOverflow { get; set; }
        public VerticalWrapMode verticalOverflow { get; set; }
        public float preferredHeight { get { throw null; } }
        public float preferredWidth { get { throw null; } }
    }

    public abstract class BaseMeshEffect : MonoBehaviour { }

    public class Shadow : BaseMeshEffect
    {
        public Color effectColor { get; set; }
        public Vector2 effectDistance { get; set; }
    }

    public class Outline : Shadow { }

    public struct ColorBlock
    {
        public Color normalColor { get; set; }
        public Color highlightedColor { get; set; }
        public Color pressedColor { get; set; }
        public Color selectedColor { get; set; }
        public Color disabledColor { get; set; }
        public float colorMultiplier { get; set; }
    }

    public class Selectable : MonoBehaviour
    {
        public Graphic targetGraphic { get; set; }
        public ColorBlock colors { get; set; }
        public bool interactable { get; set; }
    }

    public class Button : Selectable
    {
        public class ButtonClickedEvent : UnityEngine.Events.UnityEvent { }
        public ButtonClickedEvent onClick { get { throw null; } }
    }

    public class Toggle : Selectable { }
    public class Slider : Selectable { }
    public class Scrollbar : Selectable { }
    public class Dropdown : Selectable { }
    public class InputField : Selectable { public enum ContentType { Standard } }

    public class CanvasScaler : MonoBehaviour
    {
        public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }
        public ScaleMode uiScaleMode { get; set; }
        public Vector2 referenceResolution { get; set; }
        public float matchWidthOrHeight { get; set; }
    }

    public class GraphicRaycaster : MonoBehaviour { }
    public class RectMask2D : MonoBehaviour { }

    public class ScrollRect : MonoBehaviour
    {
        public enum MovementType { Unrestricted, Elastic, Clamped }
        public RectTransform content { get; set; }
        public RectTransform viewport { get; set; }
        public bool horizontal { get; set; }
        public bool vertical { get; set; }
        public MovementType movementType { get; set; }
        public float scrollSensitivity { get; set; }
    }
}

namespace UnityEngine.EventSystems
{
    public class BaseEventData { }

    public class PointerEventData : BaseEventData
    {
        public enum InputButton { Left, Right, Middle }
        public InputButton button { get; set; }
        public Vector2 position { get; set; }
        public Vector2 delta { get; set; }
        public int clickCount { get; set; }
    }

    public interface IEventSystemHandler { }
    public interface IBeginDragHandler : IEventSystemHandler { void OnBeginDrag(PointerEventData eventData); }
    public interface IDragHandler : IEventSystemHandler { void OnDrag(PointerEventData eventData); }
    public interface IEndDragHandler : IEventSystemHandler { void OnEndDrag(PointerEventData eventData); }
    public interface IPointerClickHandler : IEventSystemHandler { void OnPointerClick(PointerEventData eventData); }
    public interface IPointerEnterHandler : IEventSystemHandler { void OnPointerEnter(PointerEventData eventData); }
    public interface IPointerExitHandler : IEventSystemHandler { void OnPointerExit(PointerEventData eventData); }

    public class EventSystem : MonoBehaviour
    {
        public static EventSystem current { get; set; }
        public bool IsPointerOverGameObject() { throw null; }
    }
}
