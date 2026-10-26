using System.Linq;
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

        // Instantiates an effect prefab at a point, scaled/tilted for the 2D view and drawn above the map.
        // seconds > 0: stops emitting after that time and is destroyed once its particles fade.
        // parent != null: follows/dies with the parent (e.g. a skill area or wall).
        public static GameObject Spawn(VfxSlot slot, Vector2 point, float seconds = -1f, Transform parent = null, int sortingOrder = 24000)
        {
            if (slot == null || !slot.IsSet) return null;
            var root = new GameObject("SkillEffect");
            root.transform.SetPositionAndRotation(point + Vector2.up * slot.yOffset, Quaternion.Euler(slot.tilt, 0f, 0f));
            if (parent != null) root.transform.SetParent(parent, true);
            root.transform.localScale = Vector3.one * Mathf.Max(0.01f, slot.scale);
            var effect = Object.Instantiate(slot.prefab, root.transform, false);
            effect.transform.localPosition = Vector3.zero;

            foreach (var ps in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                if (slot.playOnce) main.loop = false;
                else if (slot.loopAll) main.loop = true;
            }
            foreach (var renderer in effect.GetComponentsInChildren<Renderer>(true))
                renderer.sortingOrder = sortingOrder;

            float life = seconds >= 0f ? seconds : slot.seconds;
            if (life > 0f) root.AddComponent<VfxLifetime>().Begin(life);
            return root;
        }

        // Spawns an effect prefab at `from` and moves it to `to` over `seconds` (accelerating like a fall),
        // then stops it emitting so its trail fades out where it landed.
        public static GameObject Drop(VfxSlot slot, Vector2 from, Vector2 to, float seconds)
        {
            var go = Spawn(slot, from, 0f);
            if (go == null) return null;
            go.AddComponent<VfxMover>().Begin(from + Vector2.up * slot.yOffset, to + Vector2.up * slot.yOffset, seconds);
            return go;
        }

        // A sprite that drops from `from` to `to` in `fallSeconds` (playing frames in order), stays stuck
        // for `stickSeconds` and fades out. Purely visual: damage is applied by the caller.
        public static GameObject Falling(Sprite[] frames, Vector2 from, Vector2 to, float fallSeconds, float stickSeconds,
            float rotation, int sortingOrder)
        {
            if (frames == null || frames.Length == 0) return null;
            var go = new GameObject("FallingSprite");
            go.transform.SetPositionAndRotation(from, Quaternion.Euler(0f, 0f, rotation));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = frames[0];
            sr.sortingOrder = sortingOrder;
            go.AddComponent<FallingSprite>().Begin(frames, from, to, fallSeconds, stickSeconds);
            return go;
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

    // Stops a looping effect after its time and removes it when the last particles have faded.
    public class VfxLifetime : MonoBehaviour
    {
        float _stopAt;
        bool _stopped;

        public void Begin(float seconds) => _stopAt = Time.time + seconds;

        void Update()
        {
            if (!_stopped && Time.time >= _stopAt)
            {
                _stopped = true;
                foreach (var ps in GetComponentsInChildren<ParticleSystem>())
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                foreach (var r in GetComponentsInChildren<MeshRenderer>()) r.enabled = false;
                Destroy(gameObject, 3f);
            }
            else if (_stopped && GetComponentsInChildren<ParticleSystem>().All(ps => !ps.IsAlive(true)))
                Destroy(gameObject);
        }
    }

    public class VfxMover : MonoBehaviour
    {
        Vector2 _from, _to;
        float _seconds, _age;

        public void Begin(Vector2 from, Vector2 to, float seconds)
        {
            _from = from;
            _to = to;
            _seconds = Mathf.Max(0.01f, seconds);
        }

        void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / _seconds);
            transform.position = Vector2.Lerp(_from, _to, t * t);
            if (t < 1f) return;
            Destroy(gameObject); // gone on impact; the landing explosion takes over

        }
    }

    public class FallingSprite : MonoBehaviour
    {
        Sprite[] _frames;
        Vector2 _from, _to;
        float _fall, _stick, _age;
        SpriteRenderer _renderer;

        public void Begin(Sprite[] frames, Vector2 from, Vector2 to, float fallSeconds, float stickSeconds)
        {
            _frames = frames;
            _from = from;
            _to = to;
            _fall = Mathf.Max(0.01f, fallSeconds);
            _stick = Mathf.Max(0f, stickSeconds);
            _renderer = GetComponent<SpriteRenderer>();
        }

        void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / _fall);
            transform.position = Vector2.Lerp(_from, _to, t * t); // accelerates like it is dropping
            _renderer.sprite = _frames[Mathf.Min(_frames.Length - 1, (int)(t * _frames.Length))];
            if (_age <= _fall) return;
            float fade = Mathf.Clamp01((_age - _fall) / Mathf.Max(0.01f, _stick));
            var c = _renderer.color;
            c.a = 1f - fade;
            _renderer.color = c;
            if (fade >= 1f) Destroy(gameObject);
        }
    }
}
