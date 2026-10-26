using UnityEngine;
using UnityEngine.EventSystems;

namespace MagicGame
{
    public class TouchStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public bool aims;
        public RectTransform knob;
        int _pointer = int.MinValue;
        RectTransform _rect;
        public Vector2 Value { get; private set; }

        void Awake() => _rect = (RectTransform)transform;

        public void OnPointerDown(PointerEventData e)
        {
            if (_pointer != int.MinValue || GameUI.GameplayBlocked) return;
            _pointer = e.pointerId;
            OnDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != _pointer || GameUI.GameplayBlocked) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, e.position, e.pressEventCamera, out var local);
            local -= _rect.rect.center;
            Value = Vector2.ClampMagnitude(local / (_rect.rect.width * 0.36f), 1f);
            knob.anchoredPosition = Value * (_rect.rect.width * 0.27f);
            if (aims) GameplayInput.SetAim(Value);
            else GameplayInput.Move = Value.magnitude > 0.15f ? Value : Vector2.zero;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId == _pointer) Clear();
        }

        public void Clear()
        {
            _pointer = int.MinValue;
            Value = Vector2.zero;
            if (knob != null) knob.anchoredPosition = Vector2.zero;
            if (aims) GameplayInput.ReleaseAim(); else GameplayInput.Move = Vector2.zero;
        }

        void OnDisable() => Clear();
    }
}
