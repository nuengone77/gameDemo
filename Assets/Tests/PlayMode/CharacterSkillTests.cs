using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace MagicGame.Tests
{
    public class CharacterSkillTests
    {
        readonly List<Object> _owned = new();
        PlayerController _player;
        CharacterCombatConfig _config;
        string _savedCheckpoint, _savedHero;
        int _savedTutorial, _savedSelection;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            // Combat tests start past onboarding and must not replace a contributor's adventure.
            _savedCheckpoint = PlayerPrefs.GetString(GameSession.CheckpointKey, "");
            _savedTutorial = PlayerPrefs.GetInt(GameSession.TutorialKey, 0);
            _savedSelection = GameSession.SelectedCharacter;
            _savedHero = GameSession.SelectedHeroId;
            PlayerPrefs.SetInt(GameSession.TutorialKey, 1);
            GameSession.SelectedHeroId = null;
            GameplayInput.Reset();
            #if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/SampleScene.unity", new LoadSceneParameters(LoadSceneMode.Single));
            #else
            SceneManager.LoadScene("MainMenu");
            #endif
            yield return null;
            var camera = Camera.main;
            if (camera == null)
            {
                var go = new GameObject("TestCamera");
                go.tag = "MainCamera";
                camera = go.AddComponent<Camera>();
                _owned.Add(go);
            }
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            _config = ScriptableObject.CreateInstance<CharacterCombatConfig>();
            _owned.Add(_config);
            _config.castDelay = 0.05f;
            _config.duration = 0.3f;
            _config.tickInterval = 0.1f;
            var root = new GameObject("SkillTestPlayer");
            root.SetActive(false);
            _owned.Add(root);
            root.transform.position = new Vector2(-2.5f, 0f);
            root.AddComponent<SpriteRenderer>();
            root.AddComponent<Rigidbody2D>().gravityScale = 0f;
            root.AddComponent<CircleCollider2D>().radius = 0.2f;
            var health = root.AddComponent<Health>();
            health.team = Team.Player;
            health.destroyOnDeath = false;
            _player = root.AddComponent<PlayerController>();
            root.SetActive(true);
            Select(HeroRole.Tanky);
            yield return null;
        }

        void Select(HeroRole role)
        {
            _config.role = role;
            _player.Skills.Initialize(_player, _config);
        }

        EnemyController Enemy(Vector2 point, string name = "Enemy")
        {
            var root = new GameObject(name);
            root.SetActive(false);
            _owned.Add(root);
            root.transform.position = point;
            root.AddComponent<SpriteRenderer>();
            root.AddComponent<Rigidbody2D>().gravityScale = 0f;
            var health = root.AddComponent<Health>();
            health.maxHp = 500f;
            health.destroyOnDeath = false;
            var hurt = new GameObject("Hurtbox");
            hurt.transform.SetParent(root.transform, false);
            var box = hurt.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.7f, 1.3f);
            box.offset = Vector2.up * 0.5f;
            box.isTrigger = true;
            var enemy = root.AddComponent<EnemyController>();
            enemy.moveSpeed = 0f;
            enemy.detectRange = 0f;
            enemy.attackRange = 0f;
            enemy.displayName = name;
            root.SetActive(true);
            return enemy;
        }

        GameObject Wall(Vector2 point)
        {
            var wall = new GameObject("TestWall");
            _owned.Add(wall);
            wall.transform.position = point;
            wall.AddComponent<BoxCollider2D>().size = new Vector2(0.2f, 8f);
            wall.AddComponent<Wall>();
            return wall;
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            foreach (var item in _owned) if (item != null) Object.Destroy(item);
            _owned.Clear();
            foreach (var visual in Object.FindObjectsByType<SkillVfx>()) Object.Destroy(visual.gameObject);
            if (_savedCheckpoint.Length > 0) PlayerPrefs.SetString(GameSession.CheckpointKey, _savedCheckpoint);
            else PlayerPrefs.DeleteKey(GameSession.CheckpointKey);
            PlayerPrefs.SetInt(GameSession.TutorialKey, _savedTutorial);
            GameSession.SelectedCharacter = _savedSelection;
            GameSession.SelectedHeroId = _savedHero;
            PlayerPrefs.Save();
            yield return null;
        }

        [UnityTest]
        public IEnumerator TankyBasicConvertsActualDamageAndCapsShield()
        {
            var enemy = Enemy(Vector2.zero);
            _player.Skills.BasicAttack(Vector2.right);
            yield return new WaitForSeconds(0.4f);
            Assert.That(enemy.Health.CurrentHp, Is.EqualTo(475f));
            Assert.That(_player.Health.ShieldHp, Is.EqualTo(2.5f).Within(0.01f));
            _player.Health.AddShield(10000f);
            Assert.That(_player.Health.ShieldHp, Is.EqualTo(_config.shieldCapacity));
        }

        [UnityTest]
        public IEnumerator TankyWallBlocksMultipleIncomingProjectilesAndExpires()
        {
            _config.duration = 0.6f;
            Assert.That(_player.Skills.TryActive(Vector2.right, Vector2.zero), Is.True);
            yield return null;
            Assert.That(_player.Health.Status.DefenseReduction, Is.EqualTo(_config.wallDefense));
            for (int i = 0; i < 3; i++)
            {
                var shot = new GameObject("EnemyShot");
                _owned.Add(shot);
                shot.transform.position = new Vector2(0f, 0.1f * i);
                shot.AddComponent<Rigidbody2D>().gravityScale = 0f;
                shot.AddComponent<CircleCollider2D>().isTrigger = true;
                var projectile = shot.AddComponent<Projectile>();
                projectile.Launch(Vector2.left, Team.Enemy, 10f);
            }
            yield return new WaitForSeconds(0.4f);
            Assert.That(Object.FindObjectsByType<Projectile>(), Is.Empty);
            yield return new WaitForSeconds(0.3f);
            Assert.That(Object.FindObjectsByType<ShieldWall>(), Is.Empty);
            Assert.That(_player.Health.Status.DefenseReduction, Is.Zero);
        }

        [UnityTest]
        public IEnumerator MeteorDamagesRootsAndReducesAttackThenExpires()
        {
            _config.rootDuration = 0.2f;
            var enemy = Enemy(Vector2.zero);
            _player.Skills.TryUltimate(Vector2.zero);
            Assert.That(_player.Skills.TryUltimate(Vector2.zero), Is.False);
            yield return new WaitForSeconds(0.12f);
            Assert.That(enemy.Health.CurrentHp, Is.EqualTo(400f));
            Assert.That(enemy.Health.Status.Rooted, Is.True);
            Assert.That(enemy.Health.Status.AttackMultiplier, Is.EqualTo(0.7f).Within(0.01f));
            yield return new WaitForSeconds(0.35f);
            Assert.That(enemy.Health.Status.AttackMultiplier, Is.EqualTo(1f));
            Assert.That(enemy.Health.Status.Rooted, Is.False);
        }

        [UnityTest]
        public IEnumerator ShadowHitsStackOnlyOnSuccessAndReset()
        {
            Select(HeroRole.Shadow);
            _config.stackTimeout = 0.15f;
            var enemy = Enemy(Vector2.zero);
            float initial = _player.Skills.AttackInterval;
            _player.Skills.BasicAttack(Vector2.up);
            Assert.That(_player.Skills.HitStacks, Is.Zero);
            for (int i = 0; i < 30; i++) enemy.Health.TakeDamage(_player.Skills.MakeHit(1f));
            Assert.That(_player.Skills.HitStacks, Is.EqualTo(_config.maxStacks));
            Assert.That(_player.Skills.AttackInterval, Is.LessThan(initial));
            yield return new WaitForSeconds(0.2f);
            Assert.That(_player.Skills.HitStacks, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ShadowOverdriveFiresFanAndDashStopsAtWall()
        {
            Select(HeroRole.Shadow);
            _config.duration = 1f;
            Wall(new Vector2(-3.1f, 0f));
            yield return new WaitForFixedUpdate();
            _player.Skills.TryActive(Vector2.right, Vector2.zero);
            _player.Skills.BasicAttack(Vector2.right);
            Assert.That(Object.FindObjectsByType<Projectile>().Length, Is.EqualTo(3));
            yield return new WaitForSeconds(0.3f);
            Assert.That(_player.transform.position.x, Is.GreaterThan(-3f));
        }

        [UnityTest]
        public IEnumerator ShadowRainDamagesVisibleTargetsOnce()
        {
            Select(HeroRole.Shadow);
            _config.castDelay = 0.15f;
            var enemy = Enemy(Vector2.zero);
            var hidden = Enemy(new Vector2(0f, 20f));
            _player.Skills.TryUltimate(Vector2.zero);
            yield return null;
            Assert.That(_player.GetComponent<SpriteRenderer>().enabled, Is.False, "Shadow's visual rises without moving the ground collider.");
            Assert.That(_player.transform.position.y, Is.Zero.Within(0.01f));
            yield return new WaitForSeconds(0.2f);
            Assert.That(enemy.Health.CurrentHp, Is.EqualTo(400f));
            Assert.That(hidden.Health.CurrentHp, Is.EqualTo(500f));
            Assert.That(_player.GetComponent<SpriteRenderer>().enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator MagicianCardsBounceWithoutRepeatingTargets()
        {
            Select(HeroRole.Magician);
            var first = Enemy(Vector2.zero);
            var second = Enemy(new Vector2(1.2f, 0f));
            _player.Skills.BasicAttack(Vector2.right);
            yield return new WaitForSeconds(0.8f);
            Assert.That(first.Health.CurrentHp, Is.EqualTo(475f));
            Assert.That(second.Health.CurrentHp, Is.EqualTo(475f));
        }

        [UnityTest]
        public IEnumerator MagicianTrapTriggersOnceAndKnockupInterrupts()
        {
            Select(HeroRole.Magician);
            var enemy = Enemy(_player.transform.position);
            _player.Skills.TryActive(Vector2.right, Vector2.zero);
            yield return new WaitForSeconds(0.12f);
            Assert.That(enemy.Health.CurrentHp, Is.EqualTo(450f));
            Assert.That(enemy.Health.Status.Interrupted, Is.True);
            yield return new WaitForSeconds(0.8f);
            Assert.That(enemy.Health.Status.Interrupted, Is.False);
            Assert.That(enemy.Health.CurrentHp, Is.EqualTo(450f));
        }

        [UnityTest]
        public IEnumerator MagicianEvasionRejectsHitsAndStormTeleportsSafely()
        {
            Select(HeroRole.Magician);
            _player.Health.Evasion = 1f;
            Assert.That(_player.Health.TakeDamage(new DamageInfo(30f)).dodged, Is.True);
            var enemy = Enemy(_player.transform.position);
            _player.Skills.TryUltimate(Vector2.zero);
            Assert.That(CombatWorld.IsSafe(_player.transform.position, _player.GetComponent<Rigidbody2D>()), Is.True);
            yield return new WaitForSeconds(0.4f);
            Assert.That(enemy.Health.CurrentHp, Is.EqualTo(400f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator OracleOrbExplodesBypassesDefenseAndPermittedInteriorWalls()
        {
            Select(HeroRole.OracleWitch);
            _config.basicDamage = 35f;
            var wall = Wall(new Vector2(-1f, 0f)).GetComponent<Wall>();
            wall.isBoundary = false;
            wall.allowMagicPassage = true;
            var first = Enemy(Vector2.zero);
            var second = Enemy(new Vector2(0.8f, 0f));
            first.Health.magicDefense = 1000f;
            _player.Skills.BasicAttack(Vector2.right);
            yield return new WaitForSeconds(0.8f);
            Assert.That(first.Health.CurrentHp, Is.EqualTo(465f));
            Assert.That(second.Health.CurrentHp, Is.EqualTo(465f));
        }

        [UnityTest]
        public IEnumerator OracleFieldRemovesSlowWhenTargetLeaves()
        {
            Select(HeroRole.OracleWitch);
            var enemy = Enemy(Vector2.zero);
            _player.Skills.TryActive(Vector2.right, Vector2.zero);
            yield return new WaitForSeconds(0.08f);
            Assert.That(enemy.Health.Status.MoveMultiplier, Is.EqualTo(0.4f).Within(0.01f));
            enemy.transform.position = new Vector2(5f, 0f);
            yield return new WaitForSeconds(0.08f);
            Assert.That(enemy.Health.Status.MoveMultiplier, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator OracleJudgmentGuaranteedHitIgnoresEvasion()
        {
            Select(HeroRole.OracleWitch);
            var enemy = Enemy(Vector2.zero);
            enemy.Health.Evasion = 1f;
            _player.Skills.TryUltimate(Vector2.zero);
            yield return new WaitForSeconds(0.15f);
            Assert.That(enemy.Health.CurrentHp, Is.EqualTo(400f));
        }

        [UnityTest]
        public IEnumerator DaphneSolarExplodesBurnsAndKnocksBack()
        {
            Select(HeroRole.Daphne);
            _config.burnDuration = 1.1f;
            var enemy = Enemy(Vector2.zero);
            _player.Skills.BasicAttack(Vector2.right);
            yield return new WaitForSeconds(0.4f);
            Assert.That(enemy.Health.CurrentHp, Is.EqualTo(475f));
            Assert.That(enemy.Health.Status.Has(StatusKind.Burn), Is.True);
            yield return new WaitForSeconds(1f);
            Assert.That(enemy.Health.CurrentHp, Is.LessThan(475f));
            yield return new WaitForSeconds(0.2f);
            Assert.That(enemy.Health.Status.Has(StatusKind.Burn), Is.False);
            _player.transform.position = new Vector2(-1f, 0f);
            _player.Skills.TryActive(Vector2.right, Vector2.zero);
            yield return new WaitForSeconds(0.3f);
            Assert.That(enemy.transform.position.x, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator DaphneLunarRootsDragonAndTransitionsDoNotStack()
        {
            Select(HeroRole.Daphne);
            Assert.That(_player.Skills.TrySwitchStance(), Is.True);
            Assert.That(_player.Skills.TrySwitchStance(), Is.False);
            Assert.That(_player.Health.Status.MoveMultiplier, Is.EqualTo(1.2f).Within(0.01f));
            var dragon = Enemy(Vector2.zero, "Dragon");
            _player.Skills.TryActive(Vector2.right, Vector2.zero);
            yield return new WaitForSeconds(0.4f);
            Assert.That(dragon.Health.Status.Rooted, Is.True);
            Assert.That(dragon.Health.CurrentHp, Is.EqualTo(450f));
            Assert.That(dragon.transform.position.y, Is.Zero.Within(0.01f));
            Assert.That(_player.Health.Status.MoveMultiplier, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator DaphneEclipseHealsWithoutChangingStance()
        {
            Select(HeroRole.Daphne);
            var enemy = Enemy(Vector2.zero);
            _player.Health.TakeDamage(40f);
            _player.Skills.TrySwitchStance();
            _player.Skills.TryUltimate(Vector2.zero);
            yield return null;
            Assert.That(_player.Health.CurrentHp, Is.EqualTo(90f));
            Assert.That(enemy.Health.CurrentHp, Is.EqualTo(400f));
            Assert.That(_player.Skills.IsLunar, Is.True);
        }

        [UnityTest]
        public IEnumerator DisablingPlayerCleansAreasProjectilesAndPassives()
        {
            _player.Skills.TryActive(Vector2.right, Vector2.zero);
            _player.Skills.BasicAttack(Vector2.right);
            _player.gameObject.SetActive(false);
            yield return null;
            Assert.That(Object.FindObjectsByType<ShieldWall>(), Is.Empty);
            Assert.That(Object.FindObjectsByType<Projectile>(), Is.Empty);
            Assert.That(_player.Health.DistanceReduction, Is.Zero);
        }

        [UnityTest]
        public IEnumerator DaphneLunarBasicDealsHigherSingleTargetDamage()
        {
            Select(HeroRole.Daphne);
            _player.Skills.TrySwitchStance();
            var first = Enemy(Vector2.zero);
            var nearby = Enemy(new Vector2(0.8f, 0f));
            _player.Skills.BasicAttack(Vector2.right);
            yield return new WaitForSeconds(0.4f);
            Assert.That(first.Health.CurrentHp, Is.EqualTo(465f));
            Assert.That(nearby.Health.CurrentHp, Is.EqualTo(500f));
            Assert.That(first.Health.Status.Has(StatusKind.Burn), Is.False);
        }

        [UnityTest]
        public IEnumerator StanceChangesRefreshBonusesAndKeepSharedActiveCooldown()
        {
            Select(HeroRole.Daphne);
            _config.stanceCooldown = 0.05f;
            _config.duration = 1f;
            _player.Skills.TryActive(Vector2.right, Vector2.zero);
            _player.Skills.TrySwitchStance();
            Assert.That(_player.Skills.TryActive(Vector2.right, Vector2.zero), Is.False);
            yield return new WaitForSeconds(0.08f);
            _player.Skills.TrySwitchStance();
            Assert.That(_player.Health.Status.MoveMultiplier, Is.EqualTo(1f));
            Assert.That(_player.Health.Status.AttackMultiplier, Is.EqualTo(1.2f).Within(0.01f));
            yield return new WaitForSeconds(0.08f);
            _player.Skills.TrySwitchStance();
            Assert.That(_player.Health.Status.AttackMultiplier, Is.EqualTo(1f));
            Assert.That(_player.Health.Status.MoveMultiplier, Is.EqualTo(1.2f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator OracleCannotPassMapBoundaryAndProjectilesDeduplicateHurtboxes()
        {
            Select(HeroRole.OracleWitch);
            var wall = Wall(new Vector2(-1f, 0f));
            var enemy = Enemy(Vector2.zero);
            _player.Skills.BasicAttack(Vector2.right);
            yield return new WaitForSeconds(0.7f);
            Assert.That(enemy.Health.CurrentHp, Is.EqualTo(500f));
            Object.Destroy(wall);
            Select(HeroRole.Tanky);
            var extra = enemy.gameObject.AddComponent<CircleCollider2D>();
            extra.isTrigger = true;
            extra.radius = 0.8f;
            _player.Skills.BasicAttack(Vector2.right);
            yield return new WaitForSeconds(0.4f);
            Assert.That(enemy.Health.CurrentHp, Is.EqualTo(475f));
        }

        [UnityTest]
        public IEnumerator TrapExpiresWithoutEnemyAndDeathCancelsDelayedUltimate()
        {
            Select(HeroRole.Magician);
            _config.trapLifetime = 0.1f;
            _player.Skills.TryActive(Vector2.right, Vector2.zero);
            yield return new WaitForSeconds(0.2f);
            Assert.That(Object.FindObjectsByType<SkillArea>(), Is.Empty);
            Select(HeroRole.Tanky);
            var enemy = Enemy(Vector2.zero);
            _player.Skills.TryUltimate(Vector2.zero);
            _player.Health.TakeDamage(new DamageInfo(10000f) { guaranteed = true, bypassDefense = true });
            yield return new WaitForSeconds(0.2f);
            Assert.That(enemy.Health.CurrentHp, Is.EqualTo(500f));
            Assert.That(_player.Skills.TryActive(Vector2.right, Vector2.zero), Is.False);
        }

        [UnityTest]
        public IEnumerator TeleportFallsBackWhenSurroundedAndVisualCountIsBounded()
        {
            var surround = new GameObject("Surround");
            _owned.Add(surround);
            surround.transform.position = _player.transform.position;
            surround.AddComponent<BoxCollider2D>().size = Vector2.one * 30f;
            yield return new WaitForFixedUpdate();
            var body = _player.GetComponent<Rigidbody2D>();
            Assert.That(CombatWorld.SafeTeleport(body, _config.range), Is.EqualTo(body.position));
            for (int i = 0; i < 300; i++) SkillVfx.Burst(Vector2.zero, Color.white);
            // Four persistent Tanky shield visuals are not part of the transient budget.
            Assert.That(Object.FindObjectsByType<SkillVfx>().Length, Is.LessThanOrEqualTo(100));
        }

        [UnityTest]
        public IEnumerator SavedScenesSelectAllFiveHeroesAndPreserveBoundaries()
        {
            var expected = new[] { HeroRole.OracleWitch, HeroRole.Daphne, HeroRole.Tanky, HeroRole.Shadow, HeroRole.Magician };
            for (int i = 0; i < expected.Length; i++)
            {
                GameSession.SelectedCharacter = i;
                SceneManager.LoadScene("Level1");
                yield return null;
                var player = PlayerController.Instance;
                Assert.That(player, Is.Not.Null);
                Assert.That(player.Skills.Config.role, Is.EqualTo(expected[i]));
                Assert.That(player.Skills.CanAct, Is.True);
                Assert.That(CombatWorld.Contains(player.transform.position), Is.True);
                Assert.That(player.Skills.TryActive(Vector2.up, player.transform.position), Is.True);
                player.Skills.BasicAttack(Vector2.up);
                yield return null;
            }
            SceneManager.LoadScene("MainMenu");
            yield return null;
            Assert.That(Object.FindAnyObjectByType<MainMenu>().characters.Length, Is.EqualTo(5));
            Assert.That(Object.FindObjectsByType<Projectile>(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator SevenLevelsStillClearTransitionAndFinishWithVictory()
        {
            GameSession.SelectedCharacter = 2;
            SceneManager.LoadScene("Level1");
            yield return null;
            for (int level = 1; level <= 7; level++)
            {
                var manager = LevelManager.Instance;
                Assert.That(manager.levelNumber, Is.EqualTo(level));
                Assert.That(CombatWorld.Contains(PlayerController.Instance.transform.position), Is.True);
                if (level == 2)
                {
                    var bounds = manager.playableBounds;
                    bool excludesOutsideCorner = !manager.ContainsPoint(bounds.min + Vector2.one * 0.01f) ||
                        !manager.ContainsPoint(bounds.max - Vector2.one * 0.01f) ||
                        !manager.ContainsPoint(new Vector2(bounds.xMin + 0.01f, bounds.yMax - 0.01f));
                    Assert.That(excludesOutsideCorner, Is.True, "The nearly closed Level2 outline must be used, not just its bounding rectangle.");
                }
                foreach (var enemy in new List<EnemyController>(LevelManager.Enemies))
                    enemy.Health.TakeDamage(new DamageInfo(10000f) { guaranteed = true, bypassDefense = true });
                yield return new WaitForSeconds(0.25f);
                Assert.That(manager.IsPlaying, Is.False);
                manager.CompleteLevel();
                yield return null;
            }
            Assert.That(PlayerController.Instance.gameObject.activeSelf, Is.False);
            Assert.That(PlayerController.Instance.CanMove, Is.False);
            SceneManager.LoadScene("Level1");
            yield return null;
            Assert.That(PlayerController.Instance.Skills.ActiveRemaining, Is.Zero);
            Assert.That(PlayerController.Instance.Health.CurrentHp, Is.EqualTo(100f));
        }

        [UnityTest]
        public IEnumerator KeyboardRoutesActiveUltimateStanceAndHeldBasicAttack()
        {
            Select(HeroRole.Daphne);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                _player.Health.TakeDamage(40f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
                yield return null;
                yield return null;
                Assert.That(_player.Skills.ActiveRemaining, Is.GreaterThan(0f));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return null;
                yield return null;
                Assert.That(_player.Skills.UltimateRemaining, Is.GreaterThan(0f));
                Assert.That(_player.Health.CurrentHp, Is.EqualTo(90f));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab));
                yield return null;
                yield return null;
                Assert.That(_player.Skills.IsLunar, Is.True);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                yield return null;
                yield return null;
                Assert.That(Object.FindObjectsByType<Projectile>().Length, Is.GreaterThan(0));
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }

        [UnityTest]
        public IEnumerator ClearedStageAllowsWalkingIntoOpenPortal()
        {
            GameSession.SelectedCharacter = 2;
            SceneManager.LoadScene("Level1");
            yield return null;
            foreach (var enemy in new List<EnemyController>(LevelManager.Enemies))
                enemy.Health.TakeDamage(new DamageInfo(10000f) { guaranteed = true, bypassDefense = true });
            yield return new WaitForSeconds(0.3f);
            var manager = LevelManager.Instance;
            var player = PlayerController.Instance;
            Assert.That(manager.IsPlaying, Is.False);
            Assert.That(manager.portal.IsOpen, Is.True);
            Assert.That(player.CanMove, Is.True);
            Assert.That(player.Skills.CanAct, Is.False);

            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                Vector2 start = player.transform.position;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return new WaitForSeconds(0.15f);
                Assert.That(Vector2.Distance(start, player.transform.position), Is.GreaterThan(0.1f),
                    "WASD must still move the player after the last enemy dies.");

                var body = player.GetComponent<Rigidbody2D>();
                body.position = (Vector2)manager.portal.transform.position - Vector2.up * 1.5f;
                body.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.6f);
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Level2"),
                    "Walking into the open portal must advance the level through its trigger.");
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }

        [UnityTest]
        public IEnumerator SavedSkeletonStagesFireArrowsThatDamageThePlayer()
        {
            foreach (int stage in new[] { 2, 3, 5, 6 })
            {
                GameSession.SelectedCharacter = 0; // Oracle has no passive evasion or shield.
                SceneManager.LoadScene($"Level{stage}");
                yield return null;
                var player = PlayerController.Instance;
                player.Health.maxHp = 10000f;
                player.Health.Heal(10000f);
                player.Health.invulnerableTime = 0f;
                foreach (var enemy in new List<EnemyController>(LevelManager.Enemies))
                    if (enemy.style == AttackStyle.Melee)
                        enemy.Health.TakeDamage(new DamageInfo(10000f) { guaranteed = true, bypassDefense = true });

                float startingHp = player.Health.CurrentHp;
                bool sawArrow = false;
                float deadline = Time.time + 4f;
                while (Time.time < deadline && (!sawArrow || player.Health.CurrentHp >= startingHp))
                {
                    foreach (var shot in Object.FindObjectsByType<Projectile>())
                    {
                        if (shot.OwnerTeam != Team.Enemy || !shot.name.StartsWith("Arrow")) continue;
                        var renderer = shot.GetComponent<SpriteRenderer>();
                        sawArrow |= renderer != null && renderer.enabled && renderer.sprite != null && renderer.color.a > 0f;
                    }
                    yield return null;
                }
                Assert.That(sawArrow, Is.True, $"Stage {stage}: saved Skeletons must fire a rendered Arrow projectile.");
                Assert.That(player.Health.CurrentHp, Is.LessThan(startingHp), $"Stage {stage}: Skeleton arrows must reach and damage the player.");
            }
        }

        [UnityTest]
        public IEnumerator SkeletonAgainstTopBoundaryStillShootsNearbyPlayer()
        {
            #if UNITY_EDITOR
            Select(HeroRole.OracleWitch);
            _player.transform.position = new Vector2(0f, -1f);
            var hurt = new GameObject("PlayerHurtbox");
            hurt.transform.SetParent(_player.transform, false);
            var hurtbox = hurt.AddComponent<BoxCollider2D>();
            hurtbox.isTrigger = true;
            hurtbox.size = new Vector2(0.7f, 1.2f);
            hurtbox.offset = Vector2.up * 0.5f;

            var wall = Wall(new Vector2(0f, 1.3f));
            wall.GetComponent<BoxCollider2D>().size = new Vector2(10f, 0.2f);
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Skeleton.prefab");
            var skeleton = Object.Instantiate(prefab, new Vector2(0f, 0.65f), Quaternion.identity);
            _owned.Add(skeleton);
            var managerObject = new GameObject("BoundaryTestLevel");
            _owned.Add(managerObject);
            managerObject.AddComponent<LevelManager>().playableBounds = new Rect(-5f, -5f, 10f, 6.2f);

            float deadline = Time.time + 3f;
            while (Time.time < deadline && _player.Health.CurrentHp >= 100f) yield return null;
            Assert.That(_player.Health.CurrentHp, Is.LessThan(100f),
                "A retreating Skeleton must still shoot when its feet reach a wall and its cast point extends beyond the walkable outline.");
            #else
            Assert.Ignore("This regression uses the Editor's saved Skeleton prefab.");
            yield break;
            #endif
        }

        [UnityTest]
        public IEnumerator DragonFireballsSpawnClearOfAngledMapWall()
        {
            #if UNITY_EDITOR
            Select(HeroRole.OracleWitch);
            _player.transform.position = new Vector2(0f, -1f);
            var hurt = new GameObject("PlayerHurtbox");
            hurt.transform.SetParent(_player.transform, false);
            var hurtbox = hurt.AddComponent<BoxCollider2D>();
            hurtbox.isTrigger = true;
            hurtbox.size = new Vector2(0.7f, 1.2f);
            hurtbox.offset = Vector2.up * 0.5f;

            var wall = Wall(new Vector2(0f, 2f));
            var wallCollider = wall.GetComponent<BoxCollider2D>();
            wallCollider.size = new Vector2(16f, 0.2f);
            wall.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Dragon.prefab");
            var dragon = Object.Instantiate(prefab, new Vector2(0f, 0.5f), Quaternion.identity);
            _owned.Add(dragon);
            var enemy = dragon.GetComponent<EnemyController>();
            enemy.moveSpeed = 0f;
            var managerObject = new GameObject("AngledWallTestLevel");
            _owned.Add(managerObject);
            managerObject.AddComponent<LevelManager>().playableBounds = new Rect(-5f, -5f, 10f, 10f);
            Physics2D.SyncTransforms();

            foreach (float height in new[] { 0.1f, 0.5f })
            {
                foreach (var old in Object.FindObjectsByType<Projectile>()) Object.Destroy(old.gameObject);
                yield return null;
                dragon.GetComponent<Rigidbody2D>().position = new Vector2(0f, height);
                Physics2D.SyncTransforms();
                // Check both an inside cast point whose hitbox overlaps, and an outside cast point.
                // Inspect before a physics step can move a fireball away from the overlap.
                typeof(EnemyController).GetMethod("PerformAttack", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(enemy, new object[] { _player, 1.5f });
                Physics2D.SyncTransforms();
                var shots = Object.FindObjectsByType<Projectile>();
                Assert.That(shots.Length, Is.GreaterThan(0));
                foreach (var shot in shots)
                    Assert.That(shot.GetComponent<Collider2D>().Distance(wallCollider).isOverlapped, Is.False,
                        "Dragon fireballs must not begin overlapping a sloped map boundary.");
            }

            float deadline = Time.time + 3f;
            while (Time.time < deadline && _player.Health.CurrentHp >= 100f) yield return null;
            Assert.That(_player.Health.CurrentHp, Is.LessThan(100f));
            #else
            Assert.Ignore("This regression uses the Editor's saved Dragon prefab.");
            yield break;
            #endif
        }
    }
}
