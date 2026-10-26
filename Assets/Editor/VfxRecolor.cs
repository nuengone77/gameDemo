using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Rendering.Universal;
using UnityEngine;

namespace MagicGame.EditorTools
{
    // Makes a recolored copy of a third-party effect prefab so it matches a hero's palette.
    // Textures are converted to grayscale copies and every particle colour is replaced by
    // tint * brightness, so a gold effect can become ice blue. The original pack is untouched.
    public static class VfxRecolor
    {
        const string Root = "Assets/Game/VFX";
        static bool s_darken;
        const float DarkenBoost = 2.5f;

        // darken = draw as dark smoke instead of light: additive materials become alpha-blended and
        // brightness becomes opacity (additive effects can't be darker than what's behind them).
        public static GameObject MakeCopy(string sourcePrefab, string name, Color tint, bool darken = false)
        {
            s_darken = darken;
            EnsureFolder(Root);
            EnsureFolder($"{Root}/Materials");
            EnsureFolder($"{Root}/Textures");

            string path = $"{Root}/{name}.prefab";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CopyAsset(sourcePrefab, path);

            var root = PrefabUtility.LoadPrefabContents(path);
            var materials = new Dictionary<Material, Material>();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var shared = renderer.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                {
                    var source = shared[i];
                    if (source == null) continue;
                    if (source.shader.name.StartsWith("Legacy Shaders"))
                    {
                        // Built-in-only material (usually an empty holder emitter): it cannot draw in URP.
                        renderer.enabled = false;
                        continue;
                    }
                    if (!materials.TryGetValue(source, out var copy))
                        materials[source] = copy = RecolorMaterial(source, name, tint);
                    shared[i] = copy;
                }
                renderer.sharedMaterials = shared;
            }

            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.startColor = Recolor(main.startColor, tint);
                var overLife = ps.colorOverLifetime;
                if (overLife.enabled) overLife.color = Recolor(overLife.color, tint);
                var bySpeed = ps.colorBySpeed;
                if (bySpeed.enabled) bySpeed.color = Recolor(bySpeed.color, tint);
                var trails = ps.trails;
                if (trails.enabled) trails.colorOverLifetime = Recolor(trails.colorOverLifetime, tint);
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        static Material RecolorMaterial(Material source, string prefix, Color tint)
        {
            var copy = new Material(source) { name = $"{prefix}_{source.name}" };
            if (s_darken && copy.shader.name == "Eric/URP_AdditiveFlow")
            {
                var alphaBlend = Shader.Find("Eric/URP_AlphaBlendFlow");
                if (alphaBlend != null) copy.shader = alphaBlend;
            }
            // Built-in particle shaders don't draw in URP; upgrade the copy (moves _MainTex to _BaseMap).
            if (copy.shader.name.StartsWith("Particles/Standard"))
                MaterialUpgrader.Upgrade(copy, new ParticleUpgrader(copy.shader.name), MaterialUpgrader.UpgradeFlags.None);
            foreach (var property in new[] { "_MainTex", "_BaseMap" })
            {
                if (!copy.HasProperty(property)) continue;
                if (copy.GetTexture(property) is Texture2D texture) copy.SetTexture(property, Grayscale(texture));
            }
            // Material-level tints are often coloured too; keep only their brightness.
            foreach (var property in new[] { "_TintColor", "_BaseColor", "_Color" })
            {
                if (!copy.HasProperty(property)) continue;
                var neutral = Neutral(copy.GetColor(property));
                if (s_darken) neutral = new Color(1f, 1f, 1f, 1f);
                copy.SetColor(property, neutral);
            }
            string path = $"{Root}/Materials/{copy.name}.mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(copy, path);
            return copy;
        }

        // Grayscale copy keeping each pixel's brightest channel and its alpha.
        static Texture2D Grayscale(Texture2D source)
        {
            string sourcePath = AssetDatabase.GetAssetPath(source);
            // Unity's built-in textures (e.g. Default-Particle) are neutral white already and not files on disk.
            if (!sourcePath.StartsWith("Assets/")) return source;
            string path = $"{Root}/Textures/{Path.GetFileNameWithoutExtension(sourcePath)}{(s_darken ? "_mask" : "_gray")}.png";

            var importer = (TextureImporter)AssetImporter.GetAtPath(sourcePath);
            var bytes = File.ReadAllBytes(sourcePath);
            var readable = new Texture2D(2, 2);
            if (!readable.LoadImage(bytes))
            {
                // Not PNG/JPG (e.g. TGA/PSD): blit through a render texture instead.
                var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(source, rt);
                var previous = RenderTexture.active;
                RenderTexture.active = rt;
                readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
            // Some packs make transparency from brightness ("Alpha from Grayscale"); bake that into the copy.
            bool alphaFromGray = importer != null && importer.alphaSource == TextureImporterAlphaSource.FromGrayScale;
            var pixels = readable.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                byte v = (byte)Mathf.Max(pixels[i].r, Mathf.Max(pixels[i].g, pixels[i].b));
                byte gray = (byte)((pixels[i].r * 77 + pixels[i].g * 150 + pixels[i].b * 29) >> 8);
                byte a = alphaFromGray ? gray : pixels[i].a;
                pixels[i] = s_darken
                    ? new Color32(255, 255, 255, (byte)(v * a / 255)) // brightness -> opacity
                    : new Color32(v, v, v, a);
            }
            readable.SetPixels32(pixels);
            File.WriteAllBytes(path, readable.EncodeToPNG());
            Object.DestroyImmediate(readable);
            AssetDatabase.ImportAsset(path);

            var copyImporter = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null)
            {
                copyImporter.wrapMode = importer.wrapMode;
                copyImporter.mipmapEnabled = importer.mipmapEnabled;
                copyImporter.alphaIsTransparency = importer.alphaIsTransparency;
            }
            copyImporter.sRGBTexture = true;
            copyImporter.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Color Neutral(Color c)
        {
            float v = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            return new Color(v, v, v, c.a);
        }

        static Color Recolor(Color c, Color tint)
        {
            float v = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            // Dark smoke needs more opacity than the original glow had brightness to read on the ground.
            if (s_darken) return new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(c.a * tint.a * v * DarkenBoost));
            return new Color(tint.r * v, tint.g * v, tint.b * v, c.a * tint.a);
        }

        static Gradient Recolor(Gradient g, Color tint)
        {
            if (g == null) return null;
            var keys = g.colorKeys;
            for (int i = 0; i < keys.Length; i++) keys[i].color = Recolor(keys[i].color, tint);
            var result = new Gradient { mode = g.mode };
            result.SetKeys(keys, g.alphaKeys);
            return result;
        }

        static ParticleSystem.MinMaxGradient Recolor(ParticleSystem.MinMaxGradient c, Color tint)
        {
            switch (c.mode)
            {
                case ParticleSystemGradientMode.Color:
                    return new ParticleSystem.MinMaxGradient(Recolor(c.color, tint));
                case ParticleSystemGradientMode.TwoColors:
                    return new ParticleSystem.MinMaxGradient(Recolor(c.colorMin, tint), Recolor(c.colorMax, tint));
                case ParticleSystemGradientMode.Gradient:
                    return new ParticleSystem.MinMaxGradient(Recolor(c.gradient, tint));
                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(Recolor(c.gradientMin, tint), Recolor(c.gradientMax, tint));
                default:
                    return c;
            }
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
