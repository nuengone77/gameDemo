using System.Linq;
using TMPro;
using UnityEngine;
using static MagicGame.ArcaneWidgets;

namespace MagicGame
{
    public partial class GameUI
    {
        public void BuildLayout()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = GetComponent<UnityEngine.UI.CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            if (GetComponent<UnityEngine.UI.GraphicRaycaster>() == null) gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            Image("VillageBackdrop", transform, theme.village, Color.white, 0, 0, 1, 1);
            Image("BackdropWash", transform, null, new Color(0.015f, 0.03f, 0.065f, 0.82f), 0, 0, 1, 1);
            var safe = Rect("SafeArea", transform, 0, 0, 1, 1);
            BuildMenu(safe);
            BuildHeroes(safe);
            BuildHUD(safe);
            BuildPause(safe);
            BuildSettings(safe);
            BuildHelp(safe);
            BuildStory(safe);
            BuildOutcome(safe, "Defeat", "A FALLEN HERO", "Your story is not over.", "Retry Stage", "DefeatRetry");
            BuildOutcome(safe, "Victory", "THE REALM IS SAFE", "The dragon is defeated.", "Play Again", "VictoryAgain");
            BuildDialog(safe);
            BuildTutorial(safe);
            BuildProgressionLayout();
            EnsureSkillControls();
        }

        RectTransform Page(string name, Transform parent, bool backdrop = true)
        {
            var page = Rect(name, parent, 0, 0, 1, 1);
            page.gameObject.AddComponent<CanvasGroup>();
            if (backdrop)
            {
                var shade = Image(name + "Shade", page, theme.surfaceTile,
                    theme.surfaceTile != null ? new Color(1, 1, 1, .96f) : new Color(.035f, .065f, .11f, .96f),
                    0, 0, 1, 1, true);
                if (theme.surfaceTile != null) shade.type = UnityEngine.UI.Image.Type.Tiled;
            }
            return page;
        }

        void Header(Transform page, string prefix, string title, string subtitle)
        {
            Text(prefix + "Heading", page, theme, title, 52, 0.21f, 0.88f, 0.58f, 0.085f, true, TextAlignmentOptions.Center);
            Text(prefix + "Subtitle", page, theme, subtitle, 27, 0.18f, 0.82f, 0.64f, 0.05f, false, TextAlignmentOptions.Center);
            SizedButton(prefix + "Back", page, theme, "Back", new Vector2(.035f, .9f),
                new Vector2(0, .5f), Vector2.zero, new Vector2(156, theme.secondaryHeight));
            Image(prefix + "Rule", page, null, ArcaneTheme.Gold, 0.04f, 0.8f, 0.92f, 0.002f);
            if (theme.headerOrnament != null)
                Image(prefix + "Ornament", page, theme.headerOrnament, Color.white, .486f, .78f, .028f, .045f).preserveAspect = true;
        }

