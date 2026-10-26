using System.Collections.Generic;
using UnityEngine;

namespace MagicGame
{
    public enum StatusKind { Root, Slow, Knockup, AttackReduction, Defense, AttackSpeed, Attack, Movement, Burn }

    // Runtime modifiers never mutate the serialized base stats on a character.
    public class StatusEffects : MonoBehaviour
    {
        class Effect
        {
            public StatusKind kind;
            public Object source;
            public float amount, until, nextTick, start;
            public Health attacker;
        }

        readonly List<Effect> _effects = new();
        Health _health;
        SpriteRenderer _renderer, _lifted;
        bool _rendererWasEnabled;
        GameObject _indicator;
        Vector2 _forcedVelocity;
        float _forcedUntil;
        float _liftStart, _liftUntil, _liftHeight;
        public bool IsDisplaced => Time.time < _forcedUntil;
        public bool Rooted => Has(StatusKind.Root);
        public bool Interrupted => Has(StatusKind.Knockup);
        public float MoveMultiplier => Rooted || Interrupted ? 0f : Mathf.Max(0f, 1f + Sum(StatusKind.Movement) - Strongest(StatusKind.Slow));
        public float AttackMultiplier => Mathf.Max(0f, 1f + Sum(StatusKind.Attack) - Strongest(StatusKind.AttackReduction));
        public float AttackSpeedMultiplier => Mathf.Clamp(1f + Sum(StatusKind.AttackSpeed), 0.1f, 5f);
        public float DefenseReduction => Sum(StatusKind.Defense);

        void Awake()
        {
            _health = GetComponent<Health>();
            _renderer = GetComponent<SpriteRenderer>();
        }

        public void Apply(StatusKind kind, float amount, float duration, Object source, Health attacker = null)
        {
            if (duration <= 0f || (_health != null && _health.IsDead)) return;
            var effect = _effects.Find(e => e.kind == kind && e.source == source);
            if (effect == null)
            {
                effect = new Effect { kind = kind, source = source, nextTick = Time.time + 1f, start = Time.time };
                _effects.Add(effect);
            }
            effect.amount = amount;
            effect.until = Time.time + duration;
            effect.attacker = attacker;
        }

        public void Remove(StatusKind kind, Object source) => _effects.RemoveAll(e => e.kind == kind && e.source == source);
        public void Displace(Vector2 delta, float duration)
        {
            _forcedVelocity = delta / Mathf.Max(0.05f, duration);
            _forcedUntil = Time.time + Mathf.Max(0.05f, duration);
        }
        public void PlayVisualLift(float height, float duration)
        {
            _liftStart = Time.time;
            _liftUntil = Time.time + Mathf.Max(0.05f, duration);
            _liftHeight = height;
        }
        public Vector2 ResolveVelocity(Rigidbody2D body, Vector2 requested)
        {
            if (Interrupted || Rooted) return Vector2.zero;
            if (!IsDisplaced) return requested * MoveMultiplier;
            return (CombatWorld.Move(body, _forcedVelocity * Time.fixedDeltaTime) - body.position) / Time.fixedDeltaTime;
        }
        public bool Has(StatusKind kind) => _effects.Exists(e => e.kind == kind && e.until > Time.time);
        float Sum(StatusKind kind)
        {
            float value = 0f;
            foreach (var e in _effects) if (e.kind == kind && e.until > Time.time) value += e.amount;
            return value;
        }
        float Strongest(StatusKind kind)
        {
            float value = 0f;
            foreach (var e in _effects) if (e.kind == kind && e.until > Time.time) value = Mathf.Max(value, e.amount);
            return value;
        }

        void Update()
        {
            // Snapshot first: damage may kill the owner and clear this list.
            foreach (var e in _effects.ToArray())
            {
                if (e.kind == StatusKind.Burn && e.nextTick <= Time.time && e.nextTick <= e.until && _health != null)
                {
                    e.nextTick += 1f;
                    _health.TakeDamage(new DamageInfo(e.amount, e.attacker));
                }
            }
            _effects.RemoveAll(e => e.until <= Time.time);
        }

        void LateUpdate()
        {
            bool active = _effects.Count > 0;
            if (active && _indicator == null)
            {
                _indicator = SkillVfx.Ring(transform.position, 0.4f, Color.white, 0f);
                _indicator.transform.SetParent(transform, true);
            }
            if (_indicator != null) _indicator.SetActive(active);
            if (_indicator != null && active)
            {
                var color = Has(StatusKind.Burn) ? new Color(1f, 0.4f, 0f) : Rooted || Interrupted ? Color.magenta : Color.cyan;
                _indicator.GetComponent<LineRenderer>().startColor = color;
                _indicator.GetComponent<LineRenderer>().endColor = color;
            }
            var lift = _effects.Find(e => e.kind == StatusKind.Knockup && e.until > Time.time);
            if ((lift != null || Time.time < _liftUntil) && _renderer != null)
            {
                if (_lifted == null)
                {
                    var go = new GameObject("KnockupVisual");
                    go.transform.SetParent(transform, false);
                    _lifted = go.AddComponent<SpriteRenderer>();
                    _rendererWasEnabled = _renderer.enabled;
                }
                _renderer.enabled = false;
                _lifted.enabled = true;
                _lifted.sprite = _renderer.sprite;
                _lifted.color = _renderer.color;
                _lifted.sortingOrder = _renderer.sortingOrder + 1;
                float start = lift != null ? lift.start : _liftStart;
                float until = lift != null ? lift.until : _liftUntil;
                float height = lift != null ? lift.amount : _liftHeight;
                float progress = Mathf.Clamp01((Time.time - start) / Mathf.Max(0.01f, until - start));
                _lifted.transform.localPosition = Vector3.up * Mathf.Sin(progress * Mathf.PI) * height;
            }
            else RestoreVisual();
        }

        void RestoreVisual()
        {
            if (_lifted == null || !_lifted.enabled) return;
            _lifted.enabled = false;
            if (_renderer != null) _renderer.enabled = _rendererWasEnabled;
        }
        public void Clear()
        {
            _effects.Clear();
            _forcedUntil = 0f;
            _liftUntil = 0f;
            RestoreVisual();
            if (_indicator != null) _indicator.SetActive(false);
        }
        void OnDisable() => Clear();
    }
}
