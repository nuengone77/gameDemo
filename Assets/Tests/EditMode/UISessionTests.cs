using NUnit.Framework;
using UnityEngine;

namespace MagicGame.Tests
{
    public class UISessionTests
    {
        [TestCase("Level1", "New")]
        [TestCase("Level7", "Nae")]
        [TestCase("Level4", "Ling")]
        public void CheckpointRestoresStableHeroAndStageIds(string scene, string hero)
        {
            var json = JsonUtility.ToJson(new GameSession.Checkpoint { scene = scene, hero = hero });
            Assert.That(GameSession.TryDecodeCheckpoint(json, out var checkpoint), Is.True);
            Assert.That(checkpoint.scene, Is.EqualTo(scene));
            Assert.That(checkpoint.hero, Is.EqualTo(hero));
        }

        [TestCase("")]
        [TestCase("broken json")]
        [TestCase("{}")]
        [TestCase("{\"version\":2,\"scene\":\"Level1\",\"hero\":\"New\"}")]
        [TestCase("{\"version\":1,\"scene\":\"Level8\",\"hero\":\"New\"}")]
        [TestCase("{\"version\":1,\"scene\":\"Level01\",\"hero\":\"New\"}")]
        [TestCase("{\"version\":1,\"scene\":\"Level1\",\"hero\":\"Missing\"}")]
        public void InvalidCheckpointsCannotBeContinued(string json)
        {
            Assert.That(GameSession.TryDecodeCheckpoint(json, out var checkpoint), Is.False);
            Assert.That(checkpoint, Is.Null);
        }
    }
}
