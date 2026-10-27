using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace MagicGame.EditorTools
{
    // Tools > Magic Game > Set Up Audio
    // Builds Assets/Audio/GameMixer.mixer (Master -> Music -> MusicDuck, SFX, UI) with exposed volume
    // parameters, and Resources/Audio/SoundBank.asset filled with the placeholder 8-bit sounds.
    // Clips already dragged into the bank are kept; only empty slots get placeholders.
    public static class AudioSetup
    {
        const string MixerPath = "Assets/Audio/GameMixer.mixer";
        const string BankPath = "Assets/Resources/Audio/SoundBank.asset";
        const string Sfx = "Assets/Audio/SFX/";
        const string Music = "Assets/Audio/Music/";
        const string Downloaded = "Assets/Audio/Downloaded/";
        static readonly string[] DownloadedMusic = { Downloaded + "lobby.mp3", Downloaded + "soundtrackAllBoss.mp3" };
        // Long effect loops: kept compressed in memory rather than decompressed (25 s would be several MB).
        static readonly string[] DownloadedLong = { Downloaded + "circleMerge.wav" };
        static readonly string[] Heroes = { "Ling", "Nae", "New", "Nueng", "Peng" };

        [MenuItem("Tools/Magic Game/Set Up Audio")]
        public static void Setup()
        {
            ImportSettings();
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath) ?? CreateMixer();

            if (!AssetDatabase.IsValidFolder("Assets/Resources/Audio")) AssetDatabase.CreateFolder("Assets/Resources", "Audio");
            var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<SoundBank>();
                AssetDatabase.CreateAsset(bank, BankPath);
            }
            bank.mixer = mixer;
            bank.musicGroup = mixer.FindMatchingGroups("MusicDuck").FirstOrDefault();
            bank.sfxGroup = mixer.FindMatchingGroups("SFX").FirstOrDefault();
            bank.uiGroup = mixer.FindMatchingGroups("UI").FirstOrDefault();

            var heroes = bank.heroes.ToList();
            foreach (var name in Heroes)
            {
                var h = heroes.FirstOrDefault(x => x.hero == name);
                if (h == null) heroes.Add(h = new SoundBank.HeroSounds { hero = name });
                Fill(h.basic, $"basic_{name.ToLower()}", 0.7f, 0.06f, 0.05f);
                Fill(h.active, $"q_{name.ToLower()}", 0.9f, 0.03f, 0.2f);
                Fill(h.ultimate, $"e_{name.ToLower()}", 1f, 0f, 0.5f);
            }
            bank.heroes = heroes.ToArray();

            Fill(bank.playerHurt, "player_hurt", 0.8f, 0.08f, 0.15f);
            Fill(bank.playerDeath, "player_death", 1f, 0f, 1f);
            Fill(bank.enemyHit, "enemy_hit", 0.6f, 0.1f, 0.05f);
            Fill(bank.enemyDeath, "enemy_death", 0.8f, 0.08f, 0.08f);
            Fill(bank.enemyMelee, "enemy_melee", 0.6f, 0.1f, 0.1f);
            Fill(bank.skeletonArrow, "enemy_arrow", 0.6f, 0.08f, 0.08f);
            Fill(bank.golemRock, "enemy_rock", 0.9f, 0.05f, 0.2f);
            Fill(bank.dragonFire, "enemy_fire", 0.8f, 0.05f, 0.15f);
            Fill(bank.witchCast, "enemy_witch", 0.7f, 0.05f, 0.15f);
            Fill(bank.burn, "status_burn", 0.5f, 0.1f, 0.15f);
            Fill(bank.rooted, "status_root", 0.6f, 0.05f, 0.15f);
            Fill(bank.portalOpen, "portal_open", 0.8f, 0f, 0.5f);
            Fill(bank.warp, "warp", 0.9f, 0f, 0.5f);
            Fill(bank.stageClear, "stage_clear", 0.8f, 0f, 0.5f);
            Fill(bank.gameOver, "game_over", 1f, 0f, 1f);
            Fill(bank.victory, "victory", 1f, 0f, 1f);
            Fill(bank.uiClick, "ui_click", 0.7f, 0.03f, 0.03f);
            Fill(bank.uiSelect, "ui_select", 0.8f, 0f, 0.05f);
            if (bank.menuMusic == null) bank.menuMusic = Load(Music + "music_menu.wav");
            if (bank.stageMusic == null) bank.stageMusic = Load(Music + "music_stage.wav");
            if (bank.bossMusic == null) bank.bossMusic = Load(Music + "music_boss.wav");

            UseDownloaded(bank);

            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
            Debug.Log("[MagicGame] Audio set up: mixer + sound bank");
        }

        static void Fill(SoundBank.Sound sound, string file, float volume, float jitter, float interval)
        {
            if (sound.IsSet) return; // keep clips the user has dragged in
            var clip = Load($"{Sfx}{file}.wav");
            if (clip == null) return;
            sound.clips = new[] { clip };
            sound.volume = volume;
            sound.pitchJitter = jitter;
            sound.minInterval = interval;
        }

        // Real sounds picked for the game; these replace the placeholders whenever the file is present.
        static void UseDownloaded(SoundBank bank)
        {
            bank.menuMusic = Load(DownloadedMusic[0]) ?? bank.menuMusic;
            bank.bossMusic = Load(DownloadedMusic[1]) ?? bank.bossMusic;

            // New, Nueng and Ling are heard when the ultimate lands, so their cast placeholder goes.
            Set(bank.Hero("New"), h => h.ultimateImpact, "explosion_metio.mp3", 1f, 0.03f, 0.3f, h => h.ultimate);
            Set(bank.Hero("Nueng"), h => h.ultimateImpact, "kunai_utimate_nueng.wav", 0.9f, 0.03f, 0.3f, h => h.ultimate);
            Set(bank.Hero("Ling"), h => h.ultimateImpact, "sword_utimate_ling.wav", 0.9f, 0f, 0.5f, h => h.ultimate);
            // Nae: a hum while the sun and moon drift together (stopped at the merge), then the ring's blast.
            Set(bank.Hero("Nae"), h => h.ultimateLoop, "circleMerge.wav", 1f, 0f, 0f, h => h.ultimate);
            Set(bank.Hero("Nae"), h => h.ultimateImpact, "circle_rangeDamge.mp3", 1f, 0f, 0.5f);
            // Peng: the trap's explosion when stepped on, and a loop for the whole card storm.
            Set(bank.Hero("Peng"), h => h.activeImpact, "explosion_trap.wav", 0.85f, 0.05f, 0.1f);
            Set(bank.Hero("Peng"), h => h.ultimateLoop, "utimate_peng.ogg", 0.7f, 0f, 0f, h => h.ultimate);
        }

        static void Set(SoundBank.HeroSounds hero, Func<SoundBank.HeroSounds, SoundBank.Sound> slot, string file,
            float volume, float jitter, float interval, Func<SoundBank.HeroSounds, SoundBank.Sound> clear = null)
        {
            var clip = hero != null ? Load(Downloaded + file) : null;
            if (clip == null) return;
            var sound = slot(hero);
            sound.clips = new[] { clip };
            sound.volume = volume;
            sound.pitchJitter = jitter;
            sound.minInterval = interval;
            if (clear != null) clear(hero).clips = new AudioClip[0];
        }

        static AudioClip Load(string path) => AssetDatabase.LoadAssetAtPath<AudioClip>(path);

        // Short SFX decompressed for instant playback; music streamed.
        static void ImportSettings()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                var settings = importer.defaultSampleSettings;
                bool music = path.StartsWith(Music) || DownloadedMusic.Contains(path);
                bool longSfx = DownloadedLong.Contains(path);
                settings.loadType = music ? AudioClipLoadType.Streaming
                    : longSfx ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = music || longSfx ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = !music; // keep music in stereo
                importer.SaveAndReimport();
            }
        }

        // Unity has no public API for authoring mixers, so this uses the editor's own AudioMixerController.
        static AudioMixer CreateMixer()
        {
            var asm = typeof(Editor).Assembly;
            var controllerType = asm.GetType("UnityEditor.Audio.AudioMixerController");
            var pathType = asm.GetType("UnityEditor.Audio.AudioGroupParameterPath");
            var exposedType = asm.GetType("UnityEditor.Audio.ExposedAudioParameter");
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

            var controller = controllerType.GetMethod("CreateMixerControllerAtPath", all).Invoke(null, new object[] { MixerPath });
            var master = controllerType.GetProperty("masterGroup", all).GetValue(controller);
            var create = controllerType.GetMethod("CreateNewGroup", all);
            var addChild = controllerType.GetMethod("AddChildToParent", all);

            object Group(string name, object parent)
            {
                var group = create.Invoke(controller, new object[] { name, false });
                addChild.Invoke(controller, new[] { group, parent });
                return group;
            }
            var music = Group("Music", master);
            var duck = Group("MusicDuck", music);
            var sfx = Group("SFX", master);
            var ui = Group("UI", master);

            var addExposed = controllerType.GetMethod("AddExposedParameter", all);
            var getGuid = master.GetType().GetMethod("GetGUIDForVolume", all);
            var names = new (object group, string name)[]
            {
                (master, "MasterVolume"), (music, "MusicVolume"), (duck, "MusicDuckVolume"), (sfx, "SFXVolume"), (ui, "UIVolume"),
            };
            foreach (var (group, _) in names)
                addExposed.Invoke(controller, new[] { Activator.CreateInstance(pathType, all, null, new[] { group, getGuid.Invoke(group, null) }, null) });

            // Rename the exposed parameters (they default to MyExposedParam...).
            var exposedProp = controllerType.GetProperty("exposedParameters", all);
            var exposed = (Array)exposedProp.GetValue(controller);
            var guidField = exposedType.GetField("guid", all);
            var nameField = exposedType.GetField("name", all);
            for (int i = 0; i < exposed.Length; i++)
            {
                var item = exposed.GetValue(i);
                var guid = guidField.GetValue(item);
                foreach (var (group, name) in names)
                    if (getGuid.Invoke(group, null).Equals(guid)) nameField.SetValue(item, name);
                exposed.SetValue(item, i);
            }
            exposedProp.SetValue(controller, exposed);

            EditorUtility.SetDirty((UnityEngine.Object)controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(MixerPath);
            return AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        }
    }
}
