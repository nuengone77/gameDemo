using UnityEngine;

namespace MagicGame
{
    public class ShieldWall : MonoBehaviour
    {
        public PlayerSkills owner;
        public Vector2 forward;
        float _until;
        CharacterCombatConfig _config;

        public void Initialize(PlayerSkills owner, Vector2 forward, CharacterCombatConfig config)
        {
            this.owner = owner;
            this.forward = forward.normalized;
            _config = config;
            _until = Time.time + config.duration;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg);
            var box = gameObject.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(0.25f, config.wallWidth);
            if (config.activeVfx.IsSet)
            {
                // The effect lives on the wall, so it disappears together with it.
                SkillVfx.Spawn(config.activeVfx, transform.position, 0f, transform);
                return;
            }
            Vector2 tangent = new Vector2(-forward.y, forward.x) * config.wallWidth / 2f;
            var visual = SkillVfx.Line((Vector2)transform.position - tangent, (Vector2)transform.position + tangent,
                new Color(0.4f, 0.9f, 1f), 0f, 0.2f);
            visual.transform.SetParent(transform, true);
        }

        public bool CanBlock(Projectile projectile) => owner != null && owner.CanAct &&
            projectile.OwnerTeam != owner.Health.team && !projectile.bypassObstacles &&
            Vector2.Dot(projectile.Velocity, forward) < 0f;

        void Update()
        {
            if (GameUI.GameplayBlocked) return;
            if (owner == null || !owner.CanAct || Time.time >= _until) { Destroy(gameObject); return; }
            Vector2 relative = (Vector2)owner.transform.position - (Vector2)transform.position;
            float behind = Vector2.Dot(relative, forward);
            float lateral = Mathf.Abs(Vector2.Dot(relative, new Vector2(-forward.y, forward.x)));
            if (behind <= 0f && behind >= -_config.range && lateral <= _config.wallWidth * 0.5f)
                owner.Health.Status.Apply(StatusKind.Defense, _config.wallDefense, 0.15f, this);
            else owner.Health.Status.Remove(StatusKind.Defense, this);
        }
        void OnDestroy()
        {
            if (owner != null && owner.Health != null) owner.Health.Status.Remove(StatusKind.Defense, this);
        }
    }
}
