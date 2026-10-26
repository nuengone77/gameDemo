using System.Collections.Generic;
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
        [Tooltip("Off = play the frames once and stay on the last one (e.g. a blast that grows as it flies).")]
        public bool loopFrames = true;

        [Header("Optional combat behavior")]
        public float explosionRadius;
        public int bounces;
        public float bounceRadius = 3f;
        public bool bypassObstacles;
        public float burnPerSecond, burnDuration, rootDuration;
        [System.NonSerialized] public VfxSlot rootVfx, rootHitVfx, explosionVfx;
        public DamageInfo Hit { get; private set; }
        public Team OwnerTeam => _team;
        public Vector2 Velocity => _rb != null ? _rb.linearVelocity : Vector2.zero;
        readonly HashSet<Health> _hitIds = new();

        Team _team;
        float _damage;
        float _age;
        bool _spent;
        Vector3 _baseScale;
        Rigidbody2D _rb;
        SpriteRenderer _renderer;

        public void Launch(Vector2 direction, Team team, float damage, float speedOverride = -1f)
        {
            Launch(direction, team, new DamageInfo(damage), speedOverride);
        }

        public void Launch(Vector2 direction, Team team, DamageInfo hit, float speedOverride = -1f)
        {
            _rb = GetComponent<Rigidbody2D>();
            _team = team;
            _damage = hit.amount;
            Hit = hit;
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
            if (_age >= lifetime || !CombatWorld.ContainsAttackPoint(transform.position))
            {
                Destroy(gameObject);
                return;
            }

            if (frames != null && frames.Length > 1 && _renderer != null)
            {
                int frame = (int)(_age * frameRate);
                _renderer.sprite = frames[loopFrames ? frame % frames.Length : Mathf.Min(frame, frames.Length - 1)];
            }
            if (spinSpeed != 0f) transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
            if (!Mathf.Approximately(growTo, 1f))
                transform.localScale = _baseScale * Mathf.Lerp(1f, growTo, _age / lifetime);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (_spent) return;

            var shield = other.GetComponent<ShieldWall>();
            if (shield != null)
            {
                if (shield.CanBlock(this)) Spend();
                return;
            }

            var wall = other.GetComponent<Wall>();
            if (wall != null)
            {
                if (!wall.blocksProjectiles) return;
                if (!bypassObstacles || wall.isBoundary || !wall.allowMagicPassage) Spend();
                return;
            }

            var health = other.GetComponentInParent<Health>();
            if (health != null)
            {
                if (health.team == _team || health.IsDead) return;
                if (_hitIds.Contains(health)) return;
                if (explosionRadius > 0f)
                {
                    // Mark spent before callbacks, then damage each actor only once.
                    _spent = true;
                    ApplyHit(health);
                    foreach (var target in CombatWorld.Enemies(transform.position, explosionRadius)) ApplyHit(target);
                    if (explosionVfx != null && explosionVfx.IsSet) SkillVfx.Spawn(explosionVfx, transform.position);
                    else SkillVfx.Burst(transform.position, Color.magenta, explosionRadius);
                    Destroy(gameObject);
                    return;
                }
                ApplyHit(health);
                if (bounces > 0 && Bounce()) { bounces--; return; }
                if (!pierce) Spend();
                return;
            }
        }

        void ApplyHit(Health health)
        {
            if (!_hitIds.Add(health)) return;
            var hit = Hit;
            hit.amount = _damage;
            var result = health.TakeDamage(hit);
            if (result.Total <= 0f || health.IsDead) return;
            if (burnPerSecond > 0f) health.Status.Apply(StatusKind.Burn, burnPerSecond, burnDuration, hit.source, hit.source);
            if (rootDuration > 0f)
            {
                health.Status.Apply(StatusKind.Root, 1f, rootDuration, hit.source);
                if (rootVfx != null && rootVfx.IsSet) SkillVfx.Spawn(rootVfx, health.transform.position, rootDuration, health.transform, 4000);
                if (rootHitVfx != null && rootHitVfx.IsSet) SkillVfx.Spawn(rootHitVfx, health.transform.position + Vector3.up * 0.5f);
            }
        }

        bool Bounce()
        {
            Health next = null;
            float distance = bounceRadius;
            foreach (var health in CombatWorld.Enemies(transform.position, bounceRadius))
            {
                if (_hitIds.Contains(health)) continue;
                float candidate = Vector2.Distance(transform.position, health.transform.position);
                if (candidate < distance) { next = health; distance = candidate; }
            }
            if (next == null) return false;
            Vector2 direction = (Vector2)next.transform.position + Vector2.up * 0.4f - (Vector2)transform.position;
            _rb.linearVelocity = direction.normalized * speed;
            if (alignToDirection) transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + alignAngleOffset);
            return true;
        }

        public void Spend()
        {
            _spent = true;
            Destroy(gameObject);
        }
    }
}
