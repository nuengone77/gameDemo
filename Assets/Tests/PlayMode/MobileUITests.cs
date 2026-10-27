using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MagicGame.Tests
{
    public class MobileUITests
    {
        string _checkpoint, _settings, _heroId, _progression, _heroNames;
        int _tutorial, _selected;
        const string SettingsKey = "MagicGame.UISettings.v1";

        [UnitySetUp]
        public IEnumerator Setup()
        {
            _checkpoint = PlayerPrefs.GetString(GameSession.CheckpointKey, "");
            _progression = PlayerPrefs.GetString(Progression.SaveKey, "");
            PlayerPrefs.DeleteKey(Progression.SaveKey);
            Progression.Reload();
            _settings = PlayerPrefs.GetString(SettingsKey, "");
            _heroNames = PlayerPrefs.GetString(HeroNames.SaveKey, "");
            PlayerPrefs.DeleteKey(HeroNames.SaveKey);
            HeroNames.Reload();
            _tutorial = PlayerPrefs.GetInt(GameSession.TutorialKey, 0);
            _selected = GameSession.SelectedCharacter;
            _heroId = GameSession.SelectedHeroId;
            PlayerPrefs.DeleteKey(GameSession.CheckpointKey);
            PlayerPrefs.DeleteKey(SettingsKey);
            PlayerPrefs.SetInt(GameSession.TutorialKey, 1);
            GameSession.SelectedCharacter = 0;
            GameSession.SelectedHeroId = "New";
            Time.timeScale = 1;
            SceneManager.LoadScene("MainMenu");
            yield return null;
            yield return null;
            Assert.That(GameUI.Instance, Is.Not.Null);
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            if (_progression.Length > 0) PlayerPrefs.SetString(Progression.SaveKey, _progression);
            else PlayerPrefs.DeleteKey(Progression.SaveKey);
            Progression.Reload();
            if (_checkpoint.Length > 0) PlayerPrefs.SetString(GameSession.CheckpointKey, _checkpoint);
            else PlayerPrefs.DeleteKey(GameSession.CheckpointKey);
            if (_settings.Length > 0) PlayerPrefs.SetString(SettingsKey, _settings);
            else PlayerPrefs.DeleteKey(SettingsKey);
            if (_heroNames.Length > 0) PlayerPrefs.SetString(HeroNames.SaveKey, _heroNames);
            else PlayerPrefs.DeleteKey(HeroNames.SaveKey);
            HeroNames.Reload();
            PlayerPrefs.SetInt(GameSession.TutorialKey, _tutorial);
            GameSession.SelectedCharacter = _selected;
            GameSession.SelectedHeroId = _heroId;
            PlayerPrefs.Save();
            Time.timeScale = 1;
            #if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/SampleScene.unity", new LoadSceneParameters(LoadSceneMode.Single));
            #endif
            yield return null;
        }

        static GameObject Node(string name) => GameUI.Instance.GetComponentsInChildren<RectTransform>(true).First(t => t.name == name).gameObject;
        static void Click(string name) => Node(name).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();

        IEnumerator EnterLevel(string scene = "Level1")
        {
            SceneManager.LoadScene(scene);
            yield return null;
            yield return null;
            if (GameUI.Instance.CurrentScreen == GameUI.ScreenId.Cards) Click("CardChoice0");
            PlayerController.Instance.Health.invulnerableTime = 1000;
        }

        static SkillJoystick Skill(SkillSlot slot) => Node("HUD" + slot).GetComponent<SkillJoystick>();

        static PointerEventData SkillPointer(SkillJoystick stick, int id = 21)
        {
            var rect = (RectTransform)stick.transform;
            return new PointerEventData(EventSystem.current) { pointerId = id, position = rect.TransformPoint(rect.rect.center) };
        }

        static void DragSkill(SkillJoystick stick, PointerEventData pointer, Vector2 value)
        {
            var rect = (RectTransform)stick.transform;
            pointer.position = rect.TransformPoint(rect.rect.center + value * 84);
            stick.OnDrag(pointer);
        }

        static void QuietEnemies()
        {
            foreach (var enemy in LevelManager.Enemies)
            {
                enemy.moveSpeed = enemy.damage = enemy.detectRange = enemy.attackRange = 0;
            }
        }

        [UnityTest]
        public IEnumerator SkillJoystickGroundCastFollowsMovementAndKeepsAttackAimSeparate()
        {
            GameSession.SelectedHeroId = "Ling";
            yield return EnterLevel();
            QuietEnemies();
            var player = PlayerController.Instance;
            player.GetComponent<Rigidbody2D>().position = Vector2.zero;
            GameplayInput.SetAim(Vector2.left);
            GameplayInput.ReleaseAim();
            var stick = Skill(SkillSlot.Active);
            var pointer = SkillPointer(stick);
            ExecuteEvents.Execute(stick.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            DragSkill(stick, pointer, new Vector2(.575f, 0)); // Half range after the deadzone.
            Assert.That(stick.IsAiming, Is.True);
            Assert.That(GameplayInput.Aim, Is.EqualTo(Vector2.left));
            Assert.That(GameplayInput.Firing, Is.False);
            Assert.That(player.Skills.ActiveRemaining, Is.Zero);
            var expected = CombatWorld.GroundTarget(player.transform.position,
                (Vector2)player.transform.position + Vector2.right * player.Skills.Config.range * .5f);
            Assert.That(Vector2.Distance(stick.PreviewCast.target, expected), Is.LessThan(.01f));
            GameplayInput.Move = Vector2.up;
            yield return new WaitForFixedUpdate();
            yield return null;
            GameplayInput.Move = Vector2.zero;
            // Release recomputes from the caster's current position, even without another drag event.
            ExecuteEvents.Execute(stick.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            var released = stick.PreviewCast.target;
            ExecuteEvents.Execute(stick.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            GameplayInput.SetAim(Vector2.down);
            GameplayInput.ReleaseAim();
            yield return null;
            var areas = Object.FindObjectsByType<SkillArea>(FindObjectsSortMode.None);
            Assert.That(areas.Length, Is.EqualTo(1), "Pointer up and click must produce only one area.");
            Assert.That(Vector2.Distance(areas[0].transform.position, released), Is.LessThan(.01f));
            Assert.That(player.Skills.ActiveRemaining, Is.GreaterThan(0));
            Assert.That(stick.preview.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator SkillJoystickCancelCanBeExitedAndWrongPointersCannotCast()
        {
            yield return EnterLevel();
            var stick = Skill(SkillSlot.Ultimate);
            var pointer = SkillPointer(stick);
            stick.OnPointerDown(pointer);
            var wrong = SkillPointer(stick, 22);
            stick.OnPointerDown(wrong);
            DragSkill(stick, wrong, Vector2.left);
            stick.OnPointerUp(wrong);
            Assert.That(stick.IsAiming, Is.True);
            pointer.position = stick.cancelArea.TransformPoint(stick.cancelArea.rect.center);
            stick.OnDrag(pointer);
            Assert.That(stick.IsCancelling, Is.True);
            DragSkill(stick, pointer, Vector2.left);
            Assert.That(stick.IsCancelling, Is.False);
            pointer.position = stick.cancelArea.TransformPoint(stick.cancelArea.rect.center);
            stick.OnPointerUp(pointer);
            Click("HUDUltimate");
            yield return null;
            Assert.That(PlayerController.Instance.Skills.UltimateRemaining, Is.Zero);
            Assert.That(SkillJoystick.Current, Is.Null);
            Assert.That(GameplayInput.TryConsumeSkillCast(SkillSlot.Ultimate, out _), Is.False);
        }

        [UnityTest]
        public IEnumerator SkillJoystickPauseAndInterruptionNeverTurnLateReleaseIntoQuickCast()
        {
            yield return EnterLevel();
            var player = PlayerController.Instance;
            var stick = Skill(SkillSlot.Ultimate);
            var pointer = SkillPointer(stick);
            stick.OnPointerDown(pointer);
            DragSkill(stick, pointer, Vector2.right);
            Click("HUDPause");
            Assert.That(stick.IsAiming, Is.False);
            Click("PauseResume");
            yield return null;
            stick.OnPointerUp(pointer);
            Click("HUDUltimate");
            yield return null;
            Assert.That(player.Skills.UltimateRemaining, Is.Zero);
            stick.OnPointerDown(pointer);
            Assert.That(stick.IsAiming, Is.True);
            player.Health.Status.Apply(StatusKind.Knockup, .2f, .05f, player);
            yield return null;
            Assert.That(stick.IsAiming, Is.False);
            yield return new WaitForSeconds(.1f);
            stick.OnPointerUp(pointer);
            Click("HUDUltimate");
            yield return null;
            Assert.That(player.Skills.UltimateRemaining, Is.Zero);
        }

        [UnityTest]
        public IEnumerator SkillJoystickModesAndShadowTravelRespectTheSelectedHeroAndStance()
        {
            yield return EnterLevel();
            QuietEnemies();
            var player = PlayerController.Instance;
            var skills = player.Skills;
            Assert.That(skills.GetTargetingMode(SkillSlot.Active), Is.EqualTo(SkillTargetingMode.Direction));
            Assert.That(skills.GetTargetingMode(SkillSlot.Ultimate), Is.EqualTo(SkillTargetingMode.Ground));
            skills.Initialize(player, GameUI.Instance.theme.heroes[1].combat);
            Assert.That(skills.GetTargetingMode(SkillSlot.Active), Is.EqualTo(SkillTargetingMode.Dash));
            Assert.That(skills.GetTargetingMode(SkillSlot.Ultimate), Is.EqualTo(SkillTargetingMode.None));
            var stick = Skill(SkillSlot.Active);
            var pointer = SkillPointer(stick);
            stick.OnPointerDown(pointer);
            DragSkill(stick, pointer, Vector2.right);
            Assert.That(stick.PreviewCast.direction.x, Is.LessThan(-.99f));
            stick.OnPointerUp(pointer);
            Vector2 before = player.transform.position;
            yield return null;
            yield return new WaitForFixedUpdate();
            Assert.That(player.GetComponent<Rigidbody2D>().position.x, Is.GreaterThan(before.x));
            skills.Initialize(player, GameUI.Instance.theme.heroes[2].combat);
            Assert.That(skills.GetTargetingMode(SkillSlot.Active), Is.EqualTo(SkillTargetingMode.None));
            Assert.That(skills.GetTargetingMode(SkillSlot.Ultimate), Is.EqualTo(SkillTargetingMode.None));
            skills.Initialize(player, GameUI.Instance.theme.heroes[3].combat);
            Assert.That(skills.GetTargetingMode(SkillSlot.Active), Is.EqualTo(SkillTargetingMode.Ground));
            Assert.That(skills.GetTargetingMode(SkillSlot.Ultimate), Is.EqualTo(SkillTargetingMode.None));
            skills.Initialize(player, GameUI.Instance.theme.heroes[4].combat);
            Assert.That(skills.GetTargetingMode(SkillSlot.Active), Is.EqualTo(SkillTargetingMode.None));
            Assert.That(skills.TrySwitchStance(), Is.True);
            Assert.That(skills.GetTargetingMode(SkillSlot.Active), Is.EqualTo(SkillTargetingMode.Direction));
            yield return null; // Let the event-driven cooldown UI reflect the new hero.
            stick.OnPointerDown(pointer);
            Assert.That(stick.IsAiming, Is.True);
            skills.Initialize(player, GameUI.Instance.theme.heroes[4].combat);
            stick.OnPointerUp(pointer);
            Assert.That(GameplayInput.TryConsumeSkillCast(SkillSlot.Active, out _), Is.False);
        }

        [UnityTest]
        public IEnumerator SkillJoystickQuickCastChoosesNearestVisibleLivingEnemyAndFallsBackToAim()
        {
            GameSession.SelectedHeroId = "Ling";
            yield return EnterLevel();
            QuietEnemies();
            var player = PlayerController.Instance;
            player.GetComponent<Rigidbody2D>().position = Vector2.zero;
            var camera = Camera.main;
            var enemies = LevelManager.Enemies.ToArray();
            Assert.That(enemies.Length, Is.GreaterThanOrEqualTo(3));
            for (int i = 0; i < enemies.Length; i++)
                enemies[i].GetComponent<Rigidbody2D>().position = new Vector2(100 + i, 100);
            enemies[0].GetComponent<Rigidbody2D>().position = new Vector2(2, 0);
            enemies[1].GetComponent<Rigidbody2D>().position = new Vector2(3, 0);
            enemies[2].GetComponent<Rigidbody2D>().position = new Vector2(1, 0);
            enemies[2].Health.destroyOnDeath = false;
            enemies[2].Health.TakeDamage(new DamageInfo(1e20f) { guaranteed = true, bypassDefense = true });
            yield return null;
            // LateUpdate has not followed the teleport yet when this coroutine resumes.
            // Center the view explicitly so the intended enemies are visible on ultrawide screens.
            camera.transform.position = player.transform.position + Vector3.back * 10;
            var stick = Skill(SkillSlot.Active);
            var pointer = SkillPointer(stick);
            stick.OnPointerDown(pointer);
            stick.OnPointerUp(pointer);
            Assert.That(GameplayInput.TryConsumeSkillCast(SkillSlot.Active, out var request), Is.True);
            var expected = CombatWorld.GroundTarget(player.transform.position, enemies[0].transform.position);
            Assert.That(Vector2.Distance(request.target, expected), Is.LessThan(.01f),
                $"target={request.target}, expected={expected}, player={player.transform.position}, nearest={enemies[0].transform.position}, camera={camera.transform.position}, viewport={camera.WorldToViewportPoint(enemies[0].transform.position)}");
            // Keep a live enemy in range but outside the camera viewport.
            Vector3 oldCamera = camera.transform.position;
            camera.transform.position = oldCamera + Vector3.right * 100;
            GameplayInput.SetAim(Vector2.left * .3f);
            GameplayInput.ReleaseAim();
            var fallback = player.Skills.ResolveSkillCast(SkillSlot.Active, Vector2.zero, false);
            expected = CombatWorld.GroundTarget(player.transform.position,
                (Vector2)player.transform.position + Vector2.left * player.Skills.Config.range);
            Assert.That(Vector2.Distance(fallback.target, expected), Is.LessThan(.01f));
            camera.transform.position = oldCamera;
            player.Skills.Initialize(player, GameUI.Instance.theme.heroes[1].combat);
            var retreat = player.Skills.ResolveSkillCast(SkillSlot.Active, Vector2.zero, false);
            Assert.That(retreat.direction.x, Is.GreaterThan(.99f), "Quick-cast retreats away from the enemy to the right.");
        }

        [UnityTest]
        public IEnumerator SkillJoystickManualAimClampsRangeAndCanTargetSelfWithoutAutoSnap()
        {
            GameSession.SelectedHeroId = "Ling";
            yield return EnterLevel();
            var player = PlayerController.Instance;
            var stick = Skill(SkillSlot.Active);
            var pointer = SkillPointer(stick);
            stick.OnPointerDown(pointer);
            DragSkill(stick, pointer, new Vector2(4, 4));
            Assert.That(Vector2.Distance(stick.PreviewCast.target, player.transform.position),
                Is.LessThanOrEqualTo(player.Skills.Config.range + .02f));
            Assert.That(CombatWorld.Contains(stick.PreviewCast.target), Is.True);
            DragSkill(stick, pointer, Vector2.zero);
            var self = CombatWorld.GroundTarget(player.transform.position, player.transform.position);
            Assert.That(Vector2.Distance(stick.PreviewCast.target, self), Is.LessThan(.01f));
            stick.OnPointerUp(pointer);
            Assert.That(GameplayInput.TryConsumeSkillCast(SkillSlot.Active, out var request), Is.True);
            Assert.That(Vector2.Distance(request.target, self), Is.LessThan(.01f));
        }

        [UnityTest]
        public IEnumerator SkillJoystickSuspensionDeathAndCooldownDiscardPendingGestures()
        {
            yield return EnterLevel();
            var player = PlayerController.Instance;
            var stick = Skill(SkillSlot.Ultimate);
            var pointer = SkillPointer(stick);
            stick.OnPointerDown(pointer);
            GameUI.Instance.SendMessage("OnApplicationPause", true);
            Assert.That(stick.IsAiming, Is.False);
            Assert.That(stick.preview.gameObject.activeSelf, Is.False);
            Click("PauseResume");
            yield return null;
            stick.OnPointerUp(pointer);
            Click("HUDUltimate");
            yield return null;
            Assert.That(player.Skills.UltimateRemaining, Is.Zero);
            player.Skills.TryUltimate(player.transform.position);
            stick.OnPointerDown(pointer);
            Assert.That(stick.IsAiming, Is.False, "A skill on cooldown cannot start an aim.");
            player.Skills.Initialize(player, player.CurrentCharacter.combat);
            GameUI.Instance.SendMessage("RefreshSkills");
            yield return null;
            stick.OnPointerUp(pointer);
            Click("HUDUltimate");
            yield return null;
            Assert.That(player.Skills.UltimateRemaining, Is.Zero, "Holding an unavailable button must not queue a cast when it becomes ready.");
            stick.OnPointerDown(pointer);
            Assert.That(stick.IsAiming, Is.True);
            player.Health.TakeDamage(new DamageInfo(1e20f) { guaranteed = true, bypassDefense = true });
            yield return null;
            stick.OnPointerUp(pointer);
            Assert.That(SkillJoystick.Current, Is.Null);
            Assert.That(GameplayInput.TryConsumeSkillCast(SkillSlot.Ultimate, out _), Is.False);
        }

        [UnityTest]
        public IEnumerator SkillJoystickControlsRemainInsideSafeAreaAtMaximumSize()
        {
            yield return EnterLevel();
            Node("TextSize").GetComponent<UnityEngine.UI.Slider>().value = 1.5f;
            Node("ControlSize").GetComponent<UnityEngine.UI.Slider>().value = 1.2f;
            Canvas.ForceUpdateCanvases();
            var stick = Skill(SkillSlot.Ultimate);
            var pointer = SkillPointer(stick);
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.First().gameObject.GetComponentInParent<UnityEngine.UI.Button>(), Is.EqualTo(stick.GetComponent<UnityEngine.UI.Button>()));
            var safe = (RectTransform)Node("SafeArea").transform;
            var buttonRect = (RectTransform)stick.transform;
            foreach (var press in new[] { buttonRect.rect.center, buttonRect.rect.max - Vector2.one })
            {
                pointer.position = buttonRect.TransformPoint(press);
                stick.OnPointerDown(pointer);
                foreach (var rect in new[] { stick.cancelArea, stick.ring })
                {
                    var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(safe, rect);
                    Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(safe.rect.xMin - .01f));
                    Assert.That(bounds.max.x, Is.LessThanOrEqualTo(safe.rect.xMax + .01f));
                    Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(safe.rect.yMin - .01f));
                    Assert.That(bounds.max.y, Is.LessThanOrEqualTo(safe.rect.yMax + .01f));
                }
                Assert.That(stick.preview.GetComponent<CanvasRenderer>(), Is.Not.Null, "World aim geometry requires a renderer on the authored overlay.");
                Canvas.ForceUpdateCanvases();
                Assert.That(stick.preview.canvasRenderer.materialCount, Is.GreaterThan(0), "The preview must submit a renderable graphic.");
                var cancelLabel = Node("SkillCancelLabel").GetComponent<TMPro.TMP_Text>();
                cancelLabel.ForceMeshUpdate();
                Assert.That(cancelLabel.isTextTruncated, Is.False, "The cancel label must fit at maximum text size.");
                SkillJoystick.CancelCurrent();
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SkillJoystickTouchscreenAllowsMovementAndCancelsInterruptedTouch()
        {
            GameSession.SelectedHeroId = "Ling";
            yield return EnterLevel();
            QuietEnemies();
            var background = UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior;
            UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            #if UNITY_EDITOR
            var editorInput = UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode;
            UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            #endif
            var screen = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Touchscreen>();
            try
            {
                Canvas.ForceUpdateCanvases();
                var move = (RectTransform)Node("MoveStick").transform;
                var stick = Skill(SkillSlot.Active);
                Vector2 movePoint = move.TransformPoint(move.rect.center + Vector2.right * 80);
                Vector2 aimPoint = SkillPointer(stick).position;
                QueueTouch(screen, 71, UnityEngine.InputSystem.TouchPhase.Began, movePoint);
                QueueTouch(screen, 72, UnityEngine.InputSystem.TouchPhase.Began, aimPoint);
                yield return null;
                yield return null;
                Assert.That(GameplayInput.Move.x, Is.GreaterThan(0));
                Assert.That(stick.IsAiming, Is.True, "The Input System UI module must deliver the second finger to the skill.");
                Assert.That(GameplayInput.Firing, Is.False);
                var rect = (RectTransform)stick.transform;
                aimPoint = rect.TransformPoint(rect.rect.center + Vector2.left * 84);
                QueueTouch(screen, 72, UnityEngine.InputSystem.TouchPhase.Moved, aimPoint);
                yield return null;
                yield return null;
                Assert.That(stick.PreviewCast.direction.x, Is.LessThan(-.99f));
                QueueTouch(screen, 72, UnityEngine.InputSystem.TouchPhase.Canceled, aimPoint);
                yield return null;
                yield return null;
                Assert.That(stick.IsAiming, Is.False);
                Assert.That(PlayerController.Instance.Skills.ActiveRemaining, Is.Zero, "OS touch cancellation must never cast.");
                Assert.That(GameplayInput.Move.x, Is.GreaterThan(0), "Cancelling the skill must preserve the movement finger.");
                QueueTouch(screen, 71, UnityEngine.InputSystem.TouchPhase.Ended, movePoint);
                yield return null;
                yield return null;
                Assert.That(GameplayInput.Move, Is.EqualTo(Vector2.zero));
                aimPoint = SkillPointer(stick).position;
                QueueTouch(screen, 73, UnityEngine.InputSystem.TouchPhase.Began, aimPoint);
                yield return null;
                yield return null;
                QueueTouch(screen, 73, UnityEngine.InputSystem.TouchPhase.Ended, aimPoint);
                yield return null;
                yield return null;
                Assert.That(PlayerController.Instance.Skills.ActiveRemaining, Is.GreaterThan(0));
                Assert.That(Object.FindObjectsByType<SkillArea>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            }
            finally
            {
                SkillJoystick.CancelCurrent();
                UnityEngine.InputSystem.InputSystem.RemoveDevice(screen);
                GameplayInput.Reset();
                UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = background;
                #if UNITY_EDITOR
                UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
                #endif
            }
        }

        static void QueueTouch(UnityEngine.InputSystem.Touchscreen screen, int id, UnityEngine.InputSystem.TouchPhase phase, Vector2 position)
        {
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(screen,
                new UnityEngine.InputSystem.LowLevel.TouchState { touchId = id, phase = phase, position = position });
        }

        [UnityTest]
        public IEnumerator SkillJoystickMouseDragDoesNotFireWhenItLeavesTheButton()
        {
            yield return EnterLevel();
            GameplayInput.Reset();
            var player = PlayerController.Instance;
            var stick = Skill(SkillSlot.Ultimate);
            stick.OnPointerDown(SkillPointer(stick));
            var system = EventSystem.current;
            var mouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            try
            {
                // No hovered UI: a held desktop mouse would normally shoot into the world.
                system.enabled = false;
                // The unfocused Editor can have its own input buffer active here.
                UnityEngine.InputSystem.LowLevel.InputState.Change(mouse,
                    new UnityEngine.InputSystem.LowLevel.MouseState().WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left),
                    UnityEngine.InputSystem.LowLevel.InputState.currentUpdateType);
                mouse.MakeCurrent();
                Assert.That(mouse.leftButton.isPressed, Is.True, "The synthetic press must reach the active input buffer.");
                GameplayInput.ReadActions(player, player.Facing, out _, out _, out bool fire, out _, out _, out _);
                Assert.That(fire, Is.False, "Dragging a skill into the world must not become a basic attack.");
                SkillJoystick.CancelCurrent();
                GameplayInput.ReadActions(player, player.Facing, out _, out _, out fire, out _, out _, out _);
                Assert.That(fire, Is.True, "Ordinary mouse attacks must remain available outside a skill gesture.");
            }
            finally
            {
                SkillJoystick.CancelCurrent();
                UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);
                system.enabled = true;
            }
        }

        [UnityTest]
        public IEnumerator MenusRestoreTheirOriginAndPreventBackgroundInteraction()
        {
            Click("MenuNew");
            Assert.That(GameUI.Instance.CurrentScreen, Is.EqualTo(GameUI.ScreenId.Heroes));
            Click("Hero3");
            Assert.That(GameSession.SelectedCharacter, Is.EqualTo(3));
            Click("HeroAbility1");
            Assert.That(GameUI.Instance.CurrentScreen, Is.EqualTo(GameUI.ScreenId.Dialog));
            Assert.That(Node("Heroes").GetComponent<CanvasGroup>().interactable, Is.False);
            GameUI.Instance.Back();
            Assert.That(GameUI.Instance.CurrentScreen, Is.EqualTo(GameUI.ScreenId.Heroes));
            GameUI.Instance.Back();
            Click("MenuSettings");
            Node("ControlSize").GetComponent<UnityEngine.UI.Slider>().value = 1.15f;
            Assert.That(UISettings.Load().controlSize, Is.EqualTo(1.15f).Within(0.01f));
            GameUI.Instance.Back();
            Assert.That(GameUI.Instance.CurrentScreen, Is.EqualTo(GameUI.ScreenId.Menu));
            yield return null;
        }

        [UnityTest]
        public IEnumerator NewAdventureConfirmationPreservesCheckpointUntilConfirmed()
        {
            GameSession.SaveCheckpoint("Level4", "Ling");
            Click("MenuNew");
            Click("Hero2");
            Click("HeroesBegin");
            Assert.That(GameUI.Instance.CurrentScreen, Is.EqualTo(GameUI.ScreenId.Dialog));
            Click("DialogCancel");
            Assert.That(GameSession.TryGetCheckpoint(out var saved), Is.True);
            Assert.That(saved.scene, Is.EqualTo("Level4"));
            Click("HeroesBegin");
            Click("DialogConfirm");
            yield return null;
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Level1"));
            Assert.That(PlayerController.Instance.CurrentCharacter.displayName, Is.EqualTo("Peng"));
        }

        [UnityTest]
        public IEnumerator GrimoireDecorationAllowsPointerAccessToButtonsAndSliders()
        {
            Canvas.ForceUpdateCanvases();
            var button = Node("MenuNew").GetComponent<UnityEngine.UI.Button>();
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = button.transform.TransformPoint(((RectTransform)button.transform).rect.center)
            };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>(), Is.EqualTo(button));
            Click("MenuSettings");
            yield return null;
            Canvas.ForceUpdateCanvases();
            var slider = Node("ControlSize").GetComponent<UnityEngine.UI.Slider>();
            pointer.position = slider.transform.TransformPoint(((RectTransform)slider.transform).rect.center);
            hits.Clear();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Slider>(), Is.EqualTo(slider));
        }

        [UnityTest]
        public IEnumerator CompactMenuFitsWithContinueAndMaximumTextSize()
        {
            Progression.Begin("New");
            SceneManager.LoadScene("MainMenu");
            yield return null;
            yield return null;
            Node("TextSize").GetComponent<UnityEngine.UI.Slider>().value = 1.5f;
            Canvas.ForceUpdateCanvases();
            var safe = (RectTransform)Node("SafeArea").transform;
            var controls = new[] { "MenuContinue", "MenuNew", "MenuStory", "MenuHelp", "MenuSettings", "MenuRebirth" };
            var bounds = controls.Select(name => RectTransformUtility.CalculateRelativeRectTransformBounds(safe, Node(name).transform)).ToArray();
            foreach (var area in bounds)
            {
                Assert.That(area.min.y, Is.GreaterThanOrEqualTo(safe.rect.yMin));
                Assert.That(area.max.y, Is.LessThanOrEqualTo(safe.rect.yMax));
                Assert.That(area.size.y, Is.GreaterThanOrEqualTo(72));
            }
            for (int i = 0; i < bounds.Length; i++)
            for (int j = i + 1; j < bounds.Length; j++)
                Assert.That(bounds[i].Intersects(bounds[j]), Is.False, controls[i] + " overlaps " + controls[j]);
            foreach (var name in controls.Concat(new[] { "MenuFooter" }))
            {
                var label = Node(name == "MenuFooter" ? name : name + "Label").GetComponent<TMPro.TMP_Text>();
                label.ForceMeshUpdate();
                Assert.That(label.isTextTruncated, Is.False, name);
            }
        }

        [UnityTest]
        public IEnumerator PauseFreezesCooldownAndPreservesShieldObjects()
        {
            yield return EnterLevel();
            var player = PlayerController.Instance;
            player.Skills.TryActive(Vector2.up, player.transform.position);
            int shields = Object.FindObjectsByType<ShieldWall>(FindObjectsSortMode.None).Length;
            float remaining = player.Skills.ActiveRemaining;
            Click("HUDPause");
            Click("PauseSettings");
            Assert.That(Time.timeScale, Is.Zero);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(player.Skills.ActiveRemaining, Is.EqualTo(remaining).Within(0.02f));
            Assert.That(Object.FindObjectsByType<ShieldWall>(FindObjectsSortMode.None).Length, Is.EqualTo(shields));
            GameUI.Instance.Back();
            Assert.That(GameUI.GameplayBlocked, Is.True);
            Click("PauseResume");
            Assert.That(Time.timeScale, Is.EqualTo(1));
            yield return new WaitForSeconds(0.05f);
            Assert.That(player.Skills.ActiveRemaining, Is.LessThan(remaining));
        }

        [UnityTest]
        public IEnumerator TwinSticksOwnSeparatePointersAndReleaseWithoutFiring()
        {
            yield return EnterLevel();
            Canvas.ForceUpdateCanvases();
            var left = Node("MoveStick").GetComponent<TouchStick>();
            var right = Node("AimStick").GetComponent<TouchStick>();
            Vector3 leftCenter = left.transform.TransformPoint(((RectTransform)left.transform).rect.center);
            Vector3 rightCenter = right.transform.TransformPoint(((RectTransform)right.transform).rect.center);
            var move = new PointerEventData(EventSystem.current) { pointerId = 11, position = leftCenter + new Vector3(80, 0) };
            var aim = new PointerEventData(EventSystem.current) { pointerId = 12, position = rightCenter };
            left.OnPointerDown(move);
            right.OnPointerDown(aim);
            Assert.That(GameplayInput.Firing, Is.False, "A centered touch must not fire.");
            Assert.That(right.Value.sqrMagnitude, Is.LessThan(0.001f));
            aim.position = rightCenter + new Vector3(0, 80);
            right.OnDrag(aim);
            Assert.That(GameplayInput.Move.x, Is.GreaterThan(0));
            Assert.That(GameplayInput.Move.y, Is.EqualTo(0).Within(0.01f));
            Assert.That(GameplayInput.Aim.y, Is.GreaterThan(0));
            Assert.That(GameplayInput.Aim.x, Is.EqualTo(0).Within(0.01f));
            Assert.That(GameplayInput.Firing, Is.True);
            right.OnPointerUp(move);
            Assert.That(GameplayInput.Firing, Is.True);
            right.OnPointerUp(aim);
            Assert.That(GameplayInput.Firing, Is.False);
            Assert.That(GameplayInput.HasTouchAim, Is.True);
            Assert.That(GameplayInput.Move.sqrMagnitude, Is.GreaterThan(0));
            Click("HUDPause");
            Assert.That(GameplayInput.Move, Is.EqualTo(Vector2.zero));
            Assert.That(GameplayInput.Firing, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ContinueRestoresHeroAndCurrentStageWithFreshHealth()
        {
            GameSession.SaveCheckpoint("Level3", "Nae");
            Click("MenuContinue");
            yield return null;
            yield return null;
            var player = PlayerController.Instance;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Level3"));
            Assert.That(player.CurrentCharacter.displayName, Is.EqualTo("Nae"));
            Assert.That(player.Health.CurrentHp, Is.EqualTo(player.Health.maxHp));
            Assert.That(player.Skills.ActiveRemaining, Is.Zero);
            Assert.That(Node("HUDStance").activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator FirstRunGuidePausesAndSkipPersistsCompletion()
        {
            PlayerPrefs.SetInt(GameSession.TutorialKey, 0);
            yield return EnterLevel();
            Assert.That(GameUI.Instance.CurrentScreen, Is.EqualTo(GameUI.ScreenId.Tutorial));
            Assert.That(Time.timeScale, Is.Zero);
            Click("TutorialSkip");
            Assert.That(PlayerPrefs.GetInt(GameSession.TutorialKey), Is.EqualTo(1));
            Assert.That(GameUI.Instance.CurrentScreen, Is.EqualTo(GameUI.ScreenId.Cards));
            Click("CardChoice0");
            Assert.That(GameUI.Instance.CurrentScreen, Is.EqualTo(GameUI.ScreenId.HUD));
            Assert.That(Time.timeScale, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator LargeTextKeepsEssentialHUDLabelsVisibleWithSafeAreaInsets()
        {
            GameUI.Instance.SelectHero(4);
            yield return EnterLevel("Level7");
            Node("TextSize").GetComponent<UnityEngine.UI.Slider>().value = 1.5f;
            Node("ControlSize").GetComponent<UnityEngine.UI.Slider>().value = 1.2f;
            var safe = (RectTransform)Node("SafeArea").transform;
            safe.anchorMin = new Vector2(0.04f, 0.03f);
            safe.anchorMax = new Vector2(0.96f, 0.97f);
            yield return null;
            Canvas.ForceUpdateCanvases();
            foreach (string name in new[] { "HUDHero", "HUDStage", "HUDObjective", "HUDBoss", "HUDHealth", "HUDSpecial", "ActiveCaption", "UltimateCaption", "StanceCaption" })
            {
                var label = Node(name).GetComponent<TMPro.TMP_Text>();
                label.ForceMeshUpdate();
                Assert.That(label.text.Length, Is.GreaterThan(0), name);
                Assert.That(label.isTextTruncated, Is.False, name + " must remain readable at 150% text size.");
            }
        }

        [UnityTest]
        public IEnumerator AppSuspensionPreservesAreaEffectsAndRequiresExplicitResume()
        {
            yield return EnterLevel();
            var ui = GameUI.Instance;
            var player = PlayerController.Instance;
            player.Skills.Initialize(player, ui.theme.heroes[3].combat);
            player.Skills.TryActive(Vector2.up, player.transform.position);
            player.Skills.BasicAttack(Vector2.up);
            int areas = Object.FindObjectsByType<SkillArea>(FindObjectsSortMode.None).Length;
            int shots = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length;
            Assert.That(areas, Is.GreaterThan(0));
            ui.SendMessage("OnApplicationPause", true);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(Object.FindObjectsByType<SkillArea>(FindObjectsSortMode.None).Length, Is.EqualTo(areas));
            Assert.That(Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length, Is.EqualTo(shots));
            ui.SendMessage("OnApplicationPause", false);
            Assert.That(GameUI.GameplayBlocked, Is.True);
            Assert.That(ui.CurrentScreen, Is.EqualTo(GameUI.ScreenId.Pause));
            Click("PauseResume");
            Assert.That(Time.timeScale, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DeathEndsRunAndDragonLoopsWithBuildIntact()
        {
            yield return EnterLevel();
            PlayerController.Instance.Health.TakeDamage(100000);
            yield return null;
            yield return null;
            Assert.That(GameUI.Instance.CurrentScreen, Is.EqualTo(GameUI.ScreenId.Defeat));
            Assert.That(Progression.HasRun, Is.False);
            Click("DefeatRetry");
            yield return null;
            yield return null;
            Assert.That(PlayerController.Instance.Health.IsDead, Is.False);
            Assert.That(Progression.Run.stage, Is.EqualTo(1));
            SceneManager.LoadScene("Level7");
            yield return null;
            yield return null;
            Click("CardChoice0");
            foreach (var enemy in LevelManager.Enemies.ToArray()) enemy.Health.TakeDamage(1000000);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(GameUI.Instance.CurrentScreen, Is.EqualTo(GameUI.ScreenId.Cards));
            Click("CardChoice0");
            int levels = Progression.Run.cards.Sum(c => c.level);
            LevelManager.Instance.CompleteLevel();
            yield return null;
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Level1"));
            Assert.That(Progression.Run.loop, Is.EqualTo(2));
            Assert.That(Progression.Run.cards.Sum(c => c.level), Is.EqualTo(levels));
            Assert.That(LevelManager.Enemies.Any(e => e.IsElite), Is.True);
        }

        [UnityTest]
        public IEnumerator AllFiveHeroesRecomputeCardsWithoutChangingSourceOrRevivingAfterDeath()
        {
            for (int i = 0; i < 5; i++)
            {
                GameSession.SelectedCharacter = i;
                GameSession.SelectedHeroId = GameSession.HeroIds[i];
                Progression.Begin(GameSession.HeroIds[i]);
                yield return EnterLevel();
                var player = PlayerController.Instance;
                var source = player.CurrentCharacter.combat;
                string original = JsonUtility.ToJson(source);
                Progression.Run.cards.Clear();
                Progression.Run.cards.Add(new CardStack { id = "power", level = 2 });
                Progression.Run.cards.Add(new CardStack { id = "vitality", level = 2 });
                Progression.Run.cards.Add(new CardStack { id = "skill", level = 2 });
                Progression.Run.cards.Add(new CardStack { id = "shield", level = 1 });
                Progression.Run.cards.Add(new CardStack { id = "radius", level = 1 });
                Progression.Run.cards.Add(new CardStack { id = "duration", level = 1 });
                player.Health.SetCurrentHp(player.Health.maxHp * .5f);
                player.ProgressionBonuses.Apply();
                float expected = 1 + .08f * (1 + 1 / Mathf.Sqrt(2));
                Assert.That(player.Skills.Config.basicDamage, Is.EqualTo(source.basicDamage * expected).Within(.001));
                Assert.That(player.Skills.Config.burnPerSecond, Is.EqualTo(source.burnPerSecond * expected).Within(.001));
                Assert.That(player.Skills.Config.activeDamage, Is.EqualTo(source.activeDamage * expected * 1.24f).Within(.001));
                Assert.That(player.Skills.Config.eclipseFieldDamage, Is.EqualTo(source.eclipseFieldDamage * expected * 1.24f).Within(.001));
                Assert.That(player.Skills.Config.eclipseFieldRadius, Is.EqualTo(source.eclipseFieldRadius * 1.1f).Within(.001));
                Assert.That(player.Skills.Config.eclipseFieldDuration, Is.EqualTo(source.eclipseFieldDuration * 1.2f).Within(.001));
                Assert.That(player.Skills.Config.ultimateVfx.prefab, Is.EqualTo(source.ultimateVfx.prefab));
                Assert.That(player.Skills.Config.eclipseSunVfx.prefab, Is.EqualTo(source.eclipseSunVfx.prefab));
                float hp = player.Health.CurrentHp;
                player.ProgressionBonuses.Apply();
                Assert.That(player.Health.CurrentHp, Is.EqualTo(hp).Within(.001));
                Assert.That(player.Health.Ratio, Is.EqualTo(.5f).Within(.001));
                Assert.That(JsonUtility.ToJson(source), Is.EqualTo(original));
                Assert.That(player.Skills.TryActive(Vector2.up, player.transform.position), Is.True);
                Assert.That(player.Health.ShieldHp, Is.EqualTo(.08f * player.Health.maxHp).Within(.001));
                player.Health.TakeDamage(new DamageInfo(1e20f) { guaranteed = true, bypassDefense = true });
                Assert.That(player.Health.IsDead, Is.True);
                yield return null;
                yield return null;
                Assert.That(Progression.Run, Is.Null);
                Assert.That(player.Health.IsDead, Is.True);
                Assert.That(player.Health.CurrentHp, Is.Zero);
                Assert.That(player.Health.ShieldHp, Is.Zero);
                Assert.That(player.ProgressionBonuses.ActiveShield, Is.Zero);
                Assert.That(player.Skills.Config.basicDamage, Is.EqualTo(source.basicDamage));
                Assert.That(player.Skills.Config.eclipseFieldDamage, Is.EqualTo(source.eclipseFieldDamage));
                Assert.That(JsonUtility.ToJson(source), Is.EqualTo(original));
            }
        }

        [UnityTest]
        public IEnumerator SkillJoystickPlaysActiveSoundOnceAndPreservesCooldown()
        {
            GameSession.SelectedHeroId = "Ling";
            yield return EnterLevel();
            QuietEnemies();
            yield return new WaitForSecondsRealtime(.25f);
            var audio = Object.FindAnyObjectByType<GameAudio>();
            Assert.That(audio, Is.Not.Null);
            var field = typeof(GameAudio).GetField("_lastPlayed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var played = (System.Collections.Generic.Dictionary<SoundBank.Sound, float>)field.GetValue(audio);
            var sound = GameAudio.Bank.Hero("Ling").active;
            var stick = Skill(SkillSlot.Active);
            var pointer = SkillPointer(stick);
            float before = Time.unscaledTime;
            stick.OnPointerDown(pointer);
            stick.OnPointerUp(pointer);
            yield return null;
            yield return null;
            Assert.That(PlayerController.Instance.Skills.ActiveRemaining, Is.GreaterThan(0));
            Assert.That(played.ContainsKey(sound), Is.True);
            Assert.That(played[sound], Is.GreaterThanOrEqualTo(before));
            float last = played[sound];
            stick.OnPointerDown(pointer);
            stick.OnPointerUp(pointer);
            yield return null;
            Assert.That(played[sound], Is.EqualTo(last), "A rejected cast must not replay its sound");
        }

        [UnityTest]
        public IEnumerator EclipseLoopStopsWhenPlayerDiesDuringMerge()
        {
            GameSession.SelectedHeroId = "Nae";
            yield return EnterLevel();
            QuietEnemies();
            var player = PlayerController.Instance;
            Assert.That(player.Skills.TryUltimate(player.transform.position), Is.True);
            var audio = Object.FindAnyObjectByType<GameAudio>();
            var clip = GameAudio.Bank.Hero("Nae").ultimateLoop.clips[0];
            Assert.That(audio.GetComponentsInChildren<AudioSource>().Any(s => s.clip == clip && s.loop && s.isPlaying), Is.True);
            player.Health.TakeDamage(new DamageInfo(1e20f) { guaranteed = true, bypassDefense = true });
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(GameUI.Instance.CurrentScreen, Is.EqualTo(GameUI.ScreenId.Defeat));
            Assert.That(audio.GetComponentsInChildren<AudioSource>().Any(s => s.clip == clip && s.loop && s.isPlaying), Is.False);
        }

        [UnityTest]
        public IEnumerator StoryAndAudioSettingsCoexistWithRebirthAndSurviveReopening()
        {
            var ui = GameUI.Instance;
            Assert.That(ui.GetComponentsInChildren<RectTransform>(true).Count(r => r.name == "MenuStory"), Is.EqualTo(1));
            Assert.That(ui.GetComponentsInChildren<RectTransform>(true).Count(r => r.name == "Story"), Is.EqualTo(1));
            Assert.That(Node("MenuStory").transform.parent.name, Is.EqualTo("MenuUtilities"));
            Click("MenuStory");
            Assert.That(ui.CurrentScreen, Is.EqualTo(GameUI.ScreenId.Story));
            var body = Node("StoryBody").GetComponent<TMPro.TMP_Text>();
            Assert.That(body.text, Is.EqualTo(Resources.Load<TextAsset>("Story/Prologue_en").text.Trim()));
            Assert.That(body.font, Is.EqualTo(ui.theme.thaiFont));
            Click("StoryClose");
            Click("MenuRebirth");
            Assert.That(ui.CurrentScreen, Is.EqualTo(GameUI.ScreenId.Rebirth));
            ui.Back();
            Click("MenuSettings");
            foreach (var name in new[] { "MasterVolume", "MusicVolume", "SfxVolume", "UiVolume" })
            {
                var slider = Node(name).GetComponent<UnityEngine.UI.Slider>();
                Assert.That(slider, Is.Not.Null);
                slider.value = .35f;
                Assert.That(Node(name + "Value").GetComponent<TMPro.TMP_Text>().text, Does.Contain("35"));
            }
            var settings = UISettings.Load();
            Assert.That(settings.masterVolume, Is.EqualTo(.35f).Within(.001f));
            Assert.That(settings.musicVolume, Is.EqualTo(.35f).Within(.001f));
            Assert.That(settings.sfxVolume, Is.EqualTo(.35f).Within(.001f));
            Assert.That(settings.uiVolume, Is.EqualTo(.35f).Within(.001f));
            ui.Back();
            Click("MenuStory");
            Assert.That(ui.CurrentScreen, Is.EqualTo(GameUI.ScreenId.Story));
            yield return null;
        }

        [UnityTest]
        public IEnumerator LanguageSwitchTranslatesUIAndStoryThenRestoresEnglish()
        {
            var ui = GameUI.Instance;
            var settingsLabel = Node("MenuSettingsLabel").GetComponent<TMPro.TMP_Text>();
            var englishFont = settingsLabel.font;
            Click("MenuSettings");
            Assert.That(Node("LanguageRow").transform.GetSiblingIndex(), Is.EqualTo(1));
            Click("LanguageThai");
            Assert.That(UISettings.Load().language, Is.EqualTo(GameLanguage.Thai));
            Assert.That(Node("SettingsBackLabel").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo(Loc.Tr("Back")));
            Assert.That(Loc.Tr("Back"), Is.Not.EqualTo("Back"));
            Assert.That(settingsLabel.font, Is.EqualTo(ui.theme.thaiFont));
            var title = Node("MenuTitle").GetComponent<TMPro.TMP_Text>();
            Assert.That(title.text, Is.EqualTo("แพนเนเลียน"));
            Assert.That(title.font, Is.EqualTo(ui.theme.thaiFont));
            ui.Back();
            Click("MenuStory");
            Assert.That(Node("StoryBody").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo(Resources.Load<TextAsset>("Story/Prologue").text.Trim()));
            Click("StoryClose");
            Click("MenuSettings");
            Click("LanguageEnglish");
            Assert.That(UISettings.Load().language, Is.EqualTo(GameLanguage.English));
            Assert.That(settingsLabel.text, Is.EqualTo("Settings"));
            Assert.That(settingsLabel.font, Is.EqualTo(englishFont));
            Assert.That(Node("MenuTitle").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("Pannelion"));
            Assert.That(Node("MenuTitle").GetComponent<TMPro.TMP_Text>().font, Is.EqualTo(ui.theme.titleFont));
            yield return null;
        }

        [UnityTest]
        public IEnumerator NamingAHeroShowsTheNameWithItsRoleAndClearingRestoresTheRole()
        {
            var ui = GameUI.Instance;
            ui.SelectHero(4);
            Click("MenuNew");
            var hero = ui.theme.heroes[4];
            var field = Node("HeroNameInput").GetComponent<TMPro.TMP_InputField>();
            var rosterName = Node("HeroName4").GetComponent<TMPro.TMP_Text>();
            Assert.That(field.text, Is.Empty);
            Assert.That(rosterName.text, Is.EqualTo(hero.MenuName));
            Assert.That(Node("HeroRole4").activeSelf, Is.False);

            field.text = "  Luna   Star  ";
            field.onEndEdit.Invoke(field.text);
            Assert.That(HeroNames.Get(hero.displayName), Is.EqualTo("Luna Star"));
            Assert.That(field.text, Is.EqualTo("Luna Star"));
            Assert.That(rosterName.text, Is.EqualTo("Luna Star"));
            Assert.That(Node("HeroRole4").activeSelf, Is.True);
            Assert.That(Node("RebirthHero4Label").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("Luna Star"));
            Assert.That(field.onValidateInput("", 0, '\uD83D'), Is.EqualTo('\0'));

            // A Thai name needs the Thai font even while the game is in English.
            field.text = "จันทรา";
            field.onEndEdit.Invoke(field.text);
            Assert.That(rosterName.font, Is.EqualTo(ui.theme.thaiFont));

            field.text = "";
            field.onEndEdit.Invoke(field.text);
            Assert.That(HeroNames.Get(hero.displayName), Is.Empty);
            Assert.That(rosterName.text, Is.EqualTo(hero.MenuName));
            Assert.That(rosterName.font, Is.Not.EqualTo(ui.theme.thaiFont));
            Assert.That(Node("HeroRole4").activeSelf, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ClearedCheckpointRestoresSameOfferWithoutEnemiesOrDuplicatePoints()
        {
            yield return EnterLevel();
            foreach (var enemy in LevelManager.Enemies.ToArray()) enemy.Health.TakeDamage(1e20f);
            yield return new WaitForSecondsRealtime(.3f);
            var offer = Progression.Run.offer.ToArray();
            long earned = Progression.Run.earned;
            Progression.Reload();
            SceneManager.LoadScene("Level1");
            yield return null;
            yield return null;
            Assert.That(LevelManager.Enemies, Is.Empty);
            Assert.That(Progression.Run.offer, Is.EqualTo(offer));
            Assert.That(Progression.Run.earned, Is.EqualTo(earned));
            Assert.That(LevelManager.Instance.portal.IsOpen, Is.False);
            Click("CardChoice0");
            Assert.That(LevelManager.Instance.portal.IsOpen, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AllFiveHeroesCanTransitionThroughThreeCompleteLoops()
        {
            for (int hero = 0; hero < 5; hero++)
            {
                GameSession.SelectedCharacter = hero;
                GameSession.SelectedHeroId = GameSession.HeroIds[hero];
                Progression.Begin(GameSession.HeroIds[hero]);
                yield return EnterLevel();
                for (int loop = 1; loop <= 3; loop++)
                for (int stage = 1; stage <= 7; stage++)
                {
                    var player = PlayerController.Instance;
                    Assert.That(player.CurrentCharacter.displayName, Is.EqualTo(GameSession.HeroIds[hero]));
                    Assert.That(player.Health.CurrentHp, Is.EqualTo(player.Health.maxHp));
                    Assert.That(Progression.Run.loop, Is.EqualTo(loop));
                    Assert.That(Progression.Run.stage, Is.EqualTo(stage));
                    foreach (var enemy in LevelManager.Enemies.ToArray()) enemy.Health.TakeDamage(1e25f);
                    yield return new WaitForSecondsRealtime(.3f);
                    Assert.That(GameUI.Instance.CurrentScreen, Is.EqualTo(GameUI.ScreenId.Cards));
                    Assert.That(Time.timeScale, Is.Zero);
                    Click("CardChoice0");
                    Assert.That(LevelManager.Instance.portal.IsOpen, Is.True);
                    LevelManager.Instance.CompleteLevel();
                    yield return null;
                    yield return null;
                }
                Assert.That(Progression.Run.loop, Is.EqualTo(4));
                Assert.That(Progression.Run.earned, Is.EqualTo(560));
                Assert.That(Progression.Run.cards.Sum(c => c.level), Is.EqualTo(22));
                Progression.Finish();
            }
        }

        [UnityTest]
        public IEnumerator CardAndRebirthTextFitsAtLargeSizeWithSafeAreaInsets()
        {
            yield return EnterLevel();
            Node("TextSize").GetComponent<UnityEngine.UI.Slider>().value = 1.5f;
            Canvas.ForceUpdateCanvases();
            var progressionCorners = new Vector3[4];
            var bossCorners = new Vector3[4];
            ((RectTransform)Node("HUDProgression").transform).GetWorldCorners(progressionCorners);
            ((RectTransform)Node("BossPanel").transform).GetWorldCorners(bossCorners);
            Assert.That(progressionCorners[1].y, Is.LessThan(bossCorners[0].y), "Progression text must stay below the boss frame at maximum text size.");
            foreach (var enemy in LevelManager.Enemies.ToArray()) enemy.Health.TakeDamage(1e20f);
            yield return new WaitForSecondsRealtime(.3f);
            var safe = (RectTransform)Node("SafeArea").transform;
            safe.anchorMin = new Vector2(.04f, .03f);
            safe.anchorMax = new Vector2(.96f, .97f);
            Canvas.ForceUpdateCanvases();
            foreach (var name in new[] { "CardsHeading", "CardsInfo", "CardChoice0Label", "CardChoice1Label", "CardChoice2Label", "CardsMenuLabel", "HUDHealth" })
            {
                var label = Node(name).GetComponent<TMPro.TMP_Text>();
                label.ForceMeshUpdate();
                Assert.That(label.isTextTruncated, Is.False, name);
                Assert.That(label.textBounds.size.y, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 2), name);
            }
            GameUI.Instance.SendMessage("OpenRebirth");
            Canvas.ForceUpdateCanvases();
            foreach (var name in new[] { "RebirthSubtitle", "RebirthInfo", "RebirthUnlocks" })
            {
                var label = Node(name).GetComponent<TMPro.TMP_Text>();
                label.ForceMeshUpdate();
                Assert.That(label.isTextTruncated, Is.False, name);
                Assert.That(label.textBounds.size.y, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 2), name);
            }
        }

        [UnityTest]
        public IEnumerator RebirthButtonsPurchaseForSelectedHeroAndRefreshWalletAndCost()
        {
            Progression.Hero("New").points = 125;
            Progression.Save();
            Click("MenuRebirth");
            Click("RebirthUpgrade0");
            Assert.That(Progression.Hero("New").points, Is.EqualTo(95));
            Assert.That(Progression.Hero("New").hpBonus, Is.EqualTo(.03f).Within(.00001));
            Assert.That(Node("RebirthInfo").GetComponent<TMPro.TMP_Text>().text, Does.Contain("42"));
            Click("RebirthHero4");
            Assert.That(Node("RebirthUpgrade0").GetComponent<UnityEngine.UI.Button>().interactable, Is.False);
            Assert.That(Progression.Hero("Nae").hpBonus, Is.Zero);
            yield return null;
        }
    }
}
