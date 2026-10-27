using System;
using UnityEngine;
using UnityEngine.Audio;

namespace MagicGame
{
    // Every sound the game plays, in one asset (Resources/Audio/SoundBank). Swap a placeholder by
    // dragging a new clip into its slot; several clips in one slot are picked at random.
    [CreateAssetMenu(menuName = "Magic Game/Sound Bank")]
    public class SoundBank : ScriptableObject
    {
        [Header("Mixer (Master -> Music -> MusicDuck, SFX, UI)")]
        public AudioMixer mixer;
        public AudioMixerGroup musicGroup, sfxGroup, uiGroup;

        [Serializable]
        public class HeroSounds
        {
            public string hero;
            public Sound basic = new(), active = new(), ultimate = new();
            [Tooltip("When the skill lands rather than when it is cast (e.g. a trap going off).")]
            public Sound activeImpact = new();
            [Tooltip("When the ultimate lands (meteor, kunai, Nae's eclipse ring) rather than when it is cast.")]
            public Sound ultimateImpact = new();
            [Tooltip("Looped while the ultimate builds or lasts (Peng's card storm, Nae's orbs drifting together).")]
            public Sound ultimateLoop = new();
        }

        [Serializable]
        public class Sound
        {
            public AudioClip[] clips = new AudioClip[0];
            [Range(0f, 1f)] public float volume = 1f;
            [Tooltip("Random pitch spread, e.g. 0.06 = up to 6% higher or lower each play.")]
            [Range(0f, 0.3f)] public float pitchJitter = 0.05f;
            [Tooltip("Ignore new plays of this sound closer together than this (stops stacking when many hit at once).")]
            public float minInterval = 0.04f;
            public bool IsSet => clips != null && clips.Length > 0;
        }

        [Header("Heroes")]
        public HeroSounds[] heroes = new HeroSounds[0];
        public Sound playerHurt = new(), playerDeath = new();

        [Header("Enemies")]
        public Sound enemyHit = new(), enemyDeath = new(), enemyMelee = new();
        public Sound skeletonArrow = new(), golemRock = new(), dragonFire = new(), witchCast = new();

        [Header("Status")]
        public Sound burn = new(), rooted = new();

        [Header("Stage")]
        public Sound portalOpen = new(), warp = new(), stageClear = new(), gameOver = new(), victory = new();

        [Header("UI")]
        public Sound uiClick = new(), uiSelect = new();

        [Header("Music (looped, crossfaded)")]
        public AudioClip menuMusic, stageMusic, bossMusic;
        [Range(0f, 1f)] public float musicVolume = 0.7f;
        public float musicFadeSeconds = 1.5f;
        [Tooltip("How far the music dips (dB) under an ultimate.")]
        public float ultimateDuckDb = -10f;

        public HeroSounds Hero(string name)
        {
            foreach (var h in heroes) if (h != null && h.hero == name) return h;
            return null;
        }
    }
}
