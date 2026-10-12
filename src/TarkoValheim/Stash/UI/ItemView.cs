using UnityEngine;
using UnityEngine.EventSystems;

namespace TarkoValheim.Stash.UI
{
    /// <summary>
    /// One drawn item. Forwards pointer events to <see cref="StashUi"/>, which
    /// owns all drag, click and tooltip behaviour.
    /// </summary>
    internal class ItemView : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public ItemDrop.ItemData Item;
        public Inventory Inventory;
        public bool Rotated;
        public float Cell;

        public void OnBeginDrag(PointerEventData e)
        {
            StashUi.Instance?.BeginDrag(this, e);
        }

        public void OnDrag(PointerEventData e)
        {
            StashUi.Instance?.Drag(e);
        }

        public void OnEndDrag(PointerEventData e)
        {
            StashUi.Instance?.EndDrag(e);
        }

        public void OnPointerClick(PointerEventData e)
        {
            StashUi.Instance?.Click(this, e);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            StashUi.Instance?.ShowTooltip(this, e.position);
        }

        public void OnPointerExit(PointerEventData e)
        {
            StashUi.Instance?.HideTooltip(this);
        }
    }

    /// <summary>A place an item can be dropped: a grid or an equipment slot.</summary>
    internal sealed class DropTarget
    {
        public RectTransform Rect;
        public Inventory Inventory;
        public string Grid;
        public TGrid Model;
        public SlotId Slot;
        public bool IsSlot;
        public float Cell;
        public int Quick = -1;

        // Scrolling grids: only the visible part counts.
        public RectTransform Clip;
    }
}
