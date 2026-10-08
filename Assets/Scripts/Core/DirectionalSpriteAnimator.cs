using UnityEngine;

namespace MagicGame
{
    // Plays walk frames for the current facing while moving. When stopped it shows the
    // stand frames for the last facing, or holds the last walk frame if the set has no stand.
    [RequireComponent(typeof(SpriteRenderer))]
    public class DirectionalSpriteAnimator : MonoBehaviour
    {
        public CharacterAnimSet animSet;
        public FacingDir facing = FacingDir.Down;

        SpriteRenderer _renderer;
        Sprite[] _clip;
        int _frame;
        float _timer;
        bool _moving;

        public FacingDir Facing => facing;

        public Vector2 FacingVector => facing switch
        {
            FacingDir.Up => Vector2.up,
            FacingDir.Left => Vector2.left,
            FacingDir.Right => Vector2.right,
            _ => Vector2.down,
        };

        void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        public void SetAnimSet(CharacterAnimSet set)
        {
            animSet = set;
            _clip = null;
            Refresh(0f);
        }

        public void SetMovement(Vector2 velocity)
        {
            _moving = velocity.sqrMagnitude > 0.0004f;
            if (_moving) facing = DirFromVector(velocity);
        }

        public void FaceTowards(Vector2 direction)
        {
            if (direction.sqrMagnitude > 0.0001f) facing = DirFromVector(direction);
        }

        public static FacingDir DirFromVector(Vector2 v)
        {
            if (Mathf.Abs(v.x) > Mathf.Abs(v.y)) return v.x < 0 ? FacingDir.Left : FacingDir.Right;
            return v.y < 0 ? FacingDir.Down : FacingDir.Up;
        }

        void Update()
        {
            Refresh(Time.deltaTime);
        }

        void Refresh(float dt)
        {
            if (animSet == null || _renderer == null) return;

            var walk = animSet.GetWalk(facing);
            var stand = animSet.GetStand(facing);
            bool hasWalk = walk != null && walk.Length > 0;
            bool hasStand = stand != null && stand.Length > 0;

            Sprite[] clip;
            float fps;
            if (_moving && hasWalk) { clip = walk; fps = animSet.walkFps; }
            else if (hasStand) { clip = stand; fps = animSet.standFps; }
            else if (hasWalk)
            {
                // No stand frames: freeze on the frame we stopped on (same index in the new facing).
                if (_clip != walk) _frame = Mathf.Clamp(_frame, 0, walk.Length - 1);
                _clip = walk;
                _renderer.sprite = walk[Mathf.Clamp(_frame, 0, walk.Length - 1)];
                return;
            }
            else return;

            if (clip != _clip)
            {
                _clip = clip;
                _frame = 0;
                _timer = 0f;
            }
            else if (clip.Length > 1 && fps > 0f)
            {
                _timer += dt;
                float step = 1f / fps;
                while (_timer >= step)
                {
                    _timer -= step;
                    _frame = (_frame + 1) % clip.Length;
                }
            }

            _renderer.sprite = clip[Mathf.Clamp(_frame, 0, clip.Length - 1)];
        }
    }
}
