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

        // Card artwork for the Magician's trap and Jackpot storm (null = placeholder rings).
        const int StormCards = 10;
        const float StormSpin = 220f;      // degrees per second around the centre
        const float StormFps = 12f;
        Sprite[] _cardFrames;
        readonly List<SpriteRenderer> _stormCards = new();
        AudioSource _loop;

        public void Initialize(PlayerSkills owner, SkillAreaKind kind, CharacterCombatConfig config)
        {
            _owner = owner;
            _kind = kind;
            _config = config;
            _until = Time.time + (kind == SkillAreaKind.Trap ? config.trapLifetime : config.duration);
            _nextTick = Time.time;
            _totalTicks = Mathf.Max(1, Mathf.CeilToInt(config.duration / Mathf.Max(0.05f, config.tickInterval)));
            _cardFrames = kind != SkillAreaKind.Slow ? owner.ProjectileFrames : null;
            if (kind == SkillAreaKind.Slow && config.activeVfx.IsSet)
                SkillVfx.Spawn(config.activeVfx, transform.position, 0f, transform, -5000);
            else if (_cardFrames != null && kind == SkillAreaKind.Trap) CreateTrapCard();
            else if (_cardFrames != null && kind == SkillAreaKind.Storm) CreateStormCards();
            else
            {
                var visual = SkillVfx.Ring(transform.position, kind == SkillAreaKind.Trap ? config.trapTriggerRadius : config.radius,
                    kind == SkillAreaKind.Slow ? Color.cyan : Color.magenta, 0f);
                visual.transform.SetParent(transform, true);
            }
            if (kind == SkillAreaKind.Storm) GameAudio.PlayHeroLoop(owner.HeroName, h => h.ultimateLoop, out _loop);
        }

        // One card frame lying on the ground, drawn above the map but under the characters.
        void CreateTrapCard()
        {
            var card = CreateCard("TrapCard", _cardFrames[_cardFrames.Length - 1], -5000);
            card.transform.localScale = Vector3.one * 0.75f;
            card.transform.localRotation = Quaternion.Euler(0f, 0f, -20f);
        }

        // Cards evenly spaced on a circle; each plays the projectile frames in order, offset so they don't pulse together.
        void CreateStormCards()
        {
            for (int i = 0; i < StormCards; i++)
                _stormCards.Add(CreateCard("StormCard", _cardFrames[i % _cardFrames.Length], 25000));
            // Glow at the centre of the card ring (same height as the ring's centre), under the cards.
            if (_config.ultimateVfx.IsSet)
                SkillVfx.Spawn(_config.ultimateVfx, (Vector2)transform.position + Vector2.up * 0.5f, 0f, transform, 24000);
            UpdateStormCards();
        }

        SpriteRenderer CreateCard(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        void UpdateStormCards()
        {
            float radius = _config.radius * 0.85f;
            float spin = Time.time * StormSpin;
            for (int i = 0; i < _stormCards.Count; i++)
            {
                float angle = (spin + 360f * i / _stormCards.Count) * Mathf.Deg2Rad;
                // Squash vertically so the ring reads as lying on the ground in the 3/4 view.
                _stormCards[i].transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.6f + 0.5f, 0f);
                _stormCards[i].transform.localRotation = Quaternion.Euler(0f, 0f, spin * 1.5f + i * 36f);
                int frame = (int)(Time.time * StormFps) + i * 2;
                _stormCards[i].sprite = _cardFrames[frame % _cardFrames.Length];
            }
        }

        void Update()
        {
            if (_loop != null) { if (GameUI.GameplayBlocked) _loop.Pause(); else _loop.UnPause(); }
            if (GameUI.GameplayBlocked) return;
            if (_owner == null || !_owner.CanAct) { Destroy(gameObject); return; }
            if (_kind == SkillAreaKind.Storm)
            {
                if (_stormCards.Count > 0) UpdateStormCards();
                while (_ticks < _totalTicks && Time.time >= _nextTick)
                {
                    _ticks++;
                    _nextTick += Mathf.Max(0.05f, _config.tickInterval);
                    _owner.AreaDamage(transform.position, _config.radius, _config.ultimateDamage / _totalTicks);
                    if (_stormCards.Count == 0) SkillVfx.Burst(transform.position, Color.magenta, _config.radius);
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
                GameAudio.PlayHero(_owner.HeroName, h => h.activeImpact);
                if (_config.activeVfx.IsSet) SkillVfx.Spawn(_config.activeVfx, transform.position);
                else SkillVfx.Burst(transform.position, Color.magenta, _config.radius);
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
            GameAudio.StopLoop(_loop);
        }
    }
}
