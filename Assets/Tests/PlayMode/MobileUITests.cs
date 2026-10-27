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
        string _checkpoint, _settings, _heroId, _progression;
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
            var controls = new[] { "MenuContinue", "MenuNew", "MenuHelp", "MenuSettings", "MenuRebirth" };
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
                player.Health.SetCurrentHp(player.Health.maxHp * .5f);
                player.ProgressionBonuses.Apply();
                float expected = 1 + .08f * (1 + 1 / Mathf.Sqrt(2));
                Assert.That(player.Skills.Config.basicDamage, Is.EqualTo(source.basicDamage * expected).Within(.001));
                Assert.That(player.Skills.Config.burnPerSecond, Is.EqualTo(source.burnPerSecond * expected).Within(.001));
                Assert.That(player.Skills.Config.activeDamage, Is.EqualTo(source.activeDamage * expected * 1.24f).Within(.001));
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
                Assert.That(JsonUtility.ToJson(source), Is.EqualTo(original));
            }
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
