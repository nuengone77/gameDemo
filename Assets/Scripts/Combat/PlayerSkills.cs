using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MagicGame
{
    // Adds only the missing abilities; movement, input, health and projectiles stay in their existing owners.
    public class PlayerSkills : MonoBehaviour
    {
        public CharacterCombatConfig Config { get; private set; }
        public Health Health { get; private set; }
        public bool IsLunar { get; private set; }
        public int HitStacks { get; private set; }
        public float ActiveRemaining => Mathf.Max(0f, _activeReady - Time.time);
        public float UltimateRemaining => Mathf.Max(0f, _ultimateReady - Time.time);
        public float StanceRemaining => Mathf.Max(0f, _stanceReady - Time.time);
        public bool CanAct => _player != null && isActiveAndEnabled && Health != null && !Health.IsDead &&
            !GameUI.GameplayBlocked &&
            (LevelManager.Instance == null || LevelManager.Instance.IsPlaying);
        public float AttackInterval => Config == null ? _player.attackCooldown : Mathf.Max(0.05f,
            Config.basicInterval / Mathf.Min(5f, Health.Status.AttackSpeedMultiplier + HitStacks * Config.speedPerStack +
                (_player.ProgressionBonuses != null ? _player.ProgressionBonuses.AttackSpeedBonus : 0)));

        PlayerController _player;
        Rigidbody2D _body;
        float _activeReady, _ultimateReady, _stanceReady, _lastHit, _overdriveUntil;
        public event System.Action Changed;
        public float ActiveReadyAt => _activeReady;
        public float UltimateReadyAt => _ultimateReady;
        public float StanceReadyAt => _stanceReady;
        readonly List<GameObject> _owned = new();
        readonly List<GameObject> _shields = new();
        ShieldWall _wall;

        public void Initialize(PlayerController player, CharacterCombatConfig config)
        {
            Cleanup();
            _player = player;
            _body = GetComponent<Rigidbody2D>();
            Health = GetComponent<Health>();
            Config = config;
            IsLunar = false;
            HitStacks = 0;
            _activeReady = _ultimateReady = _stanceReady = _overdriveUntil = 0f;
            if (Health == null) return;
            Health.ClearCombatBonuses();
            Health.DamageDealt -= OnDamageDealt;
            Health.DamageDealt += OnDamageDealt;
            if (Config == null || Health.IsDead) return;
            if (Config.role == HeroRole.Tanky)
            {
                Health.DistanceReduction = Config.distanceReduction;
                Health.DefenseDistance = Config.defenseDistance;
                Health.ShieldCapacity = Config.shieldCapacity;
                for (int i = 0; i < 4; i++)
                {
                    var visual = SkillVfx.Ring(transform.position, 0.14f, Color.cyan, 0f);
                    _shields.Add(visual);
                    _owned.Add(visual);
                }
            }
            if (Config.role == HeroRole.Magician) Health.Evasion = Config.evasion;
        }

        void Update()
        {
            // Pausing must preserve owned effects and pending cast coroutines.
            if (GameUI.GameplayBlocked) return;
            if (!CanAct)
            {
                if (_owned.Count > 0) Cleanup();
                if (HitStacks > 0)
                {
                    HitStacks = 0;
                    Changed?.Invoke();
                }
                return;
            }
            _owned.RemoveAll(item => item == null);
            if (Config != null && Config.role == HeroRole.Shadow && HitStacks > 0 && Time.time - _lastHit > Config.stackTimeout)
            {
                HitStacks = 0;
                Changed?.Invoke();
            }
            for (int i = 0; i < _shields.Count; i++)
            {
                if (_shields[i] == null) continue;
                Vector2 point;
                if (_wall != null)
                {
                    Vector2 tangent = new Vector2(-_wall.forward.y, _wall.forward.x);
                    point = (Vector2)_wall.transform.position + tangent * Config.wallWidth * ((i + 0.5f) / 4f - 0.5f);
                }
                else
                {
                    float angle = Time.time * 1.5f + i * Mathf.PI * 0.5f;
                    point = _player.Center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.65f;
                }
                _shields[i].transform.position = point;
            }
        }

        void OnDamageDealt(Health target, DamageInfo hit, DamageResult result)
        {
            if (Config == null || !CanAct || result.Total <= 0f) return;
            if (Config.role == HeroRole.Tanky) Health.AddShield(result.Total * Config.conversion);
            if (Config.role == HeroRole.Shadow && hit.kunai)
            {
                HitStacks = Mathf.Min(Config.maxStacks, HitStacks + 1);
                _lastHit = Time.time;
                Changed?.Invoke();
            }
        }

        public DamageInfo MakeHit(float damage, bool guaranteed = false)
        {
            var hit = new DamageInfo(damage * Health.Status.AttackMultiplier, Health) { guaranteed = guaranteed };
            if (Config == null) return hit;
            hit.kunai = Config.role == HeroRole.Shadow;
            hit.penetration = hit.kunai ? Config.armorPenetration : 0f;
            hit.bypassDefense = Config.role == HeroRole.OracleWitch;
            return hit;
        }

        public void BasicAttack(Vector2 direction)
        {
            if (!CanAct) return;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.down;
            int count = Config != null && Config.role == HeroRole.Shadow && Time.time < _overdriveUntil ? Mathf.Max(1, Config.fanCount) : 1;
            for (int i = 0; i < count; i++)
            {
                float angle = count == 1 ? 0f : Mathf.Lerp(-Config.fanAngle * 0.5f, Config.fanAngle * 0.5f, i / (float)(count - 1));
                Fire(Quaternion.Euler(0f, 0f, angle) * direction, false);
            }
        }

        Projectile Fire(Vector2 direction, bool lunarBind)
        {
            var anim = _player.CurrentCharacter;
            var prefab = anim != null && anim.attackProjectile != null ? anim.attackProjectile : _player.magicRingPrefab;
            Vector2 origin = CombatWorld.ClampAttackPoint(_player.Center + direction.normalized * 0.4f);
            Projectile projectile;
            if (prefab != null) projectile = Instantiate(prefab, origin, Quaternion.identity);
            else
            {
                var go = new GameObject("PlaceholderProjectile");
                go.transform.position = origin;
                go.AddComponent<Rigidbody2D>().gravityScale = 0f;
                go.AddComponent<CircleCollider2D>().isTrigger = true;
                go.GetComponent<CircleCollider2D>().radius = 0.15f;
                projectile = go.AddComponent<Projectile>();
                var visual = SkillVfx.Ring(origin, 0.15f, Color.white, 0f);
                visual.transform.SetParent(go.transform, true);
            }
            _owned.Add(projectile.gameObject);
            float damage = Config != null ? Config.basicDamage : _player.ringDamage;
            if (Config != null)
            {
                switch (Config.role)
                {
                    case HeroRole.Shadow:
                        SkillVfx.AddTrail(projectile.gameObject, new Color(0.2f, 0.1f, 0.3f));
                        break;
                    case HeroRole.Magician:
                        projectile.bounces = Config.bounces;
                        projectile.bounceRadius = Config.bounceRadius;
                        break;
                    case HeroRole.OracleWitch:
                        projectile.speed = Config.orbSpeed;
                        projectile.lifetime = Config.orbLifetime;
                        projectile.explosionRadius = Config.radius;
                        projectile.bypassObstacles = true;
                        break;
                    case HeroRole.Daphne:
                        if (IsLunar)
                        {
                            damage = lunarBind ? Config.activeDamage : Config.lunarDamage;
                            projectile.rootDuration = lunarBind ? Config.rootDuration : 0f;
                        }
                        else
                        {
                            projectile.explosionRadius = Config.radius;
                            projectile.burnPerSecond = Config.burnPerSecond;
                            projectile.burnDuration = Config.burnDuration;
                        }
                        var renderer = projectile.GetComponent<SpriteRenderer>();
                        if (renderer != null) renderer.color = IsLunar ? new Color(0.7f, 0.3f, 1f) : new Color(1f, 0.5f, 0.1f);
                        SkillVfx.Line(origin, origin + direction.normalized * 0.8f, IsLunar ? Color.magenta : new Color(1f, 0.5f, 0f));
                        break;
                }
            }
            projectile.Launch(direction, Health.team, MakeHit(damage));
            return projectile;
        }

        public bool TryActive(Vector2 direction, Vector2 target)
        {
            if (!CanAct || Config == null || ActiveRemaining > 0f || Health.Status.Interrupted) return false;
            direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.down;
            _activeReady = Time.time + Mathf.Max(0f, Config.activeCooldown);
            switch (Config.role)
            {
                case HeroRole.Tanky:
                    var wall = new GameObject("AstralShieldWall");
                    wall.transform.position = CombatWorld.Clamp((Vector2)transform.position + direction * Config.wallDistance);
                    _wall = wall.AddComponent<ShieldWall>();
                    _wall.Initialize(this, direction, Config);
                    _owned.Add(wall);
                    break;
                case HeroRole.Shadow:
                    _overdriveUntil = Time.time + Config.duration;
                    Health.Status.Apply(StatusKind.AttackSpeed, Config.overdriveSpeed, Config.duration, this);
                    Health.Status.Displace(-direction * Config.dashDistance, Config.dashDuration);
                    StartCoroutine(DashFeedback());
                    break;
                case HeroRole.Magician:
                    CreateArea(SkillAreaKind.Trap, transform.position);
                    break;
                case HeroRole.OracleWitch:
                    CreateArea(SkillAreaKind.Slow, GroundTarget(target));
                    break;
                case HeroRole.Daphne:
                    if (IsLunar) Fire(direction, true);
                    else
                    {
                        foreach (var health in AreaDamage(transform.position, Config.radius, Config.activeDamage))
                            if (!health.IsDead) health.Status.Displace(((Vector2)health.transform.position - (Vector2)transform.position).normalized * Config.knockbackDistance, 0.2f);
                        SkillVfx.Burst(transform.position, new Color(1f, 0.5f, 0f), Config.radius);
                    }
                    break;
            }
            if (_player.ProgressionBonuses != null)
                Health.AddShield(Health.maxHp * _player.ProgressionBonuses.ActiveShield);
            SkillVfx.Burst(transform.position, Color.white, 0.4f);
            Changed?.Invoke();
            return true;
        }

        Vector2 GroundTarget(Vector2 point) => CombatWorld.GroundTarget(transform.position, (Vector2)transform.position +
            Vector2.ClampMagnitude(point - (Vector2)transform.position, Config.range));

        void CreateArea(SkillAreaKind kind, Vector2 position)
        {
            var area = new GameObject(kind.ToString());
            area.transform.position = position;
            area.AddComponent<SkillArea>().Initialize(this, kind, Config);
            _owned.Add(area);
        }

        public bool TryUltimate(Vector2 target)
        {
            if (!CanAct || Config == null || UltimateRemaining > 0f || Health.Status.Interrupted) return false;
            _ultimateReady = Time.time + Mathf.Max(0f, Config.ultimateCooldown);
            switch (Config.role)
            {
                case HeroRole.Tanky:
                    StartCoroutine(Meteor(GroundTarget(target)));
                    break;
                case HeroRole.Shadow:
                case HeroRole.OracleWitch:
                    StartCoroutine(ScreenStrike(CombatWorld.Enemies(transform.position, float.PositiveInfinity, true)));
                    break;
                case HeroRole.Magician:
                    CreateArea(SkillAreaKind.Storm, transform.position);
                    Vector2 old = transform.position;
                    _body.position = CombatWorld.SafeTeleport(_body, Config.range);
                    _body.linearVelocity = Vector2.zero;
                    SkillVfx.Line(old, _body.position, Color.magenta, 0.5f);
                    break;
                case HeroRole.Daphne:
                    foreach (var health in CombatWorld.Enemies(transform.position, float.PositiveInfinity, true))
                    {
                        health.TakeDamage(MakeHit(Config.ultimateDamage));
                        SkillVfx.Burst(health.transform.position, IsLunar ? Color.magenta : new Color(1f, 0.5f, 0f));
                    }
                    Health.Heal(Config.healing);
                    SkillVfx.Burst(transform.position, Color.magenta, Config.radius);
                    SkillVfx.Burst(transform.position, new Color(1f, 0.5f, 0f), Config.radius * 0.75f);
                    break;
            }
            Changed?.Invoke();
            return true;
        }

        IEnumerator Meteor(Vector2 point)
        {
            SkillVfx.Ring(point, Config.radius, Color.yellow, Mathf.Max(0.1f, Config.castDelay));
            SkillVfx.Line(point + Vector2.up * 3f, point, Color.yellow, Mathf.Max(0.1f, Config.castDelay), 0.35f);
            yield return new WaitForSeconds(Config.castDelay);
            if (!CanAct) yield break;
            foreach (var health in AreaDamage(point, Config.radius, Config.ultimateDamage))
            {
                if (health.IsDead) continue;
                health.Status.Apply(StatusKind.Root, 1f, Config.rootDuration, this);
                health.Status.Apply(StatusKind.AttackReduction, Config.attackReduction, Config.duration, this);
            }
            SkillVfx.Burst(point, Color.yellow, Config.radius);
        }

        IEnumerator ScreenStrike(List<Health> targets)
        {
            bool oracle = Config.role == HeroRole.OracleWitch;
            if (!oracle) Health.Status.PlayVisualLift(Config.ultimateLeapHeight, Mathf.Max(0.15f, Config.castDelay));
            int visuals = 0;
            foreach (var health in targets)
                if (health != null && visuals++ < 32) SkillVfx.Ring(health.transform.position, 0.45f, oracle ? Color.cyan : Color.magenta, Mathf.Max(0.1f, Config.castDelay));
            SkillVfx.Burst(transform.position, Color.magenta, 0.8f);
            yield return new WaitForSeconds(Config.castDelay);
            if (!CanAct) yield break;
            visuals = 0;
            foreach (var health in targets)
            {
                if (health == null || health.IsDead || !health.isActiveAndEnabled) continue;
                health.TakeDamage(MakeHit(Config.ultimateDamage, oracle));
                if (visuals++ < 32)
                    SkillVfx.Line((Vector2)health.transform.position + Vector2.up * 3f, health.transform.position,
                        oracle ? Color.cyan : new Color(0.3f, 0.1f, 0.4f), 0.35f, oracle ? 0.25f : 0.1f);
            }
        }

        IEnumerator DashFeedback()
        {
            for (int i = 0; i < 5; i++)
            {
                if (!CanAct) yield break;
                SkillVfx.Ring(transform.position, 0.3f, new Color(0.4f, 0.2f, 0.7f), 0.25f);
                yield return new WaitForSeconds(Config.dashDuration / 5f);
            }
        }

        public bool TrySwitchStance()
        {
            if (!CanAct || Config == null || Config.role != HeroRole.Daphne || StanceRemaining > 0f || Health.Status.Interrupted) return false;
            IsLunar = !IsLunar;
            _stanceReady = Time.time + Config.stanceCooldown;
            Health.Status.Remove(StatusKind.Attack, this);
            Health.Status.Remove(StatusKind.Movement, this);
            Health.Status.Apply(IsLunar ? StatusKind.Movement : StatusKind.Attack, Config.transitionBuff, Config.duration, this);
            SkillVfx.Burst(transform.position, IsLunar ? Color.magenta : new Color(1f, 0.5f, 0f), 0.7f);
            Changed?.Invoke();
            return true;
        }

        public List<Health> AreaDamage(Vector2 point, float radius, float damage)
        {
            var hit = new List<Health>();
            foreach (var target in CombatWorld.Enemies(point, radius))
                if (target.TakeDamage(MakeHit(damage)).Total > 0f) hit.Add(target);
            return hit;
        }

        void Cleanup()
        {
            StopAllCoroutines();
            foreach (var item in _owned) if (item != null) Destroy(item);
            _owned.Clear();
            _shields.Clear();
            _wall = null;
        }
        void OnDisable()
        {
            Cleanup();
            if (Health != null)
            {
                Health.DamageDealt -= OnDamageDealt;
                Health.ClearCombatBonuses();
            }
        }
    }
}
