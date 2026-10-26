using TMPro;
using UnityEngine;

namespace MagicGame
{
    [CreateAssetMenu(menuName = "Magic Game/Arcane UI Theme")]
    public class ArcaneTheme : ScriptableObject
    {
        public Sprite panel;
        public Sprite ring;
        public Sprite disc;
        public Sprite village;
        public Sprite[] symbols;
        public CharacterAnimSet[] heroes;
        public TMP_FontAsset bodyFont;
        public TMP_FontAsset titleFont;

        public static readonly Color Navy = new Color32(24, 35, 55, 255);
        public static readonly Color Gold = new Color32(200, 163, 99, 255);
        public static readonly Color Paper = new Color32(243, 231, 207, 255);
        public static readonly Color Cyan = new Color32(117, 217, 232, 255);
        public static readonly Color Muted = new Color32(172, 185, 199, 255);
        public static readonly Color[] HeroColors =
        {
            new Color32(125, 221, 241, 255), new Color32(193, 151, 246, 255),
            new Color32(245, 159, 152, 255), new Color32(130, 210, 183, 255), new Color32(245, 191, 105, 255)
        };
    }
}
