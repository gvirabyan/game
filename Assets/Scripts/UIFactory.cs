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

        public static Image Image(RectTransform rt, Color color, bool raycast = false)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static Text Text(RectTransform rt, string text, int size, TextAnchor anchor, Color color)
        {
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
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
            label = Text(Fill("Label", rt), text, size, TextAnchor.MiddleCenter, Color.white);
            return button;
        }

        static readonly string[] Suffixes = { "K", "M", "B", "T", "q", "Q" };

        // Format numbers compactly: 1.75, 950, 1.2K, 34.5M ...
        public static string Number(float v)
        {
            if (v < 1000f) return v < 10f && v != Mathf.Floor(v) ? v.ToString("0.##") : Mathf.Floor(v).ToString("0");
            int i = -1;
            while (v >= 1000f && i < Suffixes.Length - 1) { v /= 1000f; i++; }
            return v.ToString(v < 100f ? "0.#" : "0") + Suffixes[i];
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
