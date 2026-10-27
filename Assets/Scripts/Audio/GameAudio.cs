using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MagicGame
{
    // The one place sounds are played from. Pools a fixed number of sources (no per-shot allocation),
    // routes everything through the mixer groups, varies pitch/volume slightly, rate-limits repeats,
    // crossfades music by scene and ducks it under ultimates.
    public class GameAudio : MonoBehaviour
    {
        const int SfxVoices = 16;
        const string MasterParam = "MasterVolume", MusicParam = "MusicVolume", SfxParam = "SFXVolume",
            UiParam = "UIVolume", DuckParam = "MusicDuckVolume";

        static GameAudio s_instance;
        SoundBank _bank;
        readonly List<AudioSource> _voices = new();
        int _nextVoice;
        readonly Dictionary<SoundBank.Sound, float> _lastPlayed = new();
        readonly Dictionary<SoundBank.Sound, int> _lastClip = new();
        AudioSource[] _music = new AudioSource[2];
        int _activeMusic;
        Coroutine _fade, _duck;

        public static SoundBank Bank => s_instance != null ? s_instance._bank : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (s_instance != null) return;
            var bank = Resources.Load<SoundBank>("Audio/SoundBank");
            if (bank == null) return;
            var go = new GameObject("GameAudio");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<GameAudio>();
            s_instance.Init(bank);
        }

        void Init(SoundBank bank)
        {
            _bank = bank;
            for (int i = 0; i < SfxVoices; i++) _voices.Add(NewSource($"Sfx{i}", null));
            for (int i = 0; i < 2; i++)
            {
                _music[i] = NewSource($"Music{i}", bank.musicGroup);
                _music[i].loop = true;
                _music[i].volume = 0f;
            }
            ApplyVolumes(UISettings.Load());
            SceneManager.sceneLoaded += (_, _) => PickMusic();
            PickMusic();
        }

        AudioSource NewSource(string name, UnityEngine.Audio.AudioMixerGroup group)
        {
            var source = new GameObject(name).AddComponent<AudioSource>();
            source.transform.SetParent(transform, false);
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = group;
            return source;
        }

        // ---------------------------------------------------------------- volume (settings sliders)

        // Slider 0..1 -> gain (squared so the whole travel is audible) -> dB on the mixer.
        public static void ApplyVolumes(UISettings settings)
        {
            if (s_instance == null || s_instance._bank.mixer == null) return;
            var mixer = s_instance._bank.mixer;
            mixer.SetFloat(MasterParam, ToDb(settings.masterVolume));
            mixer.SetFloat(MusicParam, ToDb(settings.musicVolume));
            mixer.SetFloat(SfxParam, ToDb(settings.sfxVolume));
            mixer.SetFloat(UiParam, ToDb(settings.uiVolume));
        }

        static float ToDb(float slider) => 20f * Mathf.Log10(Mathf.Max(slider * slider, 0.0001f));

        // ---------------------------------------------------------------- one-shots

        public static void Play(SoundBank.Sound sound, bool ui = false)
        {
            if (s_instance == null || sound == null || !sound.IsSet) return;
            s_instance.PlayInternal(sound, ui);
        }

        public static void Play(System.Func<SoundBank, SoundBank.Sound> pick, bool ui = false)
        {
            if (s_instance != null) Play(pick(s_instance._bank), ui);
        }

        public static void PlayHero(string hero, System.Func<SoundBank.HeroSounds, SoundBank.Sound> pick)
        {
            var sounds = Bank != null ? Bank.Hero(hero) : null;
            if (sounds != null) Play(pick(sounds));
        }

        void PlayInternal(SoundBank.Sound sound, bool ui)
        {
            float now = Time.unscaledTime;
            if (_lastPlayed.TryGetValue(sound, out float last) && now - last < sound.minInterval) return;
            _lastPlayed[sound] = now;

            // Pick a different take than last time when there are several.
            int index = Random.Range(0, sound.clips.Length);
            if (sound.clips.Length > 1 && _lastClip.TryGetValue(sound, out int previous) && index == previous)
                index = (index + 1) % sound.clips.Length;
            _lastClip[sound] = index;
            var clip = sound.clips[index];
            if (clip == null) return;

            // Round-robin over a fixed pool: the oldest voice is reused once all are busy.
            var voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Count;
            voice.outputAudioMixerGroup = ui ? _bank.uiGroup : _bank.sfxGroup;
            voice.clip = clip;
            voice.pitch = 1f + Random.Range(-sound.pitchJitter, sound.pitchJitter);
            voice.volume = sound.volume * Random.Range(0.9f, 1f);
            voice.Play();
        }

        // A sound held for as long as something lasts; stop it with StopLoop.
        public static AudioSource PlayLoop(SoundBank.Sound sound)
        {
            if (s_instance == null || sound == null || !sound.IsSet) return null;
            var clip = sound.clips[Random.Range(0, sound.clips.Length)];
            if (clip == null) return null;
            var source = s_instance.NewSource("Loop", s_instance._bank.sfxGroup);
            source.clip = clip;
            source.loop = true;
            source.volume = sound.volume;
            source.Play();
            return source;
        }

        public static void PlayHeroLoop(string hero, System.Func<SoundBank.HeroSounds, SoundBank.Sound> pick, out AudioSource source)
        {
            var sounds = Bank != null ? Bank.Hero(hero) : null;
            source = sounds != null ? PlayLoop(pick(sounds)) : null;
        }

        public static void StopLoop(AudioSource source)
        {
            if (source == null) return;
            if (s_instance == null || !s_instance.isActiveAndEnabled) { Destroy(source.gameObject); return; }
            s_instance.StartCoroutine(s_instance.FadeOut(source, 0.15f));
        }

        IEnumerator FadeOut(AudioSource source, float seconds)
        {
            float start = source.volume;
            for (float t = 0f; t < seconds && source != null; t += Time.unscaledDeltaTime)
            {
                source.volume = Mathf.Lerp(start, 0f, t / seconds);
                yield return null;
            }
            if (source != null) Destroy(source.gameObject);
        }

        // ---------------------------------------------------------------- music

        void PickMusic()
        {
            string scene = SceneManager.GetActiveScene().name;
            AudioClip clip = _bank.stageMusic;
            if (scene == "MainMenu") clip = _bank.menuMusic;
            else
            {
                foreach (var enemy in FindObjectsByType<EnemyController>())
                    if (enemy.isBoss) { clip = _bank.bossMusic; break; }
            }
            PlayMusic(clip);
        }

        public static void PlayMusic(AudioClip clip)
        {
            if (s_instance == null) return;
            var current = s_instance._music[s_instance._activeMusic];
            if (current.clip == clip && current.isPlaying) return;
            if (s_instance._fade != null) s_instance.StopCoroutine(s_instance._fade);
            s_instance._fade = s_instance.StartCoroutine(s_instance.Crossfade(clip));
        }

        IEnumerator Crossfade(AudioClip clip)
        {
            var from = _music[_activeMusic];
            _activeMusic = 1 - _activeMusic;
            var to = _music[_activeMusic];
            to.clip = clip;
            to.volume = 0f;
            if (clip != null) to.Play();
            float start = from.volume, seconds = Mathf.Max(0.05f, _bank.musicFadeSeconds);
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = t / seconds;
                from.volume = Mathf.Lerp(start, 0f, k);
                to.volume = Mathf.Lerp(0f, _bank.musicVolume, k);
                yield return null;
            }
            from.volume = 0f;
            from.Stop();
            to.volume = _bank.musicVolume;
            _fade = null;
        }

        // Dips the MusicDuck group (not the player's Music slider) and eases it back.
        public static void DuckMusic(float seconds)
        {
            if (s_instance == null || s_instance._bank.mixer == null) return;
            if (s_instance._duck != null) s_instance.StopCoroutine(s_instance._duck);
            s_instance._duck = s_instance.StartCoroutine(s_instance.Duck(seconds));
        }

        IEnumerator Duck(float hold)
        {
            var mixer = _bank.mixer;
            float target = _bank.ultimateDuckDb;
            mixer.GetFloat(DuckParam, out float db);
            for (float t = 0f; t < 0.15f; t += Time.unscaledDeltaTime)
            {
                mixer.SetFloat(DuckParam, Mathf.Lerp(db, target, t / 0.15f));
                yield return null;
            }
            mixer.SetFloat(DuckParam, target);
            yield return new WaitForSecondsRealtime(hold);
            for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
            {
                mixer.SetFloat(DuckParam, Mathf.Lerp(target, 0f, t / 0.6f));
                yield return null;
            }
            mixer.SetFloat(DuckParam, 0f);
            _duck = null;
        }
    }
}
