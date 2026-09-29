using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DublinRetrofit
{
    // Small helpers that build uGUI elements from code. Building the UI in code keeps the
    // scene file tiny and makes every panel's layout readable in one place.
    // Uses the legacy uGUI Text with Unity's built-in font so no font assets need importing.
    public static class UiFactory
    {
        public static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.11f, 0.88f);
        public static readonly Color TextColor = new Color(0.93f, 0.94f, 0.95f);
        public const float Padding = 14f;

        static Font font;
        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        // A dark rectangle anchored to one corner of its parent.
        // anchor (0,0) = bottom-left, (1,1) = top-right; the pivot uses the same corner so
        // 'position' is simply the offset from that corner.
        public static RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = NewRect(parent, name);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.gameObject.AddComponent<Image>().color = PanelColor;
            return rect;
        }

        // Text that fills its parent (minus padding). Rich text allows <b> and <color> tags.
        public static Text Label(Transform parent, string name, int fontSize, TextAnchor alignment, float padding = Padding)
        {
            var rect = NewRect(parent, name);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);

            var text = rect.gameObject.AddComponent<Text>();
            text.font = Font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = TextColor;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false; // text should never block clicks
            return text;
        }

        public static Button Button(Transform parent, string label, Vector2 anchor, Vector2 position, Vector2 size, UnityAction onClick)
        {
            RectTransform rect = Panel(parent, label, anchor, position, size);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.onClick.AddListener(onClick);
            Label(rect, "Label", 18, TextAnchor.MiddleCenter, 4f).text = label;
            return button;
        }

        // A plain coloured square, used for legend swatches.
        public static Image Swatch(Transform parent, Vector2 position, float size, Color color)
        {
            var rect = NewRect(parent, "Swatch");
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(size, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        static RectTransform NewRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
    }
}
