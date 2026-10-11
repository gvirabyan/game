using System;
using System.Collections.Generic;
using UnityEngine;

namespace Corehold
{
    // UI text in 16 languages from Resources/localization.json. The choice is saved; the first launch follows the phone.
    // The game font (Russo One) covers Latin and Cyrillic; Armenian, Chinese, Japanese and Korean use the phone's own fonts.
    public static class Loc
    {
        [Serializable]
        public class Language
        {
            public string code, name;
            public bool osFont;
        }

        [Serializable]
        class Entry
        {
            public string key;
            public string[] values;
        }

        [Serializable]
        class Data
        {
            public Language[] languages;
            public Entry[] entries;
        }

        const string PrefKey = "language";
        static Data data;
        static readonly Dictionary<string, string[]> table = new Dictionary<string, string[]>();
        static readonly Dictionary<string, Font> osFonts = new Dictionary<string, Font>();
        static int current;

        public static event Action Changed;

        public static Language[] Languages { get { Load(); return data.languages; } }
        public static Language Current { get { Load(); return data.languages[current]; } }

        static void Load()
        {
            if (data != null) return;
            data = JsonUtility.FromJson<Data>(Resources.Load<TextAsset>("localization").text);
            foreach (var e in data.entries) table[e.key] = e.values;
            var saved = PlayerPrefs.GetString(PrefKey, "");
            current = Mathf.Max(0, Index(saved != "" ? saved : SystemCode()));
        }

        static int Index(string code) => Array.FindIndex(data.languages, l => l.code == code);

        static string SystemCode()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Russian: return "ru";
                case SystemLanguage.Spanish: return "es";
                case SystemLanguage.French: return "fr";
                case SystemLanguage.German: return "de";
                case SystemLanguage.Italian: return "it";
                case SystemLanguage.Portuguese: return "pt";
                case SystemLanguage.Polish: return "pl";
                case SystemLanguage.Ukrainian: return "uk";
                case SystemLanguage.Dutch: return "nl";
                case SystemLanguage.Swedish: return "sv";
                case SystemLanguage.Indonesian: return "id";
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified: return "zh";
                case SystemLanguage.Japanese: return "ja";
                case SystemLanguage.Korean: return "ko";
                default: return "en";
            }
        }

        public static void Set(string code)
        {
            Load();
            int i = Index(code);
            if (i < 0 || i == current) return;
            current = i;
            PlayerPrefs.SetString(PrefKey, code);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static string T(string key)
        {
            Load();
            if (!table.TryGetValue(key, out var values)) return key;
            var v = current < values.Length ? values[current] : null;
            return string.IsNullOrEmpty(v) ? values[0] : v;
        }

        public static string F(string key, params object[] args) => string.Format(T(key), args);

        // Font for a language's text: the game font, or a system font that has the language's script.
        public static Font FontFor(Language language, Font gameFont)
        {
            if (!language.osFont) return gameFont;
            if (osFonts.TryGetValue(language.code, out var font)) return font;
            var names = new List<string>();
            try
            {
                var installed = Font.GetOSInstalledFontNames();
                string[] want = language.code == "hy" ? new[] { "Armenian" }
                    : language.code == "ja" ? new[] { "CJK JP", "CJKjp", "Japanese", "CJK" }
                    : language.code == "ko" ? new[] { "CJK KR", "CJKkr", "Korean", "CJK" }
                    : new[] { "CJK SC", "CJKsc", "Hans", "CJK" };
                foreach (var w in want)
                    foreach (var n in installed)
                        if (n.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0 && !names.Contains(n)) names.Add(n);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Loc] cannot list system fonts: " + e.Message);
            }
            // Common names on Android, then Windows (editor) fallbacks.
            names.AddRange(new[] { "Noto Sans CJK SC", "Noto Sans Armenian", "Droid Sans Fallback", "Roboto",
                                   "Sylfaen", "Microsoft YaHei", "Yu Gothic", "Malgun Gothic", "Arial Unicode MS" });
            Debug.Log($"[Loc] {language.code} font candidates: {string.Join(", ", names)}");
            font = Font.CreateDynamicFontFromOSFont(names.ToArray(), 40) ?? gameFont;
            osFonts[language.code] = font;
            return font;
        }
    }
}