        void BuildMenu(Transform safe)
        {
            var page = Page("Menu", safe, false);
            var emblem = Image("MenuEmblem", page, theme.ring, ArcaneTheme.Gold, 0.46f, 0.84f, 0.08f, 0.13f);
            emblem.preserveAspect = true;
            Text("MenuEyebrow", page, theme, "FIVE HEROES  /  ONE DESTINY", 25, 0.15f, 0.79f, 0.7f, 0.045f, false, TextAlignmentOptions.Center).color = ArcaneTheme.Cyan;
            Text("MenuTitle", page, theme, "Magic Ring Quest", 83, 0.08f, 0.66f, 0.84f, 0.115f, true, TextAlignmentOptions.Center);
            Text("MenuTagline", page, theme, "Beyond the village, an ancient power awakens.", 29, 0.1f, 0.59f, 0.8f, 0.065f, false, TextAlignmentOptions.Center);
            var panel = MenuGroup("MenuPanel", page, theme, 660, .07f);
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, .58f);
            panel.pivot = new Vector2(.5f, 1);
            var actions = MenuContent(panel);
            LayoutButton("MenuContinue", actions, theme, "Continue", true);
            LayoutButton("MenuNew", actions, theme, "New Adventure", true);
            var row = ButtonRow("MenuUtilities", actions, theme);
            LayoutButton("MenuStory", row, theme, "Story");
            LayoutButton("MenuHelp", row, theme, "How to Play");
            LayoutButton("MenuSettings", row, theme, "Settings");
            Text("MenuCheckpoint", page, theme, "", 22, 0.06f, 0.645f, 0.88f, 0.055f, false, TextAlignmentOptions.Center);
            var footer = Text("MenuFooter", page, theme, "AN ADVENTURE IN SEVEN CHAPTERS", 23, .15f, .005f, .7f, 0, false, TextAlignmentOptions.Center).rectTransform;
            footer.pivot = new Vector2(.5f, 0);
            footer.sizeDelta = new Vector2(0, 56);
        }

        void BuildStory(Transform safe)
        {
            var page = Page("Story", safe);
            var panel = Panel("StoryPanel", page, theme, 0.17f, 0.07f, 0.66f, 0.86f);
            Text("StoryEyebrow", panel, theme, "PANNELION  /  PROLOGUE", 24, 0.05f, 0.9f, 0.9f, 0.06f, false, TextAlignmentOptions.Center).color = ArcaneTheme.Cyan;
            var title = Text("StoryTitle", panel, theme, "เนื้อเรื่อง", 50, 0.05f, 0.8f, 0.9f, 0.1f, true, TextAlignmentOptions.Center);
            UseThaiFont(title);
            Image("StoryRule", panel, null, ArcaneTheme.Gold, 0.08f, 0.79f, 0.84f, 0.002f);
            var content = Scroll("StoryScroll", panel, 0.06f, 0.17f, 0.88f, 0.6f);
            var body = Paragraph("StoryBody", content, theme, "", 32);
            UseThaiFont(body);
            body.color = ArcaneTheme.Paper;
            body.paragraphSpacing = 24;
            Button("StoryClose", panel, theme, "Close", 0.33f, 0.035f, 0.34f, 0.11f, true);
        }

        // Thai needs the mark features so tone marks stack above upper vowels instead of overlapping them.
        void UseThaiFont(TMP_Text label)
        {
            if (theme.thaiFont == null) return;
            label.font = theme.thaiFont;
            label.fontFeatures = new System.Collections.Generic.List<UnityEngine.TextCore.OTL_FeatureTag>
                { UnityEngine.TextCore.OTL_FeatureTag.kern, UnityEngine.TextCore.OTL_FeatureTag.mark, UnityEngine.TextCore.OTL_FeatureTag.mkmk };
        }

        // Adds the Story button and page to a GameUI prefab built before they existed.
        void EnsureStory()
        {
            var safe = transform.Find("SafeArea");
            var panel = safe != null ? safe.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(r => r.name == "MenuPanel") : null;
            if (panel == null) return;
            var row = panel.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(r => r.name == "MenuUtilities");
            if (row != null && row.Find("MenuStory") == null)
                LayoutButton("MenuStory", row, theme, "Story").transform.SetAsFirstSibling();
            if (safe.Find("Story") == null) BuildStory(safe);
        }

        void BuildHeroes(Transform safe)
        {
            var page = Page("Heroes", safe);
            Header(page, "Heroes", "Choose your hero", "A different kind of magic. The same journey ahead.");
            var roster = Panel("HeroRoster", page, theme, 0.035f, 0.15f, 0.255f, 0.61f);
            var rosterContent = Scroll("RosterScroll", roster, 0.025f, 0.025f, 0.95f, 0.95f);
            rosterContent.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().spacing = 8;
            for (int i = 0; i < theme.heroes.Length; i++)
            {
                var hero = theme.heroes[i];
                var button = Button("Hero" + i, rosterContent, theme, "", 0, 0, 1, 1);
                Size((RectTransform)button.transform, new Vector2(0, theme.heroRowHeight), layout: true);
                var preview = Image("HeroPortrait" + i, button.transform, hero.PreviewSprite, Color.white, 0.025f, 0.05f, 0.23f, 0.9f);
                preview.preserveAspect = true;
                Text("HeroName" + i, button.transform, theme, hero.MenuName, 30, 0.29f, 0.48f, 0.68f, 0.45f, true);
                Text("HeroRole" + i, button.transform, theme, hero.combat.RoleName, 23, 0.29f, 0.05f, 0.68f, 0.4f).color = ArcaneTheme.HeroColors[i];
            }
            Image("HeroHalo", page, theme.ring, new Color(0.78f, 0.64f, 0.39f, 0.3f), 0.3f, 0.24f, 0.24f, 0.43f).preserveAspect = true;
            Image("SelectedHero", page, theme.heroes[0].PreviewSprite, Color.white, 0.33f, 0.25f, 0.18f, 0.42f).preserveAspect = true;
            Text("SelectedName", page, theme, "New", 47, 0.3f, 0.68f, 0.24f, 0.075f, true, TextAlignmentOptions.Center);
            Text("SelectedRole", page, theme, "Tanky", 28, 0.3f, 0.61f, 0.24f, 0.06f, false, TextAlignmentOptions.Center);
            BuildHeroNameInput(page);
            Text("HeroSelectedBadge", page, theme, "SELECTED", 23, 0.3f, 0.16f, 0.24f, 0.05f, false, TextAlignmentOptions.Center).color = ArcaneTheme.Cyan;
            var detail = Panel("HeroDetails", page, theme, 0.565f, 0.15f, 0.4f, 0.61f);
            var content = Scroll("HeroSummaryScroll", detail, 0.025f, 0.035f, 0.95f, 0.93f);
            Paragraph("HeroSummary", content, theme, "Protect yourself with astral shields and control the battlefield.", 29);
            string[] kinds = { "Basic attack", "Active skill", "Passive skill", "Ultimate" };
            for (int i = 0; i < 4; i++)
            {
                var button = Button("HeroAbility" + i, content, theme, "", 0, 0, 1, 1);
                Size((RectTransform)button.transform, new Vector2(0, theme.abilityRowHeight), layout: true);
                Image("HeroAbilityIcon" + i, button.transform, theme.symbols[i], ArcaneTheme.Gold, 0.02f, 0.16f, 0.09f, 0.68f).preserveAspect = true;
                Text("HeroAbilityKind" + i, button.transform, theme, kinds[i].ToUpperInvariant(), 20, 0.14f, 0.54f, 0.81f, 0.4f);
                Text("HeroAbilityTitle" + i, button.transform, theme, "Ability", 27, 0.14f, 0.03f, 0.81f, 0.5f, true);
            }
            SizedButton("HeroesBegin", page, theme, "Begin Adventure", new Vector2(.965f, .035f),
                new Vector2(1, 0), Vector2.zero, new Vector2(370, theme.primaryHeight), true);
            Text("HeroesFootnote", page, theme, "Swipe the roster and abilities to explore all five heroes.", 25, 0.035f, 0.04f, 0.58f, 0.06f);
        }

        void BuildHUD(Transform safe)
        {
            var page = Page("HUD", safe, false);
            var health = Panel("HealthPanel", page, theme, 0.025f, 0.815f, 0.27f, 0.16f, true);
            Image("HUDPortrait", health, theme.heroes[0].PreviewSprite, Color.white, 0.025f, 0.15f, 0.14f, 0.75f).preserveAspect = true;
            Text("HUDHero", health, theme, "New / Tanky", 26, 0.19f, 0.62f, 0.78f, 0.33f, true);
            Image("HealthTrack", health, null, new Color(0.06f, 0.1f, 0.13f), 0.2f, 0.39f, 0.74f, 0.16f);
            var fill = Image("HealthFill", health, theme.disc, new Color32(111, 198, 153, 255), 0.2f, 0.39f, 0.74f, 0.16f);
            fill.sprite = null;
            Text("HUDHealth", health, theme, "100 / 100 HP", 22, 0.19f, 0.04f, 0.4f, 0.31f);
            Text("HUDShield", health, theme, "", 22, 0.59f, 0.04f, 0.38f, 0.31f).color = ArcaneTheme.Cyan;
            Panel("StagePanel", page, theme, 0.025f, 0.705f, 0.27f, 0.1f, true);
            Text("HUDStage", page, theme, "Stage 1", 29, 0.025f, 0.755f, 0.27f, 0.05f, true, TextAlignmentOptions.Center);
            Text("HUDObjective", page, theme, "Defeat the remaining enemies", 24, 0.025f, 0.705f, 0.27f, 0.05f, false, TextAlignmentOptions.Center);
            SizedButton("HUDPause", page, theme, "Pause", new Vector2(.975f, .975f),
                Vector2.one, Vector2.zero, new Vector2(144, theme.secondaryHeight));
            var boss = Panel("BossPanel", page, theme, 0.34f, 0.88f, 0.32f, 0.095f, true);
            Text("HUDBoss", boss, theme, "Inferno Dragon", 26, 0.05f, 0.43f, 0.9f, 0.5f, true, TextAlignmentOptions.Center);
            Image("BossTrack", boss, null, new Color(0.12f, 0.06f, 0.08f), 0.04f, 0.16f, 0.92f, 0.15f);
            Image("BossFill", boss, null, new Color32(222, 116, 117, 255), 0.04f, 0.16f, 0.92f, 0.15f);
            Text("HUDSpecial", page, theme, "", 27, 0.03f, 0.645f, 0.26f, 0.05f);
            Text("HUDBanner", page, theme, "", 45, 0.2f, 0.6f, 0.6f, 0.08f, true, TextAlignmentOptions.Center);
            var controls = Rect("TouchControls", page, 0, 0, 1, 1);
            controls.gameObject.AddComponent<CanvasGroup>();
            BuildStick(controls, "MoveStick", false);
            BuildStick(controls, "AimStick", true);
            BuildAction(controls, "Active", "Active", 1, -420, 300);
            BuildAction(controls, "Ultimate", "Ultimate", 3, -100, 430);
            BuildAction(controls, "Stance", "Solar", 2, -420, 55);
            var marker = Image("AimMarker", page, theme.ring, ArcaneTheme.Cyan, 0.5f, 0.5f, 0, 0).rectTransform;
            marker.sizeDelta = new Vector2(65, 65);
            Text("AimMarkerLabel", marker, theme, "A / U", 21, -0.3f, -0.65f, 1.6f, 0.65f, false, TextAlignmentOptions.Center);
        }

        void BuildStick(Transform parent, string name, bool aims)
        {
            var rect = Rect(name, parent, aims ? 1 : 0, 0, 0, 0);
            rect.pivot = new Vector2(aims ? 1 : 0, 0);
            rect.anchoredPosition = new Vector2(aims ? -62 : 62, 55);
            rect.sizeDelta = new Vector2(280, 280);
            Image(name + "Surface", rect, theme.disc, new Color(0.04f, 0.08f, 0.14f, 0.9f), 0, 0, 1, 1, true);
            Image(name + "Ring", rect, theme.ring, ArcaneTheme.Gold, 0, 0, 1, 1);
            var knob = Image(name + "Knob", rect, theme.disc, ArcaneTheme.Cyan, 0.5f, 0.5f, 0, 0).rectTransform;
            knob.sizeDelta = new Vector2(80, 80);
            var stick = rect.gameObject.AddComponent<TouchStick>();
            stick.aims = aims;
            stick.knob = knob;
            Text(name + "Caption", rect, theme, aims ? "AIM / FIRE" : "MOVE", 22, -0.1f, -0.17f, 1.2f, 0.14f, false, TextAlignmentOptions.Center);
        }

        void BuildAction(Transform parent, string name, string caption, int symbol, float x, float y)
        {
            var button = Button("HUD" + name, parent, theme, "", 1, 0, 0, 0, true);
            var rect = (RectTransform)button.transform;
            rect.pivot = new Vector2(1, 0);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = Vector2.one * theme.actionSize;
            Image(name + "Symbol", rect, theme.symbols[symbol], ArcaneTheme.Gold, 0.28f, 0.38f, 0.44f, 0.46f).preserveAspect = true;
            var cooldown = Image(name + "Cooldown", rect, theme.disc, new Color(0, 0, 0, 0.65f), 0.09f, 0.09f, 0.82f, 0.82f);
            cooldown.type = UnityEngine.UI.Image.Type.Filled;
            cooldown.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
            cooldown.fillAmount = 0;
            Text(name + "Time", rect, theme, "", 34, 0.07f, 0.32f, 0.86f, 0.5f, true, TextAlignmentOptions.Center);
            Text(name + "Caption", rect, theme, caption, 24, 0.02f, 0.02f, 0.96f, 0.28f, false, TextAlignmentOptions.Center).color = ArcaneTheme.Paper;
        }

        void BuildPause(Transform safe)
        {
            var page = Page("Pause", safe);
            Text("PauseEyebrow", page, theme, "TAKE A BREATH", 25, 0.3f, 0.85f, 0.4f, 0.06f, false, TextAlignmentOptions.Center).color = ArcaneTheme.Cyan;
            Text("PauseHeading", page, theme, "Adventure paused", 62, 0.15f, 0.72f, 0.7f, 0.12f, true, TextAlignmentOptions.Center);
            var panel = MenuGroup("PausePanel", page, theme, 740, .66f);
            var content = MenuContent(panel);
            LayoutButton("PauseResume", content, theme, "Resume", true);
            var utilities = ButtonRow("PauseUtilities", content, theme);
            LayoutButton("PauseHelp", utilities, theme, "How to Play");
            LayoutButton("PauseSettings", utilities, theme, "Settings");
            var actions = ButtonRow("PauseActions", content, theme);
            LayoutButton("PauseRestart", actions, theme, "Restart Stage");
            LayoutButton("PauseMenu", actions, theme, "Main Menu");
        }

        void BuildSettings(Transform safe)
        {
            var page = Page("Settings", safe);
            Header(page, "Settings", "Make it yours", "Controls and readability. Saved automatically.");
            var panel = Panel("SettingsPanel", page, theme, 0.2f, 0.16f, 0.6f, 0.6f);
            var content = Scroll("SettingsScroll", panel, 0.035f, 0.025f, 0.93f, 0.95f);
            BuildLanguageSettings(content);
            Paragraph("SettingsIntro", content, theme, "<color=#C8A363>TOUCH CONTROLS</color>\nAdjust the controls to fit your thumbs.");
            BuildSlider(content, "ControlSize", "Control size", 0.8f, 1.2f, 1f);
            BuildSlider(content, "ControlOpacity", "Control opacity", 0.3f, 1f, 0.65f);
            Paragraph("SettingsAccess", content, theme, "<color=#C8A363>ACCESSIBILITY</color>\nMake important information easier to read.");
            BuildSlider(content, "TextSize", "Text size", 1f, 1.5f, 1f);
            BuildAudioSliders(content);
            var motion = Button("ReducedMotion", content, theme, "Reduced UI motion: Off", 0, 0, 1, 1);
            Size((RectTransform)motion.transform, new Vector2(0, theme.secondaryHeight), layout: true);
            Paragraph("SettingsMotionInfo", content, theme, "Reduced UI motion removes decorative rotation and menu fades. Combat effects remain visible.", 26);
            SizedButton("SettingsDefaults", page, theme, "Restore Defaults", new Vector2(.5f, .03f),
                new Vector2(.5f, 0), Vector2.zero, new Vector2(300, theme.secondaryHeight));
        }

        void BuildAudioSliders(Transform content)
        {
            Paragraph("SettingsAudio", content, theme, "<color=#C8A363>SOUND</color>\nBalance music, effects and menu sounds.");
            BuildSlider(content, "MasterVolume", "Master volume", 0f, 1f, 0.9f);
            BuildSlider(content, "MusicVolume", "Music", 0f, 1f, 0.7f);
            BuildSlider(content, "SfxVolume", "Effects", 0f, 1f, 0.85f);
            BuildSlider(content, "UiVolume", "Menu sounds", 0f, 1f, 0.8f);
        }

        // Adds the sound section to a GameUI prefab built before it existed (placed under Text size).
        void EnsureAudioSettings()
        {
            var content = transform.Find("SafeArea")?.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(r => r.name == "TextSizeRow")?.parent;
            if (content == null || content.Find("MasterVolumeRow") != null) return;
            int index = content.Find("TextSizeRow").GetSiblingIndex() + 1;
            BuildAudioSliders(content);
            foreach (var name in new[] { "SettingsAudio", "MasterVolumeRow", "MusicVolumeRow", "SfxVolumeRow", "UiVolumeRow" })
                content.Find(name).SetSiblingIndex(index++);
        }

        void BuildSlider(Transform parent, string name, string caption, float min, float max, float value)
        {
            var row = Rect(name + "Row", parent, 0, 0, 1, 1);
            Size(row, new Vector2(0, theme.sliderRowHeight), layout: true);
            Text(name + "Label", row, theme, caption, 30, 0, 0.6f, 0.72f, 0.38f, true);
            Text(name + "Value", row, theme, "100%", 27, 0.76f, 0.6f, 0.23f, 0.38f, false, TextAlignmentOptions.MidlineRight);
            var sliderRect = Rect(name, row, 0.015f, 0, 0.95f, 0.6f);
            var sliderHit = sliderRect.gameObject.AddComponent<UnityEngine.UI.Image>();
            sliderHit.color = new Color(0, 0, 0, 0.001f);
            var slider = sliderRect.gameObject.AddComponent<UnityEngine.UI.Slider>();
            Image(name + "Track", sliderRect, theme.sliderTrack != null ? theme.sliderTrack : theme.panel,
                theme.sliderTrack != null ? Color.white : new Color(.2f, .27f, .35f), 0, .4f, 1, .18f);
            var handleArea = Rect(name + "HandleArea", sliderRect, 0.03f, 0.5f, 0.94f, 0);
            var handle = Image(name + "Handle", handleArea, theme.sliderHandle != null ? theme.sliderHandle : theme.disc,
                theme.sliderHandle != null ? Color.white : ArcaneTheme.Cyan, 0, .5f, 0, 0);
            handle.rectTransform.sizeDelta = new Vector2(48, 48);
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = min; slider.maxValue = max; slider.value = value;
        }

        void BuildHelp(Transform safe)
        {
            var page = Page("Help", safe);
            Header(page, "Help", "Your field guide", "Learn the magic. Find your rhythm.");
            var panel = Panel("HelpPanel", page, theme, 0.13f, 0.15f, 0.74f, 0.61f);
            var content = Scroll("HelpScroll", panel, 0.035f, 0.035f, 0.93f, 0.93f);
            string[] titles = { "MOVE", "AIM & FIRE", "CAST YOUR MAGIC", "FOLLOW THE PORTAL", "YOUR CHECKPOINT" };
            string[] bodies = {
                "Drag the left stick to move. You can move and attack at the same time.",
                AttackHelp,
                SkillHelp,
                "Clear each stage and choose one of three cards before entering its portal. Defeat the dragon in Stage 7 to loop with your build at higher difficulty. Death banks Rebirth points and removes run cards; permanent upgrades and unlocked cards remain.",
                "Continue restarts your saved stage with full health and fresh cooldowns. Your selected hero is saved with it. A new adventure replaces this checkpoint when you confirm Begin Adventure."
            };
            for (int i = 0; i < titles.Length; i++)
                Paragraph("HelpSection" + i, content, theme, $"<color=#C8A363>{i + 1:00} / {titles[i]}</color>\n{bodies[i]}", 31);
            var replay = Button("HelpReplay", content, theme, "Replay control guide", 0, 0, 1, 1);
            Size((RectTransform)replay.transform, new Vector2(0, theme.secondaryHeight), layout: true);
            Paragraph("HelpDesktop", content, theme, "DESKTOP: WASD / arrows to move; hold Space, J, or click to attack; Q active; E ultimate; Tab stance; Esc pause/back.", 26);
        }

        void BuildOutcome(Transform safe, string name, string title, string subtitle, string action, string buttonName)
        {
            var page = Page(name, safe);
            Image(name + "Halo", page, theme.ring, name == "Victory" ? ArcaneTheme.Gold : ArcaneTheme.Muted, 0.4f, 0.62f, 0.2f, 0.26f).preserveAspect = true;
            Image(name + "Portrait", page, theme.heroes[0].PreviewSprite, Color.white, 0.455f, 0.64f, 0.09f, 0.2f).preserveAspect = true;
            Text(name + "Heading", page, theme, title, 64, 0.1f, 0.48f, 0.8f, 0.12f, true, TextAlignmentOptions.Center);
            Text(name + "Subtitle", page, theme, subtitle, 33, 0.1f, 0.4f, 0.8f, 0.07f, false, TextAlignmentOptions.Center);
            Text(name + "Stage", page, theme, "", 27, 0.1f, 0.34f, 0.8f, 0.06f, false, TextAlignmentOptions.Center);
            SizedButton(buttonName, page, theme, action, new Vector2(.5f, .2f), new Vector2(.5f, 0),
                Vector2.zero, new Vector2(360, theme.primaryHeight), true);
            SizedButton(name + "Menu", page, theme, "Main Menu", new Vector2(.5f, .095f), new Vector2(.5f, 0),
                Vector2.zero, new Vector2(300, theme.secondaryHeight));
        }

        void BuildDialog(Transform safe)
        {
            var page = Page("Dialog", safe);
            var panel = Panel("DialogPanel", page, theme, 0.2f, 0.17f, 0.6f, 0.66f);
            Text("DialogTitle", panel, theme, "Ancient magic", 46, 0.055f, 0.77f, 0.89f, 0.18f, true, TextAlignmentOptions.Center);
            var content = Scroll("DialogScroll", panel, 0.06f, 0.3f, 0.88f, 0.44f);
            Paragraph("DialogBody", content, theme, "", 32);
            SizedButton("DialogCancel", panel, theme, "Back", new Vector2(.06f, .055f), Vector2.zero,
                Vector2.zero, new Vector2(230, theme.secondaryHeight));
            SizedButton("DialogConfirm", panel, theme, "Confirm", new Vector2(.94f, .055f), new Vector2(1, 0),
                Vector2.zero, new Vector2(250, theme.primaryHeight), true);
        }

        void BuildTutorial(Transform safe)
        {
            var page = Page("Tutorial", safe);
            var panel = Panel("TutorialPanel", page, theme, 0.2f, 0.15f, 0.6f, 0.7f);
            Text("TutorialStep", panel, theme, "01 / 04", 26, 0.05f, 0.85f, 0.9f, 0.08f, false, TextAlignmentOptions.Center).color = ArcaneTheme.Cyan;
            Text("TutorialTitle", panel, theme, "Find your footing", 48, 0.04f, 0.69f, 0.92f, 0.13f, true, TextAlignmentOptions.Center);
            Image("TutorialIcon", panel, theme.ring, ArcaneTheme.Gold, 0.41f, 0.46f, 0.18f, 0.21f).preserveAspect = true;
            var content = Scroll("TutorialScroll", panel, 0.07f, 0.27f, 0.86f, 0.17f);
            Paragraph("TutorialBody", content, theme, "Drag the left stick to move.", 33);
            SizedButton("TutorialSkip", panel, theme, "Skip Guide", new Vector2(.07f, .04f), Vector2.zero,
                Vector2.zero, new Vector2(240, theme.secondaryHeight));
            SizedButton("TutorialNext", panel, theme, "Next", new Vector2(.93f, .04f), new Vector2(1, 0),
                Vector2.zero, new Vector2(220, theme.primaryHeight), true);
        }
    }
}
