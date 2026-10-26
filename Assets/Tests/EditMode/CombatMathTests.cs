using System.Collections.Generic;
using System.Reflection;
using System;
using System.IO;
using System.Linq;
using Object = UnityEngine.Object;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MagicGame.Tests
{
    public class CombatMathTests
    {
        readonly List<GameObject> _objects = new();
        Health Actor(Vector2 point = default)
        {
            var go = new GameObject("DamageTest");
            _objects.Add(go);
            go.transform.position = point;
            var health = go.AddComponent<Health>();
            health.destroyOnDeath = false;
            // Edit Mode does not run MonoBehaviour lifecycle callbacks.
            typeof(Health).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(health, null);
            typeof(StatusEffects).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(health.Status, null);
            return health;
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in _objects) if (go != null) Object.DestroyImmediate(go);
            _objects.Clear();
            foreach (var vfx in Object.FindObjectsByType<SkillVfx>()) Object.DestroyImmediate(vfx.gameObject);
        }

        [Test]
        public void ShieldAbsorbsBeforeHpAndClampsToCapacity()
        {
            var health = Actor();
            health.ShieldCapacity = 20f;
            health.AddShield(200f);
            var result = health.TakeDamage(new DamageInfo(30f));
            Assert.That(result.shieldDamage, Is.EqualTo(20f));
            Assert.That(result.hpDamage, Is.EqualTo(10f));
            Assert.That(health.CurrentHp, Is.EqualTo(90f));
        }

        [Test]
        public void DamageResultExcludesOverkill()
        {
            var result = Actor().TakeDamage(new DamageInfo(900f));
            Assert.That(result.Total, Is.EqualTo(100f));
        }

        [Test]
        public void PenetrationAndBypassUseSameDamagePath()
        {
            var health = Actor();
            health.magicDefense = 100f;
            Assert.That(health.TakeDamage(new DamageInfo(20f)).Total, Is.EqualTo(10f).Within(0.01f));
            Assert.That(health.TakeDamage(new DamageInfo(20f) { penetration = 0.5f }).Total, Is.EqualTo(20f / 1.5f).Within(0.01f));
            Assert.That(health.TakeDamage(new DamageInfo(20f) { bypassDefense = true }).Total, Is.EqualTo(20f));
        }

        [Test]
        public void PhysicalAndMagicDefenseAreIndependent()
        {
            var health = Actor();
            health.physicalDefense = 100f;
            Assert.That(health.TakeDamage(new DamageInfo(20f, null, DamageType.Physical)).Total, Is.EqualTo(10f));
            Assert.That(health.TakeDamage(new DamageInfo(20f)).Total, Is.EqualTo(20f));
        }

        [Test]
        public void DistanceDefenseUsesAttackerAndCapsAtThirtyPercent()
        {
            var target = Actor();
            var attacker = Actor(new Vector2(12f, 0f));
            target.DistanceReduction = 0.3f;
            target.DefenseDistance = 6f;
            Assert.That(target.TakeDamage(new DamageInfo(20f, attacker)).Total, Is.EqualTo(14f).Within(0.01f));
        }

        [Test]
        public void GuaranteedHitsBypassEvasionButNotInvulnerability()
        {
            var target = Actor();
            target.Evasion = 1f;
            Assert.That(target.TakeDamage(new DamageInfo(10f)).dodged, Is.True);
            target.invulnerableTime = 10f;
            Assert.That(target.TakeDamage(new DamageInfo(10f) { guaranteed = true }).Total, Is.EqualTo(10f));
            Assert.That(target.TakeDamage(new DamageInfo(10f) { guaranteed = true }).Total, Is.Zero);
        }

        [Test]
        public void SameSourceBuffRefreshesAndDifferentSlowsUseStrongest()
        {
            var health = Actor();
            var other = Actor();
            health.Status.Apply(StatusKind.Attack, 0.2f, 4f, health);
            health.Status.Apply(StatusKind.Attack, 0.2f, 4f, health);
            Assert.That(health.Status.AttackMultiplier, Is.EqualTo(1.2f).Within(0.01f));
            health.Status.Apply(StatusKind.Slow, 0.2f, 4f, health);
            health.Status.Apply(StatusKind.Slow, 0.6f, 4f, other);
            Assert.That(health.Status.MoveMultiplier, Is.EqualTo(0.4f).Within(0.01f));
            health.Status.Clear();
            Assert.That(health.Status.MoveMultiplier, Is.EqualTo(1f));
            Assert.That(health.Status.AttackMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void HealingCannotOverhealOrResurrect()
        {
            var health = Actor();
            health.TakeDamage(10f);
            health.Heal(30f);
            Assert.That(health.CurrentHp, Is.EqualTo(100f));
            health.TakeDamage(1000f);
            health.Heal(100f);
            Assert.That(health.IsDead, Is.True);
            Assert.That(health.CurrentHp, Is.Zero);
        }

        [TestCase("New", HeroRole.Tanky)]
        [TestCase("Nueng", HeroRole.Shadow)]
        [TestCase("Peng", HeroRole.Magician)]
        [TestCase("Ling", HeroRole.OracleWitch)]
        [TestCase("Nae", HeroRole.Daphne)]
        public void PersistedHeroMappingMatchesApprovedRoles(string name, HeroRole role)
        {
            var set = AssetDatabase.LoadAssetAtPath<CharacterAnimSet>($"Assets/Game/AnimSets/{name}.asset");
            Assert.That(set, Is.Not.Null);
            Assert.That(set.combat, Is.Not.Null);
            Assert.That(set.combat.role, Is.EqualTo(role));
            Assert.That(set.MenuName, Does.Contain(name));
            Assert.That(set.attackProjectile, Is.Not.Null);
        }

        [Test]
        public void RepeatedSkillSetupPreservesTuningGuidsAndAuthoredScenes()
        {
            var paths = Directory.GetFiles("Assets/Game/CombatConfigs", "*.asset")
                .Concat(Directory.GetFiles("Assets/Game/AnimSets", "*.asset"))
                .Concat(Directory.GetFiles("Assets/Game/Prefabs", "*.prefab"))
                .Concat(Directory.GetFiles("Assets/Scenes", "*.unity"))
                .ToArray();
            var before = paths.ToDictionary(path => path, File.ReadAllText);
            var setup = Type.GetType("MagicGame.EditorTools.CharacterSkillSetup, Assembly-CSharp-Editor");
            Assert.That(setup, Is.Not.Null);
            var update = setup.GetMethod("UpdateExistingContent");
            update.Invoke(null, null);
            update.Invoke(null, null);
            foreach (var path in paths) Assert.That(File.ReadAllText(path), Is.EqualTo(before[path]), path);
        }
    }
}
