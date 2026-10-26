using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace MagicGame
{
    // Touch and desktop feed the same player and skill methods. Touch pointers never become mouse attacks.
    public static class GameplayInput
    {
        public static Vector2 Move { get; set; }
        public static Vector2 Aim { get; private set; } = Vector2.down;
        public static bool Firing { get; private set; }
        public static bool HasTouchAim { get; private set; }
        static bool _active, _ultimate, _stance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            Move = Vector2.zero;
            Aim = Vector2.down;
            Firing = HasTouchAim = _active = _ultimate = _stance = false;
        }

        public static void SetAim(Vector2 value)
        {
            Firing = value.sqrMagnitude > 0.04f;
            if (!Firing) return;
            Aim = Vector2.ClampMagnitude(value, 1f);
            HasTouchAim = true;
        }

        public static void ReleaseAim() => Firing = false;
        public static void RequestActive() => _active = true;
        public static void RequestUltimate() => _ultimate = true;
        public static void RequestStance() => _stance = true;

        public static Vector2 ReadMove()
        {
            var k = Keyboard.current;
            var value = Move;
            if (k != null)
            {
                value.x += (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1f : 0f) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1f : 0f);
                value.y += (k.wKey.isPressed || k.upArrowKey.isPressed ? 1f : 0f) - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1f : 0f);
            }
            return Vector2.ClampMagnitude(value, 1f);
        }

        public static void ReadActions(PlayerController player, Vector2 facing, out Vector2 aim, out Vector2 target,
            out bool fire, out bool active, out bool ultimate, out bool stance)
        {
            var k = Keyboard.current;
            aim = HasTouchAim ? Aim.normalized : facing;
            float range = player.Skills.Config != null ? player.Skills.Config.range : 4f;
            target = Target(player.transform.position, facing, range);
            fire = Firing;
            if (k != null)
            {
                fire |= k.spaceKey.isPressed || k.jKey.isPressed;
                _active |= k.qKey.wasPressedThisFrame;
                _ultimate |= k.eKey.wasPressedThisFrame;
                _stance |= k.tabKey.wasPressedThisFrame;
            }
            var mouse = Mouse.current;
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (!Application.isMobilePlatform && mouse != null && !Firing && !overUI && mouse.delta.ReadValue().sqrMagnitude > 0.01f)
                HasTouchAim = false;
            if (!Application.isMobilePlatform && mouse != null && Camera.main != null && !HasTouchAim &&
                !overUI)
            {
                target = Camera.main.ScreenToWorldPoint(mouse.position.ReadValue());
                aim = target - player.Center;
                if (aim.sqrMagnitude < 0.001f) aim = facing;
                fire |= mouse.leftButton.isPressed;
            }
            active = _active; ultimate = _ultimate; stance = _stance;
            _active = _ultimate = _stance = false;
        }

        public static Vector2 Target(Vector2 position, Vector2 facing, float range)
        {
            var offset = HasTouchAim ? Aim * range : facing.normalized * range;
            return CombatWorld.GroundTarget(position, position + offset);
        }
    }
}
