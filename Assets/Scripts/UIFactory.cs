using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Corehold
{
    // Small helpers for building uGUI from code. Sizes are in reference pixels of a 1080x1920 canvas.
    public static class UIFactory
    {
        static Font font;
        public static Font Font => font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static readonly Color TextMain = Color.white;
        public static readonly Color TextDim = new Color(0.62f, 0.67f, 0.73f);
        public static readonly Color PanelColor = new Color(0.06f, 0.08f, 0.11f, 0.9f);
        public static readonly Color CardColor = new Color(0.13f, 0.16f, 0.21f, 1f);
        public static readonly Color DisabledColor = new Color(0.2f, 0.22f, 0.26f, 1f);

        public static Canvas CreateCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        public static RectTransform Rect(string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        public static RectTransform Fill(string name, Transform parent) =>
            Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // A box of fixed size placed relative to an anchor point of its parent.
        public static RectTransform Box(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rt = Rect(name, parent, anchor, anchor, Vector2.zero, Vector2.zero);
            rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
            return rt;
        }

        public static Image Image(RectTransform rt, Color color, bool raycast = false, bool rounded = true)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            if (rounded)
            {
                img.sprite = Art.Panel;
                img.type = UnityEngine.UI.Image.Type.Sliced;
            }
            return img;
        }

        public static Image Icon(RectTransform rt, Sprite sprite, float rotation = 0f)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            rt.localRotation = Quaternion.Euler(0f, 0f, rotation);
            return img;
        }

        public static Text Text(RectTransform rt, string text, int size, TextAnchor anchor, Color color, bool bold = false)
        {
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.alignment = anchor;
            t.color = color;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Button Button(RectTransform rt, Color color, out Text label, string text, int size)
        {
            var img = Image(rt, color, true);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;
            label = Text(Fill("Label", rt), text, size, TextAnchor.MiddleCenter, TextMain, true);
            return button;
        }
    }

    // Shrinks its RectTransform to the device safe area (notches, rounded corners).
    public sealed class SafeArea : MonoBehaviour
    {
        Rect applied;

        void Update()
        {
            var area = Screen.safeArea;
            if (area == applied || Screen.width == 0 || Screen.height == 0) return;
            applied = area;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            rt.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
