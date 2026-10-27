using UnityEngine;

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
        public PlayerSkills Skills { get; private set; }
        public PlayerProgression ProgressionBonuses { get; private set; }
        public CharacterAnimSet CurrentCharacter { get; private set; }
        public bool CanMove => !GameUI.GameplayBlocked && isActiveAndEnabled && Health != null && !Health.IsDead &&
            (LevelManager.Instance == null || LevelManager.Instance.AllowsMovement);
        public Vector2 Center => (Vector2)transform.position + Vector2.up * castHeight;
        public Vector2 Facing => _animator.FacingVector;

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
            Health.Died += _ => _animator.PlayDeath();

            if (characters != null && characters.Length > 0)
            {
                int index = Mathf.Clamp(GameSession.SelectedCharacter, 0, characters.Length - 1);
                if (!string.IsNullOrEmpty(GameSession.SelectedHeroId))
                {
                    int savedIndex = System.Array.FindIndex(characters, hero => hero != null && hero.displayName == GameSession.SelectedHeroId);
                    if (savedIndex >= 0) index = savedIndex;
                }
                CurrentCharacter = characters[index];
                _animator.SetAnimSet(CurrentCharacter);
            }
            var config = CurrentCharacter != null ? CurrentCharacter.combat : null;
            if (config != null)
            {
                Health.maxHp = Mathf.Max(1f, config.maxHp);
                Health.Heal(Health.maxHp);
                moveSpeed = Mathf.Max(0.1f, config.moveSpeed);
            }
            Skills = GetComponent<PlayerSkills>();
            if (Skills == null) Skills = gameObject.AddComponent<PlayerSkills>();
            gameObject.AddComponent<AimArrow>().Initialize(this);
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (config != null && scene.StartsWith("Level") && int.TryParse(scene.Substring(5), out int stage) && stage >= 1 && stage <= 7)
            {
                Progression.EnsureStage(CurrentCharacter.displayName, stage);
                ProgressionBonuses = gameObject.AddComponent<PlayerProgression>();
                ProgressionBonuses.Initialize(this, config);
            }
            else Skills.Initialize(this, config);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (!CanMove)
            {
                GameplayInput.ClearSkillRequests();
                _input = Vector2.zero;
                _animator.SetMovement(Vector2.zero);
                return;
            }

            _input = GameplayInput.ReadMove();
            _animator.SetMovement(_input * Health.Status.MoveMultiplier);
            if (!Skills.CanAct || Health.Status.Interrupted)
            {
                GameplayInput.ClearSkillRequests();
                return;
            }

            GameplayInput.ReadActions(this, _animator.FacingVector, out var aim, out var target,
                out bool firing, out bool active, out bool ultimate, out bool stance);
            if (stance)
            {
                SkillJoystick.CancelCurrent();
                GameplayInput.ClearSkillRequests();
            }
            if (GameplayInput.TryConsumeSkillCast(SkillSlot.Active, out var activeCast))
            {
                if (Skills.IsCurrentCast(activeCast)) CastActive(activeCast.direction, activeCast.target);
            }
            else if (active) CastActive(aim, target);
            if (GameplayInput.TryConsumeSkillCast(SkillSlot.Ultimate, out var ultimateCast))
            {
                if (Skills.IsCurrentCast(ultimateCast)) CastUltimate(ultimateCast.target);
            }
            else if (ultimate) CastUltimate(target);
            if (stance) Skills.TrySwitchStance();
            if (Health.Status.Interrupted) return;
            if (Time.time < _nextAttack) return;

            if (firing)
            {
                _animator.FaceTowards(aim);
                Cast(aim);
            }
        }

        void FixedUpdate()
        {
            _rb.linearVelocity = CanMove ? Health.Status.ResolveVelocity(_rb, _input * moveSpeed) : Vector2.zero;
        }

        string HeroName => CurrentCharacter != null ? CurrentCharacter.name : "";

        void CastActive(Vector2 direction, Vector2 target)
        {
            if (!Skills.TryActive(direction, target)) return;
            PlayCastPose(direction);
            GameAudio.PlayHero(HeroName, h => h.active);
        }

        void CastUltimate(Vector2 target)
        {
            if (!Skills.TryUltimate(target)) return;
            PlayCastPose(target - (Vector2)transform.position);
            GameAudio.PlayHero(HeroName, h => h.ultimate);
            GameAudio.DuckMusic(1.5f);
        }

        void PlayCastPose(Vector2 direction)
        {
            _animator.FaceTowards(direction);
            _animator.PlayAttack();
        }

        void Cast(Vector2 direction)
        {
            _animator.FaceTowards(direction);
            _animator.PlayAttack();
            _nextAttack = Time.time + Skills.AttackInterval;
            Skills.BasicAttack(direction);
            GameAudio.PlayHero(HeroName, h => h.basic);
        }
    }
}
