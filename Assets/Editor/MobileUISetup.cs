using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MagicGame.EditorTools
{
    public static class MobileUISetup
    {
        const string Root = "Assets/Game/UI";

        [MenuItem("Tools/Magic Game/Set Up Mobile UI")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before setting up UI.");
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory("Assets/Resources/UI");
            AssetDatabase.Refresh();
            if (AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset") == null)
                TMP_PackageResourceImporter.ImportResources(true, false, false);
            AssetDatabase.Refresh();
            var theme = AssetDatabase.LoadAssetAtPath<ArcaneTheme>(Root + "/ArcaneTheme.asset");
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<ArcaneTheme>();
                AssetDatabase.CreateAsset(theme, Root + "/ArcaneTheme.asset");
            }
            theme.bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            theme.titleFont = theme.bodyFont;
            var titleSource = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/CormorantGaramond.ttf");
            if (titleSource != null)
            {
                var title = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/TitleFont.asset");
                if (title == null)
                {
                    title = TMP_FontAsset.CreateFontAsset(titleSource);
                    title.name = "Cormorant Garamond UI";
                    title.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 !?.,:;/'-–—·&()");
                    AssetDatabase.CreateAsset(title, Root + "/TitleFont.asset");
                    AssetDatabase.AddObjectToAsset(title.material, title);
                    foreach (var atlas in title.atlasTextures) if (atlas != null) AssetDatabase.AddObjectToAsset(atlas, title);
                }
                theme.titleFont = title;
            }
            if (theme.bodyFont == null) throw new InvalidOperationException("TextMeshPro resources have not finished importing. Run Set Up Mobile UI again.");
            theme.panel = MakeSprite("Panel", 64, 64, (x, y) =>
            {
                float dx = Mathf.Max(10 - x, x - 53, 0);
                float dy = Mathf.Max(10 - y, y - 53, 0);
                return dx * dx + dy * dy <= 100;
            }, new Vector4(14, 14, 14, 14));
            theme.disc = MakeSprite("Disc", 128, 128, (x, y) => Vector2.Distance(new Vector2(x, y), new Vector2(63.5f, 63.5f)) < 62);
            theme.ring = MakeSprite("Ring", 256, 256, (x, y) =>
            {
                Vector2 p = new Vector2(x - 127.5f, y - 127.5f);
                float r = p.magnitude;
                bool rings = Mathf.Abs(r - 113) < 2.5f || Mathf.Abs(r - 100) < 1.4f;
                float angle = Mathf.Atan2(p.y, p.x);
                bool ticks = r > 98 && r < 119 && Mathf.Abs(Mathf.Sin(angle * 8)) < 0.055f;
                bool star = Mathf.Abs(p.x) + Mathf.Abs(p.y) < 28 && Mathf.Abs(p.x * p.y) < 65;
                return rings || ticks || star;
            });
            theme.symbols = new Sprite[4];
            theme.symbols[0] = MakeSprite("Crystal", 96, 96, (x, y) => Mathf.Abs(x - 48) * 1.5f + Mathf.Abs(y - 48) < 40);
            theme.symbols[1] = MakeSprite("Spell", 96, 96, (x, y) =>
            {
                float dx = Mathf.Abs(x - 48), dy = Mathf.Abs(y - 48);
                return dx + dy < 42 && (Mathf.Abs(dx + dy - 32) < 4 || dx < 4 || dy < 4);
            });
            theme.symbols[2] = MakeSprite("Moon", 96, 96, (x, y) => Vector2.Distance(new Vector2(x, y), new Vector2(45, 48)) < 34 && Vector2.Distance(new Vector2(x, y), new Vector2(60, 60)) > 30);
            theme.symbols[3] = MakeSprite("Sun", 96, 96, (x, y) =>
            {
                Vector2 p = new Vector2(x - 48, y - 48);
                float r = p.magnitude;
                return r < 18 || (r > 24 && r < 40 && Mathf.Abs(Mathf.Sin(Mathf.Atan2(p.y, p.x) * 6)) < 0.18f);
            });
            theme.heroes = GameSession.HeroIds.Select(id => AssetDatabase.LoadAssetAtPath<CharacterAnimSet>($"Assets/Game/AnimSets/{id}.asset")).ToArray();
            theme.village = LoadSprite("Assets/Art/Maps/Game Start Screen.png");
            GrimoireArtImporter.Assign(theme);
            EditorUtility.SetDirty(theme);
            var previousScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var authoringScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(authoringScene);
            GameObject root = null;
            try
            {
                root = new GameObject("Arcane Game UI", typeof(RectTransform));
                var ui = root.AddComponent<GameUI>();
                ui.theme = theme;
                ui.BuildLayout();
                PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/UI/GameUI.prefab");
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                EditorSceneManager.CloseScene(authoringScene, true);
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(previousScene);
            }
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Mobile UI ready. Existing menu and level scripts load the shared prefab automatically; scene art is preserved.");
        }

        static Sprite LoadSprite(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Sprite MakeSprite(string name, int width, int height, Func<int, int, bool> filled, Vector4 border = default)
        {
            string path = $"{Root}/{name}.png";
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++) pixels[y * width + x] = filled(x, y) ? Color.white : Color.clear;
            texture.SetPixels32(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
