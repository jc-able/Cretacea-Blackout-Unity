using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

namespace Cretacea.UI
{
    /// <summary>Settings rows: a warm plate and an amber outline on the control while it has focus or the pointer.</summary>
    [AddComponentMenu("")]
    public sealed class RowFocusFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public CretaceaPanelGraphic plate;
        public CretaceaPanelGraphic handle;
        public TextMeshProUGUI label;
        private bool _hover, _selected;
        private Color _handleBorder;
        private bool _init;

        public void OnPointerEnter(PointerEventData e)
        {
            _hover = true;
            // Hover moves the focus here, so the row the pointer is on is the only hot row.
            UnityEngine.UI.Selectable sel = GetComponent<UnityEngine.UI.Selectable>();
            if (sel != null && sel.interactable && EventSystem.current != null && EventSystem.current.currentSelectedGameObject != gameObject)
                EventSystem.current.SetSelectedGameObject(gameObject);
            Apply();
        }
        public void OnPointerExit(PointerEventData e) { _hover = false; Apply(); }
        public void OnSelect(BaseEventData e) { _selected = true; Apply(); }
        public void OnDeselect(BaseEventData e) { _selected = false; Apply(); }

        private void OnEnable()
        {
            _hover = false;
            _selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            Apply();
        }

        private void Apply()
        {
            if (!_init && handle != null) { _handleBorder = handle.borderColor; _init = true; }
            bool hot = _hover || _selected;
            if (plate != null)
            {
                plate.color = new Color(0.96f, 0.69f, 0.28f, hot ? 0.07f : 0f);
                plate.SetBorder(new Color(0.96f, 0.69f, 0.28f, _selected ? 0.75f : (hot ? 0.3f : 0f)), _selected ? 1.5f : 1f);
            }
            if (handle != null) handle.SetBorder(hot ? CretaceaUiFactory.AccentAmber : _handleBorder, hot ? 2f : 1f);
            if (label != null) label.color = hot ? Color.white : CretaceaUiFactory.TextPrimary;
        }
    }
}
