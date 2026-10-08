using UnityEngine;

namespace MagicGame
{
    public enum AttackStyle { Melee, Ranged }

    // Chases the player and attacks. Melee enemies (goblin) hit at close range; ranged
    // enemies (skeleton, golem, dragon) keep their distance and throw projectiles.
    [RequireComponent(typeof(Rigidbody2D), typeof(Health), typeof(DirectionalSpriteAnimator))]
    public class EnemyController : MonoBehaviour
    {
        public string displayName = "Enemy";
        public bool isBoss;
        public AttackStyle style = AttackStyle.Melee;
        public float moveSpeed = 2f;
        public float detectRange = 9f;
        public float attackRange = 1f;
        [Tooltip("Keeps walking toward the player until this close (0 = stop at attack range).")]
        public float chaseDistance;
        [Tooltip("Ranged enemies back away when the player gets closer than this.")]
        public float preferredDistance;
        public float attackCooldown = 1f;
        [Tooltip("Seconds the enemy stands still before the attack lands.")]
        public float windUp = 0.25f;
        public float damage = 10f;
        public float castHeight = 0.6f;

        [Header("Ranged")]
        public Projectile projectilePrefab;
        public float projectileSpeed = 6f;
        public int projectilesPerShot = 1;
        public float spreadAngle;
        [Tooltip("Every Nth attack fires a full circle instead (0 = never).")]
        public int burstEvery;
        public int burstCount = 12;

        public Health Health { get; private set; }

        Rigidbody2D _rb;
        DirectionalSpriteAnimator _animator;
        Vector2 _velocity;
        float _nextAttack;
        float _windUpEnd = -1f;
        int _attackCount;
        bool _aggro;

        Vector2 Center => (Vector2)transform.position + Vector2.up * castHeight;

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _animator = GetComponent<DirectionalSpriteAnimator>();
            Health = GetComponent<Health>();
            Health.team = Team.Enemy;
            _nextAttack = Time.time + Random.Range(0.5f, 1.5f);
        }

        void OnEnable() => LevelManager.RegisterEnemy(this);
        void OnDisable() => LevelManager.UnregisterEnemy(this);

        void Update()
        {
            _velocity = Vector2.zero;
            var player = PlayerController.Instance;
            if (player == null || player.Health.IsDead || Health.IsDead)
            {
                _animator.SetMovement(Vector2.zero);
                return;
            }

            Vector2 toPlayer = (Vector2)player.transform.position - (Vector2)transform.position;
            float dist = toPlayer.magnitude;
            if (!_aggro && (dist <= detectRange || isBoss)) _aggro = true;
            if (!_aggro)
            {
                _animator.SetMovement(Vector2.zero);
                return;
            }

            if (_windUpEnd > 0f)
            {
                _animator.SetMovement(Vector2.zero);
                _animator.FaceTowards(toPlayer);
                if (Time.time >= _windUpEnd)
                {
                    _windUpEnd = -1f;
                    PerformAttack(player, dist);
                }
                return;
            }

            Vector2 dir = dist > 0.001f ? toPlayer / dist : Vector2.zero;
            float stopAt = chaseDistance > 0f ? chaseDistance : attackRange * 0.9f;
            if (dist > stopAt) _velocity = dir * moveSpeed;
            else if (style == AttackStyle.Ranged && dist < preferredDistance) _velocity = -dir * moveSpeed * 0.8f;

            _animator.SetMovement(_velocity);
            if (_velocity == Vector2.zero) _animator.FaceTowards(toPlayer);

            if (dist <= attackRange && Time.time >= _nextAttack)
            {
                _nextAttack = Time.time + attackCooldown;
                _windUpEnd = Time.time + windUp;
                _velocity = Vector2.zero;
            }
        }

        void FixedUpdate()
        {
            _rb.linearVelocity = _velocity;
        }

        void PerformAttack(PlayerController player, float dist)
        {
            _attackCount++;
            if (style == AttackStyle.Melee)
            {
                if (dist <= attackRange * 1.2f) player.Health.TakeDamage(damage);
                return;
            }

            if (projectilePrefab == null) return;
            Vector2 aim = player.Center - Center;
            if (burstEvery > 0 && _attackCount % burstEvery == 0)
            {
                for (int i = 0; i < burstCount; i++)
                    Fire(Quaternion.Euler(0f, 0f, 360f * i / burstCount) * aim);
                return;
            }

            int count = Mathf.Max(1, projectilesPerShot);
            for (int i = 0; i < count; i++)
            {
                float angle = count == 1 ? 0f : Mathf.Lerp(-spreadAngle / 2f, spreadAngle / 2f, i / (float)(count - 1));
                Fire(Quaternion.Euler(0f, 0f, angle) * aim);
            }
        }

        void Fire(Vector2 direction)
        {
            var p = Instantiate(projectilePrefab, Center, Quaternion.identity);
            p.Launch(direction, Team.Enemy, damage, projectileSpeed);
        }
    }
}
