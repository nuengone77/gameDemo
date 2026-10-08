using System;
using System.Collections;
using UnityEngine;

namespace MagicGame
{
    public enum Team { Player, Enemy }

    public class Health : MonoBehaviour
    {
        public Team team = Team.Enemy;
        public float maxHp = 100f;
        public float invulnerableTime = 0f;
        public bool destroyOnDeath = true;

        public float CurrentHp { get; private set; }
        public bool IsDead { get; private set; }
        public float Ratio => maxHp > 0f ? CurrentHp / maxHp : 0f;

        public event Action<Health> Died;
        public event Action<Health> Damaged;

        SpriteRenderer _renderer;
        float _invulnerableUntil;
        Coroutine _flash;

        void Awake()
        {
            CurrentHp = maxHp;
            _renderer = GetComponent<SpriteRenderer>();
        }

        public void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f || Time.time < _invulnerableUntil) return;

            CurrentHp = Mathf.Max(0f, CurrentHp - amount);
            _invulnerableUntil = Time.time + invulnerableTime;
            Damaged?.Invoke(this);

            if (_renderer != null)
            {
                if (_flash != null) StopCoroutine(_flash);
                _flash = StartCoroutine(Flash());
            }

            if (CurrentHp <= 0f) Die();
        }

        void Die()
        {
            IsDead = true;
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
