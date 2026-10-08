using UnityEngine;

namespace MagicGame
{
    // Small bar floating above a character; builds its own sprites at runtime.
    [RequireComponent(typeof(Health))]
    public class WorldHealthBar : MonoBehaviour
    {
        public float width = 1f;
        public float height = 0.1f;
        public float heightOffset = 1.6f;
        [Tooltip("Place the bar just above the sprite's top, so it follows sprite size changes.")]
        public bool autoHeight = true;
        public float gapAboveHead = 0.08f;
        public Color fillColor = new Color(0.9f, 0.2f, 0.2f);

        static Sprite s_pixel;

        Health _health;
        Transform _fill;

        void Start()
        {
            _health = GetComponent<Health>();
            if (s_pixel == null)
            {
                var tex = Texture2D.whiteTexture;
                s_pixel = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0f, 0.5f), tex.width);
            }

            var sr = GetComponent<SpriteRenderer>();
            if (autoHeight && sr != null && sr.sprite != null)
                heightOffset = sr.sprite.bounds.max.y + gapAboveHead;

            var root = new GameObject("HealthBar").transform;
            root.SetParent(transform, false);
            root.localPosition = new Vector3(-width / 2f, heightOffset, 0f);

            CreatePart(root, "Back", new Color(0f, 0f, 0f, 0.7f), 30000);
            _fill = CreatePart(root, "Fill", fillColor, 30001);
        }

        Transform CreatePart(Transform parent, string name, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(width, height, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s_pixel;
            sr.color = color;
            sr.sortingOrder = order;
            return go.transform;
        }

        void LateUpdate()
        {
            if (_fill == null) return;
            _fill.localScale = new Vector3(width * _health.Ratio, height, 1f);
        }
    }
}
