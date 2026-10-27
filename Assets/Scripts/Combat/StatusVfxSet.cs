using UnityEngine;

namespace MagicGame
{
    // Effects shown on a character while it has a status. Lives at Resources/StatusVfx so every
    // StatusEffects component can find it; missing entries fall back to the coloured ring.
    [CreateAssetMenu(menuName = "Magic Game/Status Vfx Set")]
    public class StatusVfxSet : ScriptableObject
    {
        [Tooltip("Rooted, knocked up or otherwise unable to act (shown above the head).")]
        public VfxSlot disabled = new();
        [Tooltip("Burning (damage over time).")]
        public VfxSlot burn = new();
        [Tooltip("Enemy slowed or weakened.")]
        public VfxSlot slow = new();
        [Tooltip("Player buffs: defense, attack, attack speed, movement.")]
        public VfxSlot buff = new();
        [Tooltip("Always on around elite enemies (tougher monsters that appear from loop 2).")]
        public VfxSlot elite = new();

        static StatusVfxSet s_instance;
        static bool s_loaded;

        public static StatusVfxSet Instance
        {
            get
            {
                if (!s_loaded)
                {
                    s_instance = Resources.Load<StatusVfxSet>("StatusVfx");
                    s_loaded = true;
                }
                return s_instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => s_loaded = false;
    }
}
