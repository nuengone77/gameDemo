using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicGame
{
    [CreateAssetMenu(menuName = "Magic Game/Progression Config")]
    public class ProgressionConfig : ScriptableObject
    {
        public List<CardDefinition> cards = new();
        public float enemyHpGrowth = 1.55f, enemyDamageGrowth = 1.2f;
        public float enemySpeedPerLoop = 0.04f, enemySpeedCap = 0.24f;
        public float enemyCooldownPerLoop = 0.03f, enemyCooldownCap = 0.25f;
        public float eliteFractionPerLoop = 0.1f, eliteFractionCap = 0.35f;
        public float eliteHp = 1.6f, eliteDamage = 1.2f, eliteSpeed = 1.1f;
        public int normalReward = 5, witchReward = 15, golemReward = 20, dragonReward = 30, loopReward = 40;
        public float rewardGrowth = 0.5f;
        public int rebirthBaseCost = 30, rebirthLinearCost = 10, rebirthQuadraticCost = 2;
        public float rebirthHp = 0.03f, rebirthDamage = 0.02f, rebirthArmor = 2f;

        static ProgressionConfig _current;
        public static ProgressionConfig Current
        {
            get
            {
                if (_current != null) return _current;
                _current = Resources.Load<ProgressionConfig>("Progression/ProgressionConfig");
                if (_current == null)
                {
                    _current = CreateInstance<ProgressionConfig>();
                    _current.hideFlags = HideFlags.DontSave;
                    _current.cards = CreateDefaultCards();
                }
                return _current;
            }
        }

        public CardDefinition Find(string id) => cards.Find(c => c != null && c.id == id);

        // Use bounded arithmetic at the numeric limits, without imposing a gameplay loop limit.
        public static float Safe(float value) => float.IsNaN(value) ? 0f : Mathf.Clamp(value, -1e25f, 1e25f);
        public static long SafePoints(double value) => (long)Math.Min(1e15, Math.Max(0, value));
        public long RebirthCost(int count) => SafePoints(rebirthBaseCost + (double)rebirthLinearCost * count + (double)rebirthQuadraticCost * count * count);
        public long StageReward(int stage, int loop)
        {
            int reward = stage == 3 ? witchReward : stage == 5 ? golemReward : stage == 7 ? dragonReward : normalReward;
            double multiplier = 1d + rewardGrowth * Math.Max(0, loop - 1);
            return SafePoints(Math.Floor(reward * multiplier) + (stage == 7 ? Math.Floor(loopReward * multiplier) : 0));
        }

        public static List<CardDefinition> CreateDefaultCards()
        {
            var result = new List<CardDefinition>();
            void Add(string id, string title, string body, CardEffect effect, float amount, int cap = 3, string hero = "", int unlock = 0)
            {
                var card = CreateInstance<CardDefinition>();
                card.name = card.id = id;
                card.title = title;
                card.description = body;
                card.effect = effect;
                card.amount = amount;
                card.maxLevel = cap;
                card.hero = hero;
                card.unlockStage = unlock;
                result.Add(card);
            }
            Add("vitality", "Vitality", "+10% maximum HP. Repeats grant less; no level limit.", CardEffect.MaxHp, .1f, 0);
            Add("power", "Arcane Power", "+8% all damage. Repeats grant less; no level limit.", CardEffect.Damage, .08f, 0);
            Add("haste", "Quick Hands", "+6% attack speed. Repeats grant less; combat speed limits apply.", CardEffect.AttackSpeed, .06f, 0);
            Add("armor", "Warding", "+8 physical and magic armor. Repeats grant less; no level limit.", CardEffect.Armor, 8f, 0);
            Add("active", "Spell Rhythm", "Active cooldown -5% per level.", CardEffect.ActiveCooldown, .05f);
            Add("ultimate", "Astral Rhythm", "Ultimate cooldown -5% per level.", CardEffect.UltimateCooldown, .05f);
            Add("movement", "Fleet Foot", "+6% movement speed per level.", CardEffect.Movement, .06f);
            Add("radius", "Wide Magic", "+10% area radius per level.", CardEffect.Radius, .1f);
            Add("skill", "Spellcraft", "+12% active and ultimate damage per level.", CardEffect.SkillDamage, .12f);
            Add("shield", "Spellguard", "Successful active casts grant 8% maximum HP as shield per level. Card shield capacity: 24% maximum HP.", CardEffect.ActiveShield, .08f);
            Add("tanky", "Astral Reservoir", "+20% shield capacity and +0.5 percentage points damage-to-shield conversion per level.", CardEffect.Tanky, .2f, 3, "New");
            Add("shadow", "Endless Pursuit", "+2 maximum hit stacks and +0.3s stack timeout per level.", CardEffect.Shadow, 2f, 3, "Nueng");
            Add("magician", "Extra Trick", "Basic cards bounce to one additional target per level.", CardEffect.Magician, 1f, 2, "Peng");
            Add("oracle", "Frozen Hour", "+20% slow-field duration per level.", CardEffect.Oracle, .2f, 3, "Ling");
            Add("daphne", "Celestial Renewal", "+15% ultimate healing and -0.1s stance cooldown per level.", CardEffect.Daphne, .15f, 3, "Nae");
            Add("duration", "Witch's Hourglass", "+20% skill effect duration per level. Unlocked by the Frost Witch.", CardEffect.Duration, .2f, 3, "", 3);
            Add("fortress", "Golem's Bargain", "+15 physical and magic armor, but -3% movement speed per level.", CardEffect.Fortress, 15f, 3, "", 5);
            Add("dragon", "Dragon's Bargain", "+15% all damage, but -5% maximum HP per level.", CardEffect.Dragon, .15f, 3, "", 7);
            return result;
        }
    }
}
