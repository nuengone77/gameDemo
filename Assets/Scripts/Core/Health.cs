using System;
using System.Collections;
using UnityEngine;

namespace MagicGame
{
    public enum Team { Player, Enemy }

    [DefaultExecutionOrder(-100)]
    public class Health : MonoBehaviour
    {
        public Team team = Team.Enemy;
        public float maxHp = 100f;
        public float invulnerableTime = 0f;
        public bool destroyOnDeath = true;
        [Min(0)] public float physicalDefense;
        [Min(0)] public float magicDefense;

        public float ShieldHp { get; private set; }
        public float ShieldCapacity { get; set; } = 50f;
        public float Evasion { get; set; }
        public float DistanceReduction { get; set; }
        public float DefenseDistance { get; set; } = 6f;
        public StatusEffects Status { get; private set; }

        public float CurrentHp { get; private set; }
        public bool IsDead { get; private set; }
        public float Ratio => maxHp > 0f ? CurrentHp / maxHp : 0f;

        public event Action<Health> Died;
        public event Action<Health> Damaged;
        public event Action<Health> Changed;
        public event Action<Health, DamageInfo, DamageResult> DamageDealt;

        SpriteRenderer _renderer;
        float _invulnerableUntil;
        Coroutine _flash;

        void Awake()
        {
            CurrentHp = maxHp;
            _renderer = GetComponent<SpriteRenderer>();
            Status = GetComponent<StatusEffects>();
            if (Status == null) Status = gameObject.AddComponent<StatusEffects>();
        }

        public void TakeDamage(float amount)
        {
            TakeDamage(new DamageInfo(amount));
        }

        public DamageResult TakeDamage(DamageInfo hit)
        {
            if (IsDead || hit.amount <= 0f || Time.time < _invulnerableUntil) return default;
            if (!hit.guaranteed && Evasion > 0f && UnityEngine.Random.value < Evasion)
            {
                SkillVfx.Burst(transform.position, Color.cyan, 0.7f);
                return new DamageResult(0f, 0f, true);
            }

            float defense = hit.type == DamageType.Physical ? physicalDefense : magicDefense;
            float amount = hit.amount;
            if (!hit.bypassDefense)
            {
                defense = Mathf.Max(0f, defense) * (1f - Mathf.Clamp01(hit.penetration));
                amount *= 100f / (100f + defense);
                float distance = hit.source != null
                    ? Vector2.Distance(transform.position, hit.source.transform.position)
                    : Vector2.Distance(transform.position, hit.origin);
                float reduction = hit.source != null ? DistanceReduction * Mathf.Clamp01(distance / Mathf.Max(0.1f, DefenseDistance)) : 0f;
                reduction += Status != null ? Status.DefenseReduction : 0f;
                amount *= 1f - Mathf.Clamp(reduction, 0f, 0.9f);
            }
            float shieldDamage = Mathf.Min(ShieldHp, amount);
            ShieldHp -= shieldDamage;
            float hpDamage = Mathf.Min(CurrentHp, Mathf.Max(0f, amount - shieldDamage));
            if (shieldDamage + hpDamage <= 0f) return default;

            CurrentHp = Mathf.Max(0f, CurrentHp - hpDamage);
            _invulnerableUntil = Time.time + invulnerableTime;
            Damaged?.Invoke(this);
            if (CurrentHp > 0f) GameAudio.Play(b => team == Team.Player ? b.playerHurt : b.enemyHit);
            Changed?.Invoke(this);
            var result = new DamageResult(hpDamage, shieldDamage);
            if (hit.source != null) hit.source.DamageDealt?.Invoke(this, hit, result);

            if (_renderer != null)
            {
                if (_flash != null) StopCoroutine(_flash);
                _flash = StartCoroutine(Flash());
            }

            if (CurrentHp <= 0f) Die();
            return result;
        }

        public void AddShield(float amount)
        {
            float before = ShieldHp;
            ShieldHp = Mathf.Clamp(ShieldHp + Mathf.Max(0f, amount), 0f, ShieldCapacity);
            if (before != ShieldHp) Changed?.Invoke(this);
        }
        public void Heal(float amount)
        {
            if (!IsDead)
            {
                CurrentHp = Mathf.Min(maxHp, CurrentHp + Mathf.Max(0f, amount));
                Changed?.Invoke(this);
            }
        }

        public void ClearCombatBonuses()
        {
            ShieldHp = 0f;
            Evasion = DistanceReduction = 0f;
            Changed?.Invoke(this);
        }

        void Die()
        {
            IsDead = true;
            GameAudio.Play(b => team == Team.Player ? b.playerDeath : b.enemyDeath);
            if (Status != null) Status.Clear();
            Died?.Invoke(this);
            if (destroyOnDeath) Destroy(gameObject);
        }

        IEnumerator Flash()
        {
            _renderer.color = new Color(1f, 0.35f, 0.35f);
            yield return new WaitForSeconds(0.1f);
            _renderer.color = Color.white;
            _flash = null;
        }
    }
}
