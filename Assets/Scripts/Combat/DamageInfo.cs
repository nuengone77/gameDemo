using UnityEngine;

namespace MagicGame
{
    public enum DamageType { Physical, Magic }

    public struct DamageInfo
    {
        public Health source;
        public Vector2 origin;
        public float amount;
        public DamageType type;
        public float penetration;
        public bool bypassDefense;
        public bool guaranteed;
        public bool kunai;

        public DamageInfo(float amount, Health source = null, DamageType type = DamageType.Magic)
        {
            this = default;
            this.amount = amount;
            this.source = source;
            this.type = type;
            origin = source != null ? (Vector2)source.transform.position : Vector2.zero;
        }
    }

    public readonly struct DamageResult
    {
        public readonly float hpDamage;
        public readonly float shieldDamage;
        public readonly bool dodged;
        public float Total => hpDamage + shieldDamage;
        public DamageResult(float hpDamage, float shieldDamage = 0f, bool dodged = false)
        {
            this.hpDamage = hpDamage;
            this.shieldDamage = shieldDamage;
            this.dodged = dodged;
        }
    }
}
