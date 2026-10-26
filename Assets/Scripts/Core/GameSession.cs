using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MagicGame
{
    // Survives scene loads: which hero the player picked in the main menu.
    public static class GameSession
    {
        static int _selectedCharacter;
        public static int SelectedCharacter
        {
            get => _selectedCharacter;
            set { _selectedCharacter = value; SelectedHeroId = null; }
        }
        public static string SelectedHeroId;

        public const string CheckpointKey = "MagicGame.Checkpoint.v1";
        public const string TutorialKey = "MagicGame.TutorialSeen";
        public static readonly string[] HeroIds = { "New", "Nueng", "Peng", "Ling", "Nae" };

        [Serializable]
        public class Checkpoint
        {
            public int version = 1;
            public string scene;
            public string hero;
        }

        public static bool TryDecodeCheckpoint(string json, out Checkpoint checkpoint)
        {
            checkpoint = null;
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                var candidate = JsonUtility.FromJson<Checkpoint>(json);
                if (candidate == null || candidate.version != 1 || Array.IndexOf(HeroIds, candidate.hero) < 0 ||
                    !int.TryParse(candidate.scene?.Replace("Level", ""), out int stage) || stage < 1 || stage > 7 ||
                    candidate.scene != $"Level{stage}") return false;
                checkpoint = candidate;
                return true;
            }
            catch (ArgumentException) { return false; }
        }

        public static bool TryGetCheckpoint(out Checkpoint checkpoint)
        {
            if (TryDecodeCheckpoint(PlayerPrefs.GetString(CheckpointKey, ""), out checkpoint) &&
                Application.CanStreamedLevelBeLoaded(checkpoint.scene)) return true;
            if (PlayerPrefs.HasKey(CheckpointKey)) ClearCheckpoint();
            checkpoint = null;
            return false;
        }

        public static void SaveCheckpoint(string scene, string hero)
        {
            var json = JsonUtility.ToJson(new Checkpoint { scene = scene, hero = hero });
            if (!TryDecodeCheckpoint(json, out _)) return;
            PlayerPrefs.SetString(CheckpointKey, json);
            PlayerPrefs.Save();
        }

        public static void ClearCheckpoint()
        {
            PlayerPrefs.DeleteKey(CheckpointKey);
            PlayerPrefs.Save();
        }

        public static void Load(string scene)
        {
            GameplayInput.Reset();
            Time.timeScale = 1f;
            SceneManager.LoadScene(scene);
        }
    }
}
