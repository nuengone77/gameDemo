using System.Linq;
using TMPro;
using UnityEngine;
using static MagicGame.ArcaneWidgets;

namespace MagicGame
{
    public partial class GameUI
    {
        // A text field over the selected hero's portrait (where its name used to be) for naming that hero.
        void BuildHeroNameInput(Transform page)
        {
            var frame = Panel("HeroNameInput", page, theme, 0.305f, 0.675f, 0.23f, 0.08f, true);
            var area = Rect("HeroNameInputArea", frame, 0, 0, 1, 1);
            area.offsetMin = new Vector2(14, 4);
            area.offsetMax = new Vector2(-14, -4);
            area.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var placeholder = Text("HeroNameInputPlaceholder", area, theme, "Tap to name your hero", 26, 0, 0, 1, 1, false, TextAlignmentOptions.Center);
            placeholder.fontStyle = FontStyles.Italic;
            var text = Text("HeroNameInputText", area, theme, "", 38, 0, 0, 1, 1, true, TextAlignmentOptions.Center);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            // Players may type Thai or English names, and Sarabun covers both.
            UseThaiFont(text);
            UseThaiFont(placeholder);
            var field = frame.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.fontAsset = theme.thaiFont;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = HeroNames.MaxLength;
            field.targetGraphic = frame.GetComponent<UnityEngine.UI.Image>();
            var colors = field.colors;
            colors.highlightedColor = ArcaneTheme.Paper;
            colors.selectedColor = ArcaneTheme.Cyan;
            field.colors = colors;
        }

        // Adds the name field to a GameUI prefab built before it existed.
        void EnsureHeroNameInput()
        {
            var page = transform.Find("SafeArea/Heroes");
            if (page == null || page.Find("HeroNameInput") != null) return;
            BuildHeroNameInput(page);
        }

        void WireHeroNameInput()
        {
            N("SelectedName").SetActive(false);
            var field = N("HeroNameInput").GetComponent<TMP_InputField>();
            field.onValidateInput = (_, _, c) => HeroNames.Allowed(c) ? c : '\0';
            field.onEndEdit.AddListener(value =>
            {
                string id = theme.heroes[GameSession.SelectedCharacter].displayName;
                HeroNames.Set(id, value);
                field.SetTextWithoutNotify(HeroNames.Get(id));
                RefreshHeroTitles();
            });
        }

        // A named hero shows its name with the role beneath; an unnamed one shows only its role.
        string HeroTitle(string heroId)
        {
            string name = HeroNames.Get(heroId);
            if (name.Length > 0) return name;
            var hero = System.Array.Find(theme.heroes, h => h != null && h.displayName == heroId);
            return L(hero != null ? hero.MenuName : "Hero");
        }

        string HeroLabel(CharacterAnimSet hero)
        {
            string name = HeroNames.Get(hero.displayName);
            return name.Length > 0 ? $"{name} — {L(hero.MenuName)}" : L(hero.MenuName);
        }

        void SetFitted(string node, string text)
        {
            var label = T(node);
            label.text = text;
            FitFont(label);
        }

        void RefreshHeroTitles()
        {
            for (int i = 0; i < theme.heroes.Length; i++)
            {
                var hero = theme.heroes[i];
                bool named = HeroNames.Get(hero.displayName).Length > 0;
                SetFitted("HeroName" + i, HeroTitle(hero.displayName));
                var title = T("HeroName" + i).rectTransform;
                title.anchorMin = new Vector2(title.anchorMin.x, named ? .48f : .05f);
                title.anchorMax = new Vector2(title.anchorMax.x, named ? .93f : .95f);
                N("HeroRole" + i).SetActive(named);
                T("HeroRole" + i).text = L(hero.MenuName);
                SetFitted("RebirthHero" + i + "Label", HeroTitle(hero.displayName));
            }
            if (_level == null) RefreshTagline();
            if (_stack.Contains(ScreenId.Rebirth)) RefreshRebirth();
        }

        bool EditingText()
        {
            var selected = UnityEngine.EventSystems.EventSystem.current != null ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject : null;
            return selected != null && selected.TryGetComponent<TMP_InputField>(out var field) && field.isFocused;
        }
    }
}
