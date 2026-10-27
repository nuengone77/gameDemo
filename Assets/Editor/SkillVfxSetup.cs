using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Rendering.Universal;
using UnityEngine;

namespace MagicGame.EditorTools
{
    // Tools > Magic Game > Apply Skill VFX
    // Converts only the materials used by the chosen third-party effect prefabs from the Built-in
    // particle shader to URP, then assigns those prefabs to each hero's combat config.
    public static class SkillVfxSetup
    {
        const string Hovl = "Assets/Hovl Studio/Magic effects pack/Prefabs/";
        const string Matthew = "Assets/Matthew Guz/Spell Area of Effect FREE/Prefab/";
        const string Configs = "Assets/Game/CombatConfigs/";
        const string EricFree = "Assets/Eric VFX Studio/Free Game VFX/Prefab/";
        static readonly Color LingIce = new(0.45f, 0.85f, 1f, 1f);
        // Near-black shadow (drawn with darken, since additive black would be invisible).
        static readonly Color NuengBlack = new(0.05f, 0.03f, 0.08f, 1f);
        static readonly Color NaeSolar = new(1f, 0.55f, 0.12f, 1f);
        static readonly Color NaeLunar = new(0.72f, 0.38f, 1f, 1f);
        // Deeper versions for the Eclipse, drawn as solid colour (darken) so they read on bright grass.
        static readonly Color EclipseSun = new(1f, 0.42f, 0.02f, 1f);
        static readonly Color EclipseMoon = new(0.42f, 0.1f, 0.85f, 1f);

        // slot setter, prefab path, world scale, tilt (degrees around X to lay ground effects into the 3/4 view)
        class Slot
        {
            public string hero, path;
            public System.Action<CharacterCombatConfig, VfxSlot> assign;
            public float scale = 1f, tilt, yOffset, seconds;
            public bool playOnce, loopAll, darken;
            public float impactTime;
            public Color? recolor;      // set = use a recolored copy (VfxRecolor) instead of the pack prefab
            public string recolorName;
        }

        const float EclipseOrbScale = 0.7f;

