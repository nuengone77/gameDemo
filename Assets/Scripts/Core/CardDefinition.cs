using UnityEngine;

namespace MagicGame
{
    public enum CardEffect
    {
        MaxHp, Damage, AttackSpeed, Armor, ActiveCooldown, UltimateCooldown,
        Movement, Radius, SkillDamage, ActiveShield, Tanky, Shadow, Magician,
        Oracle, Daphne, Duration, Fortress, Dragon
    }

    [CreateAssetMenu(menuName = "Magic Game/Progression Card")]
    public class CardDefinition : ScriptableObject
    {
        public string id;
        public string title;
        [TextArea] public string description;
        public CardEffect effect;
        public float amount;
        [Tooltip("Zero means evergreen, with diminishing returns.")]
        public int maxLevel = 3;
        [Tooltip("Empty means usable by every hero.")]
        public string hero;
        [Tooltip("Zero means available from the start; otherwise unlock by clearing this boss stage.")]
        public int unlockStage;
        public bool Evergreen => maxLevel == 0;
    }
}
