using UnityEngine;

namespace MagicGame
{
    // Rebuild a private config from its source, never from an already-modified value.
    public class PlayerProgression : MonoBehaviour
    {
        PlayerController _player;
        CharacterCombatConfig _source, _runtime;
        float _basePhysical, _baseMagic;
        string _hero;
        public float AttackSpeedBonus { get; private set; }
        public float ActiveShield { get; private set; }

        public void Initialize(PlayerController player, CharacterCombatConfig source)
        {
            _player = player;
            _source = source;
            _hero = player.CurrentCharacter.displayName;
            _basePhysical = player.Health.physicalDefense;
            _baseMagic = player.Health.magicDefense;
            _runtime = Instantiate(source);
            _runtime.hideFlags = HideFlags.DontSave;
            Apply(true);
        }

        public void Apply(bool refill = false)
        {
            if (_source == null) return;
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(_source), _runtime);
            var permanent = Progression.Hero(_hero);
            float hp = permanent.hpBonus, damage = permanent.damageBonus, armor = permanent.armorBonus;
            float movement = 0, skillDamage = 0, radius = 0, duration = 0, activeCd = 0, ultimateCd = 0;
            AttackSpeedBonus = ActiveShield = 0;
            var run = Progression.Run;
            if (run != null && run.hero == _hero)
            {
                foreach (var stack in run.cards)
                {
                    var card = ProgressionConfig.Current.Find(stack.id);
                    if (card == null) continue;
                    float value = card.amount * Progression.Strength(run, card);
                    switch (card.effect)
                    {
                        case CardEffect.MaxHp: hp += value; break;
                        case CardEffect.Damage: damage += value; break;
                        case CardEffect.AttackSpeed: AttackSpeedBonus += value; break;
                        case CardEffect.Armor: armor += value; break;
                        case CardEffect.ActiveCooldown: activeCd += value; break;
                        case CardEffect.UltimateCooldown: ultimateCd += value; break;
                        case CardEffect.Movement: movement += value; break;
                        case CardEffect.Radius: radius += value; break;
                        case CardEffect.SkillDamage: skillDamage += value; break;
                        case CardEffect.ActiveShield: ActiveShield += value; break;
                        case CardEffect.Duration: duration += value; break;
                        case CardEffect.Fortress: armor += value; movement -= .03f * stack.level; break;
                        case CardEffect.Dragon: damage += value; hp -= .05f * stack.level; break;
                        case CardEffect.Tanky:
                            _runtime.shieldCapacity = _source.shieldCapacity * (1 + value);
                            _runtime.conversion += .005f * stack.level;
                            break;
                        case CardEffect.Shadow:
                            _runtime.maxStacks += Mathf.RoundToInt(value);
                            _runtime.stackTimeout += .3f * stack.level;
                            break;
                        case CardEffect.Magician: _runtime.bounces += Mathf.RoundToInt(value); break;
                        case CardEffect.Oracle: duration += value; break;
                        case CardEffect.Daphne:
                            _runtime.healing *= 1 + value;
                            _runtime.stanceCooldown = Mathf.Max(.3f, _runtime.stanceCooldown - .1f * stack.level);
                            break;
                    }
                }
            }
            _runtime.maxHp = Mathf.Max(1, ProgressionConfig.Safe(_source.maxHp * (1 + hp)));
            _runtime.moveSpeed = Mathf.Max(.1f, _source.moveSpeed * (1 + movement));
            float allDamage = 1 + damage;
            _runtime.basicDamage = ProgressionConfig.Safe(_source.basicDamage * allDamage);
            _runtime.lunarDamage = ProgressionConfig.Safe(_source.lunarDamage * allDamage);
            _runtime.burnPerSecond = ProgressionConfig.Safe(_source.burnPerSecond * allDamage);
            _runtime.activeDamage = ProgressionConfig.Safe(_source.activeDamage * allDamage * (1 + skillDamage));
            _runtime.ultimateDamage = ProgressionConfig.Safe(_source.ultimateDamage * allDamage * (1 + skillDamage));
            _runtime.activeCooldown = _source.activeCooldown * (1 - Mathf.Clamp(activeCd, 0, .75f));
            _runtime.ultimateCooldown = _source.ultimateCooldown * (1 - Mathf.Clamp(ultimateCd, 0, .75f));
            _runtime.radius = _source.radius * (1 + radius);
            _runtime.trapTriggerRadius = _source.trapTriggerRadius * (1 + radius);
            _runtime.duration = _source.duration * (1 + duration);
            _runtime.rootDuration = _source.rootDuration * (1 + duration);
            _runtime.burnDuration = _source.burnDuration * (1 + duration);
            _runtime.knockupDuration = _source.knockupDuration * (1 + duration);
            _runtime.trapLifetime = _source.trapLifetime * (1 + duration);
            var health = _player.Health;
            float ratio = health.Ratio;
            health.maxHp = _runtime.maxHp;
            health.physicalDefense = ProgressionConfig.Safe(_basePhysical + armor);
            health.magicDefense = ProgressionConfig.Safe(_baseMagic + armor);
            health.SetCurrentHp(refill ? health.maxHp : health.maxHp * ratio);
            _player.moveSpeed = _runtime.moveSpeed;
            _player.Skills.Initialize(_player, _runtime);
            health.ShieldCapacity = (_runtime.role == HeroRole.Tanky ? _runtime.shieldCapacity : 0) +
                (ActiveShield > 0 ? .24f * health.maxHp : 0);
        }

        void OnDestroy()
        {
            if (_runtime != null) Destroy(_runtime);
        }
    }
}
