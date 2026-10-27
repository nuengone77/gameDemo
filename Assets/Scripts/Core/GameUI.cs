using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace MagicGame
{
    [DefaultExecutionOrder(100)]
    public partial class GameUI : MonoBehaviour
    {
        public ArcaneTheme theme;
        public enum ScreenId { Menu, Heroes, HUD, Pause, Settings, Help, Defeat, Victory, Dialog, Tutorial, Cards, Rebirth, Build, Story }
        public static GameUI Instance { get; private set; }
        public static bool GameplayBlocked => Instance != null && Instance._level != null &&
            Instance._stack.Count > 0 && Instance.CurrentScreen != ScreenId.HUD;
        public ScreenId CurrentScreen => _stack.Count > 0 ? _stack[_stack.Count - 1] : ScreenId.Menu;
        public int ScreenDepth => _stack.Count;

        readonly Dictionary<string, GameObject> _nodes = new();
        readonly Dictionary<TMP_Text, float> _fontSizes = new();
        readonly List<ScreenId> _stack = new();
        readonly Dictionary<ScreenId, GameObject> _focus = new();
        UISettings _settings;
        LevelManager _level;
        PlayerController _player;
        Health _boss;
        Action _confirm;
        Rect _safeArea;
        Vector2Int _screenSize;
        int _tutorialStep;
        float _bannerUntil, _fadeStart;
        bool _started;
        float _activeReady, _ultimateReady, _stanceReady;
        string _banner;
        GameObject _lastSelected;

        static readonly string[] Summaries = {
            "A resilient ranged defender. Turn damage into shields, block incoming shots, and call down an astral meteor.",
            "An agile kunai fighter. Chain hits to build attack speed, dash out of danger, and strike enemies across the battlefield.",
            "A trickster who fights with ricocheting cards. Lay explosive traps and escape with a storm of magic.",
            "A powerful control mage. Slow enemies, pierce defenses with arcane orbs, and deliver guaranteed judgment.",
            "A celestial mage with two stances. Solar magic burns groups; Lunar magic binds a target. Eclipse damages foes and restores health."
        };
        static readonly string[][] AbilityNames = {
            new[] { "Light Crystal Shot", "Astral Shield Wall", "Radiant Conversion", "Astral Meteor" },
            new[] { "Shadow Kunai", "Shadow Overdrive", "Relentless Assault", "Shadow Execution" },
            new[] { "Ricochet Cards", "Illusion Trap", "Illusion Dodge", "Jackpot" },
            new[] { "Arcane Orb", "Time Distortion Field", "Distorted Trajectory", "Silent Judgment" },
            new[] { "Dual Celestial Attack", "Celestial Control", "Celestial Transition", "Eclipse" }
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        public static void EnsureForScene()
        {
            if (Instance != null || FindAnyObjectByType<GameUI>() != null) return;
            var prefab = Resources.Load<GameObject>("UI/GameUI");
            if (prefab != null) Instantiate(prefab);
            else Debug.LogError("Arcane UI prefab is missing. Run Tools > Magic Game > Set Up Mobile UI.");
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            GameplayInput.Reset();
            _settings = UISettings.Load();
            if (transform.Find("SafeArea") == null) BuildLayout();
            EnsureAudioSettings(); // the saved prefab predates the audio sliders
            EnsureStory();
            BuildProgressionLayout();
            EnsureSkillControls();
            foreach (var rect in GetComponentsInChildren<RectTransform>(true)) _nodes[rect.name] = rect.gameObject;
            foreach (var text in GetComponentsInChildren<TMP_Text>(true)) _fontSizes[text] = text.fontSize;
            EnsureEventSystem();
            WireButtons();
            WireProgressionButtons();
            ApplySafeArea();
            ApplySettings();
        }

        void Start()
        {
            _level = LevelManager.Instance;
            _player = PlayerController.Instance;
            _started = true;
            Progression.Changed += RefreshProgression;
            if (_level == null)
            {
                Progression.MigrateLegacy();
                ShowRoot(ScreenId.Menu);
                RefreshMenu();
                return;
            }
            _level.StateChanged += OnLevelState;
            LevelManager.EnemiesChanged += RefreshEnemies;
            if (_player != null)
            {
                _player.Health.Changed += RefreshHealth;
                _player.Skills.Changed += RefreshSkills;
                I("HUDPortrait").sprite = _player.CurrentCharacter != null ? _player.CurrentCharacter.PreviewSprite : null;
                T("HUDHero").text = _player.CurrentCharacter != null ? _player.CurrentCharacter.MenuName : "Hero";
                RefreshHealth(_player.Health);
                RefreshSkills();
            }
            T("HUDStage").text = $"Stage {_level.levelNumber} / {_level.levelTitle}";
            ShowRoot(ScreenId.HUD);
            RefreshEnemies();
            Banner($"Stage {_level.levelNumber} · {_level.levelTitle}");
            if (_level.levelNumber == 1 && PlayerPrefs.GetInt(GameSession.TutorialKey, 0) == 0) BeginTutorial();
            else PresentCards();
            RefreshProgression();
        }

        void EnsureEventSystem()
        {
            var systems = FindObjectsByType<EventSystem>();
            var system = systems.Length > 0 ? systems[0] : new GameObject("UI Event System").AddComponent<EventSystem>();
            for (int i = 1; i < systems.Length; i++) systems[i].gameObject.SetActive(false);
            var oldModule = system.GetComponent<StandaloneInputModule>();
            if (oldModule != null) { oldModule.enabled = false; Destroy(oldModule); }
            var module = system.GetComponent<InputSystemUIInputModule>();
            if (module == null) module = system.gameObject.AddComponent<InputSystemUIInputModule>();
            if (module.actionsAsset == null) module.AssignDefaultActions();
            module.pointerBehavior = UIPointerBehavior.AllPointersAsIs;
        }

        GameObject N(string name) => _nodes[name];
        TMP_Text T(string name) => N(name).GetComponent<TMP_Text>();
        UnityEngine.UI.Image I(string name) => N(name).GetComponent<UnityEngine.UI.Image>();
        UnityEngine.UI.Button B(string name) => N(name).GetComponent<UnityEngine.UI.Button>();
        void Click(string name, Action action) => B(name).onClick.AddListener(() =>
        {
            GameAudio.Play(b => b.uiClick, true);
            action();
        });

        void WireButtons()
        {
            Click("MenuContinue", Continue);
            Click("MenuNew", () => { SelectHero(GameSession.SelectedCharacter); Push(ScreenId.Heroes); });
            Click("MenuHelp", () => Push(ScreenId.Help));
            Click("MenuSettings", () => Push(ScreenId.Settings));
            Click("MenuStory", OpenStory);
            Click("StoryClose", Back);
            foreach (var id in new[] { "Heroes", "Settings", "Help" }) Click(id + "Back", Back);
            for (int i = 0; i < theme.heroes.Length; i++)
            {
                int hero = i;
                Click("Hero" + i, () => SelectHero(hero));
            }
            for (int i = 0; i < 4; i++)
            {
                int ability = i;
                Click("HeroAbility" + i, () => ShowAbility(ability));
            }
            Click("HeroesBegin", () =>
            {
                if (Progression.HasRun || GameSession.TryGetCheckpoint(out _))
                    Confirm("Begin a new adventure?", "Your current run will end and its earned points will be banked. Run cards will be lost. Your selected hero begins at Stage 1.", StartAdventure);
                else StartAdventure();
            });
            Click("HUDPause", () => Push(ScreenId.Pause));
            Click("PauseResume", Back);
            Click("PauseHelp", () => Push(ScreenId.Help));
            Click("PauseSettings", () => Push(ScreenId.Settings));
            Click("PauseRestart", () => Confirm("End this run?", "Bank earned points and lose this run's cards. Permanent rebirth bonuses remain.", () => { Progression.Finish(true); _level.ReturnToMenu(); }));
            Click("PauseMenu", () => _level.ReturnToMenu());
            Click("DefeatRetry", () => _level.RestartStage());
            Click("DefeatMenu", () => _level.ReturnToMenu());
            Click("VictoryMenu", () => _level.ReturnToMenu());
            Click("VictoryAgain", () => { GameSession.Load("MainMenu"); _openHeroesAfterLoad = true; });
            Click("HUDActive", () => OnSkillButtonClick(SkillSlot.Active));
            Click("HUDUltimate", () => OnSkillButtonClick(SkillSlot.Ultimate));
            Click("HUDStance", () =>
            {
                SkillJoystick.CancelCurrent();
                GameplayInput.ClearSkillRequests();
                GameplayInput.RequestStance();
            });
            Click("DialogCancel", Back);
            Click("DialogConfirm", () => { var callback = _confirm; _confirm = null; Back(); callback?.Invoke(); });
            Click("TutorialSkip", EndTutorial);
            Click("TutorialNext", () => { if (++_tutorialStep >= 4) EndTutorial(); else RefreshTutorial(); });
            Click("HelpReplay", BeginTutorial);
            Click("ReducedMotion", () => { _settings.reducedMotion = !_settings.reducedMotion; SaveSettings(); });
            Click("SettingsDefaults", () => { _settings = new UISettings(); SyncSliders(); SaveSettings(); });
            Slider("ControlSize", value => _settings.controlSize = value);
            Slider("ControlOpacity", value => _settings.controlOpacity = value);
            Slider("TextSize", value => _settings.textSize = value);
            Slider("MasterVolume", value => _settings.masterVolume = value);
            Slider("MusicVolume", value => _settings.musicVolume = value);
            Slider("SfxVolume", value => _settings.sfxVolume = value);
            Slider("UiVolume", value => _settings.uiVolume = value);
            SyncSliders();
        }

        static bool _openHeroesAfterLoad;

        void Slider(string name, Action<float> action)
        {
            N(name).GetComponent<UnityEngine.UI.Slider>().onValueChanged.AddListener(value => { action(value); SaveSettings(); });
        }

        void SyncSliders()
        {
            N("ControlSize").GetComponent<UnityEngine.UI.Slider>().SetValueWithoutNotify(_settings.controlSize);
            N("ControlOpacity").GetComponent<UnityEngine.UI.Slider>().SetValueWithoutNotify(_settings.controlOpacity);
            N("TextSize").GetComponent<UnityEngine.UI.Slider>().SetValueWithoutNotify(_settings.textSize);
            N("MasterVolume").GetComponent<UnityEngine.UI.Slider>().SetValueWithoutNotify(_settings.masterVolume);
            N("MusicVolume").GetComponent<UnityEngine.UI.Slider>().SetValueWithoutNotify(_settings.musicVolume);
            N("SfxVolume").GetComponent<UnityEngine.UI.Slider>().SetValueWithoutNotify(_settings.sfxVolume);
            N("UiVolume").GetComponent<UnityEngine.UI.Slider>().SetValueWithoutNotify(_settings.uiVolume);
        }

        void SaveSettings() { _settings.Save(); ApplySettings(); }

        void ApplySettings()
        {
            foreach (var pair in _fontSizes)
            {
                // Large display headings remain fixed; body text and control labels honor the text setting.
                pair.Key.fontSize = pair.Value >= 45 ? pair.Value : pair.Value * _settings.textSize;
            }
            for (int i = 0; i < 3; i++)
            {
                var card = T("CardChoice" + i + "Label");
                card.fontSizeMin = 22 * _settings.textSize;
                card.fontSizeMax = 28 * _settings.textSize;
            }
            foreach (var name in new[] { "CardsInfo", "RebirthSubtitle", "BuildSubtitle", "HUDProgression", "HUDHealth", "MoveStickCaption", "AimStickCaption" })
            {
                var label = T(name);
                label.fontSizeMin = _fontSizes[label];
                label.fontSizeMax = _fontSizes[label] * _settings.textSize;
            }
            foreach (var name in new[] { "MenuStoryLabel", "MenuHelpLabel", "MenuSettingsLabel" })
            {
                var label = T(name);
                label.enableAutoSizing = true;
                label.fontSizeMin = _fontSizes[label];
                label.fontSizeMax = _fontSizes[label] * _settings.textSize;
            }
            N("TouchControls").GetComponent<CanvasGroup>().alpha = _settings.controlOpacity;
            T("SkillCancelLabel").fontSizeMax = 26 * _settings.textSize;
            foreach (var name in new[] { "ActiveCaption", "UltimateCaption", "StanceCaption" })
            {
                T(name).fontSizeMin = _fontSizes[T(name)] * .75f * _settings.textSize;
                T(name).fontSizeMax = _fontSizes[T(name)] * _settings.textSize;
            }
            ScaleTopHUD("HealthPanel", 0.815f, 0.975f);
            ScaleTopHUD("StagePanel", 0.875f, 0.975f);
            ScaleTopHUD("HUDStage", 0.925f, 0.975f);
            ScaleTopHUD("HUDObjective", 0.875f, 0.925f);
            ScaleTopHUD("BossPanel", 0.77f, 0.865f);
            ScaleTopHUD("HUDSpecial", 0.75f, 0.8f);
            ScaleTopHUD("HUDProgression", .71f, .755f);
            foreach (var metrics in GetComponentsInChildren<ArcaneControlSize>(true))
                metrics.Apply(_settings.textSize);
            foreach (var name in new[] { "HUDActive", "HUDUltimate", "HUDStance" })
                ((RectTransform)N(name).transform).sizeDelta = Vector2.one * theme.actionSize * Mathf.Max(1, _settings.textSize * .82f);
            foreach (var name in new[] { "MoveStick", "AimStick", "HUDActive", "HUDUltimate", "HUDStance" })
            {
                var rect = (RectTransform)N(name).transform;
                rect.localScale = Vector3.one * _settings.controlSize;
            }
            T("ControlSizeValue").text = $"{_settings.controlSize:P0}";
            T("ControlOpacityValue").text = $"{_settings.controlOpacity:P0}";
            T("TextSizeValue").text = $"{_settings.textSize:P0}";
            T("MasterVolumeValue").text = $"{_settings.masterVolume:P0}";
            T("MusicVolumeValue").text = $"{_settings.musicVolume:P0}";
            T("SfxVolumeValue").text = $"{_settings.sfxVolume:P0}";
            T("UiVolumeValue").text = $"{_settings.uiVolume:P0}";
            GameAudio.ApplyVolumes(_settings);
            T("ReducedMotionLabel").text = "Reduced UI motion: " + (_settings.reducedMotion ? "On" : "Off");
            if (_settings.reducedMotion && _stack.Count > 0) N(CurrentScreen.ToString()).GetComponent<CanvasGroup>().alpha = 1;
            FitMenuGroups();
            PositionSkillCancel();
        }

        void ScaleTopHUD(string name, float bottom, float top)
        {
            var rect = (RectTransform)N(name).transform;
            var min = rect.anchorMin;
            var max = rect.anchorMax;
            min.y = 0.975f - (0.975f - bottom) * _settings.textSize;
            max.y = 0.975f - (0.975f - top) * _settings.textSize;
            rect.anchorMin = min;
            rect.anchorMax = max;
        }

        void ApplySafeArea()
        {
            _screenSize = new Vector2Int(Screen.width, Screen.height);
            _safeArea = Screen.safeArea;
            var safe = (RectTransform)N("SafeArea").transform;
            // Game view/device resize can briefly report the previous safe-area dimensions.
            var min = new Vector2(Mathf.Clamp01(_safeArea.xMin / Mathf.Max(1, _screenSize.x)), Mathf.Clamp01(_safeArea.yMin / Mathf.Max(1, _screenSize.y)));
            var max = new Vector2(Mathf.Clamp01(_safeArea.xMax / Mathf.Max(1, _screenSize.x)), Mathf.Clamp01(_safeArea.yMax / Mathf.Max(1, _screenSize.y)));
            bool valid = max.x > min.x && max.y > min.y;
            safe.anchorMin = valid ? min : Vector2.zero;
            safe.anchorMax = valid ? max : Vector2.one;
            safe.offsetMin = new Vector2(12, 12);
            safe.offsetMax = new Vector2(-12, -12);
            FitMenuGroups();
            ClearInput();
            PositionSkillCancel();
        }

        void FitMenuGroups()
        {
            var safe = (RectTransform)N("SafeArea").transform;
            Canvas.ForceUpdateCanvases();
            foreach (var name in new[] { "MenuPanel", "PausePanel" })
            {
                var panel = (RectTransform)N(name).transform;
                if (!panel.gameObject.activeInHierarchy) continue;
                var scroll = panel.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
                if (scroll == null) continue; // Older authored prefabs remain usable.
                var sizes = scroll.content.GetComponentsInChildren<ArcaneControlSize>(true);
                foreach (var size in sizes) size.Apply(_settings.textSize);
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                float available = Mathf.Max(theme.secondaryHeight + 64, safe.rect.height * panel.anchorMin.y - 64);
                float preferred = UnityEngine.UI.LayoutUtility.GetPreferredHeight(scroll.content);
                if (preferred > available && _settings.textSize > 1f)
                {
                    // Fit wide screens while preserving the minimum touch target size.
                    var layout = scroll.content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
                    int rows = 0;
                    foreach (Transform child in scroll.content) if (child.gameObject.activeSelf) rows++;
                    float spacing = layout.padding.vertical + layout.spacing * Mathf.Max(0, rows - 1);
                    float scale = Mathf.Clamp01((available - spacing) / Mathf.Max(1, preferred - spacing));
                    foreach (var size in sizes)
                    {
                        if (!size.TryGetComponent<UnityEngine.UI.LayoutElement>(out var element)) continue;
                        element.minHeight = element.preferredHeight = Mathf.Max(theme.secondaryHeight, size.referenceSize.y * _settings.textSize * scale);
                    }
                    UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                    preferred = UnityEngine.UI.LayoutUtility.GetPreferredHeight(scroll.content);
                }
                panel.sizeDelta = new Vector2(Mathf.Min(660 + (name == "PausePanel" ? 80 : 0), safe.rect.width - 48), Mathf.Min(preferred, available));
            }
            Canvas.ForceUpdateCanvases();
        }

        void Update()
        {
            if (!_started) return;
            if (_screenSize.x != Screen.width || _screenSize.y != Screen.height || _safeArea != Screen.safeArea) ApplySafeArea();
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Back();
            var gamepad = Gamepad.current;
            if (gamepad != null && gamepad.buttonEast.wasPressedThisFrame) Back();
            if (_openHeroesAfterLoad && _level == null)
            {
                _openHeroesAfterLoad = false;
                SelectHero(GameSession.SelectedCharacter);
                Push(ScreenId.Heroes);
            }
            var group = N(CurrentScreen.ToString()).GetComponent<CanvasGroup>();
            group.alpha = _settings.reducedMotion ? 1 : Mathf.Clamp01((Time.unscaledTime - _fadeStart) / 0.16f);
            KeepSelectionVisible();
            if (CurrentScreen != ScreenId.HUD || _player == null) return;
            UpdateCooldown("Active", _activeReady, _player.Skills.Config != null ? _player.Skills.Config.activeCooldown : 1);
            UpdateCooldown("Ultimate", _ultimateReady, _player.Skills.Config != null ? _player.Skills.Config.ultimateCooldown : 1);
            UpdateCooldown("Stance", _stanceReady, _player.Skills.Config != null ? _player.Skills.Config.stanceCooldown : 1);
            T("HUDBanner").text = Time.time < _bannerUntil ? _banner : "";
            UpdateMarker();
        }

        void UpdateMarker()
        {
            var config = _player.Skills.Config;
            bool marker = config != null && _level.IsPlaying && !Application.isMobilePlatform &&
                SkillJoystick.Current == null && (config.role == HeroRole.Tanky || config.role == HeroRole.OracleWitch);
            N("AimMarker").SetActive(marker);
            if (!marker || Camera.main == null) return;
            var anim = _player.GetComponent<DirectionalSpriteAnimator>();
            var target = GameplayInput.Target(_player.transform.position, anim.FacingVector, config.range);
            var screen = Camera.main.WorldToScreenPoint(target);
            var rect = (RectTransform)N("AimMarker").transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)rect.parent, screen, null, out var local);
            rect.anchoredPosition = local;
            T("AimMarkerLabel").text = config.role == HeroRole.Tanky ? "ULT" : "ACTIVE";
        }

        void KeepSelectionVisible()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == _lastSelected) return;
            _lastSelected = selected;
            if (selected == null) return;
            var scroll = selected.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            if (scroll == null || scroll.content == null) return;
            Canvas.ForceUpdateCanvases();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, selected.transform);
            var area = scroll.viewport.rect;
            float shift = bounds.min.y < area.yMin ? area.yMin - bounds.min.y :
                bounds.max.y > area.yMax ? area.yMax - bounds.max.y : 0;
            scroll.content.anchoredPosition += Vector2.up * shift;
            scroll.StopMovement();
        }

        void UpdateCooldown(string name, float readyAt, float duration)
        {
            float remaining = Mathf.Max(0, readyAt - Time.time);
            I(name + "Cooldown").fillAmount = duration > 0 ? Mathf.Clamp01(remaining / duration) : 0;
            T(name + "Time").text = remaining > 0 ? Mathf.CeilToInt(remaining).ToString() : "";
            B("HUD" + name).interactable = remaining <= 0 && _player.Skills.CanAct;
        }

        void ClearInput()
        {
            SkillJoystick.CancelCurrent();
            GameplayInput.Reset();
            foreach (var stick in GetComponentsInChildren<TouchStick>(true)) stick.Clear();
        }

        public void Push(ScreenId screen)
        {
            if (_stack.Contains(screen)) return;
            if (EventSystem.current != null) _focus[CurrentScreen] = EventSystem.current.currentSelectedGameObject;
            _stack.Add(screen);
            RefreshStack();
        }

        void ShowRoot(ScreenId screen)
        {
            _stack.Clear();
            _stack.Add(screen);
            RefreshStack();
        }

        public void Back()
        {
            if (_stack.Count == 0) return;
            if (CurrentScreen == ScreenId.Cards) return;
            if (CurrentScreen == ScreenId.Tutorial) { EndTutorial(); return; }
            if (CurrentScreen == ScreenId.HUD) { Push(ScreenId.Pause); return; }
            if (CurrentScreen == ScreenId.Defeat || CurrentScreen == ScreenId.Victory) { _level.ReturnToMenu(); return; }
            if (_stack.Count <= 1) return;
            if (CurrentScreen == ScreenId.Dialog) _confirm = null;
            _stack.RemoveAt(_stack.Count - 1);
            RefreshStack();
        }

        void RefreshStack()
        {
            ClearInput();
            foreach (ScreenId id in Enum.GetValues(typeof(ScreenId)))
            {
                var node = N(id.ToString());
                bool modalParent = (CurrentScreen == ScreenId.Dialog || CurrentScreen == ScreenId.Tutorial || CurrentScreen == ScreenId.Story) &&
                    _stack.Count > 1 && _stack[_stack.Count - 2] == id;
                bool visible = id == CurrentScreen || modalParent || (id == ScreenId.HUD && _stack.Contains(id));
                node.SetActive(visible);
                if (!visible) continue;
                node.transform.SetAsLastSibling();
                var group = node.GetComponent<CanvasGroup>();
                group.alpha = 1;
                group.interactable = group.blocksRaycasts = id == CurrentScreen;
            }
            // Stack order, rather than enum order, determines modal layering.
            foreach (var id in _stack) N(id.ToString()).transform.SetAsLastSibling();
            FitMenuGroups();
            N("VillageBackdrop").SetActive(_level == null);
            N("BackdropWash").SetActive(_level == null);
            Time.timeScale = GameplayBlocked ? 0 : 1;
            _fadeStart = Time.unscaledTime;
            var page = N(CurrentScreen.ToString());
            var buttons = page.GetComponentsInChildren<UnityEngine.UI.Selectable>();
            foreach (var button in buttons)
            {
                var navigation = button.navigation;
                navigation.mode = UnityEngine.UI.Navigation.Mode.Explicit;
                button.navigation = navigation;
            }
            var usable = new List<UnityEngine.UI.Selectable>();
            foreach (var button in buttons) if (button.IsInteractable()) usable.Add(button);
            for (int i = 0; i < usable.Count; i++)
            {
                var navigation = usable[i].navigation;
                navigation.selectOnUp = navigation.selectOnLeft = usable[(i + usable.Count - 1) % usable.Count];
                navigation.selectOnDown = navigation.selectOnRight = usable[(i + 1) % usable.Count];
                usable[i].navigation = navigation;
            }
            if (EventSystem.current != null)
            {
                var selected = _focus.TryGetValue(CurrentScreen, out var previous) && previous != null && previous.activeInHierarchy ? previous :
                    (usable.Count > 0 ? usable[0].gameObject : null);
                EventSystem.current.SetSelectedGameObject(selected);
            }
        }

        void RefreshMenu()
        {
            bool hasSave = Progression.HasRun;
            N("MenuContinue").SetActive(hasSave);
            T("MenuTagline").text = hasSave ? $"Continue with {Progression.Run.hero} / Loop {Progression.Run.loop} / Stage {Progression.Run.stage}" : "Build your magic. Face a harder loop. Leave a lasting legacy.";
            N("MenuCheckpoint").SetActive(false);
            // The layout group collapses the hidden Continue row without enlarging New Adventure.
            var colors = B("MenuNew").colors;
            colors.normalColor = hasSave ? new Color(.78f, .64f, .39f, .28f) : ArcaneTheme.Cyan;
            B("MenuNew").colors = colors;
            FitMenuGroups();
            if (!hasSave && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(N("MenuNew"));
        }

        void Continue()
        {
            if (!Progression.HasRun && !Progression.MigrateLegacy()) { RefreshMenu(); return; }
            for (int i = 0; i < theme.heroes.Length; i++)
                if (theme.heroes[i].displayName == Progression.Run.hero) GameSession.SelectedCharacter = i;
            GameSession.SelectedHeroId = Progression.Run.hero;
            GameSession.Load(Progression.Scene);
        }

        void StartAdventure()
        {
            GameSession.ClearCheckpoint();
            var hero = theme.heroes[GameSession.SelectedCharacter];
            GameSession.SelectedHeroId = hero.displayName;
            Progression.Begin(hero.displayName);
            GameSession.Load("Level1");
        }

        public void SelectHero(int index)
        {
            index = Mathf.Clamp(index, 0, theme.heroes.Length - 1);
            GameSession.SelectedCharacter = index;
            GameAudio.Play(b => b.uiSelect, true);
            var hero = theme.heroes[index];
            GameSession.SelectedHeroId = hero.displayName;
            I("SelectedHero").sprite = hero.PreviewSprite;
            T("SelectedName").text = hero.displayName;
            T("SelectedRole").text = hero.combat.RoleName;
            T("SelectedRole").color = ArcaneTheme.HeroColors[index];
            T("HeroSummary").text = Summaries[(int)hero.combat.role];
            for (int i = 0; i < theme.heroes.Length; i++)
                T("HeroName" + i).color = i == index ? ArcaneTheme.Cyan : ArcaneTheme.Paper;
            for (int i = 0; i < 4; i++) T("HeroAbilityTitle" + i).text = AbilityNames[(int)hero.combat.role][i];
        }

        void ShowAbility(int index)
        {
            var config = theme.heroes[GameSession.SelectedCharacter].combat;
            string body = AbilityDescription(config, index);
            T("DialogTitle").text = AbilityNames[(int)config.role][index];
            T("DialogBody").text = body;
            N("DialogConfirm").SetActive(false);
            T("DialogCancelLabel").text = "Back";
            Push(ScreenId.Dialog);
        }

        public static string AbilityDescription(CharacterCombatConfig config, int index)
        {
            string[][] descriptions = {
                new[] { "Fire a light crystal in the aimed direction.", "Form a shield wall ahead of you. It blocks enemy projectiles and grants defense nearby.", "Gain shield by dealing damage. Distance from an attacker also reduces incoming damage.", "Call a meteor at the aim marker. It damages and roots enemies, then reduces their attack." },
                new[] { "Throw armor-piercing kunai. Successful hits build attack-speed stacks.", "Dash backward from your aim direction and temporarily accelerate your attacks.", "Consecutive kunai hits build attack speed. Stacks expire when you stop landing hits.", "Leap and strike living enemies in the visible battlefield." },
                new[] { "Throw a magic card that ricochets between nearby enemies.", "Leave an illusion trap at your feet. An approaching enemy triggers damage and knock-up.", "Automatically evade some incoming attacks.", "Create a damaging card storm at your position, then teleport to a safe nearby location." },
                new[] { "Fire a slow arcane orb that explodes on impact and bypasses designated obstacles and defenses.", "Place a slowing field at the aim marker.", "Your magic bypasses defense and designated obstacles while respecting the map boundary.", "Mark visible enemies and strike them with guaranteed-hit magic spears." },
                new[] { "Solar: explosive fire with burn. Lunar: concentrated single-target magic.", "Solar: a nearby ring of fire pushes enemies away. Lunar: an aimed bolt roots its target.", "Switching to Solar briefly grants attack power. Switching to Lunar briefly grants movement speed.", "Damage enemies throughout the visible battlefield and restore your health. Your stance stays unchanged." }
            };
            var text = descriptions[(int)config.role][index];
            if (index == 0) text += $"\n\nBase damage: {config.basicDamage:0.#}\nAttack interval: {config.basicInterval:0.##}s";
            if (index == 1) text += $"\n\nCooldown: {config.activeCooldown:0.#}s\nDuration: {config.duration:0.#}s";
            if (index == 3) text += $"\n\nDamage: {config.ultimateDamage:0.#}\nCooldown: {config.ultimateCooldown:0.#}s";
            if (index == 2 && config.role == HeroRole.Shadow) text += $"\n\nMaximum stacks: {config.maxStacks}\nStack timeout: {config.stackTimeout:0.#}s";
            if (index == 2 && config.role == HeroRole.Magician) text += $"\n\nEvasion chance: {config.evasion:P0}";
            if (index == 2 && config.role == HeroRole.Tanky) text += $"\n\nShield capacity: {config.shieldCapacity:0.#}\nDamage to shield: {config.conversion:P0}";
            if (index == 2 && config.role == HeroRole.Daphne) text += $"\n\nStance switch cooldown: {config.stanceCooldown:0.#}s";
            return text;
        }

        // The prologue lives in Resources/Story/Prologue.txt (zero-width spaces mark where Thai lines may wrap).
        void OpenStory()
        {
            var story = Resources.Load<TextAsset>("Story/Prologue");
            T("StoryBody").text = story != null ? story.text.Trim() : "";
            N("StoryScroll").GetComponent<UnityEngine.UI.ScrollRect>().verticalNormalizedPosition = 1f;
            Push(ScreenId.Story);
        }

        void Confirm(string title, string body, Action callback)
        {
            _confirm = callback;
            T("DialogTitle").text = title;
            T("DialogBody").text = body;
            T("DialogCancelLabel").text = "Cancel";
            N("DialogConfirm").SetActive(true);
            Push(ScreenId.Dialog);
        }

        void RefreshHealth(Health health)
        {
            T("HUDHealth").text = $"{Mathf.CeilToInt(health.CurrentHp)} / {health.maxHp:0} HP";
            T("HUDShield").text = health.ShieldHp > 0 ? $"Shield {Mathf.CeilToInt(health.ShieldHp)}" : "";
            SetBar("HealthFill", 0.2f, 0.74f, health.Ratio);
        }

        void SetBar(string name, float left, float width, float ratio)
        {
            var rect = I(name).rectTransform;
            var max = rect.anchorMax;
            max.x = left + width * Mathf.Clamp01(ratio);
            rect.anchorMax = max;
        }

        void RefreshSkills()
        {
            var skills = _player.Skills;
            var config = skills.Config;
            if (config == null) return;
            _activeReady = skills.ActiveReadyAt; _ultimateReady = skills.UltimateReadyAt; _stanceReady = skills.StanceReadyAt;
            N("HUDStance").SetActive(config.role == HeroRole.Daphne);
            T("HUDSpecial").text = config.role == HeroRole.Shadow ? $"Hit stacks {skills.HitStacks} / {config.maxStacks}" :
                config.role == HeroRole.Daphne ? (skills.IsLunar ? "LUNAR / Control" : "SOLAR / Burn") : "";
            T("StanceCaption").text = skills.IsLunar ? "Lunar" : "Solar";
            I("StanceSymbol").color = skills.IsLunar ? ArcaneTheme.HeroColors[1] : ArcaneTheme.HeroColors[4];
        }

        void RefreshEnemies()
        {
            if (_level == null) return;
            T("HUDObjective").text = _level.CurrentState == LevelManager.State.Cleared ? "Portal open · Enter to continue" : $"Enemies remaining: {LevelManager.Enemies.Count}";
            if (_boss != null) _boss.Changed -= RefreshBoss;
            _boss = null;
            foreach (var enemy in LevelManager.Enemies)
            {
                if (enemy == null || !enemy.isBoss || enemy.Health == null || enemy.Health.IsDead) continue;
                _boss = enemy.Health;
                T("HUDBoss").text = enemy.displayName;
                _boss.Changed += RefreshBoss;
                RefreshBoss(_boss);
                break;
            }
            N("BossPanel").SetActive(_boss != null);
        }

        void RefreshBoss(Health health) => SetBar("BossFill", 0.04f, 0.92f, health.Ratio);

        void OnLevelState()
        {
            RefreshEnemies();
            if (_level.CurrentState == LevelManager.State.Cleared) { Banner("Stage clear! Choose a blessing, then enter the portal"); PresentCards(); return; }
            if (_level.CurrentState != LevelManager.State.GameOver && _level.CurrentState != LevelManager.State.Victory) return;
            var id = _level.CurrentState == LevelManager.State.GameOver ? ScreenId.Defeat : ScreenId.Victory;
            if (_player != null && _player.CurrentCharacter != null) I(id + "Portrait").sprite = _player.CurrentCharacter.PreviewSprite;
            T(id + "Stage").text = $"{(_player != null && _player.CurrentCharacter != null ? _player.CurrentCharacter.displayName : "Hero")} · Stage {_level.levelNumber} / {_level.levelTitle}";
            if (id == ScreenId.Defeat && Progression.Data.lastResult != null)
            {
                var result = Progression.Data.lastResult;
                T("DefeatStage").text = $"{result.hero} / Loop {result.loop} / Stage {result.stage}\nBanked {result.earned:N0} Rebirth points";
            }
            ShowRoot(id);
        }

        void Banner(string message) { _banner = message; _bannerUntil = Time.time + 2.5f; }

        void BeginTutorial()
        {
            _tutorialStep = 0;
            RefreshTutorial();
            Push(ScreenId.Tutorial);
        }

        void RefreshTutorial()
        {
            string[] titles = { "Find your footing", "Aim your magic", "Unleash your abilities", "The way forward" };
            string[] bodies = {
                "Drag the left stick to move. Your adventure is paused while you read this guide.",
                "Drag the right stick to aim and fire. Release to stop. Skill aiming is separate from this attack stick.",
                "Hold and drag an aimed skill, then release to cast. Tap to target the nearest visible enemy in range. Release over X to cancel. Shadow dashes where you drag. Timers show cooldowns; Daphne can switch Solar / Lunar stance.",
                "Clear a stage, choose a card, then enter the portal. Defeat the dragon to loop with a stronger build. Death ends your run and banks Rebirth points."
            };
            T("TutorialStep").text = $"0{_tutorialStep + 1} / 04";
            T("TutorialTitle").text = titles[_tutorialStep];
            T("TutorialBody").text = bodies[_tutorialStep];
            I("TutorialIcon").sprite = theme.symbols[_tutorialStep];
            T("TutorialNextLabel").text = _tutorialStep == 3 ? "Let's Begin" : "Next";
        }

        void EndTutorial()
        {
            PlayerPrefs.SetInt(GameSession.TutorialKey, 1);
            PlayerPrefs.Save();
            if (CurrentScreen == ScreenId.Tutorial)
            {
                _stack.RemoveAt(_stack.Count - 1);
                RefreshStack();
            }
            PresentCards();
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused || !_started) return;
            ClearInput();
            if (_level != null && CurrentScreen == ScreenId.HUD) Push(ScreenId.Pause);
            PlayerPrefs.Save();
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused && Application.isMobilePlatform) OnApplicationPause(true);
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            SkillJoystick.CancelCurrent();
            if (_level != null) _level.StateChanged -= OnLevelState;
            LevelManager.EnemiesChanged -= RefreshEnemies;
            Progression.Changed -= RefreshProgression;
            if (_player != null)
            {
                _player.Health.Changed -= RefreshHealth;
                _player.Skills.Changed -= RefreshSkills;
            }
            if (_boss != null) _boss.Changed -= RefreshBoss;
            Instance = null;
            GameplayInput.Reset();
            Time.timeScale = 1;
        }
    }
}
