using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ARCHIV3D
{
    /// <summary>
    /// Makes a button feel alive: grows a little on hover, squishes when pressed,
    /// can brighten its colors, and can "pop" in every time it appears.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public float hoverScale = 1.04f;
        public float pressedScale = 0.94f;
        public float speed = 18f;

        [Header("Pop when it appears")]
        public bool popOnEnable = false;
        public float popDuration = 0.2f;
        [Range(0.3f, 1f)] public float popFrom = 0.7f;

        [Header("Optional color change")]
        public Graphic fill;
        public Color fillNormal = Color.white;
        public Color fillHover = Color.white;
        public Graphic border;
        public Color borderNormal = Color.white;
        public Color borderHover = Color.white;

        Vector3 _baseScale = Vector3.one;
        float _current = 1f;
        float _popTime = 999f;
        bool _hover, _pressed;
        Selectable _selectable;

        void Awake()
        {
            _baseScale = transform.localScale;
            _selectable = GetComponent<Selectable>();
            ApplyColors(1f, 0f);
        }

        void OnEnable()
        {
            _current = 1f;
            _popTime = popOnEnable ? 0f : 999f;
            transform.localScale = _baseScale * (popOnEnable ? popFrom : 1f);
        }

        void OnDisable()
        {
            _hover = _pressed = false;
            transform.localScale = _baseScale;
            ApplyColors(1f, 0f);
        }

        public void OnPointerEnter(PointerEventData e) { _hover = true; }
        public void OnPointerExit(PointerEventData e) { _hover = false; _pressed = false; }
        public void OnPointerDown(PointerEventData e) { _pressed = true; }
        public void OnPointerUp(PointerEventData e) { _pressed = false; }

        void Update()
        {
            bool active = _selectable == null || _selectable.IsInteractable();
            float target = 1f;
            if (active && _pressed) target = pressedScale;
            else if (active && _hover) target = hoverScale;

            float k = 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime);
            _current = Mathf.Lerp(_current, target, k);

            float pop = 1f;
            if (_popTime < popDuration)
            {
                _popTime += Time.unscaledDeltaTime;
                float x = Mathf.Clamp01(_popTime / popDuration);
                const float c1 = 1.70158f, c3 = c1 + 1f; // ease-out-back
                float e = 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
                pop = Mathf.LerpUnclamped(popFrom, 1f, e);
            }

            transform.localScale = _baseScale * (_current * pop);
            ApplyColors(k, active && (_hover || _pressed) ? 1f : 0f);
        }

        void ApplyColors(float k, float highlight)
        {
            if (fill != null) fill.color = Color.Lerp(fill.color, Color.Lerp(fillNormal, fillHover, highlight), k);
            if (border != null) border.color = Color.Lerp(border.color, Color.Lerp(borderNormal, borderHover, highlight), k);
        }
    }
}
