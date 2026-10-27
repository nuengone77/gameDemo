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
        public int TargetingRevision { get; private set; }
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

        // Frames of the hero's basic-attack projectile, reused as artwork by skill areas (Peng's cards).
        public Sprite[] ProjectileFrames
        {
            get
            {
                var anim = _player != null ? _player.CurrentCharacter : null;
                var frames = anim != null && anim.attackProjectile != null ? anim.attackProjectile.frames : null;
                return frames != null && frames.Length > 0 ? frames : null;
            }
        }
        public string HeroName => _player != null && _player.CurrentCharacter != null ? _player.CurrentCharacter.name : "";
        public float ActiveReadyAt => _activeReady;
        public float UltimateReadyAt => _ultimateReady;
        public float StanceReadyAt => _stanceReady;
        readonly List<GameObject> _owned = new();
        readonly List<GameObject> _shields = new();
        ShieldWall _wall;
        AudioSource _eclipseHum;

        public void Initialize(PlayerController player, CharacterCombatConfig config)
        {
            TargetingRevision++;
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
                    var visual = Config.shieldOrbSprite != null
                        ? SkillVfx.StillSprite(transform.position, Config.shieldOrbSprite, Config.shieldOrbHeight)
                        : SkillVfx.Ring(transform.position, 0.14f, Color.cyan, 0f);
                    _shields.Add(visual);
                    _owned.Add(visual);
                }
            }
            if (Config.role == HeroRole.Magician) Health.Evasion = Config.evasion;
        }

        void Update()
        {
            if (_eclipseHum != null)
            {
                if (Health == null || Health.IsDead) StopEclipseHum();
                else if (GameUI.GameplayBlocked) _eclipseHum.Pause();
                else _eclipseHum.UnPause();
            }
            // Pausing must preserve owned effects and pending cast coroutines.
            if (GameUI.GameplayBlocked) return;
            if (!CanAct)
            {
                if (_owned.Count > 0) Cleanup();
                StopEclipseHum();
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
                // A drawn wall replaces the orbiting shields while it stands; the line placeholder gathers them along it.
                bool drawnWall = _wall != null && Config.shieldWallSprite != null;
                if (_shields[i].activeSelf == drawnWall) _shields[i].SetActive(!drawnWall);
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
                // Only the position follows the orbit, so each shield stays upright as drawn.
                _shields[i].transform.position = point;
                if (_shields[i].TryGetComponent<SpriteRenderer>(out var shieldSprite))
                    shieldSprite.sortingOrder = -Mathf.RoundToInt(point.y * 100f);
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
                        projectile.explosionVfx = Config.basicHitVfx;
                        projectile.bypassObstacles = true;
                        break;
                    case HeroRole.Daphne:
                        if (IsLunar)
                        {
                            damage = lunarBind ? Config.activeDamage : Config.lunarDamage;
                            projectile.rootDuration = lunarBind ? Config.rootDuration : 0f;
                            if (lunarBind)
                            {
                                projectile.rootVfx = Config.activeExtraVfx;
                                projectile.rootHitVfx = Config.lunarHitVfx;
                            }
                        }
                        else
                        {
                            projectile.explosionRadius = Config.radius;
                            projectile.explosionVfx = Config.basicHitVfx;
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

        public SkillTargetingMode GetTargetingMode(SkillSlot slot)
        {
            if (Config == null) return SkillTargetingMode.None;
            if (slot == SkillSlot.Ultimate)
                return Config.role == HeroRole.Tanky ? SkillTargetingMode.Ground : SkillTargetingMode.None;
            switch (Config.role)
            {
                case HeroRole.Tanky: return SkillTargetingMode.Direction;
                case HeroRole.Shadow: return SkillTargetingMode.Dash;
                case HeroRole.OracleWitch: return SkillTargetingMode.Ground;
                case HeroRole.Daphne: return IsLunar ? SkillTargetingMode.Direction : SkillTargetingMode.None;
                default: return SkillTargetingMode.None;
            }
        }

        public bool CanTarget(SkillSlot slot) => Config != null && CanAct && _player.CanMove &&
            !Health.Status.Interrupted && (slot == SkillSlot.Active ? ActiveRemaining : UltimateRemaining) <= 0f;

        public bool IsCurrentCast(SkillCastRequest request) => request.source == this &&
            request.revision == TargetingRevision && CanTarget(request.slot);

        public SkillCastRequest ResolveSkillCast(SkillSlot slot, Vector2 stick, bool manual)
        {
            var mode = GetTargetingMode(slot);
            Vector2 origin = transform.position;
            Vector2 direction = GameplayInput.HasTouchAim ? GameplayInput.Aim.normalized : _player.Facing.normalized;
            if (direction.sqrMagnitude < 0.001f) direction = Vector2.down;
            Vector2 target = origin + direction * Config.range;
            if (manual)
            {
                if (stick.sqrMagnitude > 0.0001f) direction = stick.normalized;
                target = origin + Vector2.ClampMagnitude(stick, 1f) * Config.range;
                // The combat skill dashes backward; the joystick describes travel direction.
                if (mode == SkillTargetingMode.Dash) direction = -direction;
            }
            else
            {
                Health nearest = null;
                float closest = float.PositiveInfinity;
                foreach (var enemy in CombatWorld.Enemies(origin, Config.range, true))
                {
                    float distance = ((Vector2)enemy.transform.position - origin).sqrMagnitude;
                    if (distance >= closest) continue;
                    nearest = enemy;
                    closest = distance;
                }
                if (nearest != null)
                {
                    target = nearest.transform.position;
                    Vector2 aimOrigin = Config.role == HeroRole.Daphne ? _player.Center : origin;
                    if ((target - aimOrigin).sqrMagnitude > 0.0001f) direction = (target - aimOrigin).normalized;
                }
            }
            if (mode == SkillTargetingMode.Ground) target = GroundTarget(target);
            return new SkillCastRequest(slot, direction, target, this);
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
                        if (Config.activeVfx.IsSet) SkillVfx.Spawn(Config.activeVfx, transform.position);
                        else SkillVfx.Burst(transform.position, new Color(1f, 0.5f, 0f), Config.radius);
                    }
                    break;
            }
            if (_player.ProgressionBonuses != null)
                Health.AddShield(Health.maxHp * _player.ProgressionBonuses.ActiveShield);
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
                    if (Config.ultimateExtraVfx.IsSet)
                    {
                        SkillVfx.Spawn(Config.ultimateExtraVfx, old);
                        SkillVfx.Spawn(Config.ultimateExtraVfx, _body.position);
                    }
                    else SkillVfx.Line(old, _body.position, Color.magenta, 0.5f);
                    break;
                case HeroRole.Daphne:
                    StartCoroutine(Eclipse());
                    break;
            }
            Changed?.Invoke();
            return true;
        }

        // The star field left by Eclipse: light damage to everyone inside, every tick, until it fades.
        IEnumerator EclipseField(Vector2 point)
        {
            float interval = Mathf.Max(0.05f, Config.tickInterval);
            float end = Time.time + Config.eclipseFieldDuration;
            while (Time.time + interval <= end + 0.001f)
            {
                yield return new WaitForSeconds(interval);
                if (!CanAct) yield break;
                AreaDamage(point, Config.eclipseFieldRadius, Config.eclipseFieldDamage);
            }
        }

        // Eclipse: the sun and moon orbs drift together over Daphne; once merged she heals, the star field
        // is left behind and a ring sweeps out until it has left the map, hitting each enemy as it arrives.
        IEnumerator Eclipse()
        {
            Vector2 ground = transform.position;
            Vector2 center = ground + Vector2.up * EclipseOrbHeight;
            float merge = Config.eclipseMergeTime;
            var side = Vector2.right * Config.eclipseOrbSpread;
            SkillVfx.Drop(Config.eclipseSunVfx, center - side, center, merge);
            SkillVfx.Drop(Config.eclipseMoonVfx, center + side, center, merge);
            GameAudio.PlayHeroLoop(HeroName, h => h.ultimateLoop, out _eclipseHum);
            if (merge > 0f) yield return new WaitForSeconds(merge);
            StopEclipseHum();
            if (!CanAct) yield break;

            Health.Heal(Config.healing);
            if (Config.eclipseFieldDamage > 0f && Config.eclipseFieldDuration > 0f) StartCoroutine(EclipseField(ground));
            if (Config.ultimateVfx.IsSet) SkillVfx.Spawn(Config.ultimateVfx, ground, Config.eclipseFieldDuration);
            if (Config.ultimateExtraVfx.IsSet) SkillVfx.Spawn(Config.ultimateExtraVfx, transform.position, -1f, transform);

            var targets = CombatWorld.Enemies(center, float.PositiveInfinity);
            var hit = new HashSet<Health>();
            float reach = MapReach(center);
            float sweep = reach / Config.eclipseWaveSpeed;
            GameAudio.PlayHero(HeroName, h => h.ultimateImpact);
            float vfxRadius = Mathf.Min(reach, Config.eclipseWaveVfxMaxRadius);
            var wave = SkillVfx.Spawn(Config.eclipseWaveVfx, center, vfxRadius / Config.eclipseWaveSpeed);
            if (wave != null)
            {
                var distortion = wave.transform.GetChild(0).Find("Distortion"); // Built-in grab-pass layer; renders as a grey disc in URP
                if (distortion != null) distortion.gameObject.SetActive(false);
            }
            // A crisp edge on the damage radius: a soft outer glow plus a solid core line.
            var glow = EdgeLine(center, new Color(Config.eclipseWaveEdge.r, Config.eclipseWaveEdge.g, Config.eclipseWaveEdge.b, Config.eclipseWaveEdge.a * 0.35f), 0.7f);
            var edge = EdgeLine(center, Config.eclipseWaveEdge, 0.22f);

            int visuals = 0;
            for (float t = 0f; ; t += Time.deltaTime)
            {
                float radius = Mathf.Min(reach, t * Config.eclipseWaveSpeed);
                if (wave != null) wave.transform.localScale = Vector3.one * Mathf.Max(0.01f, Mathf.Min(radius, vfxRadius) / Config.eclipseWaveRingRadius);
                SetCircle(glow, radius);
                SetCircle(edge, radius);
                // The ring always finishes its sweep (even once the stage clears); it only hurts while Daphne can act.
                foreach (var health in targets)
                {
                    if (!CanAct) break;
                    if (health == null || health.IsDead || !health.isActiveAndEnabled || hit.Contains(health)) continue;
                    Vector2 at = health.transform.position;
                    if (Vector2.Distance(at, center) > radius) continue;
                    hit.Add(health);
                    health.TakeDamage(MakeHit(Config.ultimateDamage));
                    if (visuals++ >= 32) continue;
                    if (Config.ultimateHitVfx.IsSet) SkillVfx.Spawn(Config.ultimateHitVfx, at + Vector2.up * 0.5f);
                    else SkillVfx.Burst(at, IsLunar ? Color.magenta : new Color(1f, 0.5f, 0f));
                }
                if (radius >= reach)
                {
                    Destroy(glow.gameObject);
                    Destroy(edge.gameObject);
                    yield break;
                }
                yield return null;
            }
        }

        const int EdgePoints = 128;

        static LineRenderer EdgeLine(Vector2 center, Color color, float width)
        {
            var line = SkillVfx.Ring(center, 0.01f, color, 0f).GetComponent<LineRenderer>();
            line.positionCount = EdgePoints;
            line.startWidth = line.endWidth = width;
            return line;
        }

        static void SetCircle(LineRenderer line, float radius)
        {
            for (int i = 0; i < EdgePoints; i++)
            {
                float angle = Mathf.PI * 2f * i / EdgePoints;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        const float EclipseOrbHeight = 0.8f;

        // Distance from a point to the farthest corner of the map: once the ring is this big it has left the map.
        static float MapReach(Vector2 point)
        {
            var map = CombatWorld.AttackBounds;
            float far = 0f;
            foreach (var corner in new[] { map.min, map.max, new Vector2(map.xMin, map.yMax), new Vector2(map.xMax, map.yMin) })
                far = Mathf.Max(far, Vector2.Distance(point, corner));
            return far + 1f;
        }

        IEnumerator Meteor(Vector2 point)
        {
            if (Config.ultimateVfx.IsSet || Config.ultimateFallVfx.IsSet)
            {
                if (Config.ultimateVfx.IsSet) SkillVfx.Spawn(Config.ultimateVfx, point, Config.castDelay + 0.4f);
                if (Config.ultimateFallVfx.IsSet)
                    SkillVfx.Drop(Config.ultimateFallVfx, point + Config.ultimateFallOffset, point, Config.castDelay);
            }
            else
            {
                SkillVfx.Ring(point, Config.radius, Color.yellow, Mathf.Max(0.1f, Config.castDelay));
                SkillVfx.Line(point + Vector2.up * 3f, point, Color.yellow, Mathf.Max(0.1f, Config.castDelay), 0.35f);
            }
            yield return new WaitForSeconds(Config.castDelay);
            if (!CanAct) yield break;
            foreach (var health in AreaDamage(point, Config.radius, Config.ultimateDamage))
            {
                if (health.IsDead) continue;
                health.Status.Apply(StatusKind.Root, 1f, Config.rootDuration, this);
                health.Status.Apply(StatusKind.AttackReduction, Config.attackReduction, Config.duration, this);
            }
            GameAudio.PlayHero(HeroName, h => h.ultimateImpact);
            if (Config.ultimateExtraVfx.IsSet) SkillVfx.Spawn(Config.ultimateExtraVfx, point);
            else SkillVfx.Burst(point, Color.yellow, Config.radius);
        }

        const float KunaiFallTime = 0.3f;
        const int KunaiPerTarget = 3;

        IEnumerator ScreenStrike(List<Health> targets)
        {
            bool oracle = Config.role == HeroRole.OracleWitch;
            if (!oracle) Health.Status.PlayVisualLift(Config.ultimateLeapHeight, Mathf.Max(0.15f, Config.castDelay));
            int visuals = 0;

            // Shadow: real kunai sprites rain down so they land exactly when the damage is applied.
            var kunai = oracle ? null : ProjectileFrames;
            bool swords = oracle && Config.ultimateVfx.IsSet;
            float swordLead = swords ? Mathf.Clamp(Config.ultimateVfx.impactTime, 0f, Config.castDelay) : 0f;
            if (kunai != null)
            {
                float fall = Mathf.Min(KunaiFallTime, Config.castDelay);
                yield return new WaitForSeconds(Config.castDelay - fall);
                if (!CanAct) yield break;
                visuals = 0;
                foreach (var health in targets)
                {
                    if (health == null || health.IsDead || visuals++ >= 32) continue;
                    // Light beam on each target as its kunai come down.
                    if (Config.ultimateVfx.IsSet) SkillVfx.Spawn(Config.ultimateVfx, health.transform.position);
                    for (int k = 0; k < KunaiPerTarget; k++)
                    {
                        Vector2 land = (Vector2)health.transform.position + new Vector2((k - (KunaiPerTarget - 1) * 0.5f) * 0.3f, 0.15f * (k % 2));
                        // Cosmetic and self-destroying, so it is not owned: it may finish fading after the stage clears.
                        SkillVfx.Falling(kunai, land + new Vector2(-0.6f, 3.5f), land, fall, 0.35f, -80f, 24000);
                    }
                }
                yield return new WaitForSeconds(fall);
                if (CanAct) GameAudio.PlayHero(HeroName, h => h.ultimateImpact);
            }
            else if (swords)
            {
                // Spawn the swords early so they strike on the frame the damage is applied.
                yield return new WaitForSeconds(Config.castDelay - swordLead);
                if (!CanAct) yield break;
                visuals = 0;
                foreach (var health in targets)
                    if (health != null && !health.IsDead && visuals++ < 32) SkillVfx.Spawn(Config.ultimateVfx, health.transform.position);
                yield return new WaitForSeconds(swordLead);
                if (CanAct) GameAudio.PlayHero(HeroName, h => h.ultimateImpact);
            }
            else yield return new WaitForSeconds(Config.castDelay);
            if (!CanAct) yield break;
            visuals = 0;
            foreach (var health in targets)
            {
                if (health == null || health.IsDead || !health.isActiveAndEnabled) continue;
                health.TakeDamage(MakeHit(Config.ultimateDamage, oracle));
                if (visuals++ < 32 && kunai == null && !swords)
                    SkillVfx.Line((Vector2)health.transform.position + Vector2.up * 3f, health.transform.position,
                        oracle ? Color.cyan : new Color(0.3f, 0.1f, 0.4f), 0.35f, oracle ? 0.25f : 0.1f);
            }
        }

        IEnumerator DashFeedback()
        {
            for (int i = 0; i < 5; i++)
            {
                if (!CanAct) yield break;
                if (Config.activeVfx.IsSet) SkillVfx.Spawn(Config.activeVfx, transform.position, 0.1f, null, 4000);
                else SkillVfx.Ring(transform.position, 0.3f, new Color(0.4f, 0.2f, 0.7f), 0.25f);
                yield return new WaitForSeconds(Config.dashDuration / 5f);
            }
        }

        public bool TrySwitchStance()
        {
            if (!CanAct || Config == null || Config.role != HeroRole.Daphne || StanceRemaining > 0f || Health.Status.Interrupted) return false;
            IsLunar = !IsLunar;
            TargetingRevision++;
            _stanceReady = Time.time + Config.stanceCooldown;
            Health.Status.Remove(StatusKind.Attack, this);
            Health.Status.Remove(StatusKind.Movement, this);
            Health.Status.Apply(IsLunar ? StatusKind.Movement : StatusKind.Attack, Config.transitionBuff, Config.duration, this);
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
            StopEclipseHum();
            foreach (var item in _owned) if (item != null) Destroy(item);
            _owned.Clear();
            _shields.Clear();
            _wall = null;
        }

        void StopEclipseHum()
        {
            GameAudio.StopLoop(_eclipseHum);
            _eclipseHum = null;
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
