using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TaintlessGlade.Tests.EditMode
{
    public class CharacterStatsTests
    {
        CharacterStats _stats;

        [SetUp]
        public void SetUp() => _stats = ScriptableObject.CreateInstance<CharacterStats>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_stats);

        [TestCase(1f)]
        [TestCase(2f)]
        [TestCase(4f)]
        public void MoveSpeed_scales_linearly_with_speed(float speed)
        {
            _stats.speed = speed;
            Assert.AreEqual(speed * _stats.speedToMetersPerSecond, _stats.MoveSpeed, 1e-4f);
        }

        [Test]
        public void Derived_values_follow_conversion_factors()
        {
            _stats.dashSpeed = 3f;
            _stats.shotSpeed = 2f;
            _stats.shotLength = 4f;
            Assert.AreEqual(3f * _stats.dashToMeters, _stats.DashDistance, 1e-4f);
            Assert.AreEqual(2f * _stats.shotSpeedToMetersPerSecond, _stats.ProjectileSpeed, 1e-4f);
            Assert.AreEqual(4f * _stats.shotLengthToMeters, _stats.ProjectileRange, 1e-4f);
        }

        [TestCase(1f, 1f)]
        [TestCase(4f, 0.25f)]
        public void AttackCooldown_is_inverse_of_attack_rate(float perSecond, float expected)
        {
            _stats.attacksPerSecond = perSecond;
            Assert.AreEqual(expected, _stats.AttackCooldown, 1e-4f);
        }

        [TestCase(0f)]
        [TestCase(-5f)]
        public void AttackCooldown_stays_finite_for_invalid_rates(float perSecond)
        {
            _stats.attacksPerSecond = perSecond;
            Assert.IsFalse(float.IsInfinity(_stats.AttackCooldown));
            Assert.IsFalse(float.IsNaN(_stats.AttackCooldown));
            Assert.Greater(_stats.AttackCooldown, 0f);
        }

        [Test]
        public void Every_stats_asset_in_the_project_is_playable()
        {
            string[] guids = AssetDatabase.FindAssets("t:CharacterStats");
            Assert.IsNotEmpty(guids, "Aucun asset CharacterStats dans le projet");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var stats = AssetDatabase.LoadAssetAtPath<CharacterStats>(path);
                Assert.IsNotNull(stats, path);
                // Un personnage sans PV (ex. le Poisson) doit avoir une autre protection, décrite dans bonusHp.
                Assert.IsTrue(stats.maxHp >= 1 || (stats.maxHp == 0 && !string.IsNullOrEmpty(stats.bonusHp)), $"{path}: maxHp (0 PV sans bonusHp)");
                Assert.Greater(stats.speed, 0f, $"{path}: speed");
                Assert.Greater(stats.dashSpeed, 0f, $"{path}: dashSpeed");
                Assert.Greater(stats.shotSpeed, 0f, $"{path}: shotSpeed");
                Assert.Greater(stats.shotLength, 0f, $"{path}: shotLength");
                Assert.Greater(stats.attacksPerSecond, 0f, $"{path}: attacksPerSecond");
                Assert.Greater(stats.speedToMetersPerSecond, 0f, $"{path}: speedToMetersPerSecond");
                Assert.Greater(stats.dashToMeters, 0f, $"{path}: dashToMeters");
                Assert.Greater(stats.shotSpeedToMetersPerSecond, 0f, $"{path}: shotSpeedToMetersPerSecond");
                Assert.Greater(stats.shotLengthToMeters, 0f, $"{path}: shotLengthToMeters");
            }
        }
    }
}
