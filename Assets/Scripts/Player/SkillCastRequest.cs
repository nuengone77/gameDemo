using UnityEngine;

namespace MagicGame
{
    public enum SkillSlot { Active, Ultimate }
    public enum SkillTargetingMode { None, Direction, Ground, Dash }

    // A release captures its own aim; moving the attack stick cannot change this cast.
    public readonly struct SkillCastRequest
    {
        public readonly SkillSlot slot;
        public readonly Vector2 direction, target;
        public readonly PlayerSkills source;
        public readonly int revision;

        public SkillCastRequest(SkillSlot slot, Vector2 direction, Vector2 target, PlayerSkills source)
        {
            this.slot = slot;
            this.direction = direction;
            this.target = target;
            this.source = source;
            revision = source.TargetingRevision;
        }
    }
}
