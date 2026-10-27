using UnityEngine;

namespace MagicGame
{
    // Applied only when settings change; layout rows grow with accessible text sizes.
    public class ArcaneControlSize : MonoBehaviour
    {
        public Vector2 referenceSize;
        public bool growWidth;

        public void Apply(float textSize)
        {
            float height = referenceSize.y * textSize;
            if (TryGetComponent<UnityEngine.UI.LayoutElement>(out var layout))
            {
                layout.minHeight = layout.preferredHeight = height;
                if (transform.parent.GetComponent<UnityEngine.UI.LayoutGroup>() == null)
                {
                    var row = (RectTransform)transform;
                    row.sizeDelta = new Vector2(row.sizeDelta.x, height);
                }
            }
            else
            {
                var rect = (RectTransform)transform;
                rect.sizeDelta = new Vector2(referenceSize.x * (growWidth ? textSize : 1), height);
            }
        }
    }
}
