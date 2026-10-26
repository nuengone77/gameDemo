using UnityEngine;

namespace MagicGame
{
    public enum HeroRole { Tanky, Shadow, Magician, OracleWitch, Daphne }

    [CreateAssetMenu(menuName = "Magic Game/Character Combat Config")]
    public class CharacterCombatConfig : ScriptableObject
    {
        public HeroRole role;
        [Header("Base stats (applied once when the player spawns)")]
        [Min(1)] public float maxHp = 100f;
        [Min(0.1f)] public float moveSpeed = 4f;
        [Header("Combat tuning; all times in seconds")]
        [Min(0)] public float basicDamage = 25f;
        [Min(0.05f)] public float basicInterval = 0.35f;
        [Min(0)] public float activeDamage = 50f;
        [Min(0)] public float ultimateDamage = 100f;
        [Min(0)] public float activeCooldown = 10f;
        [Min(0)] public float ultimateCooldown = 30f;
        [Min(0.1f)] public float duration = 4f;
        [Min(0.1f)] public float radius = 2f;
        [Min(0.1f)] public float range = 6f;
        [Min(0)] public float castDelay = 0.5f;
        [Min(0.05f)] public float tickInterval = 0.5f;
        [Header("Tanky")]
        [Range(0, 1)] public float distanceReduction = 0.3f;
        [Min(0.1f)] public float defenseDistance = 6f;
        [Range(0, 1)] public float conversion = 0.1f;
        [Min(0)] public float shieldCapacity = 50f;
        [Range(0, 1)] public float wallDefense = 0.2f;
        [Min(0.1f)] public float wallWidth = 3f;
        [Min(0.1f)] public float wallDistance = 1.2f;
        [Min(0)] public float rootDuration = 2.5f;
        [Range(0, 1)] public float attackReduction = 0.3f;
        [Header("Shadow")]
        [Range(0, 1)] public float armorPenetration = 0.2f;
        [Min(1)] public int maxStacks = 10;
        [Min(0)] public float speedPerStack = 0.05f;
        [Min(0.1f)] public float stackTimeout = 2f;
        [Min(0)] public float overdriveSpeed = 0.5f;
        [Min(1)] public int fanCount = 3;
        [Range(0, 180)] public float fanAngle = 30f;
        [Min(0)] public float dashDistance = 2f;
        [Min(0.05f)] public float dashDuration = 0.2f;
        [Min(0)] public float ultimateLeapHeight = 0.6f;
        [Header("Magician")]
        [Min(0)] public int bounces = 3;
        [Min(0)] public float bounceRadius = 3f;
        [Range(0, 1)] public float evasion = 0.15f;
        [Min(0.1f)] public float trapLifetime = 8f;
        [Min(0.1f)] public float trapTriggerRadius = 0.8f;
        [Min(0.1f)] public float knockupDuration = 0.7f;
        [Min(0)] public float knockupHeight = 0.7f;
        [Header("Oracle Witch")]
        [Min(0.1f)] public float orbSpeed = 4f;
        [Min(0.1f)] public float orbLifetime = 3f;
        [Range(0, 0.95f)] public float slow = 0.6f;
        [Header("Daphne")]
        [Min(0)] public float lunarDamage = 35f;
        [Min(0)] public float burnPerSecond = 5f;
        [Min(0.1f)] public float burnDuration = 3f;
        [Min(0)] public float stanceCooldown = 1f;
        [Range(0, 1)] public float transitionBuff = 0.2f;
        [Min(0)] public float knockbackDistance = 1.5f;
        [Min(0)] public float healing = 30f;

        public string RoleName => role == HeroRole.OracleWitch ? "Oracle Witch" : role.ToString();
    }
}
