using UnityEngine;

namespace MagicGame
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class Projectile : MonoBehaviour
    {
        public float speed = 8f;
        public float lifetime = 2.5f;
        public float spinSpeed = 0f;
        public bool alignToDirection;
        [Tooltip("Extra rotation when aligning, e.g. -90 for art that points up instead of right.")]
        public float alignAngleOffset;
        public bool pierce;
        [Tooltip("Scale multiplier reached at the end of the lifetime (magic ring grows as it flies).")]
        public float growTo = 1f;

        [Header("Sprite animation (optional, loops)")]
        public Sprite[] frames;
        public float frameRate = 12f;

        Team _team;
        float _damage;
        float _age;
        bool _spent;
        Vector3 _baseScale;
        Rigidbody2D _rb;
        SpriteRenderer _renderer;

        public void Launch(Vector2 direction, Team team, float damage, float speedOverride = -1f)
        {
            _rb = GetComponent<Rigidbody2D>();
            _team = team;
            _damage = damage;
            if (speedOverride > 0f) speed = speedOverride;
            direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
            _rb.linearVelocity = direction * speed;
            if (alignToDirection)
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + alignAngleOffset);
        }

        void Awake()
        {
            _baseScale = transform.localScale;
            _renderer = GetComponent<SpriteRenderer>();
        }

        void Update()
        {
            _age += Time.deltaTime;
            if (_age >= lifetime)
            {
                Destroy(gameObject);
                return;
            }

            if (frames != null && frames.Length > 1 && _renderer != null)
                _renderer.sprite = frames[(int)(_age * frameRate) % frames.Length];
            if (spinSpeed != 0f) transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
            if (!Mathf.Approximately(growTo, 1f))
                transform.localScale = _baseScale * Mathf.Lerp(1f, growTo, _age / lifetime);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (_spent) return;

            var health = other.GetComponentInParent<Health>();
            if (health != null)
            {
                if (health.team == _team || health.IsDead) return;
                health.TakeDamage(_damage);
                if (!pierce)
                {
                    _spent = true;
                    Destroy(gameObject);
                }
                return;
            }

            if (other.GetComponent<Wall>() != null)
            {
                _spent = true;
                Destroy(gameObject);
            }
        }
    }
}
