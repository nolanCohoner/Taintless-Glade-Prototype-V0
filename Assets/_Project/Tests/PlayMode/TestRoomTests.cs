using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TaintlessGlade.Tests.PlayMode
{
    public class TestRoomTests
    {
        const string SceneName = "TestRoom";
        const int WarmupFrames = 30;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync(SceneName);
            for (int i = 0; i < WarmupFrames; i++) yield return null;
        }

        static PlayerHealth FindPlayerHealth()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player, "Aucun PlayerController dans la scène");
            Assert.IsNotNull(player.stats, "Le PlayerController n'a pas de CharacterStats");
            var health = player.GetComponent<PlayerHealth>();
            Assert.IsNotNull(health, "PlayerHealth n'a pas été ajouté au joueur");
            return health;
        }

        [UnityTest]
        public IEnumerator Scene_runs_without_errors_and_contains_a_player_and_dummies()
        {
            Assert.IsNotNull(Object.FindFirstObjectByType<PlayerController>());
            Assert.IsNotEmpty(Object.FindObjectsByType<TrainingDummy>());
            yield return null;
        }

        [UnityTest]
        public IEnumerator Player_starts_with_full_health()
        {
            var health = FindPlayerHealth();
            Assert.AreEqual(health.Max, health.Current, 1e-4f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Player_health_drops_and_never_goes_below_zero()
        {
            var health = FindPlayerHealth();
            float before = health.Current;

            health.Hurt(1f);
            Assert.AreEqual(Mathf.Max(0f, before - 1f), health.Current, 1e-4f);

            health.Hurt(1000f);
            Assert.AreEqual(0f, health.Current, 1e-4f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Player_heal_restores_health_and_raises_event()
        {
            var health = FindPlayerHealth();
            health.Hurt(1f);

            int events = 0;
            health.Changed += (_, _) => events++;
            health.Heal();

            Assert.AreEqual(health.Max, health.Current, 1e-4f);
            Assert.AreEqual(1, events);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Player_ignores_non_positive_damage()
        {
            var health = FindPlayerHealth();
            float before = health.Current;
            health.Hurt(0f);
            health.Hurt(-3f);
            Assert.AreEqual(before, health.Current, 1e-4f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Training_dummies_are_immortal_and_absorb_any_damage()
        {
            var dummies = Object.FindObjectsByType<TrainingDummy>();
            Assert.IsNotEmpty(dummies);

            var info = new DamageInfo
            {
                amount = 9999f,
                point = Vector3.zero,
                direction = Vector3.forward,
                knockback = 2f
            };

            foreach (var dummy in dummies)
            {
                IDamageable damageable = dummy;
                for (int i = 0; i < 25; i++) damageable.TakeDamage(info);
            }

            yield return null;
            foreach (var dummy in dummies)
                Assert.IsTrue(dummy != null && dummy.gameObject.activeInHierarchy, "Un pantin d'entraînement a été détruit");
        }
    }
}
