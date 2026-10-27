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

        [Header("Flying (dragon stage)")]
        [Tooltip("Hover inside the sky band instead of walking: drifts above the player and ignores walls.")]
        public bool flyInSky;
        public float skyMinY = 1f;
        public float skyMaxY = 3f;
        public float skyMinX = -10f;
        public float skyMaxX = 10f;
        [Tooltip("How far left/right of the player the flyer sways.")]
        public float swayDistance = 3f;

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
            // Kinematic bodies are not pushed by walls or the player, so the flyer stays in the sky.
            if (flyInSky) _rb.bodyType = RigidbodyType2D.Kinematic;
            _nextAttack = Time.time + Random.Range(0.5f, 1.5f);
        }

        void OnEnable() => LevelManager.RegisterEnemy(this);
        void OnDisable() => LevelManager.UnregisterEnemy(this);

        void Update()
        {
            if (GameUI.GameplayBlocked) return;
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

            if (Health.Status.Interrupted)
            {
                _windUpEnd = -1f;
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

            if (flyInSky) _velocity = FlyVelocity(player);
            else _velocity = ChaseVelocity(toPlayer, dist);

            _animator.SetMovement(_velocity * Health.Status.MoveMultiplier);
            if (_velocity == Vector2.zero) _animator.FaceTowards(toPlayer);

            if (dist <= attackRange && Time.time >= _nextAttack)
            {
                _nextAttack = Time.time + attackCooldown;
                _windUpEnd = Time.time + windUp;
                _velocity = Vector2.zero;
            }
        }

        Vector2 ChaseVelocity(Vector2 toPlayer, float dist)
        {
            Vector2 dir = dist > 0.001f ? toPlayer / dist : Vector2.zero;
            float stopAt = chaseDistance > 0f ? chaseDistance : attackRange * 0.9f;
            if (dist > stopAt) return dir * moveSpeed;
            if (style == AttackStyle.Ranged && dist < preferredDistance) return -dir * moveSpeed * 0.8f;
            return Vector2.zero;
        }

        // Sways left/right around the player's x and bobs up and down within the sky band.
        Vector2 FlyVelocity(PlayerController player)
        {
            float t = Time.time;
            var target = new Vector2(
                player.transform.position.x + Mathf.Sin(t * 0.4f) * swayDistance,
                Mathf.Lerp(skyMinY, skyMaxY, 0.5f + 0.5f * Mathf.Sin(t * 0.9f)));
            Vector2 delta = target - (Vector2)transform.position;
            return delta.magnitude > 0.05f ? Vector2.ClampMagnitude(delta * 2f, moveSpeed) : Vector2.zero;
        }

        void FixedUpdate()
        {
            Vector2 velocity = Health.IsDead || GameUI.GameplayBlocked
                ? Vector2.zero : Health.Status.ResolveVelocity(_rb, _velocity);
            if (flyInSky)
            {
                var next = _rb.position + velocity * Time.fixedDeltaTime;
                next.x = Mathf.Clamp(next.x, skyMinX, skyMaxX);
                next.y = Mathf.Clamp(next.y, skyMinY, skyMaxY);
                _rb.MovePosition(next);
            }
            else _rb.linearVelocity = velocity;
        }

        // Picked by name so the bank stays readable; unknown ranged enemies fall back to the arrow.
        void PlayAttackSound()
        {
            if (style == AttackStyle.Melee) { GameAudio.Play(b => b.enemyMelee); return; }
            string n = displayName.ToLowerInvariant();
            if (n.Contains("golem")) GameAudio.Play(b => b.golemRock);
            else if (n.Contains("dragon")) GameAudio.Play(b => b.dragonFire);
            else if (n.Contains("witch")) GameAudio.Play(b => b.witchCast);
            else GameAudio.Play(b => b.skeletonArrow);
        }

        void PerformAttack(PlayerController player, float dist)
        {
            _attackCount++;
            PlayAttackSound();
            if (style == AttackStyle.Melee)
            {
                if (dist <= attackRange * 1.2f) player.Health.TakeDamage(new DamageInfo(damage * Health.Status.AttackMultiplier, Health, DamageType.Physical));
                return;
            }

            if (projectilePrefab == null) return;
            // Feet can fit beside a boundary while the elevated cast point lies outside it.
            // Leave room for the projectile collider, then aim from the corrected origin.
            Vector2 origin = flyInSky ? CombatWorld.ClampAttackPoint(Center, ProjectileClearance())
                : CombatWorld.GroundTarget(transform.position, Center, ProjectileClearance());
            Vector2 aim = player.Center - origin;
            if (burstEvery > 0 && _attackCount % burstEvery == 0)
            {
                for (int i = 0; i < burstCount; i++)
                    Fire(origin, Quaternion.Euler(0f, 0f, 360f * i / burstCount) * aim);
                return;
            }

            int count = Mathf.Max(1, projectilesPerShot);
            for (int i = 0; i < count; i++)
            {
                float angle = count == 1 ? 0f : Mathf.Lerp(-spreadAngle / 2f, spreadAngle / 2f, i / (float)(count - 1));
                Fire(origin, Quaternion.Euler(0f, 0f, angle) * aim);
            }
        }

        float ProjectileClearance()
        {
            Vector2 scale = projectilePrefab.transform.lossyScale;
            var collider = projectilePrefab.GetComponent<Collider2D>();
            float radius = 0.1f;
            if (collider is CircleCollider2D circle)
                radius = circle.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            else if (collider is BoxCollider2D box)
                radius = Vector2.Scale(box.size * 0.5f, scale).magnitude;
            return radius + 0.03f;
        }

        void Fire(Vector2 origin, Vector2 direction)
        {
            var p = Instantiate(projectilePrefab, origin, Quaternion.identity);
            p.Launch(direction, Team.Enemy, new DamageInfo(damage * Health.Status.AttackMultiplier, Health,
                displayName.Contains("Dragon") ? DamageType.Magic : DamageType.Physical), projectileSpeed);
        }
    }
}
