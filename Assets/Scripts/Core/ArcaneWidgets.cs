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
            float x, float y, float width, float height, bool compact = false)
        {
            if (theme.panelFrame != null && theme.surfaceTile != null)
            {
                var surface = Image(name, parent, theme.surfaceTile, Color.white, x, y, width, height, true);
                surface.type = UnityEngine.UI.Image.Type.Tiled;
                surface.pixelsPerUnitMultiplier = compact ? 1.4f : 1f;
                var frame = Image(name + "Frame", surface.transform,
                    compact && theme.hudFrame != null ? theme.hudFrame : theme.panelFrame,
                    Color.white, 0, 0, 1, 1);
                frame.fillCenter = false;
                IgnoreLayout(frame.gameObject);
                return surface.rectTransform;
            }
            var border = Image(name, parent, theme.panel, ArcaneTheme.Gold, x, y, width, height, true).rectTransform;
            var inner = Image(name + "Fill", border, theme.panel, ArcaneTheme.Navy, 0f, 0f, 1f, 1f).rectTransform;
            inner.offsetMin = new Vector2(3, 3);
            inner.offsetMax = new Vector2(-3, -3);
            return border;
        }

        static void IgnoreLayout(GameObject node) => node.AddComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;

        public static ArcaneControlSize Size(RectTransform rect, Vector2 size, bool growWidth = false, bool layout = false)
        {
            if (layout) rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            var metrics = rect.gameObject.AddComponent<ArcaneControlSize>();
            metrics.referenceSize = size;
            metrics.growWidth = growWidth;
            metrics.Apply(1);
            return metrics;
        }

        public static UnityEngine.UI.Button SizedButton(string name, Transform parent, ArcaneTheme theme,
            string caption, Vector2 anchor, Vector2 pivot, Vector2 offset, Vector2 size, bool primary = false)
        {
            var button = Button(name, parent, theme, caption, anchor.x, anchor.y, 0, 0, primary);
            var rect = (RectTransform)button.transform;
            rect.pivot = pivot;
            rect.anchoredPosition = offset;
            Size(rect, size, true);
            return button;
        }

        public static UnityEngine.UI.Button LayoutButton(string name, Transform parent, ArcaneTheme theme,
            string caption, bool primary = false)
        {
            var button = Button(name, parent, theme, caption, 0, 0, 1, 1, primary);
            Size((RectTransform)button.transform, new Vector2(0, primary ? theme.primaryHeight : theme.secondaryHeight), layout: true);
            return button;
        }

        public static RectTransform MenuGroup(string name, Transform parent, ArcaneTheme theme, float width, float bottom)
        {
            var panel = Panel(name, parent, theme, .5f, bottom, 0, 0);
            panel.pivot = new Vector2(.5f, 1);
            panel.sizeDelta = new Vector2(width, 0);
            var content = Scroll(name + "Scroll", panel, 0, 0, 1, 1);
            var layout = content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(32, 32, 32, 32);
            layout.spacing = 10;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return panel;
        }

        public static RectTransform MenuContent(Transform panel)
        {
            var scroll = panel.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);
            return scroll != null ? scroll.content : (RectTransform)panel;
        }

        public static RectTransform ButtonRow(string name, Transform parent, ArcaneTheme theme)
        {
            var row = Rect(name, parent, 0, 0, 1, 1);
            Size(row, new Vector2(0, theme.secondaryHeight), layout: true);
            var layout = row.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 12;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return row;
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
            bool textured = theme.buttonFrame != null && theme.focusFrame != null;
            if (textured)
            {
                var frame = rect.Find(name + "Frame").GetComponent<UnityEngine.UI.Image>();
                frame.sprite = theme.buttonFrame;
            }
            var button = rect.gameObject.AddComponent<ArcaneButton>();
            button.targetGraphic = textured ? Image(name + "Focus", rect, theme.focusFrame, Color.white, 0, 0, 1, 1) : rect.GetComponent<UnityEngine.UI.Image>();
            if (textured)
            {
                ((UnityEngine.UI.Image)button.targetGraphic).fillCenter = false;
                IgnoreLayout(button.targetGraphic.gameObject);
            }
            button.targetGraphic.color = Color.white;
            var colors = button.colors;
            colors.normalColor = primary ? ArcaneTheme.Cyan : new Color(.78f, .64f, .39f, .28f);
            colors.highlightedColor = ArcaneTheme.Paper;
            colors.selectedColor = ArcaneTheme.Cyan;
            colors.pressedColor = new Color(.23f, .65f, .78f);
            colors.disabledColor = new Color(.3f, .34f, .4f, .2f);
            colors.fadeDuration = .08f;
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
