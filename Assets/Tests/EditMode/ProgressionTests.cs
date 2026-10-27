using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MagicGame.Tests
{
    public class ProgressionTests
    {
        string _save, _checkpoint;

        [SetUp]
        public void Setup()
        {
            _save = PlayerPrefs.GetString(Progression.SaveKey, "");
            _checkpoint = PlayerPrefs.GetString(GameSession.CheckpointKey, "");
            PlayerPrefs.DeleteKey(Progression.SaveKey);
            PlayerPrefs.DeleteKey(GameSession.CheckpointKey);
            Progression.Reload();
        }

        [TearDown]
        public void Teardown()
        {
            Restore(Progression.SaveKey, _save);
            Restore(GameSession.CheckpointKey, _checkpoint);
            Progression.Reload();
        }

        static void Restore(string key, string value)
        {
            if (value.Length == 0) PlayerPrefs.DeleteKey(key);
            else PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
        }

        static void Choose() => Assert.That(Progression.Choose(Progression.Run.offer[0]), Is.True);

        [Test]
        public void ThreeLoopsPreserveBuildAwardExpectedPointsAndSettleOnlyOnce()
        {
            Progression.Begin("New");
            Choose();
            for (int loop = 1; loop <= 3; loop++)
            for (int stage = 1; stage <= 7; stage++)
            {
                Assert.That(Progression.Run.loop, Is.EqualTo(loop));
                Assert.That(Progression.Run.stage, Is.EqualTo(stage));
                Assert.That(Progression.ClearStage(), Is.True);
                long earned = Progression.Run.earned;
                Assert.That(Progression.ClearStage(), Is.False);
                Assert.That(Progression.Run.earned, Is.EqualTo(earned));
                Choose();
                Assert.That(Progression.Advance(), Is.True);
            }
            Assert.That(Progression.Run.loop, Is.EqualTo(4));
            Assert.That(Progression.Run.cards.Sum(c => c.level), Is.EqualTo(22));
            Assert.That(Progression.Run.earned, Is.EqualTo(125 + 185 + 250));
            Assert.That(Progression.Data.unlocked.Count, Is.EqualTo(3));
            Assert.That(Progression.Finish(), Is.True);
            Assert.That(Progression.Finish(), Is.False);
            Progression.Reload();
            Assert.That(Progression.Run, Is.Null);
            Assert.That(Progression.Hero("New").points, Is.EqualTo(560));
            Assert.That(Progression.Hero("New").bestLoop, Is.EqualTo(3));
        }

        [Test]
        public void ReloadRetainsExactOfferAndClearCannotPayTwice()
        {
            Progression.Begin("Ling");
            string[] starter = Progression.Run.offer.ToArray();
            Progression.Reload();
            Assert.That(Progression.Run.offer, Is.EqualTo(starter));
            Choose();
            Progression.ClearStage();
            string[] reward = Progression.Run.offer.ToArray();
            Progression.Reload();
            Assert.That(Progression.Run.offer, Is.EqualTo(reward));
            Assert.That(Progression.ClearStage(), Is.False);
            Assert.That(Progression.Run.earned, Is.EqualTo(5));
            Choose();
            Progression.Reload();
            Assert.That(Progression.Run.phase, Is.EqualTo(RunPhase.Cleared));
            Assert.That(Progression.Choose(reward[0]), Is.False);
            Assert.That(Progression.Valid(Progression.Data), Is.True);
        }

        [Test]
        public void RebirthCostsIncreaseAndBonusesDiminishPerStatAndStayHeroSpecific()
        {
            var hero = Progression.Hero("New");
            hero.points = 1000;
            Assert.That(ProgressionConfig.Current.RebirthCost(0), Is.EqualTo(30));
            Assert.That(ProgressionConfig.Current.RebirthCost(1), Is.EqualTo(42));
            Assert.That(ProgressionConfig.Current.RebirthCost(2), Is.EqualTo(58));
            Assert.That(Progression.Rebirth("New", RebirthStat.Hp), Is.True);
            Assert.That(hero.hpBonus, Is.EqualTo(.03f).Within(.00001f));
            Assert.That(Progression.Rebirth("New", RebirthStat.Hp), Is.True);
            Assert.That(hero.hpBonus, Is.EqualTo(.03f + .03f / Mathf.Sqrt(2)).Within(.00001f));
            Assert.That(hero.points, Is.EqualTo(928));
            Assert.That(Progression.Hero("Nae").hpBonus, Is.Zero);
            Progression.Reload();
            Assert.That(Progression.Hero("New").rebirths, Is.EqualTo(2));
            Progression.Begin("New");
            Assert.That(Progression.Rebirth("New", RebirthStat.Damage), Is.False);
            Assert.That(Progression.Rebirth("New", (RebirthStat)99), Is.False);
        }

        [Test]
        public void OffersAreUniqueEligibleAndNeverExhaustAfterSpecialCardsAreMaxed()
        {
            Progression.Begin("Peng");
            Choose();
            foreach (var card in ProgressionConfig.Current.cards.Where(c => !c.Evergreen && c.unlockStage == 0 && (c.hero == "" || c.hero == "Peng")))
                Progression.Run.cards.Add(new CardStack { id = card.id, level = card.maxLevel });
            for (int i = 0; i < 40; i++)
            {
                Progression.ClearStage();
                var offer = Progression.Run.offer;
                Assert.That(offer.Distinct().Count(), Is.EqualTo(3));
                Assert.That(offer.All(id => ProgressionConfig.Current.Find(id).Evergreen), Is.True);
                Choose();
                Progression.Advance();
            }
        }

        [Test]
        public void UnlocksEnterNextRunAndAbandonBanksOnlyEarnedPoints()
        {
            Progression.Begin("Nueng", 3);
            Choose();
            Progression.ClearStage();
            Assert.That(Progression.Data.unlocked, Does.Contain("duration"));
            Assert.That(Progression.Run.unlockedAtStart, Does.Not.Contain("duration"));
            Progression.Begin("Nae");
            Assert.That(Progression.Hero("Nueng").points, Is.EqualTo(15));
            Assert.That(Progression.Run.unlockedAtStart, Does.Contain("duration"));
            Progression.Finish();
            Assert.That(Progression.Hero("Nae").points, Is.Zero);
        }

        [TestCase("New")]
        [TestCase("Nueng")]
        [TestCase("Peng")]
        [TestCase("Ling")]
        [TestCase("Nae")]
        public void EachHeroOnlyReceivesUsableCards(string hero)
        {
            Progression.Begin(hero);
            for (int i = 0; i < 30; i++)
            {
                var offer = Progression.Run.offer.Select(id => ProgressionConfig.Current.Find(id)).ToArray();
                Assert.That(offer.All(c => c.hero == "" || c.hero == hero), Is.True);
                Assert.That(offer.Any(c => c.Evergreen), Is.True);
                if (hero == "Nueng") Assert.That(offer.All(c => c.effect != CardEffect.Radius), Is.True);
                Choose();
                if (Progression.Run.phase == RunPhase.Cleared) Progression.Advance();
                Progression.ClearStage();
            }
        }

        [Test]
        public void LegacyCheckpointMigratesWithoutRetroactiveRewards()
        {
            GameSession.SaveCheckpoint("Level5", "Nae");
            Assert.That(Progression.MigrateLegacy(), Is.True);
            Assert.That(Progression.Run.stage, Is.EqualTo(5));
            Assert.That(Progression.Run.loop, Is.EqualTo(1));
            Assert.That(Progression.Run.earned, Is.Zero);
            Assert.That(Progression.Run.phase, Is.EqualTo(RunPhase.Starter));
            Assert.That(PlayerPrefs.HasKey(GameSession.CheckpointKey), Is.False);
        }

        [Test]
        public void CorruptSaveIsRejectedAndLargeLoopArithmeticRemainsFinite()
        {
            PlayerPrefs.SetString(Progression.SaveKey, "{broken");
            Progression.Reload();
            Assert.That(Progression.HasRun, Is.False);
            Progression.Begin("New");
            Progression.Run.offer.Add("unknown");
            Assert.That(Progression.Valid(Progression.Data), Is.False);
            Assert.That(float.IsInfinity(ProgressionConfig.Safe(Mathf.Pow(1.55f, 10000))), Is.False);
            Assert.That(ProgressionConfig.Current.RebirthCost(int.MaxValue), Is.GreaterThan(0));
        }
    }
}
