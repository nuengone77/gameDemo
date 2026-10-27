using UnityEngine;

namespace MagicGame
{
    // A reticle (cut from Art/UI/target_circle.png) that circles the hero and sits where the aim joystick
    // is pushed, so the attack direction is readable before the shot lands.
    public class AimArrow : MonoBehaviour
    {
        const float Radius = 1.05f, Pulse = 0.06f, Size = 0.6f, FadeSpeed = 12f;

        PlayerController _player;
        SpriteRenderer _renderer;
        float _alpha;

        public void Initialize(PlayerController player)
        {
            _player = player;
            var sprite = Resources.Load<Sprite>("UI/AimReticle");
            if (sprite == null) return;
            var go = new GameObject("AimReticle");
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * Size / Mathf.Max(0.01f, sprite.bounds.size.x);
            _renderer = go.AddComponent<SpriteRenderer>();
            _renderer.sprite = sprite;
            _renderer.sortingOrder = 24500;
            _renderer.color = new Color(1f, 1f, 1f, 0f);
        }

        void LateUpdate()
        {
            // Paused: keep the reticle where it is.
            if (_renderer == null || GameUI.GameplayBlocked) return;
            bool show = GameplayInput.Firing && SkillJoystick.Current == null && _player.Skills != null && _player.Skills.CanAct;
            _alpha = Mathf.MoveTowards(_alpha, show ? 1f : 0f, FadeSpeed * Time.deltaTime);
            _renderer.enabled = _alpha > 0f;
            if (!_renderer.enabled) return;
            if (show)
            {
                Vector2 aim = GameplayInput.Aim.sqrMagnitude > 0.0001f ? GameplayInput.Aim.normalized : Vector2.down;
                float radius = Radius + Mathf.Sin(Time.time * 6f) * Pulse;
                _renderer.transform.position = _player.Center + aim * radius;
            }
            _renderer.color = new Color(1f, 1f, 1f, _alpha);
        }
    }
}
