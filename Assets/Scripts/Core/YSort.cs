using UnityEngine;

namespace MagicGame
{
    // Characters lower on screen draw in front of those higher up.
    [RequireComponent(typeof(SpriteRenderer))]
    public class YSort : MonoBehaviour
    {
        public int offset;

        SpriteRenderer _renderer;

        void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        void LateUpdate()
        {
            _renderer.sortingOrder = offset - Mathf.RoundToInt(transform.position.y * 100f);
        }
    }
}
