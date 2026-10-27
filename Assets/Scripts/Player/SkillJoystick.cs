using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace MagicGame
{
    // This augments the authored Button. Pointer casts suppress only that release's onClick.
    [DisallowMultipleComponent]
    public class SkillJoystick : MonoBehaviour, IPointerDownHandler, IInitializePotentialDragHandler,
        IDragHandler, IPointerUpHandler
    {
        public SkillSlot slot;
        public RectTransform ring, knob, cancelArea;
        public SkillAimPreview preview;
        [SerializeField, Min(1)] float dragRadius = 84f;
        [SerializeField, Range(0, 0.9f)] float deadzone = 0.15f;

        public static SkillJoystick Current { get; private set; }
        public bool IsAiming => _pointer != int.MinValue;
        public bool IsCancelling { get; private set; }
        public bool SuppressClick => IsAiming || _ignoreClickFrame == Time.frameCount;
        public SkillCastRequest PreviewCast { get; private set; }

        RectTransform _rect;
        UnityEngine.UI.Button _button;
        UnityEngine.UI.Image _cancelImage;
        PlayerSkills _skills;
        SkillTargetingMode _mode;
        int _pointer = int.MinValue, _ignoreClickFrame = -1, _revision;
        Vector2 _origin, _value, _lastDirection, _knobOrigin;
        bool _manual;
        Camera _eventCamera;
        readonly HashSet<int> _suppressedPointers = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;

        void Awake()
        {
            _rect = (RectTransform)transform;
            _button = GetComponent<UnityEngine.UI.Button>();
        }

        public static void CancelCurrent()
        {
            if (Current != null) Current.Clear();
        }

        public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            if (IsAiming)
            {
                if (e.pointerId != _pointer) _suppressedPointers.Add(e.pointerId);
                return;
            }
            _suppressedPointers.Remove(e.pointerId);
            // An overlapping second pointer must not fall through to a regular button click.
            if (Current != null) { _suppressedPointers.Add(e.pointerId); return; }
            var player = PlayerController.Instance;
            if (player == null || GameUI.GameplayBlocked || !_button.IsInteractable())
            {
                _suppressedPointers.Add(e.pointerId);
                return;
            }
            _skills = player.Skills;
            _mode = _skills.GetTargetingMode(slot);
            if (_mode == SkillTargetingMode.None) return;
            _suppressedPointers.Add(e.pointerId);
            if (!_skills.CanTarget(slot)) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, e.position, e.pressEventCamera, out _origin)) return;
            _eventCamera = e.pressEventCamera;
            _pointer = e.pointerId;
            _revision = _skills.TargetingRevision;
            _manual = IsCancelling = false;
            _value = Vector2.zero;
            _lastDirection = Vector2.down;
            Current = this;
            _cancelImage = cancelArea.GetComponent<UnityEngine.UI.Image>();
            ring.anchoredPosition = _origin - _rect.rect.center;
            ring.gameObject.SetActive(true);
            ClampRingToSafeArea();
            cancelArea.gameObject.SetActive(true);
            preview.gameObject.SetActive(true);
            RefreshPreview();
        }

        void ClampRingToSafeArea()
        {
            _knobOrigin = Vector2.zero;
            if (GameUI.Instance == null) return;
            var safe = GameUI.Instance.transform.Find("SafeArea") as RectTransform;
            if (safe == null) return;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(safe, ring);
            Vector2 shift = new Vector2(
                Mathf.Max(0, safe.rect.xMin - bounds.min.x) - Mathf.Max(0, bounds.max.x - safe.rect.xMax),
                Mathf.Max(0, safe.rect.yMin - bounds.min.y) - Mathf.Max(0, bounds.max.y - safe.rect.yMax));
            Vector3 worldShift = safe.TransformVector(shift);
            ring.position += worldShift;
            // Keep the input origin at the actual press, even when the visual ring is inset.
            _knobOrigin = ring.InverseTransformVector(-worldShift);
            knob.anchoredPosition = Vector2.ClampMagnitude(_knobOrigin, dragRadius);
        }

        bool IsValid => _skills != null && _skills.TargetingRevision == _revision &&
            _skills.GetTargetingMode(slot) == _mode && _skills.CanTarget(slot) && _button.IsInteractable();

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != _pointer) return;
            if (!IsValid) { Clear(); return; }
            Sample(e.position);
        }

        void Sample(Vector2 screenPosition)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, screenPosition, _eventCamera, out var local)) return;
            Vector2 raw = Vector2.ClampMagnitude((local - _origin) / dragRadius, 1f);
            float magnitude = raw.magnitude;
            if (magnitude > deadzone)
            {
                _manual = true;
                _lastDirection = raw.normalized;
            }
            _value = magnitude > deadzone ? raw.normalized * ((magnitude - deadzone) / (1f - deadzone)) : Vector2.zero;
            knob.anchoredPosition = Vector2.ClampMagnitude(_knobOrigin + raw * dragRadius, dragRadius);
            IsCancelling = RectTransformUtility.RectangleContainsScreenPoint(cancelArea, screenPosition, _eventCamera);
            RefreshPreview();
        }

        void RefreshPreview()
        {
            Vector2 value = _mode == SkillTargetingMode.Ground || _value.sqrMagnitude > 0.0001f ? _value : _lastDirection;
            PreviewCast = _skills.ResolveSkillCast(slot, value, _manual);
            preview.Show(PreviewCast, _mode, IsCancelling);
            _cancelImage.color = IsCancelling
                ? new Color(0.9f, 0.2f, 0.25f, 0.95f) : new Color(0.25f, 0.08f, 0.12f, 0.9f);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (_suppressedPointers.Remove(e.pointerId)) _ignoreClickFrame = Time.frameCount;
            if (e.pointerId != _pointer) return;
            if (IsValid && !WasTouchCancelled(e))
            {
                Sample(e.position);
                if (!IsCancelling) GameplayInput.RequestSkillCast(PreviewCast);
            }
            Clear();
        }

        static bool WasTouchCancelled(PointerEventData e)
        {
            if (!(e is ExtendedPointerEventData pointer) || !(pointer.device is Touchscreen screen)) return false;
            foreach (var touch in screen.touches)
                if (touch.touchId.ReadValue() == pointer.touchId)
                    return touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled;
            return false;
        }

        void Update()
        {
            if (!IsAiming) return;
            if (!IsValid) Clear();
            else RefreshPreview(); // Follow a moving caster and the nearest quick-cast target.
        }

        public void Clear()
        {
            if (!IsAiming) return;
            _ignoreClickFrame = Time.frameCount;
            _pointer = int.MinValue;
            IsCancelling = false;
            if (ring != null) ring.gameObject.SetActive(false);
            if (knob != null) knob.anchoredPosition = Vector2.zero;
            if (Current == this)
            {
                Current = null;
                if (cancelArea != null) cancelArea.gameObject.SetActive(false);
                if (preview != null) preview.gameObject.SetActive(false);
            }
        }

        void OnDisable() => Clear();
    }
}
