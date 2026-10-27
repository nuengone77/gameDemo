using UnityEngine;
using UnityEngine.Sprites;

namespace MagicGame
{
    // Reuse the grimoire's square border artwork along a circular rim, including its jewels.
    [RequireComponent(typeof(CanvasRenderer))]
    public class CircularSkillGraphic : UnityEngine.UI.MaskableGraphic, ICanvasRaycastFilter
    {
        [SerializeField] Sprite source;
        [SerializeField] bool border;

        public override Texture mainTexture => source != null ? source.texture : Texture2D.whiteTexture;

        public void Configure(Sprite sprite, bool isBorder)
        {
            source = sprite;
            border = isBorder;
            SetAllDirty();
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out var local)) return false;
            var rect = rectTransform.rect;
            if (rect.width <= 0 || rect.height <= 0) return false;
            var offset = local - rect.center;
            return new Vector2(offset.x / (rect.width * .5f), offset.y / (rect.height * .5f)).sqrMagnitude <= 1f;
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            const int segments = 128;
            var rect = GetPixelAdjustedRect();
            Vector4 uv = source != null ? DataUtility.GetOuterUV(source) : new Vector4(0, 0, 1, 1);
            if (!border)
            {
                AddVertex(mesh, rect, uv, Vector2.zero, Vector2.zero);
                for (int i = 0; i <= segments; i++)
                {
                    float angle = i * Mathf.PI * 2f / segments;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    AddVertex(mesh, rect, uv, direction, direction);
                    if (i > 0) mesh.AddTriangle(0, i + 1, i);
                }
                return;
            }

            // Radial subdivisions preserve the source's thin parallel rails and corner ornament.
            const int bands = 12;
            for (int band = 0; band <= bands; band++)
            {
                float radius = Mathf.Lerp(.6f, 1f, band / (float)bands);
                for (int i = 0; i <= segments; i++)
                {
                    float angle = i * Mathf.PI * 2f / segments;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Vector2 squareEdge = direction / Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.y));
                    AddVertex(mesh, rect, uv, direction * radius, squareEdge * radius);
                    if (band == 0 || i == 0) continue;
                    int outer = band * (segments + 1) + i;
                    int inner = outer - (segments + 1);
                    mesh.AddTriangle(inner - 1, outer, outer - 1);
                    mesh.AddTriangle(inner - 1, inner, outer);
                }
            }
        }

        void AddVertex(UnityEngine.UI.VertexHelper mesh, Rect rect, Vector4 uv, Vector2 position, Vector2 texturePoint)
        {
            Vector2 point = rect.center + Vector2.Scale(position, rect.size * .5f);
            Vector2 sample = texturePoint * .5f + Vector2.one * .5f;
            mesh.AddVert(point, color, new Vector2(Mathf.Lerp(uv.x, uv.z, sample.x), Mathf.Lerp(uv.y, uv.w, sample.y)));
        }
    }
}
