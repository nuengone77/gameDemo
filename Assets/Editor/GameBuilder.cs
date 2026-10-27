using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace MagicGame.EditorTools
{
    // Tools > Magic Game > Build Everything
    // Re-slices the character sheets (one sprite per frame, bottom-center pivot), creates the
    // animation sets, projectile/character/portal prefabs, the main menu and the 7 level scenes.
    public static class GameBuilder
    {
        const string ArtRoot = "Assets/Art";
        const string GameRoot = "Assets/Game";
        const string SceneRoot = "Assets/Scenes";
        const string TemplateScene = "Assets/Scenes/SampleScene.unity";
        const float MapWidth = 15.36f, MapHeight = 10.24f;

        enum Boss { None, Golem, Dragon, Witch }

        class LevelDef
        {
            public int number;
            public string title, map;
            public Rect walkable;     // normalized 0..1, origin bottom-left
            public float portalX;     // normalized
            public int goblins, skeletons;
            public Boss boss;
        }

        static readonly LevelDef[] Levels =
        {
            new() { number = 1, title = "Forest Path",      map = "first",        walkable = R(0.04f, 0.66f, 0.06f, 0.86f), portalX = 0.39f, goblins = 3 },
            new() { number = 2, title = "Ancient Ruins",    map = "green1",       walkable = R(0.13f, 0.87f, 0.14f, 0.80f), portalX = 0.50f, goblins = 3, skeletons = 2 },
            new() { number = 3, title = "Frost Witch's Domain", map = "mini_boss", walkable = R(0.13f, 0.87f, 0.12f, 0.86f), portalX = 0.50f, skeletons = 2, boss = Boss.Witch },
            new() { number = 4, title = "Night Village",    map = "Village",      walkable = R(0.10f, 0.90f, 0.10f, 0.82f), portalX = 0.50f, goblins = 4, skeletons = 3 },
            new() { number = 5, title = "Golem's Fortress", map = "Fortress",     walkable = R(0.10f, 0.90f, 0.16f, 0.80f), portalX = 0.50f, goblins = 3, boss = Boss.Golem },
            new() { number = 6, title = "Lava Keep",        map = "Pixelfantasy", walkable = R(0.14f, 0.86f, 0.16f, 0.80f), portalX = 0.50f, goblins = 4, skeletons = 5 },
            new() { number = 7, title = "Dragon's Lair",    map = "final_boss",   walkable = R(0.13f, 0.87f, 0.13f, 0.83f), portalX = 0.50f, boss = Boss.Dragon },
        };

        static Rect R(float xMin, float xMax, float yMin, float yMax) => Rect.MinMaxRect(xMin, yMin, xMax, yMax);

        class Shapes { public Sprite ring, circle, square; }

        class Prefabs
        {
            public GameObject player, goblin, skeleton, golem, dragon, witch, portal;
        }

        [MenuItem("Tools/Magic Game/Build Everything")]
        public static void BuildAll()
        {
            EnsureFolder(GameRoot);
            EnsureFolder($"{GameRoot}/AnimSets");
            EnsureFolder($"{GameRoot}/Prefabs");
            EnsureFolder($"{GameRoot}/Sprites");

            var shapes = CreateShapes();
            var sets = CreateAnimSets();
            PrepareMaps();
            var prefabs = CreatePrefabs(sets, shapes);
            ApplyAttacks();
            CreateScenes(sets, prefabs);
            SetupStageMaps();
            if (File.Exists(DragonStageMap)) SetupDragonStage();
            CharacterSkillSetup.UpdateExistingContent();
            AssetDatabase.SaveAssets();
            Debug.Log("[MagicGame] Build complete. Open Assets/Scenes/MainMenu.unity and press Play.");
        }

        // ---------------------------------------------------------------- sprite sheets

        static Dictionary<string, CharacterAnimSet> CreateAnimSets()
        {
            const string p = ArtRoot + "/Characters/Player";
            const string m = ArtRoot + "/Characters/Monster";
            var sets = new Dictionary<string, CharacterAnimSet>
            {
                ["Goblin"] = MakeGoblinSet(),
                ["Skeleton"] = MakeSet("Skeleton", $"{m}/skeleton/skeleton_animation_walk.png", null, 1.5f, 8f),
                ["Golem"] = MakeGolemSet(),
                ["Dragon"] = MakeSet("Dragon", null, $"{m}/dragon/dragon_finalboss.png", 3.0f),
                ["Witch"] = MakeWitchSet(),
            };
            foreach (var hero in PlayerSheets) sets[hero.name] = MakeHeroSet(hero);
            AssetDatabase.SaveAssets();
            return sets;
        }

        // Player heroes are 20% smaller than the 1.4-unit monsters.
        const float PlayerHeight = 1.12f;

        // Row order of the original sheets is Down, Up, Left, Right; the new pixel sheets
        // are drawn Left, Right, Down, Up.
        static readonly FacingDir[] PixelRowOrder = { FacingDir.Left, FacingDir.Right, FacingDir.Down, FacingDir.Up };

        // pixel = file prefix of the new pixel sheets (<pixel>_leftright.png / <pixel>_topdown.png);
        // walk/stand/walkRows = the older sheets, used only when the pixel sheets are missing.
        static readonly (string name, string pixel, string walk, string stand, FacingDir[] walkRows)[] PlayerSheets =
        {
            ("Ling", "Ling/ling", "Ling/ling(newPixel)_animation_walk.png", "Ling/ling_stand.png", PixelRowOrder),
            ("Nae", "Nae/nae", "Nae/nae_animation_walk.png", "Nae/nae_stand.png", null),
            ("New", "New/new", "New/new_animation_walk.png", "New/new_stand.png", null),
            ("Nueng", "Nueng/neung", "Nueng/nueng_animation_walk.png", "Nueng/nueng_stand.png", null),
            ("Peng", "Peng/pleng", "Peng/peg_animation_walk.png", "Peng/peg_stand.png", null),
        };

        static CharacterAnimSet MakeHeroSet((string name, string pixel, string walk, string stand, FacingDir[] walkRows) hero)
        {
            const string dir = ArtRoot + "/Characters/Player";
            string side = $"{dir}/{hero.pixel}_leftright.png", frontBack = $"{dir}/{hero.pixel}_topdown.png";
            if (File.Exists(side) && File.Exists(frontBack))
            {
                var set = MakePixelSet(hero.name, TransparentVersion(side), TransparentVersion(frontBack), PlayerHeight);
                string folder = $"{dir}/{Path.GetDirectoryName(hero.pixel)}";
                set.die = FindDeathSprite(folder);
                ApplyHeroAttack(set, folder);
                EditorUtility.SetDirty(set);
                return set;
            }
            return MakeSet(hero.name, $"{dir}/{hero.walk}", $"{dir}/{hero.stand}", PlayerHeight, walkRows: hero.walkRows);
        }

        // Per-hero attack art in the hero's folder:
        //   "*_attack.png"            4 poses in one row: left, right, up (back), down (front)
        //   "*_attack_projectile.png" frames of the thrown object, art pointing right, growing as it flies
        class HeroAttack
        {
            public bool usePose = true;   // false = keep the walk/stand pose while casting
            public float height = 0.5f;   // world height of the projectile's tallest frame
            public bool pointsRight = true;
            public float speed = 9f, lifetime = 1.1f, fps = 14f;
        }

        static readonly Dictionary<string, HeroAttack> HeroAttacks = new()
        {
            ["Ling"] = new() { height = 0.45f },
            ["Nae"] = new() { height = 0.5f },
            ["New"] = new() { height = 0.6f, speed = 10f, lifetime = 1f },
            ["Nueng"] = new() { height = 0.45f, speed = 12f, lifetime = 0.9f },
            ["Peng"] = new() { height = 0.7f, pointsRight = false, lifetime = 1.6f },
        };

        static readonly FacingDir[] AttackPoseOrder = { FacingDir.Left, FacingDir.Right, FacingDir.Up, FacingDir.Down };

        static string HeroFolder(string heroName)
        {
            var hero = PlayerSheets.First(h => h.name == heroName);
            return $"{ArtRoot}/Characters/Player/{Path.GetDirectoryName(hero.pixel)}";
        }

        static string FindHeroFile(string folder, string pattern) =>
            Directory.GetFiles(folder, pattern).Select(f => f.Replace('\\', '/')).FirstOrDefault();

        static bool HasHeroProjectile(string heroName) =>
            FindHeroFile(HeroFolder(heroName), "*_attack_projectile.png") != null;

        static void ApplyHeroAttack(CharacterAnimSet set, string folder)
        {
            var cfg = HeroAttacks.TryGetValue(set.name, out var c) ? c : new HeroAttack();

            set.attackDown = set.attackUp = set.attackLeft = set.attackRight = new Sprite[0];
            var poseFile = FindHeroFile(folder, "*_attack.png");
            if (cfg.usePose && poseFile != null)
            {
                var row = SliceSheet(TransparentVersion(poseFile), PlayerHeight, false, 20).FirstOrDefault() ?? new Sprite[0];
                for (int i = 0; i < AttackPoseOrder.Length && i < row.Length; i++)
                {
                    var pose = new[] { row[i] };
                    switch (AttackPoseOrder[i])
                    {
                        case FacingDir.Left: set.attackLeft = pose; break;
                        case FacingDir.Right: set.attackRight = pose; break;
                        case FacingDir.Up: set.attackUp = pose; break;
                        case FacingDir.Down: set.attackDown = pose; break;
                    }
                }
            }

            var projectileFile = FindHeroFile(folder, "*_attack_projectile.png");
            if (projectileFile != null)
            {
                set.attackProjectile = SaveProjectileSheet($"Attack_{set.name}", TransparentVersion(projectileFile), cfg.height, 10, p =>
                {
                    p.speed = cfg.speed;
                    p.lifetime = cfg.lifetime;
                    p.frameRate = cfg.fps;
                    p.loopFrames = false;
                    p.alignToDirection = cfg.pointsRight;
                });
            }
        }

        // Lying-down death pose: one image named "*_die.png" in the hero's folder.
        const float DeathPoseWidth = 1.35f;

        static Sprite[] FindDeathSprite(string folder)
        {
            var file = Directory.GetFiles(folder, "*_die.png").FirstOrDefault();
            if (file == null) return new Sprite[0];
            var sprite = ImportCroppedSprite(TransparentVersion(file.Replace('\\', '/')), DeathPoseWidth);
            return sprite != null ? new[] { sprite } : new Sprite[0];
        }

        // Imports a single image as one sprite sized so its visible part is worldWidth units wide,
        // with the pivot at the bottom-center of the visible part (so it lies where the feet were).
        static Sprite ImportCroppedSprite(string path, float worldWidth)
        {
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(path));
            int w = tex.width, h = tex.height;
            var px = tex.GetPixels32();
            Object.DestroyImmediate(tex);
            int xMin = w, xMax = -1, yMin = h, yMax = -1; // y from the bottom, as Unity stores it
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (px[y * w + x].a > 20)
                    {
                        if (x < xMin) xMin = x; if (x > xMax) xMax = x;
                        if (y < yMin) yMin = y; if (y > yMax) yMax = y;
                    }
            if (xMax < 0) return null;

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = (xMax - xMin + 1) / worldWidth;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            importer.SetTextureSettings(settings);
            importer.spritePivot = new Vector2((xMin + xMax + 1) / 2f / w, (float)yMin / h);
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // Uses the sheet itself once it has a transparent background; while it is still on a white
        // background, falls back to the cleaned "<name>_nobg.png" copy next to it.
        static string TransparentVersion(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            bool hasAlpha = importer != null && importer.DoesSourceTextureHaveAlpha();
            string cleaned = path.Replace(".png", "_nobg.png");
            return !hasAlpha && File.Exists(cleaned) ? cleaned : path;
        }

        // Pixel hero sheets come in two files with 2 rows each:
        //   side:       row 1 = facing right, row 2 = facing left
        //   front/back: row 1 = facing down (front), row 2 = facing up (back)
        // In every row frame 1 is the stand pose for that direction and the rest is the walk cycle.
        static CharacterAnimSet MakePixelSet(string name, string sidePath, string frontBackPath, float worldHeight,
            FacingDir sideRow1 = FacingDir.Right, FacingDir frontBackRow1 = FacingDir.Down)
        {
            var set = LoadOrCreate<CharacterAnimSet>($"{GameRoot}/AnimSets/{name}.asset");
            set.displayName = name;
            set.walkFps = 8f;
            set.standFps = 6f;

            var side = SliceSheet(sidePath, worldHeight, false, 20);
            var frontBack = SliceSheet(frontBackPath, worldHeight, false, 20);
            var sideRow2 = sideRow1 == FacingDir.Right ? FacingDir.Left : FacingDir.Right;
            var frontBackRow2 = frontBackRow1 == FacingDir.Down ? FacingDir.Up : FacingDir.Down;
            AssignStandAndWalk(set, sideRow1, side, 0);
            AssignStandAndWalk(set, sideRow2, side, 1);
            AssignStandAndWalk(set, frontBackRow1, frontBack, 0);
            AssignStandAndWalk(set, frontBackRow2, frontBack, 1);
            EditorUtility.SetDirty(set);
            return set;
        }

        static void AssignStandAndWalk(CharacterAnimSet set, FacingDir dir, List<Sprite[]> rows, int row)
        {
            var frames = row < rows.Count ? rows[row] : new Sprite[0];
            var stand = frames.Take(1).ToArray();
            var walk = frames.Skip(1).ToArray();
            switch (dir)
            {
                case FacingDir.Down: set.standDown = stand; set.walkDown = walk; break;
                case FacingDir.Up: set.standUp = stand; set.walkUp = walk; break;
                case FacingDir.Left: set.standLeft = stand; set.walkLeft = walk; break;
                case FacingDir.Right: set.standRight = stand; set.walkRight = walk; break;
            }
        }

        // Re-slices the 5 hero sheets at PlayerHeight and refreshes the Player prefab; scenes are untouched.
        [MenuItem("Tools/Magic Game/Rebuild Players Only")]
        public static void RebuildPlayers()
        {
            CharacterAnimSet first = null;
            foreach (var hero in PlayerSheets)
            {
                var set = MakeHeroSet(hero);
                if (first == null) first = set;
            }
            AssetDatabase.SaveAssets();

            string path = $"{GameRoot}/Prefabs/Player.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            var size = FitColliders(root, first.PreviewSprite);
            root.GetComponent<PlayerController>().castHeight = size.y * 0.5f;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log($"[MagicGame] Players rebuilt at height {PlayerHeight}");
        }

        static Vector2 FitColliders(GameObject root, Sprite sprite)
        {
            root.GetComponent<SpriteRenderer>().sprite = sprite;
            var size = (Vector2)sprite.bounds.size;
            var feet = root.GetComponent<CircleCollider2D>();
            feet.radius = Mathf.Min(size.x * 0.22f, 0.6f);
            feet.offset = new Vector2(0f, feet.radius);
            var box = root.transform.Find("Hurtbox").GetComponent<BoxCollider2D>();
            box.size = new Vector2(size.x * 0.55f, size.y * 0.8f);
            box.offset = new Vector2(0f, size.y * 0.45f);
            return size;
        }

        // Goblin: one sheet, rows Down, Up, Left, Right, walk frames only (stops on the last walk frame).
        const string GoblinSheet = ArtRoot + "/Characters/Monster/goblin/goblin(new).png";
        const float GoblinHeight = 1.1f;

        static CharacterAnimSet MakeGoblinSet() => MakeSet("Goblin", TransparentVersion(GoblinSheet), null, GoblinHeight, 8f);

        // Golem: side sheet (row 1 = facing left, row 2 = facing right) and front/back sheet
        // (row 1 = front, row 2 = back); frame 1 of each row is the stand pose.
        const string GolemFolder = ArtRoot + "/Characters/Monster/golem";

        static CharacterAnimSet MakeGolemSet()
        {
            var set = MakePixelSet("Golem", TransparentVersion($"{GolemFolder}/golem_leftright.png"),
                TransparentVersion($"{GolemFolder}/golem_topdown.png"), 2.6f, FacingDir.Left, FacingDir.Down);
            set.walkFps = 6f;
            EditorUtility.SetDirty(set);
            return set;
        }

        // Re-slices the goblin and golem art and refits their prefabs; scenes keep their monsters.
        [MenuItem("Tools/Magic Game/Rebuild Goblin + Golem")]
        public static void RebuildGoblinAndGolem()
        {
            RebuildGoblin();
            var golem = MakeGolemSet();
            AssetDatabase.SaveAssets();
            RefitEnemyPrefab("Golem", golem);
            Debug.Log("[MagicGame] Golem rebuilt from golem_leftright / golem_topdown");
        }

        static void RefitEnemyPrefab(string prefabName, CharacterAnimSet set)
        {
            string path = $"{GameRoot}/Prefabs/{prefabName}.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            root.GetComponent<DirectionalSpriteAnimator>().animSet = set;
            var size = FitColliders(root, set.PreviewSprite);
            root.GetComponent<EnemyController>().castHeight = size.y * 0.5f;
            var bar = root.GetComponent<WorldHealthBar>();
            if (bar != null) bar.heightOffset = size.y + 0.08f;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // Re-slices only the goblin sheet and refreshes its prefab; scenes keep their goblins.
        [MenuItem("Tools/Magic Game/Rebuild Goblin Only")]
        public static void RebuildGoblin()
        {
            var set = MakeGoblinSet();
            AssetDatabase.SaveAssets();
            RefitEnemyPrefab("Goblin", set);
            Debug.Log("[MagicGame] Goblin rebuilt from goblin(new).png");
        }

        static CharacterAnimSet MakeSet(string name, string walkPath, string standPath, float worldHeight, float walkFps = 10f,
            FacingDir[] walkRows = null)
        {
            var set = LoadOrCreate<CharacterAnimSet>($"{GameRoot}/AnimSets/{name}.asset");
            set.displayName = name;
            set.walkFps = walkFps;
            set.standFps = 6f;

            var walk = walkPath != null ? SliceSheet(walkPath, worldHeight) : null;
            var stand = standPath != null ? SliceSheet(standPath, worldHeight) : null;
            set.walkDown = Row(walk, FacingDir.Down, walkRows);
            set.walkUp = Row(walk, FacingDir.Up, walkRows);
            set.walkLeft = Row(walk, FacingDir.Left, walkRows);
            set.walkRight = Row(walk, FacingDir.Right, walkRows);
            set.standDown = Row(stand, FacingDir.Down);
            set.standUp = Row(stand, FacingDir.Up);
            set.standLeft = Row(stand, FacingDir.Left);
            set.standRight = Row(stand, FacingDir.Right);
            EditorUtility.SetDirty(set);
            return set;
        }

        // A sheet with a single row (the dragon) uses that row for every direction.
        // rowOrder lists which facing each sheet row holds, top to bottom (null = Down, Up, Left, Right).
        static Sprite[] Row(List<Sprite[]> rows, FacingDir dir, FacingDir[] rowOrder = null)
        {
            if (rows == null || rows.Count == 0) return new Sprite[0];
            if (rows.Count < 4) return rows[0];
            int index = rowOrder != null ? System.Array.IndexOf(rowOrder, dir) : (int)dir;
            return rows[index];
        }

        // Finds frames from the alpha channel: rows are horizontal bands of opaque pixels,
        // frames are column runs inside a band. Every frame spans the full band height so
        // the bottom-center pivot keeps the feet steady while animating.
        static List<Sprite[]> SliceSheet(string path, float worldHeight, bool centerPivot = false, int frameGap = 8)
        {
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(path));
            int w = tex.width, h = tex.height;
            var px = tex.GetPixels32();
            Object.DestroyImmediate(tex);
            bool Opaque(int x, int yTop) => px[(h - 1 - yTop) * w + x].a > 20;

            var rowMask = new bool[h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (Opaque(x, y)) { rowMask[y] = true; break; }
            var bands = Merge(Runs(rowMask), 2).Where(b => b.y - b.x > 20).ToList();

            string baseName = Path.GetFileNameWithoutExtension(path);
            var rects = new List<SpriteRect>();
            var layout = new List<List<string>>();
            int tallest = 0;
            for (int r = 0; r < bands.Count; r++)
            {
                var band = bands[r];
                tallest = Mathf.Max(tallest, band.y - band.x);
                var colMask = new bool[w];
                for (int x = 0; x < w; x++)
                    for (int y = band.x; y < band.y; y++)
                        if (Opaque(x, y)) { colMask[x] = true; break; }
                var cols = Merge(Runs(colMask), frameGap).Where(c => c.y - c.x >= 20).ToList();

                var names = new List<string>();
                for (int c = 0; c < cols.Count; c++)
                {
                    int x0 = Mathf.Max(0, cols[c].x - 2), x1 = Mathf.Min(w, cols[c].y + 2);
                    int yBottom = Mathf.Max(0, h - band.y - 2), yTop = Mathf.Min(h, h - band.x + 2);
                    string name = $"{baseName}_r{r}_c{c}";
                    names.Add(name);
                    rects.Add(new SpriteRect
                    {
                        name = name,
                        rect = new Rect(x0, yBottom, x1 - x0, yTop - yBottom),
                        alignment = centerPivot ? SpriteAlignment.Center : SpriteAlignment.BottomCenter,
                        pivot = centerPivot ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 0f),
                        spriteID = GUID.Generate(),
                    });
                }
                layout.Add(names);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            provider.SetSpriteRects(rects.ToArray());
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>()
                ?.SetNameFileIdPairs(rects.Select(s => new SpriteNameFileIdPair(s.name, s.spriteID)));
            provider.Apply();

            importer.spritePixelsPerUnit = tallest / worldHeight;
            importer.maxTextureSize = 4096;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            var byName = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);
            return layout.Select(row => row.Where(byName.ContainsKey).Select(n => byName[n]).ToArray()).ToList();
        }

        static List<Vector2Int> Runs(bool[] mask)
        {
            var runs = new List<Vector2Int>();
            int start = -1;
            for (int i = 0; i < mask.Length; i++)
            {
                if (mask[i] && start < 0) start = i;
                if (!mask[i] && start >= 0) { runs.Add(new Vector2Int(start, i)); start = -1; }
            }
            if (start >= 0) runs.Add(new Vector2Int(start, mask.Length));
            return runs;
        }

        static List<Vector2Int> Merge(List<Vector2Int> runs, int maxGap)
        {
            var merged = new List<Vector2Int>();
            foreach (var r in runs)
            {
                if (merged.Count > 0 && r.x - merged[^1].y <= maxGap)
                    merged[^1] = new Vector2Int(merged[^1].x, r.y);
                else
                    merged.Add(r);
            }
            return merged;
        }

        static void PrepareMaps()
        {
            foreach (var level in Levels)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(MapPath(level.map));
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.spritePivot = new Vector2(0.5f, 0.5f);
                importer.maxTextureSize = 2048;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
        }

        static string MapPath(string map) => $"{ArtRoot}/Maps/{map}.png";

        // ---------------------------------------------------------------- generated shapes

        static Shapes CreateShapes()
        {
            return new Shapes
            {
                ring = MakeShape("ring", 128, (d, _, _) =>
                {
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.78f) / 0.17f);
                    return new Color(1f, 1f, 1f, Mathf.Max(a, d < 0.62f ? 0.15f : 0f));
                }),
                circle = MakeShape("circle", 64, (d, _, _) => new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) / 0.2f))),
                square = MakeShape("square", 32, (_, u, v) =>
                {
                    bool edge = Mathf.Abs(u) > 0.8f || Mathf.Abs(v) > 0.8f;
                    float shade = edge ? 0.55f : 1f;
                    return new Color(shade, shade, shade, 1f);
                }),
            };
        }

        // color(distanceFromCenter, u, v) with u,v in -1..1
        static Sprite MakeShape(string name, int size, System.Func<float, float, float, Color> color)
        {
            string path = $"{GameRoot}/Sprites/{name}.png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    tex.SetPixel(x, y, color(Mathf.Sqrt(u * u + v * v), u, v));
                }
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = size;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ---------------------------------------------------------------- attack effects

        const string AttackRoot = ArtRoot + "/Characters/animation_attack";

        // Builds an animated projectile prefab per attack sheet, gives each hero its own attack
        // and swaps the golem rock / dragon fireball placeholders. Scenes are untouched.
        [MenuItem("Tools/Magic Game/Rebuild Attacks Only")]
        public static void ApplyAttacks()
        {
            var ice = SaveAttack("Attack_LingIce", "character/ling_ice_circle_sheet.png", 0.9f, p =>
                { p.speed = 9f; p.lifetime = 1.1f; p.growTo = 1.3f; p.frameRate = 14f; });
            var witch = SaveAttack("Attack_NaeWitchCircle", "character/nae_witch_circle_sheet.png", 0.9f, p =>
                { p.speed = 9f; p.lifetime = 1.1f; p.growTo = 1.3f; p.frameRate = 14f; });
            // Crystal art points up, so rotate -90 to lead with the tip.
            var crystal = SaveAttack("Attack_NewCrystal", "character/new_crystals_sheet.png", 0.6f, p =>
                { p.speed = 10f; p.lifetime = 1f; p.frameRate = 10f; p.alignToDirection = true; p.alignAngleOffset = -90f; });
            // Single frame: flies straight, pointing where it goes.
            var kunai = SaveAttack("Attack_NuengKunai", "character/nueng_kunai.png", 0.22f, p =>
                { p.speed = 12f; p.lifetime = 0.9f; p.alignToDirection = true; });
            var card = SaveAttack("Attack_PengCard", "character/peg_card_spin_sheet.png", 0.7f, p =>
                { p.speed = 9f; p.lifetime = 1.6f; p.frameRate = 16f; });
            var fireball = SaveAttack("Attack_DragonFireball", "monster/dragon_fireball_sheet.png", 0.8f, p =>
                { p.speed = 6f; p.lifetime = 4f; p.frameRate = 12f; });
            var boulder = SaveAttack("Attack_GolemBoulder", "monster/golem_boulder_roll_sheet.png", GolemBoulderSize, p =>
                { p.speed = 5f; p.lifetime = 4f; p.frameRate = 12f; });

            // Heroes with their own "*_attack_projectile.png" get it from MakeHeroSet instead.
            if (!HasHeroProjectile("Ling")) SetAttack("Ling", ice);
            if (!HasHeroProjectile("Nae")) SetAttack("Nae", witch);
            if (!HasHeroProjectile("New")) SetAttack("New", crystal);
            if (!HasHeroProjectile("Nueng")) SetAttack("Nueng", kunai);
            if (!HasHeroProjectile("Peng")) SetAttack("Peng", card);
            SetEnemyProjectile("Golem", boulder, GolemAttack);
            SetEnemyProjectile("Dragon", fireball);
            CharacterSkillSetup.UpdateConfigs();
            AssetDatabase.SaveAssets();
            Debug.Log("[MagicGame] Attack effects applied");
        }

        static Projectile SaveAttack(string name, string sheet, float worldHeight, System.Action<Projectile> configure) =>
            SaveProjectileSheet(name, $"{AttackRoot}/{sheet}", worldHeight, 2, configure);

        static Projectile SaveProjectileSheet(string name, string path, float worldHeight, int frameGap,
            System.Action<Projectile> configure)
        {
            var frames = SliceSheet(path, worldHeight, true, frameGap).SelectMany(r => r).ToArray();
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = frames[0];
            sr.sortingOrder = 20000;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var size = (Vector2)frames[frames.Length - 1].bounds.size;
            if (Mathf.Max(size.x, size.y) > 2f * Mathf.Min(size.x, size.y))
            {
                var box = go.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = size * 0.85f;
            }
            else
            {
                var circle = go.AddComponent<CircleCollider2D>();
                circle.isTrigger = true;
                circle.radius = Mathf.Min(size.x, size.y) * 0.42f;
            }

            var projectile = go.AddComponent<Projectile>();
            projectile.frames = frames;
            configure(projectile);
            return SavePrefab(go, name).GetComponent<Projectile>();
        }

        static void SetAttack(string setName, Projectile projectile)
        {
            var set = AssetDatabase.LoadAssetAtPath<CharacterAnimSet>($"{GameRoot}/AnimSets/{setName}.asset");
            set.attackProjectile = projectile;
            EditorUtility.SetDirty(set);
        }

        // Golem throws one big boulder per attack: slow, heavy hit, no spread or ring burst.
        const float GolemBoulderSize = 1.8f;

        static void GolemAttack(EnemyController e)
        {
            e.damage = 20f;
            e.projectileSpeed = 4f;
            e.projectilesPerShot = 1;
            e.spreadAngle = 0f;
            e.burstEvery = 0;
        }

        static void SetEnemyProjectile(string prefabName, Projectile projectile, System.Action<EnemyController> configure = null)
        {
            string path = $"{GameRoot}/Prefabs/{prefabName}.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            var enemy = root.GetComponent<EnemyController>();
            enemy.projectilePrefab = projectile;
            configure?.Invoke(enemy);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // ---------------------------------------------------------------- prefabs

        static Prefabs CreatePrefabs(Dictionary<string, CharacterAnimSet> sets, Shapes shapes)
        {
            var ring = SaveProjectile("MagicRing", shapes.ring, new Color(0.55f, 0.85f, 1f), Vector3.one * 0.8f, 0.45f, true, go =>
            {
                AddChildSprite(go, "Core", shapes.circle, new Color(0.7f, 0.4f, 1f, 0.35f), Vector3.one * 0.7f, 20001);
                var p = go.GetComponent<Projectile>();
                p.speed = 9f; p.lifetime = 1.1f; p.spinSpeed = 540f; p.growTo = 1.4f;
            });
            var arrow = SaveProjectile("Arrow", shapes.square, new Color(0.75f, 0.6f, 0.4f), new Vector3(0.7f, 0.08f, 1f), 0f, false, go =>
            {
                var p = go.GetComponent<Projectile>();
                p.speed = 8f; p.lifetime = 3f; p.alignToDirection = true;
            });
            var rock = SaveProjectile("Rock", shapes.square, new Color(0.6f, 0.55f, 0.48f), Vector3.one * 0.6f, 0f, false, go =>
            {
                var p = go.GetComponent<Projectile>();
                p.speed = 5f; p.lifetime = 4f; p.spinSpeed = 240f;
            });
            var fireball = SaveProjectile("Fireball", shapes.circle, new Color(1f, 0.4f, 0.08f), Vector3.one * 0.6f, 0.45f, true, go =>
            {
                AddChildSprite(go, "Core", shapes.circle, new Color(1f, 0.9f, 0.3f), Vector3.one * 0.55f, 20001);
                var p = go.GetComponent<Projectile>();
                p.speed = 6f; p.lifetime = 4f;
            });

            var prefabs = new Prefabs();

            var players = PlayerSets(sets);
            var player = BuildCharacter("Player", players[0], Team.Player, 100f, 1f);
            player.GetComponent<Health>().invulnerableTime = 0.4f;
            player.GetComponent<Health>().destroyOnDeath = false;
            var pc = player.AddComponent<PlayerController>();
            pc.characters = players;
            pc.magicRingPrefab = ring;
            pc.castHeight = CastHeight(player);
            prefabs.player = SavePrefab(player, "Player");

            prefabs.goblin = SaveEnemy(sets["Goblin"], 80f, 1f, e =>
            {
                e.displayName = "Goblin"; e.style = AttackStyle.Melee;
                e.moveSpeed = 2.2f; e.attackRange = 1.0f;
            });
            prefabs.skeleton = SaveEnemy(sets["Skeleton"], 60f, 1f, e =>
            {
                e.displayName = "Skeleton Archer"; e.style = AttackStyle.Ranged; e.projectilePrefab = arrow;
                e.moveSpeed = 1.8f; e.attackRange = 6f; e.preferredDistance = 3.5f;
            });
            prefabs.golem = SaveEnemy(sets["Golem"], 1600f, 50f, e =>
            {
                e.displayName = "Forest Golem"; e.isBoss = true; e.style = AttackStyle.Ranged; e.projectilePrefab = rock;
                e.moveSpeed = 0.8f; e.attackRange = 9f; e.chaseDistance = 1.8f;
                GolemAttack(e);
            });
            prefabs.dragon = SaveEnemy(sets["Dragon"], 2800f, 50f, e =>
            {
                e.displayName = "Inferno Dragon"; e.isBoss = true; e.style = AttackStyle.Ranged; e.projectilePrefab = fireball;
                e.moveSpeed = 1.5f; e.attackRange = 12f; e.chaseDistance = 5f; e.preferredDistance = 4f;
                e.projectilesPerShot = 5; e.spreadAngle = 50f; e.burstEvery = 3; e.burstCount = 16;
            });

            prefabs.witch = SaveWitch(sets["Witch"]);
            prefabs.portal = BuildPortal(shapes);
            return prefabs;
        }

        static CharacterAnimSet[] PlayerSets(Dictionary<string, CharacterAnimSet> sets) =>
            new[] { sets["Ling"], sets["Nae"], sets["New"], sets["Nueng"], sets["Peng"] };

        static GameObject BuildCharacter(string name, CharacterAnimSet set, Team team, float hp, float mass)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = set.PreviewSprite;
            go.AddComponent<DirectionalSpriteAnimator>().animSet = set;
            go.AddComponent<YSort>();

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var size = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
            var feet = go.AddComponent<CircleCollider2D>();
            feet.radius = Mathf.Min(size.x * 0.22f, 0.6f);
            feet.offset = new Vector2(0f, feet.radius);

            var hurt = new GameObject("Hurtbox");
            hurt.transform.SetParent(go.transform, false);
            var box = hurt.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(size.x * 0.55f, size.y * 0.8f);
            box.offset = new Vector2(0f, size.y * 0.45f);

            var health = go.AddComponent<Health>();
            health.team = team;
            health.maxHp = hp;
            return go;
        }

        static float CastHeight(GameObject go)
        {
            var sprite = go.GetComponent<SpriteRenderer>().sprite;
            return sprite != null ? sprite.bounds.size.y * 0.5f : 0.6f;
        }

        static GameObject SaveEnemy(CharacterAnimSet set, float hp, float mass, System.Action<EnemyController> configure)
        {
            var go = BuildCharacter(set.displayName, set, Team.Enemy, hp, mass);
            var enemy = go.AddComponent<EnemyController>();
            enemy.castHeight = CastHeight(go);
            configure(enemy);
            ApplyEnemyBalance(enemy, set.displayName);
            if (!enemy.isBoss)
            {
                var bar = go.AddComponent<WorldHealthBar>();
                bar.heightOffset = go.GetComponent<SpriteRenderer>().sprite.bounds.size.y + 0.1f;
                bar.width = 0.8f;
            }
            return SavePrefab(go, set.displayName);
        }

        // Shared by generation and the explicit one-time update of existing prefabs.
        public static void ApplyEnemyBalance(EnemyController enemy, string prefabName)
        {
            (float hp, float defense, float damage, float interval, float windUp, float speed) stats;
            switch (prefabName)
            {
                case "Goblin": stats = (80f, 0f, 8f, 1.2f, 0.4f, 0f); break;
                case "Skeleton": stats = (60f, 5f, 8f, 2f, 0.5f, 7f); break;
                case "Witch": stats = (1200f, 10f, 12f, 2.2f, 0.65f, 2.5f); break;
                case "Golem": stats = (1600f, 35f, 20f, 2.6f, 0.8f, 4f); break;
                case "Dragon": stats = (2800f, 20f, 18f, 3f, 0.65f, 3f); break;
                default: throw new System.ArgumentException($"Unknown enemy prefab: {prefabName}", nameof(prefabName));
            }
            var health = enemy.GetComponent<Health>();
            health.maxHp = stats.hp;
            health.physicalDefense = health.magicDefense = stats.defense;
            enemy.damage = stats.damage;
            enemy.attackCooldown = stats.interval;
            enemy.windUp = stats.windUp;
            if (stats.speed > 0f) enemy.projectileSpeed = stats.speed;
        }

        static Projectile SaveProjectile(string name, Sprite sprite, Color color, Vector3 scale, float circleRadius,
            bool circle, System.Action<GameObject> configure)
        {
            var go = new GameObject(name);
            go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = 20000;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = false;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            if (circle)
            {
                var c = go.AddComponent<CircleCollider2D>();
                c.isTrigger = true;
                c.radius = circleRadius;
            }
            else
            {
                var b = go.AddComponent<BoxCollider2D>();
                b.isTrigger = true;
                b.size = Vector2.one;
            }

            go.AddComponent<Projectile>();
            configure(go);
            return SavePrefab(go, name).GetComponent<Projectile>();
        }

        static void AddChildSprite(GameObject parent, string name, Sprite sprite, Color color, Vector3 scale, int order)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            child.transform.localScale = scale;
            var sr = child.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
        }

        static GameObject BuildPortal(Shapes shapes)
        {
            var go = new GameObject("Portal");
            var trigger = go.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 0.5f;
            trigger.offset = new Vector2(0f, 0.2f);

            AddChildSprite(go, "Glow", shapes.circle, new Color(0.45f, 0.85f, 1f, 0.45f), Vector3.one * 1.5f, -10000);
            AddChildSprite(go, "Ring", shapes.ring, Color.white, Vector3.one * 1.5f, -9999);

            var portal = go.AddComponent<Portal>();
            portal.glowRenderer = go.transform.Find("Glow").GetComponent<SpriteRenderer>();
            portal.ringRenderer = go.transform.Find("Ring").GetComponent<SpriteRenderer>();
            return SavePrefab(go, "Portal");
        }

        static GameObject SavePrefab(GameObject go, string name)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{GameRoot}/Prefabs/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ---------------------------------------------------------------- scenes

        static void CreateScenes(Dictionary<string, CharacterAnimSet> sets, Prefabs prefabs)
        {
            var scenes = new List<EditorBuildSettingsScene>();

            string menuPath = $"{SceneRoot}/MainMenu.unity";
            var menuScene = NewSceneFromTemplate(menuPath);
            AddBackground(MapPath("first"), new Color(0.35f, 0.35f, 0.45f));
            new GameObject("MainMenu").AddComponent<MainMenu>().characters = PlayerSets(sets);
            EditorSceneManager.SaveScene(menuScene);
            scenes.Add(new EditorBuildSettingsScene(menuPath, true));

            foreach (var level in Levels)
            {
                string path = $"{SceneRoot}/Level{level.number}.unity";
                var scene = NewSceneFromTemplate(path);
                AddBackground(MapPath(level.map), Color.white);

                var area = ToWorld(level.walkable);
                BuildWalls(area);

                var player = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.player);
                player.transform.position = new Vector3(area.center.x, area.yMin + 0.6f, 0f);

                var portal = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.portal);
                portal.transform.position = new Vector3((level.portalX - 0.5f) * MapWidth, area.yMax - 0.7f, 0f);

                var enemies = new GameObject("Enemies").transform;
                var rng = new System.Random(level.number * 97);
                if (level.boss == Boss.Golem) Spawn(prefabs.golem, enemies, new Vector2(area.center.x, area.center.y + area.height * 0.15f));
                if (level.boss == Boss.Witch) Spawn(prefabs.witch, enemies, area.center);
                if (level.boss == Boss.Dragon) Spawn(prefabs.dragon, enemies, new Vector2(area.center.x, area.center.y + area.height * 0.1f));
                for (int i = 0; i < level.goblins; i++) Spawn(prefabs.goblin, enemies, RandomPoint(rng, area));
                for (int i = 0; i < level.skeletons; i++) Spawn(prefabs.skeleton, enemies, RandomPoint(rng, area));

                var manager = new GameObject("LevelManager").AddComponent<LevelManager>();
                manager.levelNumber = level.number;
                manager.playableBounds = area;
                manager.levelTitle = level.title;
                manager.isFinalLevel = level.number == Levels.Length;
                manager.portal = portal.GetComponent<Portal>();

                EditorSceneManager.SaveScene(scene);
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            MobileUISetup.Setup();
            EditorSceneManager.OpenScene(menuPath);
        }

        static UnityEngine.SceneManagement.Scene NewSceneFromTemplate(string path)
        {
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CopyAsset(TemplateScene, path);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            var cam = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.gameObject.AddComponent<CameraFit>().mapSize = new Vector2(MapWidth, MapHeight);
            return scene;
        }

        static void AddBackground(string mapPath, Color tint)
        {
            var go = new GameObject("Map");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(mapPath);
            sr.color = tint;
            sr.sortingOrder = -32000;
        }

        static Rect ToWorld(Rect n) => Rect.MinMaxRect(
            (n.xMin - 0.5f) * MapWidth, (n.yMin - 0.5f) * MapHeight,
            (n.xMax - 0.5f) * MapWidth, (n.yMax - 0.5f) * MapHeight);

        static void BuildWalls(Rect area)
        {
            var root = new GameObject("Walls");
            const float t = 2f;
            AddWall(root, "Top", new Vector2(area.center.x, area.yMax + t / 2f), new Vector2(area.width + 2 * t, t));
            AddWall(root, "Bottom", new Vector2(area.center.x, area.yMin - t / 2f), new Vector2(area.width + 2 * t, t));
            AddWall(root, "Left", new Vector2(area.xMin - t / 2f, area.center.y), new Vector2(t, area.height + 2 * t));
            AddWall(root, "Right", new Vector2(area.xMax + t / 2f, area.center.y), new Vector2(t, area.height + 2 * t));
        }

        static void AddWall(GameObject root, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.position = position;
            go.AddComponent<BoxCollider2D>().size = size;
            go.AddComponent<Wall>();
        }

        static void Spawn(GameObject prefab, Transform parent, Vector2 position)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = position;
        }

        // Enemies start in the upper part of the arena, away from the player spawn.
        static Vector2 RandomPoint(System.Random rng, Rect area)
        {
            float x = Mathf.Lerp(area.xMin + 0.8f, area.xMax - 0.8f, (float)rng.NextDouble());
            float y = Mathf.Lerp(area.yMin + area.height * 0.45f, area.yMax - 1.2f, (float)rng.NextDouble());
            return new Vector2(x, y);
        }

        // ---------------------------------------------------------------- new stage maps (levels 1-6)

        // Each stage_N.png replaces the art of level N. Its walkable ground is an outline traced on the
        // image (normalized 0..1, origin bottom-left); the player and enemies are kept inside it by an
        // edge collider. Maps are scaled to the usual 10.24-unit height, so wider images scroll sideways.
        class StageMap
        {
            public int level;
            public string file;
            public Vector2 spawn, portal;
            public Vector2[] walk;
        }

        static Vector2 V(float x, float y) => new Vector2(x, y);

        // The new stage art is drawn at a large scale, so the maps are blown up relative to the
        // characters (characters keep their size) and the camera is pulled in closer.
        const float StageScale = 1.6f;
        const float StageCameraZoom = 2f;

        static readonly StageMap[] StageMaps =
        {
            new() { level = 1, file = "stage_1", spawn = V(0.450f, 0.330f), portal = V(0.740f, 0.690f),
                    walk = new[] { V(0.100f, 0.300f), V(0.100f, 0.550f), V(0.200f, 0.620f), V(0.320f, 0.720f), V(0.450f, 0.860f), V(0.580f, 0.860f), V(0.660f, 0.740f), V(0.800f, 0.660f), V(0.920f, 0.620f), V(0.930f, 0.400f), V(0.850f, 0.300f), V(0.700f, 0.260f), V(0.400f, 0.260f), V(0.200f, 0.280f) } },
            new() { level = 2, file = "stage_2", spawn = V(0.350f, 0.220f), portal = V(0.140f, 0.680f),
                    walk = new[] { V(0.060f, 0.450f), V(0.060f, 0.700f), V(0.200f, 0.750f), V(0.300f, 0.720f), V(0.500f, 0.700f), V(0.550f, 0.600f), V(0.580f, 0.450f), V(0.620f, 0.350f), V(0.600f, 0.180f), V(0.450f, 0.130f), V(0.250f, 0.150f), V(0.100f, 0.250f) } },
            new() { level = 3, file = "stage_4", spawn = V(0.350f, 0.220f), portal = V(0.250f, 0.760f),
                    walk = new[] { V(0.050f, 0.500f), V(0.050f, 0.750f), V(0.120f, 0.800f), V(0.300f, 0.800f), V(0.400f, 0.780f), V(0.500f, 0.680f), V(0.550f, 0.580f), V(0.580f, 0.450f), V(0.650f, 0.380f), V(0.620f, 0.220f), V(0.500f, 0.150f), V(0.350f, 0.150f), V(0.200f, 0.180f), V(0.100f, 0.280f) } },
            new() { level = 4, file = "stage_3", spawn = V(0.070f, 0.535f), portal = V(0.525f, 0.860f),
                    walk = new[] { V(0.301f, 0.450f), V(0.311f, 0.397f), V(0.325f, 0.347f), V(0.344f, 0.301f), V(0.366f, 0.260f), V(0.392f, 0.225f), V(0.421f, 0.196f), V(0.452f, 0.175f), V(0.484f, 0.161f), V(0.517f, 0.155f), V(0.551f, 0.157f), V(0.584f, 0.168f), V(0.615f, 0.186f), V(0.645f, 0.211f), V(0.672f, 0.243f), V(0.697f, 0.282f), V(0.717f, 0.326f), V(0.733f, 0.374f), V(0.745f, 0.426f), V(0.753f, 0.480f), V(0.755f, 0.535f), V(0.753f, 0.590f), V(0.745f, 0.644f), V(0.733f, 0.696f), V(0.717f, 0.744f), V(0.697f, 0.788f), V(0.672f, 0.827f), V(0.645f, 0.859f), V(0.615f, 0.884f), V(0.584f, 0.902f), V(0.551f, 0.913f), V(0.517f, 0.915f), V(0.484f, 0.909f), V(0.452f, 0.895f), V(0.421f, 0.874f), V(0.392f, 0.845f), V(0.366f, 0.810f), V(0.344f, 0.769f), V(0.325f, 0.723f), V(0.311f, 0.673f), V(0.301f, 0.620f), V(0.020f, 0.620f), V(0.020f, 0.450f) } },
            new() { level = 5, file = "stage_5", spawn = V(0.070f, 0.500f), portal = V(0.545f, 0.780f),
                    walk = new[] { V(0.285f, 0.440f), V(0.299f, 0.400f), V(0.318f, 0.363f), V(0.342f, 0.329f), V(0.370f, 0.299f), V(0.403f, 0.274f), V(0.438f, 0.254f), V(0.475f, 0.240f), V(0.514f, 0.232f), V(0.554f, 0.230f), V(0.593f, 0.235f), V(0.632f, 0.245f), V(0.668f, 0.262f), V(0.702f, 0.284f), V(0.732f, 0.311f), V(0.759f, 0.343f), V(0.781f, 0.379f), V(0.798f, 0.417f), V(0.809f, 0.458f), V(0.814f, 0.500f), V(0.814f, 0.543f), V(0.808f, 0.585f), V(0.796f, 0.626f), V(0.779f, 0.664f), V(0.757f, 0.700f), V(0.730f, 0.731f), V(0.699f, 0.758f), V(0.665f, 0.780f), V(0.629f, 0.796f), V(0.590f, 0.806f), V(0.551f, 0.810f), V(0.511f, 0.808f), V(0.472f, 0.799f), V(0.435f, 0.785f), V(0.400f, 0.765f), V(0.368f, 0.739f), V(0.340f, 0.709f), V(0.317f, 0.675f), V(0.298f, 0.637f), V(0.285f, 0.597f), V(0.277f, 0.555f), V(0.020f, 0.555f), V(0.020f, 0.440f) } },
            new() { level = 6, file = "stage_6", spawn = V(0.070f, 0.500f), portal = V(0.620f, 0.720f),
                    walk = new[] { V(0.295f, 0.450f), V(0.304f, 0.404f), V(0.318f, 0.361f), V(0.335f, 0.322f), V(0.356f, 0.287f), V(0.381f, 0.257f), V(0.408f, 0.233f), V(0.437f, 0.215f), V(0.467f, 0.204f), V(0.498f, 0.200f), V(0.530f, 0.203f), V(0.560f, 0.213f), V(0.590f, 0.231f), V(0.617f, 0.254f), V(0.641f, 0.283f), V(0.663f, 0.318f), V(0.681f, 0.357f), V(0.695f, 0.400f), V(0.704f, 0.445f), V(0.709f, 0.493f), V(0.710f, 0.540f), V(0.705f, 0.588f), V(0.696f, 0.633f), V(0.683f, 0.676f), V(0.666f, 0.716f), V(0.645f, 0.752f), V(0.621f, 0.782f), V(0.594f, 0.806f), V(0.565f, 0.824f), V(0.534f, 0.836f), V(0.503f, 0.840f), V(0.472f, 0.837f), V(0.441f, 0.827f), V(0.412f, 0.811f), V(0.385f, 0.787f), V(0.360f, 0.758f), V(0.338f, 0.724f), V(0.320f, 0.685f), V(0.306f, 0.642f), V(0.296f, 0.597f), V(0.291f, 0.550f), V(0.020f, 0.550f), V(0.020f, 0.450f) } },
        };

        [MenuItem("Tools/Magic Game/Setup Stage Maps 1-6")]
        public static void SetupStageMaps()
        {
            foreach (var stage in StageMaps)
            {
                string mapPath = $"{ArtRoot}/Maps/{stage.file}.png";
                if (!File.Exists(mapPath))
                {
                    Debug.LogWarning($"[MagicGame] {mapPath} not found, level {stage.level} keeps its old map");
                    continue;
                }

                var size = ImportStageMap(mapPath);
                Vector2 ToWorld(Vector2 n) => new Vector2((n.x - 0.5f) * size.x, (n.y - 0.5f) * size.y);
                var outline = stage.walk.Select(ToWorld).ToArray();

                var scene = EditorSceneManager.OpenScene($"{SceneRoot}/Level{stage.level}.unity", OpenSceneMode.Single);
                GameObject Root(string name) => scene.GetRootGameObjects().First(g => g.name == name);

                var map = Root("Map").GetComponent<SpriteRenderer>();
                map.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(mapPath);
                map.color = Color.white;
                var cameraFit = Camera.main.GetComponent<CameraFit>();
                cameraFit.mapSize = size;
                cameraFit.zoom = StageCameraZoom;

                Object.DestroyImmediate(Root("Walls"));
                var walls = new GameObject("Walls");
                walls.AddComponent<EdgeCollider2D>().SetPoints(outline.Append(outline[0]).ToList());
                walls.AddComponent<Wall>();

                var spawn = ToWorld(stage.spawn);
                Root("Player").transform.position = spawn;
                Root("Portal").transform.position = ToWorld(stage.portal);

                // Bosses take the middle of the arena when it is open ground; everyone else is scattered.
                var bounds = new Bounds(outline[0], Vector3.zero);
                foreach (var pt in outline) bounds.Encapsulate(pt);
                Vector2 middle = bounds.center;
                bool middleOk = InsidePolygon(middle, outline) && Vector2.Distance(middle, spawn) > 3f;
                var rng = new System.Random(stage.level * 31);
                foreach (var enemy in Root("Enemies").GetComponentsInChildren<EnemyController>())
                {
                    enemy.transform.position = enemy.isBoss && middleOk ? middle : RandomInside(outline, rng, spawn, 4f);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(enemy.transform);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[MagicGame] Level {stage.level} now uses {stage.file} ({size.x:F2} x {size.y:F2} units)");
            }
        }

        // StageScale times the height of the original maps (10.24 units) whatever the image resolution.
        static Vector2 ImportStageMap(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.GetSourceTextureWidthAndHeight(out int pixelW, out int pixelH);
            float worldHeight = MapHeight * StageScale;
            float ppu = pixelH / worldHeight;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            importer.mipmapEnabled = false;
            // Small pixel-art images stretched ~2x on screen: keep the pixels sharp.
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return new Vector2(pixelW / ppu, worldHeight);
        }

        static bool InsidePolygon(Vector2 p, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            }
            return inside;
        }

        // A point inside the outline, at least minDistance from the player spawn and a little away from the edge.
        static Vector2 RandomInside(Vector2[] poly, System.Random rng, Vector2 avoid, float minDistance)
        {
            var bounds = new Bounds(poly[0], Vector3.zero);
            foreach (var pt in poly) bounds.Encapsulate(pt);
            for (int attempt = 0; attempt < 500; attempt++)
            {
                var p = new Vector2(
                    Mathf.Lerp(bounds.min.x, bounds.max.x, (float)rng.NextDouble()),
                    Mathf.Lerp(bounds.min.y, bounds.max.y, (float)rng.NextDouble()));
                if (!InsidePolygon(p, poly) || Vector2.Distance(p, avoid) < minDistance) continue;
                bool nearEdge = false;
                foreach (var offset in new[] { Vector2.left, Vector2.right, Vector2.up, Vector2.down })
                    if (!InsidePolygon(p + offset * 0.6f, poly)) nearEdge = true;
                if (!nearEdge) return p;
            }
            return bounds.center;
        }

        // ---------------------------------------------------------------- frost witch boss (level 3)

        // Sheets: Monster/witch_boss/witch-boss_leftright.png (row 1 = facing left, row 2 = facing right)
        // and witch-boss_topdown.png (row 1 = facing up/back, row 2 = facing down/front); frame 1 of each
        // row is the stand pose. Her ice orb is animation_attack/monster/witch-boss_projectile_attck.png.
        const string WitchFolder = ArtRoot + "/Characters/Monster/witch_boss";
        const string WitchProjectile = AttackRoot + "/monster/witch-boss_projectile_attck.png";

        static CharacterAnimSet MakeWitchSet() =>
            MakePixelSet("Witch", TransparentVersion($"{WitchFolder}/witch-boss_leftright.png"),
                TransparentVersion($"{WitchFolder}/witch-boss_topdown.png"), 2.4f, FacingDir.Left, FacingDir.Up);

        static GameObject SaveWitch(CharacterAnimSet set)
        {
            var orb = SaveProjectileSheet("Attack_WitchIceOrb", TransparentVersion(WitchProjectile), 0.75f, 10, p =>
            {
                p.speed = 3f; p.lifetime = 8f; p.frameRate = 10f; p.loopFrames = false; p.spinSpeed = 90f;
            });
            return SaveEnemy(set, 1200f, 50f, e =>
            {
                e.displayName = "Frost Witch"; e.isBoss = true; e.style = AttackStyle.Ranged; e.projectilePrefab = orb;
                e.moveSpeed = 1.3f; e.attackRange = 10f; e.chaseDistance = 4f; e.preferredDistance = 3f;
                e.projectilesPerShot = 3; e.spreadAngle = 36f; e.burstEvery = 4; e.burstCount = 10;
            });
        }

        // The witch and golem levels get their bosses and minions from the Levels table.
        // Edits the two scenes in place, then re-runs the stage-map placement so everyone stands on the ground.
        [MenuItem("Tools/Magic Game/Setup Boss Levels (Witch + Golem)")]
        public static void SetupWitchAndGolem()
        {
            var witch = SaveWitch(MakeWitchSet());
            var golem = AssetDatabase.LoadAssetAtPath<GameObject>($"{GameRoot}/Prefabs/Golem.prefab");
            AssetDatabase.SaveAssets();

            foreach (var def in Levels.Where(l => l.boss == Boss.Witch || l.boss == Boss.Golem))
                ApplyLevelRoster(def.number, witch, golem);
            SetupStageMaps();
            Debug.Log("[MagicGame] Witch and golem level rosters updated");
        }

        // Replaces a level's enemies with the boss and minion counts from the Levels table.
        static void ApplyLevelRoster(int level, GameObject witch, GameObject golem)
        {
            var def = Levels[level - 1];
            var goblin = AssetDatabase.LoadAssetAtPath<GameObject>($"{GameRoot}/Prefabs/Goblin.prefab");
            var skeleton = AssetDatabase.LoadAssetAtPath<GameObject>($"{GameRoot}/Prefabs/Skeleton.prefab");
            var dragon = AssetDatabase.LoadAssetAtPath<GameObject>($"{GameRoot}/Prefabs/Dragon.prefab");

            var scene = EditorSceneManager.OpenScene($"{SceneRoot}/Level{level}.unity", OpenSceneMode.Single);
            var enemies = scene.GetRootGameObjects().First(g => g.name == "Enemies").transform;
            for (int i = enemies.childCount - 1; i >= 0; i--) Object.DestroyImmediate(enemies.GetChild(i).gameObject);

            var boss = def.boss switch { Boss.Witch => witch, Boss.Golem => golem, Boss.Dragon => dragon, _ => null };
            if (boss != null) Spawn(boss, enemies, Vector2.zero);
            for (int i = 0; i < def.goblins; i++) Spawn(goblin, enemies, Vector2.zero);
            for (int i = 0; i < def.skeletons; i++) Spawn(skeleton, enemies, Vector2.zero);

            Object.FindAnyObjectByType<LevelManager>().levelTitle = def.title;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // ---------------------------------------------------------------- dragon sky stage (level 7)

        // Level 7 uses a wide side-on backdrop: a gray castle floor along the bottom and a red sky
        // above. The player walks top-down but only on the floor; the dragon flies in the sky.
        const string DragonStageMap = ArtRoot + "/Maps/dragon_sky.png";
        static readonly Rect DragonFloor = R(0.025f, 0.975f, 0.055f, 0.245f); // normalized walkable floor (measured on dragon_sky.png)
        const float DragonSkyMin = 0.40f, DragonSkyMax = 0.60f;           // normalized band for the dragon's feet

        [MenuItem("Tools/Magic Game/Setup Dragon Sky Stage")]
        public static void SetupDragonStage()
        {
            if (!File.Exists(DragonStageMap))
            {
                Debug.LogError($"[MagicGame] Dragon stage map not found: {DragonStageMap}");
                return;
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(DragonStageMap);
            importer.GetSourceTextureWidthAndHeight(out int pixelW, out int pixelH);
            float ppu = pixelH / MapHeight; // same world height as the other stages
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            importer.maxTextureSize = 4096;
            importer.mipmapEnabled = false;
            // Low-res pixel art stretched ~2x on screen: keep pixels sharp instead of blurring them.
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            float worldW = pixelW / ppu, worldH = MapHeight;
            Rect ToStage(Rect n) => Rect.MinMaxRect(
                (n.xMin - 0.5f) * worldW, (n.yMin - 0.5f) * worldH,
                (n.xMax - 0.5f) * worldW, (n.yMax - 0.5f) * worldH);
            var floor = ToStage(DragonFloor);
            float skyMinY = (DragonSkyMin - 0.5f) * worldH, skyMaxY = (DragonSkyMax - 0.5f) * worldH;

            var scene = EditorSceneManager.OpenScene($"{SceneRoot}/Level{Levels.Length}.unity", OpenSceneMode.Single);
            GameObject Root(string name) => scene.GetRootGameObjects().First(g => g.name == name);

            var map = Root("Map").GetComponent<SpriteRenderer>();
            map.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(DragonStageMap);
            map.color = Color.white;
            Camera.main.GetComponent<CameraFit>().mapSize = new Vector2(worldW, worldH);

            Object.DestroyImmediate(Root("Walls"));
            BuildWalls(floor);
            // The top edge still stops walking, but shots must fly through it to reach the dragon.
            Object.DestroyImmediate(Root("Walls").transform.Find("Top").GetComponent<Wall>());

            Root("Player").transform.position = new Vector3(floor.xMin + 1.5f, floor.center.y, 0f);
            Root("Portal").transform.position = new Vector3(floor.xMax - 1.2f, floor.center.y, 0f);

            foreach (var enemy in Root("Enemies").GetComponentsInChildren<EnemyController>())
            {
                if (!enemy.isBoss) continue;
                enemy.transform.position = new Vector3(0f, (skyMinY + skyMaxY) / 2f, 0f);
                enemy.flyInSky = true;
                enemy.skyMinY = skyMinY;
                enemy.skyMaxY = skyMaxY;
                enemy.skyMinX = -worldW / 2f + 2f;
                enemy.skyMaxX = worldW / 2f - 2f;
                enemy.attackRange = 20f;
                enemy.moveSpeed = 2.5f;
                PrefabUtility.RecordPrefabInstancePropertyModifications(enemy);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[MagicGame] Dragon sky stage set up ({worldW:F2} x {worldH:F2} units)");
        }

        // ---------------------------------------------------------------- helpers

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
