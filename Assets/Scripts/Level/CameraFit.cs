using UnityEngine;

namespace MagicGame
{
    // Zooms so the map fills the whole screen (no black bars), then follows the player
    // and clamps to the map edges so the parts cropped off-screen can still be reached.
    [RequireComponent(typeof(Camera))]
    public class CameraFit : MonoBehaviour
    {
        public Vector2 mapSize = new Vector2(15.36f, 10.24f);
        public Vector2 mapCenter = Vector2.zero;
        [Tooltip("Fill the screen (crops the map edges). Off = show the whole map with black bars.")]
        public bool fillScreen = true;
        public bool followPlayer = true;
        public float followSmoothing = 8f;

        Camera _camera;

        void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        void Start()
        {
            UpdateSize();
            Vector3 p = transform.position;
            if (followPlayer && PlayerController.Instance != null) p = PlayerController.Instance.transform.position;
            transform.position = Clamp(p);
        }

        void LateUpdate()
        {
            UpdateSize();

            Vector3 target = mapCenter;
            if (followPlayer && PlayerController.Instance != null) target = PlayerController.Instance.transform.position;
            target = Clamp(target);
            float t = followSmoothing > 0f ? 1f - Mathf.Exp(-followSmoothing * Time.deltaTime) : 1f;
            transform.position = Vector3.Lerp(transform.position, target, t);
        }

        void UpdateSize()
        {
            float aspect = Mathf.Max(0.1f, _camera.aspect);
            float fitHeight = mapSize.y / 2f, fitWidth = mapSize.x / 2f / aspect;
            _camera.orthographicSize = fillScreen ? Mathf.Min(fitHeight, fitWidth) : Mathf.Max(fitHeight, fitWidth);
        }

        Vector3 Clamp(Vector3 p)
        {
            float halfH = _camera.orthographicSize, halfW = halfH * _camera.aspect;
            float rangeX = Mathf.Max(0f, mapSize.x / 2f - halfW), rangeY = Mathf.Max(0f, mapSize.y / 2f - halfH);
            return new Vector3(
                Mathf.Clamp(p.x, mapCenter.x - rangeX, mapCenter.x + rangeX),
                Mathf.Clamp(p.y, mapCenter.y - rangeY, mapCenter.y + rangeY),
                transform.position.z);
        }
    }
}
