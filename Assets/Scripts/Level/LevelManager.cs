using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MagicGame
{
    // One per level scene: tracks enemies, opens the portal when they are all gone,
    // handles game over / victory and draws the HUD.
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        static readonly List<EnemyController> s_enemies = new();

        public int levelNumber = 1;
        public string levelTitle = "Level";
        public bool isFinalLevel;
        public Portal portal;
        public string menuScene = "MainMenu";

        enum State { Playing, Cleared, GameOver, Victory }
        State _state = State.Playing;
        float _bannerUntil;
        GUIStyle _big, _mid, _center, _small;

        public static void RegisterEnemy(EnemyController e)
        {
            if (!s_enemies.Contains(e)) s_enemies.Add(e);
        }

        public static void UnregisterEnemy(EnemyController e) => s_enemies.Remove(e);

        void Awake()
        {
            Instance = this;
            _bannerUntil = Time.time + 2.5f;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var player = PlayerController.Instance;

            if (_state == State.Playing)
            {
                if (player != null && player.Health.IsDead)
                {
                    _state = State.GameOver;
                    player.GetComponent<SpriteRenderer>().color = new Color(0.4f, 0.4f, 0.4f, 0.8f);
                }
                else if (Time.timeSinceLevelLoad > 0.2f && s_enemies.Count == 0)
                {
                    _state = State.Cleared;
                    if (portal != null) portal.SetOpen(true);
                    _bannerUntil = Time.time + 2.5f;
                }
            }

            if (keyboard == null) return;
            if (keyboard.rKey.wasPressedThisFrame && _state != State.Victory)
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            if (keyboard.escapeKey.wasPressedThisFrame ||
                (_state == State.Victory && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)))
                SceneManager.LoadScene(menuScene);
        }

        public void CompleteLevel()
        {
            int next = SceneManager.GetActiveScene().buildIndex + 1;
            if (isFinalLevel || next >= SceneManager.sceneCountInBuildSettings)
            {
                _state = State.Victory;
                if (PlayerController.Instance != null) PlayerController.Instance.gameObject.SetActive(false);
                return;
            }
            SceneManager.LoadScene(next);
        }

        void OnGUI()
        {
            EnsureStyles();
            float scale = Screen.height / 720f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale;
            const float h = 720f;

            var player = PlayerController.Instance;
            GUI.Label(new Rect(20, 12, 600, 30), $"Stage {levelNumber}: {levelTitle}", _mid);
            if (player != null)
            {
                DrawBar(new Rect(20, 48, 260, 18), player.Health.Ratio, new Color(0.2f, 0.85f, 0.35f));
                GUI.Label(new Rect(290, 44, 200, 26), $"HP {Mathf.CeilToInt(player.Health.CurrentHp)}", _small);
            }
            GUI.Label(new Rect(20, 72, 400, 26), $"Monsters left: {s_enemies.Count}", _small);
            GUI.Label(new Rect(20, h - 34, 900, 26),
                "Move: WASD / Arrows   Cast ring: Space / J / Left click   R: restart   Esc: menu", _small);

            foreach (var e in s_enemies)
            {
                if (e == null || !e.isBoss) continue;
                GUI.Label(new Rect(w / 2 - 250, 12, 500, 28), e.displayName, _mid);
                DrawBar(new Rect(w / 2 - 250, 44, 500, 20), e.Health.Ratio, new Color(0.9f, 0.2f, 0.15f));
                break;
            }

            switch (_state)
            {
                case State.Playing when Time.time < _bannerUntil:
                    GUI.Label(new Rect(0, h / 2 - 60, w, 60), $"Stage {levelNumber}", _big);
                    break;
                case State.Cleared when Time.time < _bannerUntil:
                    GUI.Label(new Rect(0, h / 2 - 60, w, 60), "Stage clear! Enter the portal", _big);
                    break;
                case State.GameOver:
                    Dim(w, h);
                    GUI.Label(new Rect(0, h / 2 - 70, w, 60), "Game Over", _big);
                    GUI.Label(new Rect(0, h / 2, w, 30), "Press R to try again, Esc for menu", _center);
                    break;
                case State.Victory:
                    Dim(w, h);
                    GUI.Label(new Rect(0, h / 2 - 70, w, 60), "Victory! The dragon is defeated", _big);
                    GUI.Label(new Rect(0, h / 2, w, 30), "Press Enter to return to the menu", _center);
                    break;
            }
        }

        static void Dim(float w, float h)
        {
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = old;
        }

        static void DrawBar(Rect r, float ratio, Color fill)
        {
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = fill;
            GUI.DrawTexture(new Rect(r.x + 2, r.y + 2, (r.width - 4) * Mathf.Clamp01(ratio), r.height - 4), Texture2D.whiteTexture);
            GUI.color = old;
        }

        void EnsureStyles()
        {
            if (_big != null) return;
            _big = new GUIStyle(GUI.skin.label) { fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _big.normal.textColor = Color.white;
            _mid = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _mid.normal.textColor = Color.white;
            _center = new GUIStyle(_mid) { alignment = TextAnchor.MiddleCenter };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            _small.normal.textColor = Color.white;
        }
    }
}
