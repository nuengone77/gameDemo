using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MagicGame.EditorTools
{
    public static class CharacterSkillSetup
    {
        const string ConfigRoot = "Assets/Game/CombatConfigs";
        static readonly (string name, HeroRole role)[] Heroes =
        {
            ("New", HeroRole.Tanky), ("Nueng", HeroRole.Shadow), ("Peng", HeroRole.Magician),
            ("Ling", HeroRole.OracleWitch), ("Nae", HeroRole.Daphne)
        };

        public static void UpdateConfigs()
        {
            if (!AssetDatabase.IsValidFolder(ConfigRoot)) AssetDatabase.CreateFolder("Assets/Game", "CombatConfigs");
            foreach (var hero in Heroes)
            {
                string path = $"{ConfigRoot}/{hero.name}.asset";
                var config = AssetDatabase.LoadAssetAtPath<CharacterCombatConfig>(path);
                if (config == null)
                {
                    config = ScriptableObject.CreateInstance<CharacterCombatConfig>();
                    config.role = hero.role;
                    ApplyInitialBalance(config);
                    AssetDatabase.CreateAsset(config, path);
                }
                // Existing designer tuning is deliberately preserved.
                var set = AssetDatabase.LoadAssetAtPath<CharacterAnimSet>($"Assets/Game/AnimSets/{hero.name}.asset");
                if (set != null && set.combat != config)
                {
                    set.combat = config;
                    EditorUtility.SetDirty(set);
                }
            }
            AssetDatabase.SaveAssets();
        }

        // Initial round-one values, not an automatic reset of existing designer tuning.
        public static void ApplyInitialBalance(CharacterCombatConfig config)
        {
            switch (config.role)
            {
                case HeroRole.Tanky:
                    config.maxHp = 120f; config.moveSpeed = 4f;
                    config.basicDamage = 22f; config.basicInterval = 0.4f;
                    config.activeCooldown = 12f; config.ultimateCooldown = 30f; config.ultimateDamage = 100f;
                    config.distanceReduction = 0.2f; config.conversion = 0.025f; config.shieldCapacity = 30f;
                    config.wallDefense = 0.15f; config.duration = 3f; config.radius = 2.4f;
                    config.rootDuration = 2f; config.attackReduction = 0.2f;
                    break;
                case HeroRole.Shadow:
                    config.maxHp = 110f; config.moveSpeed = 4.3f;
                    config.basicDamage = 12f; config.basicInterval = 0.3f;
                    config.activeCooldown = 18f; config.ultimateCooldown = 30f; config.ultimateDamage = 70f;
                    config.speedPerStack = 0.03f; config.stackTimeout = 3f;
                    config.overdriveSpeed = 0.2f; config.duration = 2f;
                    break;
                case HeroRole.Magician:
                    config.maxHp = 115f; config.moveSpeed = 4.1f;
                    config.basicDamage = 24f; config.basicInterval = 0.4f;
                    config.activeCooldown = 8f; config.ultimateCooldown = 30f; config.ultimateDamage = 180f;
                    config.activeDamage = 50f; config.radius = 2.4f; config.evasion = 0.12f;
                    break;
                case HeroRole.OracleWitch:
                    config.maxHp = 110f; config.moveSpeed = 4f;
                    config.basicDamage = 48f; config.basicInterval = 0.92f;
                    config.activeCooldown = 12f; config.ultimateCooldown = 30f; config.ultimateDamage = 75f;
                    config.radius = 1.6f; config.slow = 0.45f; config.duration = 3.5f;
                    break;
                case HeroRole.Daphne:
                    config.maxHp = 120f; config.moveSpeed = 4f;
                    config.basicDamage = 19f; config.lunarDamage = 25f; config.basicInterval = 0.42f;
                    config.activeCooldown = 9f; config.ultimateCooldown = 35f; config.ultimateDamage = 60f;
                    config.activeDamage = 45f; config.radius = 1.4f; config.burnPerSecond = 4f;
                    config.rootDuration = 1.5f; config.stanceCooldown = 3f;
                    config.transitionBuff = 0.15f; config.duration = 3f; config.healing = 20f;
                    break;
            }
        }

        [MenuItem("Tools/Magic Game/Update Character Skills")]
        public static void UpdateExistingContent()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before updating assets.");
            UpdateConfigs();
            foreach (var name in new[] { "Player", "Goblin", "Skeleton", "Golem", "Dragon", "Witch" })
            {
                string path = $"Assets/Game/Prefabs/{name}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool changed = false;
                    if (root.GetComponent<StatusEffects>() == null) { root.AddComponent<StatusEffects>(); changed = true; }
                    if (name == "Player" && root.GetComponent<PlayerSkills>() == null) { root.AddComponent<PlayerSkills>(); changed = true; }
                    if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var active = SceneManager.GetActiveScene();
            foreach (var buildScene in EditorBuildSettings.scenes.Where(s => s.enabled && s.path.Contains("/Level")))
            {
                var scene = SceneManager.GetSceneByPath(buildScene.path);
                bool opened = !scene.isLoaded;
                if (!opened && scene.isDirty)
                {
                    Debug.LogWarning($"[MagicGame] Kept unsaved edits in {scene.path}; bounds will be inferred at runtime.");
                    continue;
                }
                if (opened) scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Additive);
                try
                {
                    var manager = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LevelManager>(true)).FirstOrDefault();
                    bool changed = false;
                    var walls = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Walls");
                    if (walls != null)
                        foreach (var collider in walls.GetComponentsInChildren<Collider2D>(true))
                            if (!collider.isTrigger && collider.GetComponent<Wall>() == null)
                            {
                                collider.gameObject.AddComponent<Wall>();
                                changed = true;
                            }
                    if (manager != null && TryGetBounds(scene, out var bounds) && manager.playableBounds != bounds)
                    {
                        manager.playableBounds = bounds;
                        EditorUtility.SetDirty(manager);
                        changed = true;
                    }
                    if (changed)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                    }
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            AssetDatabase.SaveAssets();
            Debug.Log("[MagicGame] Character skills updated in place; existing tuning and scene content preserved.");
        }

        static bool TryGetBounds(Scene scene, out Rect bounds)
        {
            bounds = default;
            var root = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Walls");
            if (root == null) return false;
            var left = root.transform.Find("Left")?.GetComponent<BoxCollider2D>();
            var right = root.transform.Find("Right")?.GetComponent<BoxCollider2D>();
            var top = root.transform.Find("Top")?.GetComponent<BoxCollider2D>();
            var bottom = root.transform.Find("Bottom")?.GetComponent<BoxCollider2D>();
            if (left == null || right == null || top == null || bottom == null)
            {
                var edge = root.GetComponentInChildren<EdgeCollider2D>();
                if (edge == null || edge.pointCount < 3) return false;
                var points = edge.points.Select(p => (Vector2)edge.transform.TransformPoint(p + edge.offset)).ToArray();
                bounds = Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
                return true;
            }
            bounds = Rect.MinMaxRect(left.transform.position.x + left.size.x * 0.5f,
                bottom.transform.position.y + bottom.size.y * 0.5f,
                right.transform.position.x - right.size.x * 0.5f,
                top.transform.position.y - top.size.y * 0.5f);
            return true;
        }
    }
}
