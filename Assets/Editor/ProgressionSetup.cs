using System.IO;
using UnityEditor;
using UnityEngine;

namespace MagicGame.EditorTools
{
    public static class ProgressionSetup
    {
        [MenuItem("Tools/Magic Game/Set Up Endless Progression")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Exit Play mode first.");
            const string root = "Assets/Resources/Progression";
            Directory.CreateDirectory(root + "/Cards");
            AssetDatabase.Refresh();
            var config = AssetDatabase.LoadAssetAtPath<ProgressionConfig>(root + "/ProgressionConfig.asset");
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<ProgressionConfig>();
                AssetDatabase.CreateAsset(config, root + "/ProgressionConfig.asset");
            }
            // Preserve designer edits on subsequent setup runs.
            foreach (var defaults in ProgressionConfig.CreateDefaultCards())
            {
                string path = root + "/Cards/" + defaults.id + ".asset";
                var card = AssetDatabase.LoadAssetAtPath<CardDefinition>(path);
                if (card == null) { card = defaults; AssetDatabase.CreateAsset(card, path); }
                else Object.DestroyImmediate(defaults);
                if (!config.cards.Contains(card)) config.cards.Add(card);
            }
            EditorUtility.SetDirty(config);
            // Add just the progression screens to the existing UI prefab.
            const string prefab = "Assets/Resources/UI/GameUI.prefab";
            var ui = PrefabUtility.LoadPrefabContents(prefab);
            try
            {
                ui.GetComponent<GameUI>().BuildProgressionLayout();
                PrefabUtility.SaveAsPrefabAsset(ui, prefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(ui); }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Endless progression ready: card/config assets and existing UI prefab updated. Scenes are preserved.");
        }
    }
}
