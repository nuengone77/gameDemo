using TMPro;
using UnityEngine;

namespace MagicGame
{
    // Shared by the editor prefab builder and the runtime fallback; layout lives in editable uGUI prefabs.
    public static class ArcaneWidgets
    {
        public static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(x, y);
            rect.anchorMax = new Vector2(x + width, y + height);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        public static UnityEngine.UI.Image Image(string name, Transform parent, Sprite sprite, Color color,
            float x, float y, float width, float height, bool raycast = false)
        {
            var image = Rect(name, parent, x, y, width, height).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycast;
            if (sprite != null && sprite.border.sqrMagnitude > 0f) image.type = UnityEngine.UI.Image.Type.Sliced;
            return image;
        }

        public static RectTransform Panel(string name, Transform parent, ArcaneTheme theme,
            float x, float y, float width, float height)
        {
            var border = Image(name, parent, theme.panel, ArcaneTheme.Gold, x, y, width, height, true).rectTransform;
            var inner = Image(name + "Fill", border, theme.panel, ArcaneTheme.Navy, 0f, 0f, 1f, 1f).rectTransform;
            inner.offsetMin = new Vector2(3, 3);
            inner.offsetMax = new Vector2(-3, -3);
            return border;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, ArcaneTheme theme, string text,
            float size, float x, float y, float width, float height, bool title = false,
            TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            var label = Rect(name, parent, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = title ? theme.titleFont : theme.bodyFont;
            label.text = text;
            label.fontSize = size;
            label.color = title ? ArcaneTheme.Paper : ArcaneTheme.Muted;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            label.margin = new Vector4(5, 2, 5, 2);
            return label;
        }

        public static UnityEngine.UI.Button Button(string name, Transform parent, ArcaneTheme theme, string caption,
            float x, float y, float width, float height, bool primary = false)
        {
            var rect = Panel(name, parent, theme, x, y, width, height);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
            button.targetGraphic.color = Color.white;
            var colors = button.colors;
            colors.normalColor = primary ? ArcaneTheme.Cyan : ArcaneTheme.Gold;
            colors.highlightedColor = Color.white;
            colors.selectedColor = ArcaneTheme.Cyan;
            colors.pressedColor = new Color(0.6f, 0.75f, 0.8f);
            colors.disabledColor = new Color(0.25f, 0.3f, 0.36f);
            button.colors = colors;
            Text(name + "Label", rect, theme, caption, 29, 0.04f, 0.03f, 0.92f, 0.94f, false, TextAlignmentOptions.Center).color = ArcaneTheme.Paper;
            return button;
        }

        public static RectTransform Scroll(string name, Transform parent, float x, float y, float width, float height)
        {
            var root = Rect(name, parent, x, y, width, height);
            var scroll = root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            var viewport = Rect(name + "Viewport", root, 0, 0, 1, 1);
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var hit = viewport.gameObject.AddComponent<UnityEngine.UI.Image>();
            hit.color = new Color(0, 0, 0, 0.001f);
            var content = Rect(name + "Content", viewport, 0, 1, 1, 0);
            content.pivot = new Vector2(0.5f, 1f);
            var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 20, 10, 10);
            layout.spacing = 18;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            var rail = Image(name + "ScrollRail", root, null, new Color(0.2f, 0.27f, 0.35f), 0.986f, 0.025f, 0.008f, 0.95f);
            var bar = rail.gameObject.AddComponent<UnityEngine.UI.Scrollbar>();
            var thumb = Image(name + "ScrollThumb", rail.transform, null, ArcaneTheme.Cyan, 0, 0, 1, 1);
            bar.handleRect = thumb.rectTransform;
            bar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            bar.interactable = false;
            bar.transition = UnityEngine.UI.Selectable.Transition.None;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }

        public static TextMeshProUGUI Paragraph(string name, Transform parent, ArcaneTheme theme, string text, float size = 30)
        {
            var label = Text(name, parent, theme, text, size, 0, 0, 1, 1);
            label.alignment = TextAlignmentOptions.TopLeft;
            label.overflowMode = TextOverflowModes.Overflow;
            var layout = label.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight = 70;
            return label;
        }
    }
}
