using UnityEngine;

namespace MagicGame
{
    // Sheet rows are laid out top-to-bottom as Down, Up, Left, Right.
    public enum FacingDir { Down = 0, Up = 1, Left = 2, Right = 3 }

    [CreateAssetMenu(menuName = "Magic Game/Character Anim Set")]
    public class CharacterAnimSet : ScriptableObject
    {
        public string displayName;

        [Header("Walk (played left to right while moving)")]
        public Sprite[] walkDown;
        public Sprite[] walkUp;
        public Sprite[] walkLeft;
        public Sprite[] walkRight;

        [Header("Stand (shown when stopped; empty = hold the last walk frame)")]
        public Sprite[] standDown;
        public Sprite[] standUp;
        public Sprite[] standLeft;
        public Sprite[] standRight;

        [Header("Attack")]
        [Tooltip("Projectile this character shoots (players). Empty = the player's default magic ring.")]
        public Projectile attackProjectile;

        public float walkFps = 10f;
        public float standFps = 6f;

        public Sprite[] GetWalk(FacingDir dir) => dir switch
        {
            FacingDir.Up => walkUp,
            FacingDir.Left => walkLeft,
            FacingDir.Right => walkRight,
            _ => walkDown,
        };

        public Sprite[] GetStand(FacingDir dir) => dir switch
        {
            FacingDir.Up => standUp,
            FacingDir.Left => standLeft,
            FacingDir.Right => standRight,
            _ => standDown,
        };

        public Sprite PreviewSprite
        {
            get
            {
                if (standDown != null && standDown.Length > 0) return standDown[0];
                if (walkDown != null && walkDown.Length > 0) return walkDown[0];
                return null;
            }
        }
    }
}
