using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MagicGame
{
    // One per level scene: tracks enemies, opens the portal when they are all gone,
    // handles game over / victory; the shared uGUI view presents these states.
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        static readonly List<EnemyController> s_enemies = new();
        public static IReadOnlyList<EnemyController> Enemies => s_enemies;
        public Rect playableBounds = new Rect(-7.68f, -5.12f, 15.36f, 10.24f);
        public bool IsPlaying => _state == State.Playing;
        public bool AllowsMovement => _state == State.Playing || _state == State.Cleared;
        Vector2[] _outline;
        Rect _skyCombatBounds;
        public bool HasSkyArena { get; private set; }
        public Rect CombatBounds => HasSkyArena ? _skyCombatBounds : playableBounds;

        public bool ContainsCombatPoint(Vector2 point) => HasSkyArena
            ? _skyCombatBounds.Contains(point) : ContainsPoint(point);

        public bool ContainsPoint(Vector2 point)
        {
            if (!playableBounds.Contains(point)) return false;
            if (_outline == null || _outline.Length < 3) return true;
            bool inside = false;
            for (int i = 0, j = _outline.Length - 1; i < _outline.Length; j = i++)
            {
                var a = _outline[i];
                var b = _outline[j];
                if ((a.y > point.y) != (b.y > point.y) &&
                    point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry()
        {
            s_enemies.Clear();
            EnemiesChanged = null;
        }

        public int levelNumber = 1;
        public string levelTitle = "Level";
        public bool isFinalLevel;
        public Portal portal;
        public string menuScene = "MainMenu";

        public enum State { Playing, Cleared, GameOver, Victory }
        State _state = State.Playing;
        public State CurrentState => _state;
        public event System.Action StateChanged;
        public static event System.Action EnemiesChanged;

        public static void RegisterEnemy(EnemyController e)
        {
            if (!s_enemies.Contains(e))
            {
                s_enemies.Add(e);
                EnemiesChanged?.Invoke();
            }
        }

        public static void UnregisterEnemy(EnemyController e)
        {
            if (s_enemies.Remove(e)) EnemiesChanged?.Invoke();
        }

        void Start()
        {
            var player = PlayerController.Instance;
            if (Progression.HasRun)
            {
                var run = Progression.Run;
                if (run.phase == RunPhase.Reward || run.phase == RunPhase.Cleared)
                {
                    // Restore a cleared checkpoint without replaying combat or enemy death callbacks.
                    foreach (var enemy in new List<EnemyController>(s_enemies))
                        if (enemy != null) enemy.gameObject.SetActive(false);
                    _state = State.Cleared;
                    if (portal != null) portal.SetOpen(run.phase == RunPhase.Cleared);
                }
                else
                {
                    var normal = s_enemies.FindAll(e => e != null && !e.isBoss);
                    normal.Sort((a, b) => string.CompareOrdinal(a.name + a.transform.GetSiblingIndex(), b.name + b.transform.GetSiblingIndex()));
                    var random = new System.Random(unchecked(run.seed + run.loop * 97 + run.stage * 13));
                    for (int i = normal.Count - 1; i > 0; i--)
                    { int j = random.Next(i + 1); (normal[i], normal[j]) = (normal[j], normal[i]); }
                    var config = ProgressionConfig.Current;
                    int eliteCount = Mathf.CeilToInt(normal.Count * Mathf.Min(config.eliteFractionCap, config.eliteFractionPerLoop * (run.loop - 1)));
                    foreach (var enemy in s_enemies)
                        if (enemy != null) enemy.ApplyProgression(run.loop, !enemy.isBoss && normal.IndexOf(enemy) < eliteCount);
                }
                Progression.Save();
            }
            GameUI.EnsureForScene();
        }

        public void RestartStage()
        {
            string hero = PlayerController.Instance != null && PlayerController.Instance.CurrentCharacter != null
                ? PlayerController.Instance.CurrentCharacter.displayName : GameSession.HeroIds[GameSession.SelectedCharacter];
            Progression.Begin(hero);
            GameSession.SelectedHeroId = hero;
            GameSession.Load("Level1");
        }
        public void ReturnToMenu() => GameSession.Load(menuScene);

        void SetState(State state)
        {
            _state = state;
            StateChanged?.Invoke();
        }

        void Awake()
        {
            Instance = this;
            // Compatibility for old scenes and scenes with unsaved editor changes.
            var walls = GameObject.Find("Walls");
            if (walls != null)
            {
                var left = walls.transform.Find("Left")?.GetComponent<BoxCollider2D>();
                var right = walls.transform.Find("Right")?.GetComponent<BoxCollider2D>();
                var top = walls.transform.Find("Top")?.GetComponent<BoxCollider2D>();
                var bottom = walls.transform.Find("Bottom")?.GetComponent<BoxCollider2D>();
                if (left != null && right != null && top != null && bottom != null)
                    playableBounds = Rect.MinMaxRect(left.bounds.max.x, bottom.bounds.max.y, right.bounds.min.x, top.bounds.min.y);
                else
                {
                    // Authored maps use a nearly closed EdgeCollider2D outline.
                    foreach (var edge in walls.GetComponentsInChildren<EdgeCollider2D>())
                    {
                        var points = edge.points;
                        if (points.Length < 3 || Vector2.Distance(points[0], points[points.Length - 1]) > 0.25f) continue;
                        _outline = new Vector2[points.Length];
                        for (int i = 0; i < points.Length; i++) _outline[i] = edge.transform.TransformPoint(points[i] + edge.offset);
                        playableBounds = new Rect(edge.bounds.min, edge.bounds.size);
                        break;
                    }
                }
                foreach (var collider in walls.GetComponentsInChildren<Collider2D>())
                    if (!collider.isTrigger && collider.GetComponent<Wall>() == null) collider.gameObject.AddComponent<Wall>();
            }

            // The dragon fights above the walkable floor. Keep movement/teleports on the
            // floor, while attacks can cross its ceiling and reach the authored sky band.
            _skyCombatBounds = playableBounds;
            foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var enemy in root.GetComponentsInChildren<EnemyController>())
            {
                if (!enemy.flyInSky) continue;
                HasSkyArena = true;
                _skyCombatBounds = Rect.MinMaxRect(
                    Mathf.Min(_skyCombatBounds.xMin, enemy.skyMinX - 1f),
                    Mathf.Min(_skyCombatBounds.yMin, enemy.skyMinY - 1f),
                    Mathf.Max(_skyCombatBounds.xMax, enemy.skyMaxX + 1f),
                    Mathf.Max(_skyCombatBounds.yMax, enemy.skyMaxY + enemy.castHeight + 1f));
            }
            if (HasSkyArena && walls != null)
            {
                var ceiling = walls.transform.Find("Top")?.GetComponent<Wall>();
                if (ceiling != null) ceiling.blocksProjectiles = false;
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (GameUI.GameplayBlocked) return;
            var player = PlayerController.Instance;

            if (_state == State.Playing)
            {
                if (player != null && player.Health.IsDead)
                {
                    Progression.Finish();
                    if (player.ProgressionBonuses != null) player.ProgressionBonuses.Apply();
                    SetState(State.GameOver);
                }
                else if (Time.timeSinceLevelLoad > 0.2f && s_enemies.Count == 0)
                {
                    Progression.ClearStage();
                    SetState(State.Cleared);
                    if (portal != null) portal.SetOpen(!Progression.HasRun || Progression.Run.phase == RunPhase.Cleared);
                }
            }


        }

        public void CompleteLevel()
        {
            if (_state != State.Cleared || !Progression.Advance()) return;
            GameSession.Load(Progression.Scene);
        }

    }
}
