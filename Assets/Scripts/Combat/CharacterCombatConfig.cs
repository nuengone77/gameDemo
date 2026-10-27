using UnityEngine;

namespace MagicGame
{
    public enum HeroRole { Tanky, Shadow, Magician, OracleWitch, Daphne }

    // An optional effect prefab (particles etc.) shown by a skill. Empty = the simple placeholder lines.
    [System.Serializable]
    public class VfxSlot
    {
        public GameObject prefab;
        [Tooltip("Uniform scale; third-party packs are authored for ~2 m tall 3D characters.")]
        public float scale = 1f;
        [Tooltip("Rotation around X, used to lay ground effects into the 3/4 view.")]
        public float tilt;
        public float yOffset;
        [Tooltip("Seconds before the effect stops emitting and fades (0 = until its owner is destroyed).")]
        public float seconds;
        [Tooltip("Play the effect a single time even if the prefab is authored to loop (one-shot hits).")]
        public bool playOnce;
        [Tooltip("Make every part loop (for one-shot bursts that should keep pulsing while a skill lasts).")]
        public bool loopAll;
        [Tooltip("Seconds from spawn until the effect visibly lands; skills spawn it early by this much so the hit lines up with the damage.")]
        public float impactTime;
        public bool IsSet => prefab != null;
    }

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
        [Tooltip("Eclipse leaves a star field that keeps hitting enemies inside it (light damage every Tick Interval).")]
        [Min(0)] public float eclipseFieldDamage = 4f;
        [Min(0.1f)] public float eclipseFieldRadius = 2.3f;
        [Min(0)] public float eclipseFieldDuration = 3f;

        [Header("Effects (optional prefabs; empty = placeholder lines)")]
        public VfxSlot activeVfx = new();
        public VfxSlot activeExtraVfx = new();
        public VfxSlot ultimateVfx = new();
        public VfxSlot ultimateExtraVfx = new();
        [Tooltip("Daphne: burst on the target hit by Moonlight Bind.")]
        public VfxSlot lunarHitVfx = new();
        [Tooltip("Explosion shown where an exploding basic attack lands (Oracle orb, Daphne Solar).")]
        public VfxSlot basicHitVfx = new();
        [Tooltip("Tanky: the meteor that drops from the sky onto the Meteor Binding target.")]
        public VfxSlot ultimateFallVfx = new();
        public Vector2 ultimateFallOffset = new(-1.5f, 6f);
        [Tooltip("Burst on every enemy hit by the ultimate (e.g. Daphne's Eclipse).")]
        public VfxSlot ultimateHitVfx = new();

        [Header("Daphne: Eclipse (a sun and a moon orb merge, then a ring sweeps out over the screen)")]
        public VfxSlot eclipseSunVfx = new();
        public VfxSlot eclipseMoonVfx = new();
        [Tooltip("Ring that grows from the merge point; every enemy on the map is hit as it reaches them.")]
        public VfxSlot eclipseWaveVfx = new();
        [Min(0)] public float eclipseMergeTime = 1.2f;
        [Tooltip("How far left/right of Daphne the two orbs start.")]
        [Min(0)] public float eclipseOrbSpread = 2.5f;
        [Tooltip("World units per second the ring grows; it keeps growing until it has left the whole map.")]
        [Min(0.1f)] public float eclipseWaveSpeed = 9f;
        [Tooltip("The wave prefab grows with the ring up to this radius, then fades there (larger, its sparks smear into streaks).")]
        [Min(0.1f)] public float eclipseWaveVfxMaxRadius = 3f;
        [Tooltip("Solid edge drawn exactly on the damage radius, all the way off the map.")]
        public Color eclipseWaveEdge = new(0.42f, 0.1f, 0.85f, 0.95f);
        [Tooltip("Radius of the wave prefab's ring at scale 1 (sizes it to match the damage radius).")]
        public float eclipseWaveRingRadius = 0.72f;

        public string RoleName => role == HeroRole.OracleWitch ? "Oracle Witch" : role.ToString();
    }
}
