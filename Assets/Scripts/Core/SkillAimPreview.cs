using UnityEngine;

namespace MagicGame
{
    // Project world geometry into the HUD so the preview follows camera zoom and aspect ratio.
    [RequireComponent(typeof(CanvasRenderer))]
    public class SkillAimPreview : UnityEngine.UI.MaskableGraphic
    {
        SkillCastRequest _cast;
        SkillTargetingMode _mode;
        bool _cancel;
        Camera _camera;

        public void Show(SkillCastRequest cast, SkillTargetingMode mode, bool cancel)
        {
            _cast = cast;
            _mode = mode;
            _cancel = cancel;
            _camera = Camera.main;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            if (_cast.source == null || _camera == null || _cast.source.Config == null) return;
            var config = _cast.source.Config;
            Vector2 origin = _cast.source.transform.position;
            Color tint = _cancel ? new Color(1f, 0.3f, 0.35f, 0.95f) : ArcaneTheme.Cyan;
            var faint = tint; faint.a = 0.3f;
            Circle(mesh, origin, config.range, faint);
            if (_mode == SkillTargetingMode.Ground)
            {
                Circle(mesh, _cast.target, config.radius, tint);
                const float cross = 0.12f;
                Line(mesh, _cast.target - Vector2.right * cross, _cast.target + Vector2.right * cross, tint);
                Line(mesh, _cast.target - Vector2.up * cross, _cast.target + Vector2.up * cross, tint);
            }
            else if (_mode == SkillTargetingMode.Dash)
            {
                Vector2 end = CombatWorld.Clamp(origin - _cast.direction * config.dashDistance);
                Arrow(mesh, origin, end, tint);
            }
            else if (config.role == HeroRole.Tanky)
            {
                Vector2 center = CombatWorld.Clamp(origin + _cast.direction * config.wallDistance);
                Vector2 tangent = new Vector2(-_cast.direction.y, _cast.direction.x) * config.wallWidth * 0.5f;
                Line(mesh, center - tangent, center + tangent, tint, 6f);
                Arrow(mesh, origin, center, tint);
            }
            else
            {
                var player = PlayerController.Instance;
                Vector2 start = player != null ? player.Center : origin;
                Arrow(mesh, start, CombatWorld.ClampAttackPoint(start + _cast.direction * config.range), tint);
            }
        }

        void Circle(UnityEngine.UI.VertexHelper mesh, Vector2 center, float radius, Color tint)
        {
            const int segments = 64;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments, b = (i + 1) * Mathf.PI * 2f / segments;
                Line(mesh, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                    center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, tint);
            }
        }

        void Arrow(UnityEngine.UI.VertexHelper mesh, Vector2 start, Vector2 end, Color tint)
        {
            Line(mesh, start, end, tint);
            Vector2 back = (start - end).normalized * 0.35f;
            Vector2 side = new Vector2(-back.y, back.x) * 0.6f;
            Line(mesh, end, end + back + side, tint);
            Line(mesh, end, end + back - side, tint);
        }

        void Line(UnityEngine.UI.VertexHelper mesh, Vector2 from, Vector2 to, Color tint, float width = 3f)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, _camera.WorldToScreenPoint(from), null, out var a);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, _camera.WorldToScreenPoint(to), null, out var b);
            Vector2 side = new Vector2(a.y - b.y, b.x - a.x).normalized * width * 0.5f;
            int index = mesh.currentVertCount;
            mesh.AddVert(a - side, tint, Vector2.zero);
            mesh.AddVert(a + side, tint, Vector2.zero);
            mesh.AddVert(b + side, tint, Vector2.zero);
            mesh.AddVert(b - side, tint, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
