using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MagicGame.EditorTools
{
    // Technical normalization only. Generated originals and prompts are retained under Docs/Art/Grimoire.
    public static class GrimoireArtImporter
    {
        const string Root = "Assets/Art/UI/Grimoire";
        static readonly string[] Palette = {
            "08131E", "101E2E", "182B40", "243C54", "304C62", "3D5B6B",
            "251C15", "3B291B", "513923", "6B4A2A", "835E33", "9A703B", "AF844B",
            "C29858", "D2AC68", "E1BD7B", "ECCD94", "F3DDAE", "FFF0CB",
            "174453", "24667A", "348DA0", "4CB4C5", "75D9E8", "AFF0F1", "E3FFFF"
        };

        [MenuItem("Tools/Magic Game/Normalize Grimoire Art")]
        public static void Normalize()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            Directory.CreateDirectory(Root);
            var source = Read("Docs/Art/Grimoire/FrameSource.png");
            var grain = Read("Docs/Art/Grimoire/SurfaceSource.png");
            try
            {
                var bounds = AlphaBounds(source);
                Save("PanelFrame", Sample(source, bounds, 256, 256, true), new Vector4(44, 44, 44, 44));
                Save("ButtonFrame", Sample(source, bounds, 128, 128, true), new Vector4(22, 22, 22, 22));
                Save("HUDFrame", Sample(source, bounds, 96, 96, true), new Vector4(17, 17, 17, 17));
                var topRail = new RectInt(bounds.x + bounds.width * 3 / 8, bounds.y + bounds.height * 92 / 100,
                    bounds.width / 4, bounds.height * 6 / 100);
                Save("SliderTrack", Sample(source, topRail, 64, 12, true), new Vector4(6, 4, 6, 4));
                var jewel = new RectInt(bounds.x + bounds.width * 35 / 1000, bounds.y + bounds.height * 916 / 1000,
                    bounds.width * 52 / 1000, bounds.height * 52 / 1000);
                Save("SliderHandle", Jewel(source, jewel), Vector4.zero);
                Save("HeaderOrnament", Jewel(source, jewel), Vector4.zero);

                // Mirroring a sampled quadrant guarantees identical opposing edges without a blurry seam repair.
                var tile = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++)
                {
                    int tx = x < 128 ? x : 255 - x;
                    int ty = y < 128 ? y : 255 - y;
                    var pixel = grain.GetPixel(tx * grain.width / 128, ty * grain.height / 128);
                    int tone = Mathf.Clamp(Mathf.RoundToInt(pixel.grayscale * 90), 0, 24);
                    tile.SetPixel(x, y, new Color32((byte)(15 + tone), (byte)(25 + tone), (byte)(39 + tone), 255));
                }
                Save("SurfaceTile", tile, Vector4.zero, true);
                var focus = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    int edge = Mathf.Min(x, y, 63 - x, 63 - y);
                    focus.SetPixel(x, y, edge >= 2 && edge <= 3 ? Color.white : Color.clear);
                }
                Save("FocusFrame", focus, new Vector4(8, 8, 8, 8));
            }
            finally { UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(grain); }
            AssetDatabase.Refresh();
        }

        public static void Assign(ArcaneTheme theme)
        {
            if (!File.Exists(Root + "/PanelFrame.png")) Normalize();
            theme.surfaceTile = Import("SurfaceTile", Vector4.zero, true);
            theme.panelFrame = Import("PanelFrame", new Vector4(44, 44, 44, 44));
            theme.buttonFrame = Import("ButtonFrame", new Vector4(22, 22, 22, 22));
            theme.hudFrame = Import("HUDFrame", new Vector4(17, 17, 17, 17));
            theme.focusFrame = Import("FocusFrame", new Vector4(8, 8, 8, 8));
            theme.sliderTrack = Import("SliderTrack", new Vector4(6, 4, 6, 4));
            theme.sliderHandle = Import("SliderHandle", Vector4.zero);
            theme.headerOrnament = Import("HeaderOrnament", Vector4.zero);
        }

        static Texture2D Read(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(path))) throw new InvalidDataException(path);
            return texture;
        }

        static RectInt AlphaBounds(Texture2D source)
        {
            int left = source.width, bottom = source.height, right = 0, top = 0;
            for (int y = 0; y < source.height; y++)
            for (int x = 0; x < source.width; x++)
            {
                if (source.GetPixel(x, y).a < .5f) continue;
                left = Mathf.Min(left, x); bottom = Mathf.Min(bottom, y);
                right = Mathf.Max(right, x); top = Mathf.Max(top, y);
            }
            return new RectInt(left, bottom, right - left + 1, top - bottom + 1);
        }

        static Texture2D Sample(Texture2D source, RectInt bounds, int width, int height, bool quantize)
        {
            var palette = Array.ConvertAll(Palette, hex => { ColorUtility.TryParseHtmlString("#" + hex, out var color); return color; });
            var result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var color = source.GetPixel(bounds.x + (2 * x + 1) * bounds.width / (2 * width),
                    bounds.y + (2 * y + 1) * bounds.height / (2 * height));
                if (color.a < .5f) color = Color.clear;
                else if (quantize)
                {
                    float best = float.MaxValue;
                    Color nearest = Color.black;
                    foreach (var candidate in palette)
                    {
                        float distance = (new Vector3(color.r, color.g, color.b) - new Vector3(candidate.r, candidate.g, candidate.b)).sqrMagnitude;
                        if (distance < best) { best = distance; nearest = candidate; }
                    }
                    color = nearest;
                }
                result.SetPixel(x, y, color);
            }
            return result;
        }

        static Texture2D Jewel(Texture2D source, RectInt bounds)
        {
            var result = Sample(source, bounds, 24, 24, true);
            for (int y = 0; y < 24; y++)
            for (int x = 0; x < 24; x++)
                if (Mathf.Abs(x - 11.5f) + Mathf.Abs(y - 11.5f) > 12)
                    result.SetPixel(x, y, Color.clear);
            return result;
        }

        static void Save(string name, Texture2D texture, Vector4 border, bool repeat = false)
        {
            texture.Apply();
            File.WriteAllBytes(Root + "/" + name + ".png", texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(Root + "/" + name + ".png");
            Import(name, border, repeat);
        }

        static Sprite Import(string name, Vector4 border, bool repeat = false)
        {
            string path = Root + "/" + name + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.spriteBorder = border;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
