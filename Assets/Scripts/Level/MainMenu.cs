using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MagicGame
{
    // Title screen: pick a hero (click, or Left/Right) and start at stage 1.
    public class MainMenu : MonoBehaviour
    {
        public CharacterAnimSet[] characters;
        public string firstLevelScene = "Level1";

        GUIStyle _title, _label, _button;

        void Update()
        {
            var k = Keyboard.current;
            if (k == null || characters == null || characters.Length == 0) return;
            if (k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame)
                GameSession.SelectedCharacter = (GameSession.SelectedCharacter + characters.Length - 1) % characters.Length;
            if (k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame)
                GameSession.SelectedCharacter = (GameSession.SelectedCharacter + 1) % characters.Length;
            if (k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame)
                SceneManager.LoadScene(firstLevelScene);
        }

        void OnGUI()
        {
            EnsureStyles();
            float scale = Screen.height / 720f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale;

            GUI.Label(new Rect(0, 60, w, 70), "Magic Ring Quest", _title);
            GUI.Label(new Rect(0, 140, w, 30), "Choose your hero", _label);

            if (characters == null) return;
            const float card = 180f, gap = 20f;
            float total = characters.Length * card + (characters.Length - 1) * gap;
            float x0 = (w - total) / 2f;
            for (int i = 0; i < characters.Length; i++)
            {
                var r = new Rect(x0 + i * (card + gap), 200, card, 300);
                bool selected = i == GameSession.SelectedCharacter;
                var old = GUI.color;
                GUI.color = selected ? new Color(0.3f, 0.6f, 1f, 0.5f) : new Color(1f, 1f, 1f, 0.12f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = old;

                DrawSprite(new Rect(r.x + 15, r.y + 15, card - 30, 230), characters[i].PreviewSprite);
                GUI.Label(new Rect(r.x, r.y + 255, card, 30), characters[i].displayName, _label);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) GameSession.SelectedCharacter = i;
            }

            if (GUI.Button(new Rect(w / 2 - 120, 540, 240, 60), "Start", _button))
                SceneManager.LoadScene(firstLevelScene);
            GUI.Label(new Rect(0, 620, w, 30), "Left/Right to choose, Enter to start", _label);
        }

        static void DrawSprite(Rect area, Sprite sprite)
        {
            if (sprite == null) return;
            var tr = sprite.textureRect;
            float aspect = tr.width / tr.height;
            float hgt = area.height, wid = hgt * aspect;
            if (wid > area.width) { wid = area.width; hgt = wid / aspect; }
            var dst = new Rect(area.center.x - wid / 2f, area.yMax - hgt, wid, hgt);
            var tex = sprite.texture;
            var uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
            GUI.DrawTextureWithTexCoords(dst, tex, uv);
        }

        void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 56, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _title.normal.textColor = new Color(0.75f, 0.9f, 1f);
            _label = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
            _label.normal.textColor = Color.white;
            _button = new GUIStyle(GUI.skin.button) { fontSize = 30, fontStyle = FontStyle.Bold };
        }
    }
}
