using UnityEngine;

namespace MagicGame
{
    public class ArcaneButton : UnityEngine.UI.Button
    {
        CanvasGroup _visuals;
        UnityEngine.UI.Image _surface;

        protected override void Awake()
        {
            _visuals = gameObject.GetComponent<CanvasGroup>();
            if (_visuals == null) _visuals = gameObject.AddComponent<CanvasGroup>();
            _surface = GetComponent<UnityEngine.UI.Image>();
            base.Awake();
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            if (_visuals != null) _visuals.alpha = state == SelectionState.Disabled ? .45f : 1;
            if (_surface != null)
                _surface.color = state == SelectionState.Pressed ? new Color(.6f, .72f, .82f) : Color.white;
        }
    }
}
