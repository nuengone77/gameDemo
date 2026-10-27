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
        }

        RectTransform Page(string name, Transform parent, bool backdrop = true)
        {
            var page = Rect(name, parent, 0, 0, 1, 1);
            page.gameObject.AddComponent<CanvasGroup>();
            if (backdrop) Image(name + "Shade", page, null, new Color(0.035f, 0.065f, 0.11f, 0.96f), 0, 0, 1, 1, true);
            return page;
        }

        void Header(Transform page, string prefix, string title, string subtitle)
        {
            Text(prefix + "Heading", page, theme, title, 52, 0.21f, 0.88f, 0.58f, 0.085f, true, TextAlignmentOptions.Center);
            Text(prefix + "Subtitle", page, theme, subtitle, 27, 0.18f, 0.82f, 0.64f, 0.05f, false, TextAlignmentOptions.Center);
            Button(prefix + "Back", page, theme, "Back", 0.035f, 0.835f, 0.12f, 0.13f);
            Image(prefix + "Rule", page, null, ArcaneTheme.Gold, 0.04f, 0.8f, 0.92f, 0.002f);
        }

        void BuildMenu(Transform safe)
        {
            var page = Page("Menu", safe, false);
            var emblem = Image("MenuEmblem", page, theme.ring, ArcaneTheme.Gold, 0.44f, 0.76f, 0.12f, 0.18f);
            emblem.preserveAspect = true;
            Text("MenuEyebrow", page, theme, "FIVE HEROES  /  ONE DESTINY", 25, 0.15f, 0.72f, 0.7f, 0.05f, false, TextAlignmentOptions.Center).color = ArcaneTheme.Cyan;
            Text("MenuTitle", page, theme, "Magic Ring Quest", 83, 0.08f, 0.59f, 0.84f, 0.13f, true, TextAlignmentOptions.Center);
            Text("MenuTagline", page, theme, "Beyond the village, an ancient power awakens.", 29, 0.13f, 0.53f, 0.74f, 0.06f, false, TextAlignmentOptions.Center);
            var panel = Panel("MenuPanel", page, theme, 0.33f, 0.06f, 0.34f, 0.47f);
            Button("MenuContinue", panel, theme, "Continue", 0.07f, 0.69f, 0.86f, 0.28f, true);
            Button("MenuNew", panel, theme, "New Adventure", 0.07f, 0.36f, 0.86f, 0.28f);
            Button("MenuStory", panel, theme, "Story", StoryRow[0], 0.03f, StoryRowWidth, 0.28f);
            Button("MenuHelp", panel, theme, "How to Play", StoryRow[1], 0.03f, StoryRowWidth, 0.28f);
            Button("MenuSettings", panel, theme, "Settings", StoryRow[2], 0.03f, StoryRowWidth, 0.28f);
            Text("MenuCheckpoint", panel, theme, "", 22, 0.06f, 0.645f, 0.88f, 0.055f, false, TextAlignmentOptions.Center);
            Text("MenuFooter", page, theme, "AN ADVENTURE IN SEVEN CHAPTERS", 23, 0.15f, 0.005f, 0.7f, 0.045f, false, TextAlignmentOptions.Center);
        }

        // Bottom row of the menu panel: Story, How to Play, Settings.
        static readonly float[] StoryRow = { 0.07f, 0.365f, 0.66f };
        const float StoryRowWidth = 0.27f;

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
            if (panel == null || panel.Find("MenuStory") != null) return;
            void Place(string name, int slot)
            {
                var rect = (RectTransform)panel.Find(name);
                if (rect == null) return;
                rect.anchorMin = new Vector2(StoryRow[slot], rect.anchorMin.y);
                rect.anchorMax = new Vector2(StoryRow[slot] + StoryRowWidth, rect.anchorMax.y);
            }
            Place("MenuHelp", 1);
            Place("MenuSettings", 2);
            Button("MenuStory", panel, theme, "Story", StoryRow[0], 0.03f, StoryRowWidth, 0.28f);
            BuildStory(safe);
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
                button.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 144;
                var preview = Image("HeroPortrait" + i, button.transform, hero.PreviewSprite, Color.white, 0.025f, 0.05f, 0.23f, 0.9f);
                preview.preserveAspect = true;
                Text("HeroName" + i, button.transform, theme, hero.displayName, 30, 0.29f, 0.48f, 0.68f, 0.45f, true);
                Text("HeroRole" + i, button.transform, theme, hero.combat.RoleName, 23, 0.29f, 0.05f, 0.68f, 0.4f).color = ArcaneTheme.HeroColors[i];
            }
            Image("HeroHalo", page, theme.ring, new Color(0.78f, 0.64f, 0.39f, 0.3f), 0.3f, 0.24f, 0.24f, 0.43f).preserveAspect = true;
            Image("SelectedHero", page, theme.heroes[0].PreviewSprite, Color.white, 0.33f, 0.25f, 0.18f, 0.42f).preserveAspect = true;
            Text("SelectedName", page, theme, "New", 47, 0.3f, 0.68f, 0.24f, 0.075f, true, TextAlignmentOptions.Center);
            Text("SelectedRole", page, theme, "Tanky", 28, 0.3f, 0.61f, 0.24f, 0.06f, false, TextAlignmentOptions.Center);
            Text("HeroSelectedBadge", page, theme, "SELECTED", 23, 0.3f, 0.16f, 0.24f, 0.05f, false, TextAlignmentOptions.Center).color = ArcaneTheme.Cyan;
            var detail = Panel("HeroDetails", page, theme, 0.565f, 0.15f, 0.4f, 0.61f);
            var content = Scroll("HeroSummaryScroll", detail, 0.025f, 0.035f, 0.95f, 0.93f);
            Paragraph("HeroSummary", content, theme, "Protect yourself with astral shields and control the battlefield.", 29);
            string[] kinds = { "Basic attack", "Active skill", "Passive skill", "Ultimate" };
            for (int i = 0; i < 4; i++)
            {
                var button = Button("HeroAbility" + i, content, theme, "", 0, 0, 1, 1);
                button.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 144;
                Image("HeroAbilityIcon" + i, button.transform, theme.symbols[i], ArcaneTheme.Gold, 0.02f, 0.16f, 0.09f, 0.68f).preserveAspect = true;
                Text("HeroAbilityKind" + i, button.transform, theme, kinds[i].ToUpperInvariant(), 20, 0.14f, 0.54f, 0.81f, 0.4f);
                Text("HeroAbilityTitle" + i, button.transform, theme, "Ability", 27, 0.14f, 0.03f, 0.81f, 0.5f, true);
            }
            Button("HeroesBegin", page, theme, "Begin Adventure", 0.64f, 0.015f, 0.325f, 0.13f, true);
            Text("HeroesFootnote", page, theme, "Swipe the roster and abilities to explore all five heroes.", 25, 0.035f, 0.04f, 0.58f, 0.06f);
        }

        void BuildHUD(Transform safe)
        {
            var page = Page("HUD", safe, false);
            var health = Panel("HealthPanel", page, theme, 0.025f, 0.815f, 0.27f, 0.16f);
            Image("HUDPortrait", health, theme.heroes[0].PreviewSprite, Color.white, 0.025f, 0.15f, 0.14f, 0.75f).preserveAspect = true;
            Text("HUDHero", health, theme, "New / Tanky", 26, 0.19f, 0.62f, 0.78f, 0.33f, true);
            Image("HealthTrack", health, null, new Color(0.06f, 0.1f, 0.13f), 0.2f, 0.39f, 0.74f, 0.16f);
            var fill = Image("HealthFill", health, theme.disc, new Color32(111, 198, 153, 255), 0.2f, 0.39f, 0.74f, 0.16f);
            fill.sprite = null;
            Text("HUDHealth", health, theme, "100 / 100 HP", 22, 0.19f, 0.04f, 0.4f, 0.31f);
            Text("HUDShield", health, theme, "", 22, 0.59f, 0.04f, 0.38f, 0.31f).color = ArcaneTheme.Cyan;
            Panel("StagePanel", page, theme, 0.32f, 0.875f, 0.36f, 0.1f);
            Text("HUDStage", page, theme, "Stage 1", 29, 0.32f, 0.925f, 0.36f, 0.055f, true, TextAlignmentOptions.Center);
            Text("HUDObjective", page, theme, "Defeat the remaining enemies", 24, 0.32f, 0.875f, 0.36f, 0.05f, false, TextAlignmentOptions.Center);
            Button("HUDPause", page, theme, "Pause", 0.865f, 0.84f, 0.11f, 0.135f);
            var boss = Panel("BossPanel", page, theme, 0.34f, 0.77f, 0.32f, 0.095f);
            Text("HUDBoss", boss, theme, "Inferno Dragon", 26, 0.05f, 0.43f, 0.9f, 0.5f, true, TextAlignmentOptions.Center);
            Image("BossTrack", boss, null, new Color(0.12f, 0.06f, 0.08f), 0.04f, 0.16f, 0.92f, 0.15f);
            Image("BossFill", boss, null, new Color32(222, 116, 117, 255), 0.04f, 0.16f, 0.92f, 0.15f);
            Text("HUDSpecial", page, theme, "", 27, 0.03f, 0.75f, 0.26f, 0.05f);
            Text("HUDBanner", page, theme, "", 45, 0.2f, 0.6f, 0.6f, 0.08f, true, TextAlignmentOptions.Center);
            var controls = Rect("TouchControls", page, 0, 0, 1, 1);
            controls.gameObject.AddComponent<CanvasGroup>();
            BuildStick(controls, "MoveStick", false);
            BuildStick(controls, "AimStick", true);
            BuildAction(controls, "Active", "Active", 1, -420, 270);
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
            rect.sizeDelta = new Vector2(170, 170);
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
            var panel = Panel("PausePanel", page, theme, 0.24f, 0.08f, 0.52f, 0.62f);
            Button("PauseResume", panel, theme, "Resume", 0.07f, 0.7f, 0.86f, 0.23f, true);
            Button("PauseHelp", panel, theme, "How to Play", 0.07f, 0.395f, 0.41f, 0.23f);
            Button("PauseSettings", panel, theme, "Settings", 0.52f, 0.395f, 0.41f, 0.23f);
            Button("PauseRestart", panel, theme, "Restart Stage", 0.07f, 0.09f, 0.41f, 0.23f);
            Button("PauseMenu", panel, theme, "Main Menu", 0.52f, 0.09f, 0.41f, 0.23f);
        }

        void BuildSettings(Transform safe)
        {
            var page = Page("Settings", safe);
            Header(page, "Settings", "Make it yours", "Controls and readability. Saved automatically.");
            var panel = Panel("SettingsPanel", page, theme, 0.16f, 0.19f, 0.68f, 0.57f);
            var content = Scroll("SettingsScroll", panel, 0.035f, 0.025f, 0.93f, 0.95f);
            Paragraph("SettingsIntro", content, theme, "<color=#C8A363>TOUCH CONTROLS</color>\nAdjust the controls to fit your thumbs.");
            BuildSlider(content, "ControlSize", "Control size", 0.8f, 1.2f, 1f);
            BuildSlider(content, "ControlOpacity", "Control opacity", 0.3f, 1f, 0.65f);
            Paragraph("SettingsAccess", content, theme, "<color=#C8A363>ACCESSIBILITY</color>\nMake important information easier to read.");
            BuildSlider(content, "TextSize", "Text size", 1f, 1.5f, 1f);
            BuildAudioSliders(content);
            var motion = Button("ReducedMotion", content, theme, "Reduced UI motion: Off", 0, 0, 1, 1);
            motion.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 144;
            Paragraph("SettingsMotionInfo", content, theme, "Reduced UI motion removes decorative rotation and menu fades. Combat effects remain visible.", 26);
            Button("SettingsDefaults", page, theme, "Restore Defaults", 0.35f, 0.025f, 0.3f, 0.13f);
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
            int before = content.childCount;
            BuildAudioSliders(content);
            for (int i = before; i < content.childCount; i++) content.GetChild(i).SetSiblingIndex(index++);
        }

        void BuildSlider(Transform parent, string name, string caption, float min, float max, float value)
        {
            var row = Rect(name + "Row", parent, 0, 0, 1, 1);
            row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 240;
            Text(name + "Label", row, theme, caption, 30, 0, 0.7f, 0.72f, 0.25f, true);
            Text(name + "Value", row, theme, "100%", 27, 0.76f, 0.7f, 0.23f, 0.25f, false, TextAlignmentOptions.MidlineRight);
            var sliderRect = Rect(name, row, 0.015f, 0, 0.95f, 0.6f);
            var sliderHit = sliderRect.gameObject.AddComponent<UnityEngine.UI.Image>();
            sliderHit.color = new Color(0, 0, 0, 0.001f);
            var slider = sliderRect.gameObject.AddComponent<UnityEngine.UI.Slider>();
            Image(name + "Track", sliderRect, theme.panel, new Color(0.2f, 0.27f, 0.35f), 0, 0.35f, 1, 0.3f, true);
            var handleArea = Rect(name + "HandleArea", sliderRect, 0.03f, 0.5f, 0.94f, 0);
            var handle = Image(name + "Handle", handleArea, theme.disc, ArcaneTheme.Cyan, 0, 0.5f, 0, 0, true);
            handle.rectTransform.sizeDelta = new Vector2(65, 65);
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
                "Drag the right stick to aim and fire continuously. Release to stop firing. Its direction and distance remain available for your next skill.",
                "Tap Active or Ultimate to cast. Ground-targeted abilities land at their marked destination. The marker uses the last aim direction and distance. Cooldown numbers show when each spell returns. Daphne can also switch Solar and Lunar stance.",
                "Defeat every enemy in a stage to open its portal. Walk into the portal to reach the next stage. Defeat the dragon in Stage 7 to complete your adventure.",
                "Continue restarts your saved stage with full health and fresh cooldowns. Your selected hero is saved with it. A new adventure replaces this checkpoint when you confirm Begin Adventure."
            };
            for (int i = 0; i < titles.Length; i++)
                Paragraph("HelpSection" + i, content, theme, $"<color=#C8A363>{i + 1:00} / {titles[i]}</color>\n{bodies[i]}", 31);
            var replay = Button("HelpReplay", content, theme, "Replay control guide", 0, 0, 1, 1);
            replay.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 144;
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
            Button(buttonName, page, theme, action, 0.34f, 0.19f, 0.32f, 0.135f, true);
            Button(name + "Menu", page, theme, "Main Menu", 0.34f, 0.025f, 0.32f, 0.135f);
        }

        void BuildDialog(Transform safe)
        {
            var page = Page("Dialog", safe);
            var panel = Panel("DialogPanel", page, theme, 0.2f, 0.17f, 0.6f, 0.66f);
            Text("DialogTitle", panel, theme, "Ancient magic", 46, 0.055f, 0.77f, 0.89f, 0.18f, true, TextAlignmentOptions.Center);
            var content = Scroll("DialogScroll", panel, 0.06f, 0.3f, 0.88f, 0.44f);
            Paragraph("DialogBody", content, theme, "", 32);
            Button("DialogCancel", panel, theme, "Back", 0.06f, 0.055f, 0.41f, 0.2f);
            Button("DialogConfirm", panel, theme, "Confirm", 0.53f, 0.055f, 0.41f, 0.2f, true);
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
            Button("TutorialSkip", panel, theme, "Skip Guide", 0.07f, 0.04f, 0.4f, 0.2f);
            Button("TutorialNext", panel, theme, "Next", 0.53f, 0.04f, 0.4f, 0.2f, true);
        }
    }
}
