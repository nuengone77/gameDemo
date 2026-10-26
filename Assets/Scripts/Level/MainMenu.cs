using UnityEngine;

namespace MagicGame
{
    // The menu scene supplies hero assets; the shared uGUI prefab owns presentation and navigation.
    public class MainMenu : MonoBehaviour
    {
        public CharacterAnimSet[] characters;
        public string firstLevelScene = "Level1";

        void Start() => GameUI.EnsureForScene();
    }
}
