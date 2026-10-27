using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace MagicGame.EditorTools
{
    // Tools > Magic Game > Build Thai Font
    // Bakes Sarabun (OFL) into a static TMP font asset holding the Thai block, ASCII and every character
    // of the story, and assigns it to the UI theme. Static, so playing the game never rewrites the asset.
    public static class ThaiFontSetup
    {
        const string Root = "Assets/Game/UI";
        const string SourcePath = Root + "/Fonts/Sarabun-Regular.ttf";
        const string AssetPath = Root + "/ThaiFont.asset";

        [MenuItem("Tools/Magic Game/Build Thai Font")]
        public static void Build()
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
            if (source == null) { Debug.LogError("[MagicGame] Missing " + SourcePath); return; }

            var chars = new StringBuilder();
            for (int c = 0x20; c < 0x7F; c++) chars.Append((char)c);
            for (int c = 0x0E01; c <= 0x0E5B; c++) chars.Append((char)c);
            chars.Append("“”‘’…–—·");
            var story = Resources.Load<TextAsset>("Story/Prologue");
            if (story != null) chars.Append(story.text);
            string set = new string(chars.ToString().Distinct().Where(c => !char.IsControl(c) && c != '​').ToArray());

            AssetDatabase.DeleteAsset(AssetPath);
            var font = TMP_FontAsset.CreateFontAsset(source, 64, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
            font.name = "Sarabun Thai UI";
            // Thai tone marks stacked over upper vowels need the font's GPOS mark positioning.
            font.getFontFeatures = true;
            font.TryAddCharacters(set, out string missing);
            typeof(TMP_FontAsset).GetMethod("ImportFontFeatures", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.Invoke(font, null);
            if (!string.IsNullOrEmpty(missing)) Debug.LogWarning("[MagicGame] Thai font is missing: " + missing);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            KeepFeaturesForBakedGlyphs(font);
            AssetDatabase.CreateAsset(font, AssetPath);
            font.material.name = font.name + " Material";
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var atlas in font.atlasTextures)
                if (atlas != null) { atlas.name = font.name + " Atlas"; AssetDatabase.AddObjectToAsset(atlas, font); }

            var theme = AssetDatabase.LoadAssetAtPath<ArcaneTheme>(Root + "/ArcaneTheme.asset");
            if (theme != null) { theme.thaiFont = font; EditorUtility.SetDirty(theme); }
            AssetDatabase.SaveAssets();
            Debug.Log($"[MagicGame] Thai font built: {font.characterTable.Count} characters, " +
                $"{font.fontFeatureTable.MarkToBaseAdjustmentRecords.Count} mark-to-base and " +
                $"{font.fontFeatureTable.MarkToMarkAdjustmentRecords.Count} mark-to-mark adjustments");
        }

        // The font's GPOS covers every glyph pair; keep only pairs whose glyphs are in the atlas (MBs smaller).
        static void KeepFeaturesForBakedGlyphs(TMP_FontAsset font)
        {
            var baked = new System.Collections.Generic.HashSet<uint>(font.glyphTable.Select(g => g.index));
            var table = font.fontFeatureTable;
            table.MarkToBaseAdjustmentRecords.RemoveAll(r => !baked.Contains(r.baseGlyphID) || !baked.Contains(r.markGlyphID));
            table.MarkToMarkAdjustmentRecords.RemoveAll(r => !baked.Contains(r.baseMarkGlyphID) || !baked.Contains(r.combiningMarkGlyphID));
            table.glyphPairAdjustmentRecords.RemoveAll(r => !baked.Contains(r.firstAdjustmentRecord.glyphIndex) || !baked.Contains(r.secondAdjustmentRecord.glyphIndex));
        }
    }
}
