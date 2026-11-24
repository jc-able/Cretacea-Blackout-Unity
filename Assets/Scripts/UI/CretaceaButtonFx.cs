using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Cretacea.UI
{
    /// <summary>
    /// Visible hover / focus / press feedback for factory buttons: the warm border turns amber (2 px when focused
    /// by keyboard or gamepad, 1.5 px on mouse hover), the fill warms up, the label brightens and the button grows
    /// 3 %. Works with unscaled time so it animates in the pause menu.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class CretaceaButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler, IPointerDownHandler, IPointerUpHandler
    {
        public CretaceaPanelGraphic panel;
        public TextMeshProUGUI label;
        public Image accentBar;
        public Color baseColor;
        public Color hoverColor;
        public Color pressedColor;
        public Color baseBorder;
        public Color labelColor;
        public Color labelHotColor = Color.white;
        public bool listStyle;            // main-menu list rows: no box until hovered / focused
        public bool noScale;              // tabs and row-aligned controls: never grow (the fill and border mark them)

        private bool _hover, _selected, _pressed;
        private float _t;                 // 0 = idle, 1 = hot
        private Selectable _selectable;

        public void SetBaseColor(Color c)
        {
            baseColor = c;
            Apply(true);
        }

        private void Awake()
        {
            _selectable = GetComponent<Selectable>();
        }

        private void OnEnable()
        {
            _hover = _pressed = false;
            _selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            _t = _selected ? 1f : 0f;
            Apply(true);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            _hover = true;
            if (_selectable == null || _selectable.interactable)
            {
                Cretacea.Core.GameEvents.RaisePlaySound("ui_hover", null, 0.35f);
                // Hover moves the keyboard / gamepad focus here, so only one control is ever hot.
                if (_selectable != null && EventSystem.current != null && EventSystem.current.currentSelectedGameObject != gameObject)
                    EventSystem.current.SetSelectedGameObject(gameObject);
            }
        }
        public void OnPointerExit(PointerEventData e) { _hover = false; _pressed = false; }
        public void OnSelect(BaseEventData e) { _selected = true; }
        public void OnDeselect(BaseEventData e) { _selected = false; }
        public void OnPointerDown(PointerEventData e) { _pressed = true; }
        public void OnPointerUp(PointerEventData e) { _pressed = false; }

        private void Update()
        {
            float target = (_hover || _selected) ? 1f : 0f;
            if (!Mathf.Approximately(_t, target))
            {
                _t = Mathf.MoveTowards(_t, target, Time.unscaledDeltaTime / 0.09f);
                Apply(false);
            }
            else if (_pressed) Apply(false);
        }

        private void Apply(bool force)
        {
            bool interactable = _selectable == null || _selectable.interactable;
            float t = interactable ? _t : 0f;
            if (panel != null)
            {
                Color fill = Color.Lerp(baseColor, hoverColor, t);
                if (_pressed && interactable) fill = pressedColor;
                if (listStyle) fill.a = Mathf.Lerp(0f, hoverColor.a, t);
                panel.color = interactable ? fill : new Color(fill.r, fill.g, fill.b, fill.a * 0.5f);
                Color border = Color.Lerp(listStyle ? new Color(baseBorder.r, baseBorder.g, baseBorder.b, 0f) : baseBorder,
                    CretaceaUiFactory.AccentAmber, t);
                panel.SetBorder(border, _selected && interactable ? 2f : Mathf.Lerp(1f, 1.5f, t));
            }
            if (label != null)
            {
                Color lc = Color.Lerp(labelColor, labelHotColor, t);
                if (!interactable) lc.a *= 0.45f;
                label.color = lc;
            }
            if (accentBar != null)
            {
                Color ac = accentBar.color;
                ac.a = listStyle ? t : ac.a;
                accentBar.color = ac;
            }
            // Grow small centre-pivoted buttons a little. Wide rows (list entries, chapter rows), tabs and anything
            // pivoted off-centre stay put: scaling about an edge makes the whole row jump sideways.
            RectTransform rt = transform as RectTransform;
            bool wide = rt != null && rt.rect.width > 300f;
            bool centred = rt != null && Mathf.Abs(rt.pivot.x - 0.5f) < 0.01f && Mathf.Abs(rt.pivot.y - 0.5f) < 0.01f;
            transform.localScale = (wide || noScale || !centred) ? Vector3.one : Vector3.one * (1f + 0.03f * t);
        }
    }
}
