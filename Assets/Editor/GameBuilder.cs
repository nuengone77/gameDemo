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

        enum Boss { None, Golem, Dragon }

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
            new() { number = 3, title = "Night Village",    map = "Village",      walkable = R(0.10f, 0.90f, 0.10f, 0.82f), portalX = 0.50f, goblins = 4, skeletons = 3 },
            new() { number = 4, title = "Golem's Domain",   map = "mini_boss",    walkable = R(0.13f, 0.87f, 0.12f, 0.86f), portalX = 0.50f, boss = Boss.Golem },
            new() { number = 5, title = "Burning Fortress", map = "Fortress",     walkable = R(0.10f, 0.90f, 0.16f, 0.80f), portalX = 0.50f, goblins = 5, skeletons = 3 },
            new() { number = 6, title = "Lava Keep",        map = "Pixelfantasy", walkable = R(0.14f, 0.86f, 0.16f, 0.80f), portalX = 0.50f, goblins = 4, skeletons = 5 },
            new() { number = 7, title = "Dragon's Lair",    map = "final_boss",   walkable = R(0.13f, 0.87f, 0.13f, 0.83f), portalX = 0.50f, boss = Boss.Dragon },
        };

        static Rect R(float xMin, float xMax, float yMin, float yMax) => Rect.MinMaxRect(xMin, yMin, xMax, yMax);

        class Shapes { public Sprite ring, circle, square; }

        class Prefabs
        {
            public GameObject player, goblin, skeleton, golem, dragon, portal;
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
                ["Ling"] = MakeSet("Ling", $"{p}/Ling/ling_animation_walk.png", $"{p}/Ling/ling_stand.png", PlayerHeight),
                ["Nae"] = MakeSet("Nae", $"{p}/Nae/nae_animation_walk.png", $"{p}/Nae/nae_stand.png", PlayerHeight),
                ["New"] = MakeSet("New", $"{p}/New/new_animation_walk.png", $"{p}/New/new_stand.png", PlayerHeight),
                ["Nueng"] = MakeSet("Nueng", $"{p}/Nueng/nueng_animation_walk.png", $"{p}/Nueng/nueng_stand.png", PlayerHeight),
                ["Peng"] = MakeSet("Peng", $"{p}/Peng/peg_animation_walk.png", $"{p}/Peng/peg_stand.png", PlayerHeight),
                ["Goblin"] = MakeSet("Goblin", GoblinSheet, null, 1.4f, 8f),
                ["Skeleton"] = MakeSet("Skeleton", $"{m}/skeleton/skeleton_animation_walk.png", null, 1.5f, 8f),
                ["Golem"] = MakeSet("Golem", $"{m}/golem/golem_animation_walk.png", $"{m}/golem/golem_stand.png", 2.6f, 6f),
                ["Dragon"] = MakeSet("Dragon", null, $"{m}/dragon/dragon_finalboss.png", 3.0f),
            };
            AssetDatabase.SaveAssets();
            return sets;
        }

        // Player heroes are 20% smaller than the 1.4-unit monsters.
        const float PlayerHeight = 1.12f;

        static readonly (string name, string walk, string stand)[] PlayerSheets =
        {
            ("Ling", "Ling/ling_animation_walk.png", "Ling/ling_stand.png"),
            ("Nae", "Nae/nae_animation_walk.png", "Nae/nae_stand.png"),
            ("New", "New/new_animation_walk.png", "New/new_stand.png"),
            ("Nueng", "Nueng/nueng_animation_walk.png", "Nueng/nueng_stand.png"),
            ("Peng", "Peng/peg_animation_walk.png", "Peng/peg_stand.png"),
        };

        // Re-slices the 5 hero sheets at PlayerHeight and refreshes the Player prefab; scenes are untouched.
        [MenuItem("Tools/Magic Game/Rebuild Players Only")]
        public static void RebuildPlayers()
        {
            const string dir = ArtRoot + "/Characters/Player";
            CharacterAnimSet first = null;
            foreach (var (name, walk, stand) in PlayerSheets)
            {
                var set = MakeSet(name, $"{dir}/{walk}", $"{dir}/{stand}", PlayerHeight);
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

        const string GoblinSheet = ArtRoot + "/Characters/Monster/goblin/goblin_club_sheet.png";

        // Re-slices only the goblin sheet and refreshes its prefab; scenes keep their goblins.
        [MenuItem("Tools/Magic Game/Rebuild Goblin Only")]
        public static void RebuildGoblin()
        {
            var set = MakeSet("Goblin", GoblinSheet, null, 1.4f, 8f);
            AssetDatabase.SaveAssets();

            string path = $"{GameRoot}/Prefabs/Goblin.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            var sr = root.GetComponent<SpriteRenderer>();
            sr.sprite = set.PreviewSprite;
            var size = (Vector2)sr.sprite.bounds.size;
            var feet = root.GetComponent<CircleCollider2D>();
            feet.radius = Mathf.Min(size.x * 0.22f, 0.6f);
            feet.offset = new Vector2(0f, feet.radius);
            var box = root.transform.Find("Hurtbox").GetComponent<BoxCollider2D>();
            box.size = new Vector2(size.x * 0.55f, size.y * 0.8f);
            box.offset = new Vector2(0f, size.y * 0.45f);
            root.GetComponent<EnemyController>().castHeight = size.y * 0.5f;
            root.GetComponent<WorldHealthBar>().heightOffset = size.y + 0.1f;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("[MagicGame] Goblin rebuilt from goblin_club_sheet.png");
        }

        static CharacterAnimSet MakeSet(string name, string walkPath, string standPath, float worldHeight, float walkFps = 10f)
        {
            var set = LoadOrCreate<CharacterAnimSet>($"{GameRoot}/AnimSets/{name}.asset");
            set.displayName = name;
            set.walkFps = walkFps;
            set.standFps = 6f;

            var walk = walkPath != null ? SliceSheet(walkPath, worldHeight) : null;
            var stand = standPath != null ? SliceSheet(standPath, worldHeight) : null;
            set.walkDown = Row(walk, FacingDir.Down);
            set.walkUp = Row(walk, FacingDir.Up);
            set.walkLeft = Row(walk, FacingDir.Left);
            set.walkRight = Row(walk, FacingDir.Right);
            set.standDown = Row(stand, FacingDir.Down);
            set.standUp = Row(stand, FacingDir.Up);
            set.standLeft = Row(stand, FacingDir.Left);
            set.standRight = Row(stand, FacingDir.Right);
            EditorUtility.SetDirty(set);
            return set;
        }

        // A sheet with a single row (the dragon) uses that row for every direction.
        static Sprite[] Row(List<Sprite[]> rows, FacingDir dir)
        {
            if (rows == null || rows.Count == 0) return new Sprite[0];
            return rows.Count >= 4 ? rows[(int)dir] : rows[0];
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
            var bands = Merge(Runs(rowMask), 6).Where(b => b.y - b.x > 20).ToList();

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
                { p.speed = 9f; p.lifetime = 1.1f; p.frameRate = 16f; });
            var fireball = SaveAttack("Attack_DragonFireball", "monster/dragon_fireball_sheet.png", 0.8f, p =>
                { p.speed = 6f; p.lifetime = 4f; p.frameRate = 12f; });
            var boulder = SaveAttack("Attack_GolemBoulder", "monster/golem_boulder_roll_sheet.png", GolemBoulderSize, p =>
                { p.speed = 5f; p.lifetime = 4f; p.frameRate = 12f; });

            SetAttack("Ling", ice);
            SetAttack("Nae", witch);
            SetAttack("New", crystal);
            SetAttack("Nueng", kunai);
            SetAttack("Peng", card);
            SetEnemyProjectile("Golem", boulder, GolemAttack);
            SetEnemyProjectile("Dragon", fireball);
            AssetDatabase.SaveAssets();
            Debug.Log("[MagicGame] Attack effects applied");
        }

        static Projectile SaveAttack(string name, string sheet, float worldHeight, System.Action<Projectile> configure)
        {
            var frames = SliceSheet($"{AttackRoot}/{sheet}", worldHeight, true, 2).SelectMany(r => r).ToArray();
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = frames[0];
            sr.sortingOrder = 20000;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var size = (Vector2)frames[0].bounds.size;
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
            e.damage = 25f;
            e.projectileSpeed = 4.5f;
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

            prefabs.goblin = SaveEnemy(sets["Goblin"], 60f, 1f, e =>
            {
                e.displayName = "Goblin"; e.style = AttackStyle.Melee;
                e.moveSpeed = 2.2f; e.attackRange = 1.0f; e.attackCooldown = 1f; e.windUp = 0.3f; e.damage = 10f;
            });
            prefabs.skeleton = SaveEnemy(sets["Skeleton"], 45f, 1f, e =>
            {
                e.displayName = "Skeleton Archer"; e.style = AttackStyle.Ranged; e.projectilePrefab = arrow;
                e.moveSpeed = 1.8f; e.attackRange = 6f; e.preferredDistance = 3.5f; e.attackCooldown = 1.8f;
                e.windUp = 0.35f; e.damage = 8f; e.projectileSpeed = 8f;
            });
            prefabs.golem = SaveEnemy(sets["Golem"], 600f, 50f, e =>
            {
                e.displayName = "Forest Golem"; e.isBoss = true; e.style = AttackStyle.Ranged; e.projectilePrefab = rock;
                e.moveSpeed = 0.8f; e.attackRange = 9f; e.chaseDistance = 1.8f; e.attackCooldown = 2f; e.windUp = 0.6f;
                GolemAttack(e);
            });
            prefabs.dragon = SaveEnemy(sets["Dragon"], 1000f, 50f, e =>
            {
                e.displayName = "Inferno Dragon"; e.isBoss = true; e.style = AttackStyle.Ranged; e.projectilePrefab = fireball;
                e.moveSpeed = 1.5f; e.attackRange = 12f; e.chaseDistance = 5f; e.preferredDistance = 4f; e.attackCooldown = 1.4f; e.windUp = 0.4f;
                e.damage = 12f; e.projectileSpeed = 6f; e.projectilesPerShot = 5; e.spreadAngle = 50f; e.burstEvery = 3; e.burstCount = 16;
            });

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
            if (!enemy.isBoss)
            {
                var bar = go.AddComponent<WorldHealthBar>();
                bar.heightOffset = go.GetComponent<SpriteRenderer>().sprite.bounds.size.y + 0.1f;
                bar.width = 0.8f;
            }
            return SavePrefab(go, set.displayName);
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
                if (level.boss == Boss.Dragon) Spawn(prefabs.dragon, enemies, new Vector2(area.center.x, area.center.y + area.height * 0.1f));
                for (int i = 0; i < level.goblins; i++) Spawn(prefabs.goblin, enemies, RandomPoint(rng, area));
                for (int i = 0; i < level.skeletons; i++) Spawn(prefabs.skeleton, enemies, RandomPoint(rng, area));

                var manager = new GameObject("LevelManager").AddComponent<LevelManager>();
                manager.levelNumber = level.number;
                manager.levelTitle = level.title;
                manager.isFinalLevel = level.number == Levels.Length;
                manager.portal = portal.GetComponent<Portal>();

                EditorSceneManager.SaveScene(scene);
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
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
