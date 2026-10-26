using UnityEngine;

namespace MagicGame
{
    // Optional, bounded procedural feedback. Nothing in combat waits for a visual.
    public class SkillVfx : MonoBehaviour
    {
        static Material s_material;
        static int s_transientCount;
        float _life, _age;
        bool _counted;
        LineRenderer _line;
        Color _color;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCount() => s_transientCount = 0;

        public static GameObject Ring(Vector2 point, float radius, Color color, float seconds = 0.35f)
        {
            var go = Create(point, color, seconds);
            if (go == null) return null;
            var line = go.GetComponent<LineRenderer>();
            line.loop = true;
            line.positionCount = 40;
            for (int i = 0; i < 40; i++)
            {
                float angle = Mathf.PI * 2f * i / 40f;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
            return go;
        }

        public static GameObject Line(Vector2 from, Vector2 to, Color color, float seconds = 0.2f, float width = 0.12f)
        {
            var go = Create(from, color, seconds);
            if (go == null) return null;
            var line = go.GetComponent<LineRenderer>();
            line.positionCount = 2;
            line.SetPosition(0, Vector3.zero);
            line.SetPosition(1, to - from);
            line.startWidth = line.endWidth = width;
            return go;
        }

        public static void Burst(Vector2 point, Color color, float radius = 1f) => Ring(point, radius, color);

        public static void AddTrail(GameObject owner, Color color)
        {
            if (s_material == null) s_material = new Material(Shader.Find("Sprites/Default"));
            var trail = owner.AddComponent<TrailRenderer>();
            trail.sharedMaterial = s_material;
            trail.time = 0.15f;
            trail.minVertexDistance = 0.05f;
            trail.startWidth = 0.12f;
            trail.endWidth = 0f;
            trail.startColor = color;
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
            trail.sortingOrder = 24000;
        }

        static GameObject Create(Vector2 point, Color color, float seconds)
        {
            if (seconds > 0f && s_transientCount >= 96) return null;
            var go = new GameObject("SkillPlaceholder");
            go.transform.position = point;
            var line = go.AddComponent<LineRenderer>();
            if (s_material == null) s_material = new Material(Shader.Find("Sprites/Default"));
            line.sharedMaterial = s_material;
            line.useWorldSpace = false;
            line.startWidth = line.endWidth = 0.06f;
            line.startColor = line.endColor = color;
            line.sortingOrder = 25000;
            var visual = go.AddComponent<SkillVfx>();
            visual._line = line;
            visual._color = color;
            visual._life = seconds;
            visual._counted = seconds > 0f;
            if (visual._counted) s_transientCount++;
            return go;
        }

        void Update()
        {
            if (_life <= 0f) return;
            _age += Time.deltaTime;
            var color = _color;
            color.a *= 1f - Mathf.Clamp01(_age / _life);
            _line.startColor = _line.endColor = color;
            if (_age >= _life) Destroy(gameObject);
        }
        void OnDestroy()
        {
            if (_counted) s_transientCount = Mathf.Max(0, s_transientCount - 1);
        }
    }
}
