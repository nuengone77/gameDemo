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
        string _checkpoint, _settings, _heroId;
        int _tutorial, _selected;
        const string SettingsKey = "MagicGame.UISettings.v1";

        [UnitySetUp]
        public IEnumerator Setup()
        {
            _checkpoint = PlayerPrefs.GetString(GameSession.CheckpointKey, "");
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
        public IEnumerator DefeatOffersRetryAndVictoryClearsCheckpoint()
        {
            yield return EnterLevel();
            PlayerController.Instance.Health.TakeDamage(100000);
            yield return null;
            yield return null;
            Assert.That(GameUI.Instance.CurrentScreen, Is.EqualTo(GameUI.ScreenId.Defeat));
            Click("DefeatRetry");
            yield return null;
            yield return null;
            Assert.That(PlayerController.Instance.Health.IsDead, Is.False);
            Assert.That(GameSession.TryGetCheckpoint(out _), Is.True);
            SceneManager.LoadScene("Level7");
            yield return null;
            yield return null;
            LevelManager.Instance.CompleteLevel();
            Assert.That(GameUI.Instance.CurrentScreen, Is.EqualTo(GameUI.ScreenId.Victory));
            Assert.That(GameSession.TryGetCheckpoint(out _), Is.False);
        }
    }
}
