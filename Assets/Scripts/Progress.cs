using UnityEngine;

namespace Corehold
{
    // Campaign progress kept on the device (PlayerPrefs): stars per mission, best endless wave and the mute setting.
    // Missions unlock in order: mission N needs mission N-1 won.
    public static class Progress
    {
        const string StarsKey = "stars", EndlessKey = "endlessBest", MutedKey = "muted";

        static int[] stars;

        public static int[] Stars(int count)
        {
            if (stars != null && stars.Length == count) return stars;
            stars = new int[count];
            var saved = PlayerPrefs.GetString(StarsKey, "");
            for (int i = 0; i < saved.Length && i < count; i++) stars[i] = Mathf.Clamp(saved[i] - '0', 0, 3);
            return stars;
        }

        public static void SetStars(int index, int value)
        {
            if (stars == null || index < 0 || index >= stars.Length || value <= stars[index]) return;
            stars[index] = value;
            var chars = new char[stars.Length];
            for (int i = 0; i < stars.Length; i++) chars[i] = (char)('0' + stars[i]);
            PlayerPrefs.SetString(StarsKey, new string(chars));
            PlayerPrefs.Save();
        }

        public static bool Unlocked(int index) => index == 0 || (stars != null && index < stars.Length && stars[index - 1] > 0);

        // First mission not yet won (the one to play next).
        public static int Current
        {
            get
            {
                if (stars == null) return 0;
                int i = System.Array.FindIndex(stars, s => s == 0);
                return i < 0 ? stars.Length - 1 : i;
            }
        }

        public static int EndlessBest
        {
            get => PlayerPrefs.GetInt(EndlessKey, 0);
            set
            {
                if (value <= EndlessBest) return;
                PlayerPrefs.SetInt(EndlessKey, value);
                PlayerPrefs.Save();
            }
        }

        public static bool Muted
        {
            get => PlayerPrefs.GetInt(MutedKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(MutedKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        // Tests and a fresh start.
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(StarsKey);
            PlayerPrefs.DeleteKey(EndlessKey);
            stars = null;
        }
    }
}
