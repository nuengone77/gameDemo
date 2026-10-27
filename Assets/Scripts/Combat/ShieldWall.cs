using UnityEngine;

namespace MagicGame
{
    public class ShieldWall : MonoBehaviour
    {
        public PlayerSkills owner;
        public Vector2 forward;
        float _until, _start;
        CharacterCombatConfig _config;
        SpriteRenderer _sprite;
        Vector3 _spriteScale;

        const float RiseTime = 0.18f, FadeTime = 0.3f;

        public void Initialize(PlayerSkills owner, Vector2 forward, CharacterCombatConfig config)
        {
            this.owner = owner;
            this.forward = forward.normalized;
            _config = config;
            _start = Time.time;
            _until = Time.time + config.duration;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg);
            var box = gameObject.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(0.25f, config.wallWidth);
            if (config.shieldWallSprite != null)
            {
                BuildSprite(config.shieldWallSprite);
                return;
            }
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

        // The wall art is a row of shields seen from the front: its width lies along the wall and its top
        // always points back toward the hero.
        void BuildSprite(Sprite art)
        {
            var visual = new GameObject("WallArt");
            visual.transform.SetParent(transform, false);
            Vector2 up = -forward;
            visual.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(up.y, up.x) * Mathf.Rad2Deg - 90f);
            _sprite = visual.AddComponent<SpriteRenderer>();
            _sprite.sprite = art;
            // In front of characters standing behind the wall, behind those in front of it.
            _sprite.sortingOrder = -Mathf.RoundToInt(transform.position.y * 100f);
            float scale = _config.wallWidth / Mathf.Max(0.01f, art.bounds.size.x);
            _spriteScale = new Vector3(scale, scale, 1f);
            visual.transform.localScale = new Vector3(scale, 0f, 1f);
        }

        // Rises out of the ground when cast and fades as it expires.
        void AnimateSprite()
        {
            if (_sprite == null) return;
            float rise = Mathf.Clamp01((Time.time - _start) / RiseTime);
            float grow = 1f + Mathf.Sin(rise * Mathf.PI) * 0.08f;
            _sprite.transform.localScale = new Vector3(_spriteScale.x * grow, _spriteScale.y * rise * grow, 1f);
            var color = _sprite.color;
            color.a = Mathf.Clamp01((_until - Time.time) / FadeTime);
            _sprite.color = color;
        }

        public bool CanBlock(Projectile projectile) => owner != null && owner.CanAct &&
            projectile.OwnerTeam != owner.Health.team && !projectile.bypassObstacles &&
            Vector2.Dot(projectile.Velocity, forward) < 0f;

        void Update()
        {
            if (GameUI.GameplayBlocked) return;
            if (owner == null || !owner.CanAct || Time.time >= _until) { Destroy(gameObject); return; }
            AnimateSprite();
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
