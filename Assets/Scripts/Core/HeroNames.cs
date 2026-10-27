using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MagicGame
{
    // Names players give their heroes, keyed by the hero's save ID. A hero without a name is shown by its role.
    public static class HeroNames
    {
        public const string SaveKey = "MagicGame.HeroNames.v1";
        // Counted in characters, so Thai vowels and tone marks use some of it.
        public const int MaxLength = 20;

        [Serializable] class Entry { public string hero, name; }
        [Serializable] class Data { public List<Entry> names = new(); }

        static Data s_data;

        public static event Action Changed;

        // Latin and Thai only: the UI fonts have no glyphs for other scripts or emoji.
        public static bool Allowed(char c) => (c >= ' ' && c <= '~') || (c >= 'ก' && c <= '๛');

        public static string Get(string heroId)
        {
            var entry = Load().names.Find(e => e.hero == heroId);
            return entry != null ? entry.name : "";
        }

        public static void Set(string heroId, string name)
        {
            name = Clean(name);
            var data = Load();
            data.names.RemoveAll(e => e.hero == heroId);
            if (name.Length > 0) data.names.Add(new Entry { hero = heroId, name = name });
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        // Drops unsupported characters, collapses runs of spaces and trims to the length limit.
        public static string Clean(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            var text = new StringBuilder();
            foreach (char c in name)
            {
                if (!Allowed(c)) continue;
                if (c == ' ' && (text.Length == 0 || text[text.Length - 1] == ' ')) continue;
                text.Append(c);
            }
            string result = text.ToString().Trim();
            if (result.Length <= MaxLength) return result;
            // Never cut between a Thai letter and the vowel or tone mark drawn on it.
            int cut = MaxLength;
            while (cut > 0 && IsThaiMark(result[cut])) cut--;
            // Nor leave a vowel written before its letter (เ แ โ ใ ไ) without that letter.
            while (cut > 0 && result[cut - 1] >= '\u0E40' && result[cut - 1] <= '\u0E44') cut--;
            return result.Substring(0, cut).TrimEnd();
        }

        static bool IsThaiMark(char c) => c == '\u0E31' || (c >= '\u0E34' && c <= '\u0E3A') || (c >= '\u0E47' && c <= '\u0E4E');

        public static void Reload() => s_data = null;

        static Data Load()
        {
            if (s_data != null) return s_data;
            try { s_data = JsonUtility.FromJson<Data>(PlayerPrefs.GetString(SaveKey, "")); }
            catch (ArgumentException) { s_data = null; }
            s_data ??= new Data();
            s_data.names ??= new List<Entry>();
            return s_data;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() { s_data = null; Changed = null; }
    }
}
