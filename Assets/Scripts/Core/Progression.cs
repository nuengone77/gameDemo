using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicGame
{
    public enum RunPhase { Starter, Fighting, Reward, Cleared }
    public enum RebirthStat { Hp, Damage, Armor }

    [Serializable] public class CardStack { public string id; public int level; }
    [Serializable] public class HeroProgress
    {
        public string hero;
        public long points;
        public int rebirths, hpUpgrades, damageUpgrades, armorUpgrades, bestLoop, bestStage;
        public float hpBonus, damageBonus, armorBonus;
    }
    [Serializable] public class ProgressionRun
    {
        public string id, hero;
        public int loop = 1, stage = 1, seed;
        public long earned;
        public RunPhase phase;
        public List<CardStack> cards = new();
        public List<string> offer = new();
        public List<string> unlockedAtStart = new();
    }
    [Serializable] public class RunResult
    {
        public string hero;
        public int loop, stage;
        public long earned;
        public bool abandoned;
    }
    [Serializable] public class ProgressionSave
    {
        public int version = 1;
        public bool hasRun;
        public List<HeroProgress> heroes = new();
        public List<string> unlocked = new();
        public ProgressionRun run;
        public RunResult lastResult;
    }

    // One save document commits the run, wallet and unlocks together. A settled run is removed
    // in the same write that credits its wallet, so reloading cannot settle it twice.
    public static class Progression
    {
        public const string SaveKey = "MagicGame.Progression.v1";
        static ProgressionSave _data;
        public static event Action Changed;
        public static ProgressionSave Data => _data ??= Read();
        public static ProgressionRun Run => Data.run;
        public static bool HasRun => Run != null;
        public static string Scene => HasRun ? "Level" + Run.stage : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reload() { _data = null; Changed = null; }

        static bool IsHero(string hero) => Array.IndexOf(GameSession.HeroIds, hero) >= 0;
        static ProgressionSave Read()
        {
            ProgressionSave data = null;
            try { data = JsonUtility.FromJson<ProgressionSave>(PlayerPrefs.GetString(SaveKey, "")); }
            catch (ArgumentException) { }
            // Unity serializes inline null classes as default objects. The explicit presence
            // flag distinguishes a settled run from a default-constructed deserialization.
            if (data != null && !data.hasRun) data.run = null;
            if (data?.lastResult != null && !IsHero(data.lastResult.hero)) data.lastResult = null;
            if (!Valid(data)) data = new ProgressionSave();
            return data;
        }

        public static bool Valid(ProgressionSave data)
        {
            if (data == null || data.version != 1 || data.heroes == null || data.unlocked == null) return false;
            var ids = new HashSet<string>();
            foreach (var hero in data.heroes)
            {
                if (hero == null || !IsHero(hero.hero) || !ids.Add(hero.hero) || hero.points < 0 ||
                    hero.rebirths < 0 || hero.hpUpgrades < 0 || hero.damageUpgrades < 0 || hero.armorUpgrades < 0 ||
                    hero.hpUpgrades + (long)hero.damageUpgrades + hero.armorUpgrades != hero.rebirths ||
                    !Finite(hero.hpBonus) || !Finite(hero.damageBonus) || !Finite(hero.armorBonus)) return false;
            }
            var run = data.run;
            if (run == null) return true;
            if (string.IsNullOrEmpty(run.id) || !IsHero(run.hero) || run.loop < 1 || run.stage < 1 || run.stage > 7 || run.earned < 0 ||
                !Enum.IsDefined(typeof(RunPhase), run.phase) || run.cards == null || run.offer == null || run.unlockedAtStart == null) return false;
            ids.Clear();
            foreach (var stack in run.cards)
            {
                var card = stack == null ? null : ProgressionConfig.Current.Find(stack.id);
                if (card == null || !ids.Add(stack.id) || stack.level < 1 ||
                    (!card.Evergreen && stack.level > card.maxLevel) || !Eligible(card, run)) return false;
            }
            bool choosing = run.phase == RunPhase.Starter || run.phase == RunPhase.Reward;
            if (run.offer.Count != (choosing ? 3 : 0)) return false;
            ids.Clear();
            foreach (var id in run.offer)
            {
                var card = ProgressionConfig.Current.Find(id);
                if (card == null || !ids.Add(id) || !Eligible(card, run) || (!card.Evergreen && Level(run, id) >= card.maxLevel)) return false;
            }
            return true;
        }

        static bool Finite(float value) => value >= 0 && !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Eligible(CardDefinition card, ProgressionRun run) =>
            (string.IsNullOrEmpty(card.hero) || card.hero == run.hero) &&
            !(card.effect == CardEffect.Radius && run.hero == "Nueng") &&
            (card.unlockStage == 0 || run.unlockedAtStart.Contains(card.id));

        public static void Save()
        {
            Data.hasRun = Data.run != null;
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Data));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static HeroProgress Hero(string id)
        {
            if (!IsHero(id)) throw new ArgumentException("Unknown hero", nameof(id));
            var hero = Data.heroes.Find(h => h.hero == id);
            if (hero == null) { hero = new HeroProgress { hero = id }; Data.heroes.Add(hero); }
            return hero;
        }

        public static void Begin(string hero, int stage = 1)
        {
            if (!IsHero(hero) || stage < 1 || stage > 7) throw new ArgumentException("Invalid run start");
            if (HasRun) Finish(true);
            Data.run = new ProgressionRun
            {
                id = Guid.NewGuid().ToString("N"), hero = hero, stage = stage,
                seed = Guid.NewGuid().GetHashCode(), phase = RunPhase.Starter,
                unlockedAtStart = new List<string>(Data.unlocked)
            };
            MakeOffer();
            PlayerPrefs.DeleteKey(GameSession.CheckpointKey);
            Save();
        }

        public static bool MigrateLegacy()
        {
            if (HasRun) return true;
            if (!GameSession.TryDecodeCheckpoint(PlayerPrefs.GetString(GameSession.CheckpointKey, ""), out var old)) return false;
            Begin(old.hero, int.Parse(old.scene.Substring(5)));
            return true;
        }

        public static void EnsureStage(string hero, int stage)
        {
            if (HasRun && Run.hero == hero && Run.stage == stage) return;
            if (!HasRun && MigrateLegacy() && Run.hero == hero && Run.stage == stage) return;
            Begin(hero, stage);
        }

        public static int Level(ProgressionRun run, string id) => run?.cards.Find(c => c.id == id)?.level ?? 0;
        public static float Strength(ProgressionRun run, CardDefinition card)
        {
            int level = Level(run, card.id);
            if (!card.Evergreen) return level;
            // Exact running sums are retained through 4096; a continuous tail avoids O(level)
            // work for very long runs while retaining diminishing, unbounded growth.
            float sum = 0;
            int exact = Math.Min(level, 4096);
            for (int n = 1; n <= exact; n++) sum += 1f / Mathf.Sqrt(n);
            if (level > exact) sum += 2f * (Mathf.Sqrt(level + .5f) - Mathf.Sqrt(exact + .5f));
            return sum;
        }

        static void MakeOffer()
        {
            var run = Run;
            var pool = ProgressionConfig.Current.cards.FindAll(c => c != null && Eligible(c, run) &&
                (c.Evergreen || Level(run, c.id) < c.maxLevel));
            int salt = run.loop * 97 + run.stage * 13 + (int)run.phase;
            var random = new System.Random(unchecked(run.seed + salt));
            var evergreen = pool.FindAll(c => c.Evergreen);
            var first = evergreen[random.Next(evergreen.Count)];
            run.offer.Clear();
            run.offer.Add(first.id);
            pool.Remove(first);
            while (run.offer.Count < 3)
            {
                int index = random.Next(pool.Count);
                run.offer.Add(pool[index].id);
                pool.RemoveAt(index);
            }
        }

        public static bool Choose(string id)
        {
            var run = Run;
            if (run == null || (run.phase != RunPhase.Starter && run.phase != RunPhase.Reward) || !run.offer.Contains(id)) return false;
            var stack = run.cards.Find(c => c.id == id);
            if (stack == null) run.cards.Add(new CardStack { id = id, level = 1 });
            else stack.level++;
            run.phase = run.phase == RunPhase.Starter ? RunPhase.Fighting : RunPhase.Cleared;
            run.offer.Clear();
            Save();
            return true;
        }

        public static bool ClearStage()
        {
            var run = Run;
            if (run == null || run.phase != RunPhase.Fighting) return false;
            run.earned = ProgressionConfig.SafePoints((double)run.earned + ProgressionConfig.Current.StageReward(run.stage, run.loop));
            foreach (var card in ProgressionConfig.Current.cards)
                if (card.unlockStage == run.stage && !Data.unlocked.Contains(card.id)) Data.unlocked.Add(card.id);
            run.phase = RunPhase.Reward;
            var hero = Hero(run.hero);
            if (run.loop > hero.bestLoop || (run.loop == hero.bestLoop && run.stage > hero.bestStage))
            { hero.bestLoop = run.loop; hero.bestStage = run.stage; }
            MakeOffer();
            Save();
            return true;
        }

        public static bool Advance()
        {
            if (Run == null || Run.phase != RunPhase.Cleared) return false;
            if (Run.stage == 7) { Run.stage = 1; Run.loop++; }
            else Run.stage++;
            Run.phase = RunPhase.Fighting;
            Save();
            return true;
        }

        public static bool Finish(bool abandoned = false)
        {
            var run = Run;
            if (run == null) return false;
            var hero = Hero(run.hero);
            hero.points = ProgressionConfig.SafePoints((double)hero.points + run.earned);
            Data.lastResult = new RunResult { hero = run.hero, loop = run.loop, stage = run.stage, earned = run.earned, abandoned = abandoned };
            Data.run = null;
            PlayerPrefs.DeleteKey(GameSession.CheckpointKey);
            Save();
            return true;
        }

        public static bool Rebirth(string id, RebirthStat stat)
        {
            if (!Enum.IsDefined(typeof(RebirthStat), stat) || (HasRun && Run.hero == id)) return false;
            var hero = Hero(id);
            var config = ProgressionConfig.Current;
            long cost = config.RebirthCost(hero.rebirths);
            if (hero.points < cost || hero.rebirths == int.MaxValue) return false;
            hero.points -= cost;
            hero.rebirths++;
            switch (stat)
            {
                case RebirthStat.Hp: hero.hpBonus += config.rebirthHp / Mathf.Sqrt(++hero.hpUpgrades); break;
                case RebirthStat.Damage: hero.damageBonus += config.rebirthDamage / Mathf.Sqrt(++hero.damageUpgrades); break;
                case RebirthStat.Armor: hero.armorBonus += config.rebirthArmor / Mathf.Sqrt(++hero.armorUpgrades); break;
            }
            Save();
            return true;
        }
    }
}
