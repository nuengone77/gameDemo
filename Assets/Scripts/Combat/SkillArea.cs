using System.Collections.Generic;
using UnityEngine;

namespace MagicGame
{
    public enum SkillAreaKind { Trap, Slow, Storm }

    public class SkillArea : MonoBehaviour
    {
        PlayerSkills _owner;
        CharacterCombatConfig _config;
        SkillAreaKind _kind;
        float _until, _nextTick;
        int _ticks, _totalTicks;
        readonly HashSet<Health> _slowed = new();

        public void Initialize(PlayerSkills owner, SkillAreaKind kind, CharacterCombatConfig config)
        {
            _owner = owner;
            _kind = kind;
            _config = config;
            _until = Time.time + (kind == SkillAreaKind.Trap ? config.trapLifetime : config.duration);
            _nextTick = Time.time;
            _totalTicks = Mathf.Max(1, Mathf.CeilToInt(config.duration / Mathf.Max(0.05f, config.tickInterval)));
            var visual = SkillVfx.Ring(transform.position, kind == SkillAreaKind.Trap ? config.trapTriggerRadius : config.radius,
                kind == SkillAreaKind.Slow ? Color.cyan : Color.magenta, 0f);
            visual.transform.SetParent(transform, true);
        }

        void Update()
        {
            if (GameUI.GameplayBlocked) return;
            if (_owner == null || !_owner.CanAct) { Destroy(gameObject); return; }
            if (_kind == SkillAreaKind.Storm)
            {
                while (_ticks < _totalTicks && Time.time >= _nextTick)
                {
                    _ticks++;
                    _nextTick += Mathf.Max(0.05f, _config.tickInterval);
                    _owner.AreaDamage(transform.position, _config.radius, _config.ultimateDamage / _totalTicks);
                    SkillVfx.Burst(transform.position, Color.magenta, _config.radius);
                }
                if (_ticks >= _totalTicks && Time.time >= _until) Destroy(gameObject);
                return;
            }
            if (Time.time >= _until) { Destroy(gameObject); return; }
            if (Time.time < _nextTick) return;
            _nextTick = Time.time + 0.05f;
            if (_kind == SkillAreaKind.Trap)
            {
                if (CombatWorld.Enemies(transform.position, _config.trapTriggerRadius).Count == 0) return;
                foreach (var health in _owner.AreaDamage(transform.position, _config.radius, _config.activeDamage))
                    if (!health.IsDead) health.Status.Apply(StatusKind.Knockup, _config.knockupHeight, _config.knockupDuration, this);
                SkillVfx.Burst(transform.position, Color.magenta, _config.radius);
                Destroy(gameObject);
            }
            else
            {
                var inside = CombatWorld.Enemies(transform.position, _config.radius);
                foreach (var old in _slowed)
                    if (old != null && !inside.Contains(old)) old.Status.Remove(StatusKind.Slow, this);
                _slowed.Clear();
                foreach (var target in inside)
                {
                    target.Status.Apply(StatusKind.Slow, _config.slow, 0.15f, this);
                    _slowed.Add(target);
                }
            }
        }
        void OnDestroy()
        {
            foreach (var target in _slowed) if (target != null) target.Status.Remove(StatusKind.Slow, this);
        }
    }
}