        static readonly Slot[] Slots =
        {
            // New (Tanky)
            // New's shield wall and orbiting shields are sprites (shieldWallSprite / shieldOrbSprite, set in Apply).
            // Meteor Binding: gold warning circle on the ground + Eric's fireball dropping from the sky onto it.
            new() { hero = "New", path = EricFree + "FX_Magic Door_Gold.prefab", scale = 1.35f, tilt = -55f,
                assign = (c, v) => c.ultimateVfx = v },
            new() { hero = "New", path = EricFree + "FX_Fireball.prefab", scale = 1.37f,
                assign = (c, v) => c.ultimateFallVfx = v },
            new() { hero = "New", path = Hovl + "AoE effects/Ground AOE explosion.prefab", scale = 0.9f, tilt = -55f, seconds = 1.2f, playOnce = true,
                assign = (c, v) => c.ultimateExtraVfx = v },
            // Nueng (Shadow)
            // Dark Kunai Rain: Eric's light pillar recolored grey, one per target while the kunai fall.
            new() { hero = "Nueng", path = EricFree + "FX_LootDrop_Blue.prefab", recolor = NuengBlack, darken = true, recolorName = "Nueng_KunaiBeam",
                scale = 0.6f, seconds = 0.9f, playOnce = true,
                assign = (c, v) => c.ultimateVfx = v },
            new() { hero = "Nueng", path = Hovl + "Smoke effects/Smoke puff.prefab", scale = 0.15f, seconds = 1f,
                assign = (c, v) => c.activeVfx = v },
            // Peng (Magician)
            new() { hero = "Peng", path = Hovl + "Hits and explosions/Explosion.prefab", scale = 0.6f, seconds = 1f, playOnce = true,
                assign = (c, v) => c.activeVfx = v },
            // Jackpot: glowing core in the middle of the spinning card storm (lives as long as the storm).
            // Its visuals sit on a child 2.5 units above the origin, so offset down by 2.5 * scale;
            // its parts are one-shot bursts, so they are looped to keep pulsing for the whole storm.
            new() { hero = "Peng", path = EricFree + "FX_Greenlight_shrink.prefab", scale = 0.8f, yOffset = -2f, loopAll = true,
                assign = (c, v) => c.ultimateVfx = v },
            new() { hero = "Peng", path = Hovl + "Environment/Teleport.prefab", scale = 0.8f, tilt = -55f, seconds = 1f, playOnce = true,
                assign = (c, v) => c.ultimateExtraVfx = v },
            // Ling (Oracle Witch)
            new() { hero = "Ling", path = Hovl + "Magic circles/Freeze circle.prefab", scale = 0.5f, tilt = -55f,
                assign = (c, v) => c.activeVfx = v },
            // Eric's gold sword, recolored ice blue for Ling; it lands ~0.3 s after it appears.
            new() { hero = "Ling", path = EricFree + "FX_Weapon Effect.prefab", recolor = LingIce, recolorName = "Ling_IceSword",
                scale = 0.35f, seconds = 1.2f, playOnce = true, impactTime = 0.3f,
                assign = (c, v) => c.ultimateVfx = v },
            // Exploding basic attacks: Ling's ice orb shatters, Nae's Solar fireball bursts (orange).
            new() { hero = "Ling", path = Hovl + "Hits and explosions/Snow hit.prefab", scale = 1.4f, seconds = 1f, playOnce = true,
                assign = (c, v) => c.basicHitVfx = v },
            new() { hero = "Nae", path = Hovl + "Hits and explosions/Holy hit.prefab", recolor = NaeSolar, recolorName = "Nae_SolarBurst",
                scale = 1.4f, seconds = 1f, playOnce = true,
                assign = (c, v) => c.basicHitVfx = v },
            // Nae (Daphne): Solar = orange like her hair, Lunar = purple like her dress.
            new() { hero = "Nae", path = Hovl + "AoE effects/AoE slash orange.prefab", recolor = NaeSolar, recolorName = "Nae_SolarRing",
                scale = 1.4f, tilt = -55f, seconds = 0.8f, playOnce = true,
                assign = (c, v) => c.activeVfx = v },
            new() { hero = "Nae", path = Hovl + "Magic circles/Magic circle.prefab", recolor = NaeLunar, recolorName = "Nae_LunarBindCircle",
                scale = 0.45f, tilt = -55f,
                assign = (c, v) => c.activeExtraVfx = v },
            new() { hero = "Nae", path = EricFree + "FX_Purple_Hit_02.prefab", recolor = NaeLunar, recolorName = "Nae_LunarHit",
                scale = 0.45f, seconds = 0.8f, playOnce = true,
                assign = (c, v) => c.lunarHitVfx = v },
            // Eclipse hit on each enemy: Eric's purple hit in its original colours (as chosen by the user).
            new() { hero = "Nae", path = EricFree + "FX_Purple_Hit_02.prefab", scale = 0.45f, seconds = 0.8f, playOnce = true,
                assign = (c, v) => c.ultimateHitVfx = v },
            new() { hero = "Nae", path = Matthew + "AoE Stars.prefab", recolor = NaeLunar, recolorName = "Nae_EclipseStars",
                scale = 0.6f, tilt = -55f, seconds = 3f,
                assign = (c, v) => c.ultimateVfx = v },
            // Eclipse: Eric's glowing core as a sun (deep orange) and a moon (deep purple) that drift together.
            // Its visuals sit 2.5 units above the origin, so offset down by 2.5 * scale; looped while moving.
            new() { hero = "Nae", path = EricFree + "FX_Greenlight_shrink.prefab", recolor = EclipseSun, darken = true, recolorName = "Nae_EclipseSunDeep",
                scale = EclipseOrbScale, yOffset = -2.5f * EclipseOrbScale, loopAll = true,
                assign = (c, v) => c.eclipseSunVfx = v },
            new() { hero = "Nae", path = EricFree + "FX_Greenlight_shrink.prefab", recolor = EclipseMoon, darken = true, recolorName = "Nae_EclipseMoonDeep",
                scale = EclipseOrbScale, yOffset = -2.5f * EclipseOrbScale, loopAll = true,
                assign = (c, v) => c.eclipseMoonVfx = v },
            // The merged eclipse's damage ring: Hovl's blue portal in deep purple, scaled up at runtime.
            new() { hero = "Nae", path = Hovl + "Portals/Portal blue.prefab", recolor = EclipseMoon, darken = true, recolorName = "Nae_EclipseWave",
                assign = (c, v) => c.eclipseWaveVfx = v },
        };

        const string TankyArt = "Assets/Art/Characters/Player/New/";

