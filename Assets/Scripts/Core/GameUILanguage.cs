using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using static MagicGame.ArcaneWidgets;

namespace MagicGame
{
    public partial class GameUI
    {
        // Every label's authored English text and look, so switching language can restore or translate it.
        readonly Dictionary<TMP_Text, (string text, TMP_FontAsset font, FontStyles style, TextOverflowModes overflow, List<UnityEngine.TextCore.OTL_FeatureTag> features)> _authored = new();
        // "English" stays in the display font even in Thai mode, so players can find their way back.
        static readonly HashSet<string> KeepFont = new() { "LanguageEnglishLabel" };

        // Language choice at the top of Settings; each option is written in its own language.
        void BuildLanguageSettings(Transform content)
        {
            Paragraph("SettingsLanguage", content, theme, "<color=#C8A363>LANGUAGE</color>\nChoose the language for menus and the story.");
            var row = ButtonRow("LanguageRow", content, theme);
            LayoutButton("LanguageEnglish", row, theme, "English");
            UseThaiFont(LayoutButton("LanguageThai", row, theme, "ไทย").GetComponentInChildren<TMP_Text>());
        }

        // Adds the language section to a GameUI prefab built before it existed.
        void EnsureLanguageSettings()
        {
            var content = transform.Find("SafeArea")?.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(r => r.name == "SettingsIntro")?.parent;
            if (content == null || content.Find("LanguageRow") != null) return;
            BuildLanguageSettings(content);
            content.Find("SettingsLanguage").SetSiblingIndex(0);
            content.Find("LanguageRow").SetSiblingIndex(1);
        }

        void RecordAuthoredText()
        {
            foreach (var label in GetComponentsInChildren<TMP_Text>(true))
                _authored[label] = (label.text, label.font, label.fontStyle, label.overflowMode, new List<UnityEngine.TextCore.OTL_FeatureTag>(label.fontFeatures));
        }

        void SetLanguage(GameLanguage language)
        {
            if (_settings.language == language) return;
            _settings.language = language;
            Loc.Current = language;
            _settings.Save();
            ApplyLanguage();
        }

        void ApplyLanguage()
        {
            foreach (var pair in _authored)
            {
                var label = pair.Key;
                var authored = pair.Value;
                // Labels still showing their authored text are translated here; the rest are filled in by their refresh below.
                if (Loc.Has(authored.text) && (label.text == authored.text || label.text == Loc.ThaiOf(authored.text)))
                    label.text = Loc.Tr(authored.text);
                FitFont(label);
            }

            RefreshHeroTitles();
            if (_level == null)
            {
                RefreshTagline();
                ShowHero(GameSession.SelectedCharacter);
            }
            else
            {
                if (_player != null)
                {
                    if (_player.CurrentCharacter != null) SetFitted("HUDHero", HeroLabel(_player.CurrentCharacter));
                    RefreshHealth(_player.Health);
                    RefreshSkills();
                }
                T("HUDStage").text = LF("Stage {0} / {1}", _level.levelNumber, L(_level.levelTitle));
                RefreshEnemies();
                RefreshProgression();
                if (_stack.Contains(ScreenId.Cards)) PresentCards();
                if (_stack.Contains(ScreenId.Build)) RefreshBuild();
            }
            if (_stack.Contains(ScreenId.Tutorial)) RefreshTutorial();
            if (_stack.Contains(ScreenId.Rebirth)) RefreshRebirth();
            foreach (var (name, language) in new[] { ("LanguageEnglish", GameLanguage.English), ("LanguageThai", GameLanguage.Thai) })
            {
                var colors = B(name).colors;
                colors.normalColor = _settings.language == language ? ArcaneTheme.Cyan : new Color(.78f, .64f, .39f, .28f);
                B(name).colors = colors;
            }
            ApplySettings();
        }

        // Thai text needs Sarabun: every label in Thai mode, and in English any label showing a Thai hero name.
        void FitFont(TMP_Text label)
        {
            if (!_authored.TryGetValue(label, out var authored)) return;
            if (theme.thaiFont == null || authored.font == theme.thaiFont || KeepFont.Contains(label.name)) return;
            if (Loc.IsThai || HasThai(label.text))
            {
                UseThaiFont(label);
                // Sarabun has one weight; headings set in the display font keep their weight as bold.
                label.fontStyle = authored.font == theme.titleFont ? authored.style | FontStyles.Bold : authored.style;
                // Stacked tone marks rise above the line box, which ellipsis mode would treat as overflow and cut.
                if (authored.overflow == TextOverflowModes.Ellipsis) label.overflowMode = TextOverflowModes.Overflow;
            }
            else
            {
                label.font = authored.font;
                label.fontStyle = authored.style;
                label.overflowMode = authored.overflow;
                label.fontFeatures = new List<UnityEngine.TextCore.OTL_FeatureTag>(authored.features);
            }
        }

        static bool HasThai(string text)
        {
            foreach (char c in text) if (c >= '\u0E00' && c <= '\u0E7F') return true;
            return false;
        }
    }
}
