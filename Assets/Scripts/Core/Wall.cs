using UnityEngine;

namespace MagicGame
{
    // Marks a collider that blocks projectiles.
    public class Wall : MonoBehaviour
    {
        public bool isBoundary = true;
        public bool allowMagicPassage;
    }
}
