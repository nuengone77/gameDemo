using System.Collections.Generic;
using UnityEngine;

namespace MagicGame
{
    public enum GameLanguage { English, Thai }

    // UI text is written in English in code; Resources/Localization/Thai.txt maps each English string
    // to its Thai text (one "english<TAB>thai" pair per line, \n for line breaks, zero-width spaces
    // marking where Thai lines may wrap). Strings without an entry stay in English.
    public static class Loc
    {
        static Dictionary<string, string> s_thai;

        public static GameLanguage Current { get; set; }
        public static bool IsThai => Current == GameLanguage.Thai;

        public static string Tr(string english)
        {
            if (!IsThai || string.IsNullOrEmpty(english)) return english;
            return Thai.TryGetValue(english, out var thai) ? thai : english;
        }

        public static string F(string englishFormat, params object[] args) => string.Format(Tr(englishFormat), args);

        public static bool Has(string english) => english != null && Thai.ContainsKey(english);

        public static string ThaiOf(string english) => Thai.TryGetValue(english, out var thai) ? thai : english;

        static Dictionary<string, string> Thai
        {
            get
            {
                if (s_thai != null) return s_thai;
                s_thai = new Dictionary<string, string>();
                var file = Resources.Load<TextAsset>("Localization/Thai");
                if (file == null) return s_thai;
                foreach (var line in file.text.Split('\n'))
                {
                    int tab = line.IndexOf('\t');
                    if (tab <= 0 || line.StartsWith("#")) continue;
                    s_thai[Unescape(line.Substring(0, tab))] = Unescape(line.Substring(tab + 1).TrimEnd('\r'));
                }
                return s_thai;
            }
        }

        static string Unescape(string text) => text.Replace("\\n", "\n");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => s_thai = null;
    }
}
