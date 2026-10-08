using UnityEngine;
using UnityEngine.InputSystem;

namespace MagicGame
{
    // WASD / arrows to move. Space, J or left mouse to cast a magic ring
    // (keyboard casts in the facing direction, mouse casts toward the cursor).
    [RequireComponent(typeof(Rigidbody2D), typeof(Health), typeof(DirectionalSpriteAnimator))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        public CharacterAnimSet[] characters;
        public float moveSpeed = 4f;

        [Header("Magic ring")]
        public Projectile magicRingPrefab;
        public float ringDamage = 25f;
        public float attackCooldown = 0.35f;
        public float castHeight = 0.6f;

        public Health Health { get; private set; }
        public Vector2 Center => (Vector2)transform.position + Vector2.up * castHeight;

        Rigidbody2D _rb;
        DirectionalSpriteAnimator _animator;
        Vector2 _input;
        float _nextAttack;

        void Awake()
        {
            Instance = this;
            _rb = GetComponent<Rigidbody2D>();
            _animator = GetComponent<DirectionalSpriteAnimator>();
            Health = GetComponent<Health>();

            if (characters != null && characters.Length > 0)
            {
                int index = Mathf.Clamp(GameSession.SelectedCharacter, 0, characters.Length - 1);
                _animator.SetAnimSet(characters[index]);
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (Health.IsDead)
            {
                _input = Vector2.zero;
                _animator.SetMovement(Vector2.zero);
                return;
            }

            _input = ReadMove();
            _animator.SetMovement(_input);

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (Time.time < _nextAttack) return;

            if (mouse != null && mouse.leftButton.wasPressedThisFrame && Camera.main != null)
            {
                Vector2 target = Camera.main.ScreenToWorldPoint(mouse.position.ReadValue());
                Vector2 dir = target - Center;
                _animator.FaceTowards(dir);
                Cast(dir);
            }
            else if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.jKey.wasPressedThisFrame))
            {
                Cast(_animator.FacingVector);
            }
        }

        void FixedUpdate()
        {
            _rb.linearVelocity = _input * moveSpeed;
        }

        static Vector2 ReadMove()
        {
            var k = Keyboard.current;
            if (k == null) return Vector2.zero;
            float x = 0f, y = 0f;
            if (k.aKey.isPressed || k.leftArrowKey.isPressed) x -= 1f;
            if (k.dKey.isPressed || k.rightArrowKey.isPressed) x += 1f;
            if (k.sKey.isPressed || k.downArrowKey.isPressed) y -= 1f;
            if (k.wKey.isPressed || k.upArrowKey.isPressed) y += 1f;
            var v = new Vector2(x, y);
            return v.sqrMagnitude > 1f ? v.normalized : v;
        }

        void Cast(Vector2 direction)
        {
            var prefab = _animator.animSet != null && _animator.animSet.attackProjectile != null
                ? _animator.animSet.attackProjectile
                : magicRingPrefab;
            if (prefab == null) return;
            _nextAttack = Time.time + attackCooldown;
            var ring = Instantiate(prefab, Center + direction.normalized * 0.4f, Quaternion.identity);
            ring.Launch(direction, Team.Player, ringDamage);
        }
    }
}