        // New's orbiting shields and Astral Shield Wall are painted sprites drawn far smaller than their source,
        // so they are capped in size and mipmapped to stay smooth instead of shimmering.
        static Sprite ImportShieldArt(string path, int maxSize)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { Debug.LogWarning("[MagicGame] Missing " + path); return null; }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        [MenuItem("Tools/Magic Game/Apply Skill VFX")]
        public static void Apply()
        {
            int converted = ConvertMaterials();
            int assigned = 0;
            var tanky = AssetDatabase.LoadAssetAtPath<CharacterCombatConfig>($"{Configs}New.asset");
            if (tanky != null)
            {
                tanky.activeVfx = new VfxSlot();
                tanky.shieldOrbSprite = ImportShieldArt(TankyArt + "real_armorr.png", 256);
                tanky.shieldWallSprite = ImportShieldArt(TankyArt + "TowerDefeneNew.png", 1024);
                EditorUtility.SetDirty(tanky);
            }
            foreach (var slot in Slots)
            {
                var prefab = slot.recolor.HasValue
                    ? VfxRecolor.MakeCopy(slot.path, slot.recolorName, slot.recolor.Value, slot.darken)
                    : AssetDatabase.LoadAssetAtPath<GameObject>(slot.path);
                var config = AssetDatabase.LoadAssetAtPath<CharacterCombatConfig>($"{Configs}{slot.hero}.asset");
                if (prefab == null || config == null)
                {
                    Debug.LogWarning($"[MagicGame] Skill VFX skipped: {slot.path} for {slot.hero}");
                    continue;
                }
                slot.assign(config, new VfxSlot
                {
                    prefab = prefab, scale = slot.scale, tilt = slot.tilt, yOffset = slot.yOffset, seconds = slot.seconds,
                    playOnce = slot.playOnce, loopAll = slot.loopAll, impactTime = slot.impactTime,
                });
                EditorUtility.SetDirty(config);
                assigned++;
            }
            BuildStatusVfx();
            AssetDatabase.SaveAssets();
            Debug.Log($"[MagicGame] Skill VFX: converted {converted} materials, assigned {assigned} effects");
        }

        // ---------------------------------------------------------------- status effects

        const string Status = "Assets/Matthew Guz/Status and Auras FREE/Prefab/";
        static readonly Color SlowIce = new(0.45f, 0.85f, 1f, 1f);

        // Effects shown while a character has a status (see StatusEffects / StatusVfxSet).
        public static void BuildStatusVfx()
        {
            string[] packPrefabs = { Status + "Stun.prefab", Status + "Aura Burn.prefab", Status + "Aura Buff.prefab", EliteAura };
            ConvertMaterials(packPrefabs);

            const string path = "Assets/Resources/StatusVfx.asset";
            var set = AssetDatabase.LoadAssetAtPath<StatusVfxSet>(path);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<StatusVfxSet>();
                AssetDatabase.CreateAsset(set, path);
            }
            // Stun stars keep their original yellow and sit above the head (StatusEffects adds head height).
            set.disabled = new VfxSlot { prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Status + "Stun.prefab"), scale = STUN_SCALE, loopAll = true,
                yOffset = -0.55f * STUN_SCALE - 0.08f }; // its stars sit 0.55 above the prefab origin
            set.burn = new VfxSlot { prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Status + "Aura Burn.prefab"), scale = BURN_SCALE, loopAll = true };
            set.slow = new VfxSlot { prefab = VfxRecolor.MakeCopy(Status + "Aura .prefab", "Status_SlowAura", SlowIce), scale = SLOW_SCALE, loopAll = true };
            set.buff = new VfxSlot { prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Status + "Aura Buff.prefab"), scale = BUFF_SCALE, loopAll = true };
            set.elite = new VfxSlot { prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EliteAura), scale = ELITE_SCALE, loopAll = true };
            EditorUtility.SetDirty(set);
        }

        const float STUN_SCALE = 0.6f, BURN_SCALE = 0.25f, SLOW_SCALE = 0.35f, BUFF_SCALE = 0.55f, ELITE_SCALE = 0.4f;
        // Gold glow around elite enemies (chosen by the user from the auras in the packs).
        const string EliteAura = Hovl + "Character auras/Buff.prefab";

        // Only the materials these prefabs actually use; everything else in the packs is left alone.
        public static int ConvertMaterials() => ConvertMaterials(Slots.Select(s => s.path));

        static int ConvertMaterials(IEnumerable<string> prefabPaths)
        {
            var materials = prefabPaths.SelectMany(p => AssetDatabase.GetDependencies(p, true))
                .Where(p => p.EndsWith(".mat")).Distinct()
                .Select(AssetDatabase.LoadAssetAtPath<Material>)
                .Where(m => m != null && m.shader != null && m.shader.name.StartsWith("Particles/Standard"))
                .Where(m => !m.name.Contains("Distortion")) // grab-pass layers have no URP equivalent; hidden at runtime
                .ToList();
            foreach (var material in materials)
            {
                var upgrader = new ParticleUpgrader(material.shader.name);
                MaterialUpgrader.Upgrade(material, upgrader, MaterialUpgrader.UpgradeFlags.None);
                EditorUtility.SetDirty(material);
            }
            return materials.Count;
        }
    }
}
