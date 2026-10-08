using UnityEngine;

namespace MagicGame
{
    // Warp to the next level. Stays closed until every enemy in the level is defeated.
    [RequireComponent(typeof(Collider2D))]
    public class Portal : MonoBehaviour
    {
        public SpriteRenderer ringRenderer;
        public SpriteRenderer glowRenderer;
        public Color openColor = new Color(0.4f, 0.9f, 1f, 1f);
        public Color closedColor = new Color(0.4f, 0.4f, 0.5f, 0.35f);

        public bool IsOpen { get; private set; }

        Collider2D _trigger;
        bool _used;

        void Awake()
        {
            _trigger = GetComponent<Collider2D>();
            SetOpen(false);
        }

        public void SetOpen(bool open)
        {
            IsOpen = open;
            _trigger.enabled = open;
            if (ringRenderer != null) ringRenderer.color = open ? openColor : closedColor;
            if (glowRenderer != null) glowRenderer.enabled = open;
        }

        void Update()
        {
            if (ringRenderer != null) ringRenderer.transform.Rotate(0f, 0f, (IsOpen ? 120f : 20f) * Time.deltaTime);
            if (IsOpen && glowRenderer != null)
            {
                float pulse = 0.85f + Mathf.Sin(Time.time * 4f) * 0.15f;
                glowRenderer.transform.localScale = Vector3.one * pulse;
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (_used || !IsOpen) return;
            if (other.GetComponentInParent<PlayerController>() == null) return;
            _used = true;
            LevelManager.Instance.CompleteLevel();
        }
    }
}
