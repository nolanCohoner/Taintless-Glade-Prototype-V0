using UnityEngine;

namespace TaintlessGlade
{
    [RequireComponent(typeof(Light))]
    public class LightFlicker : MonoBehaviour
    {
        public float amplitude = 0.25f;
        public float speed = 6f;

        Light _light;
        float _baseIntensity;
        float _seed;

        void Awake()
        {
            _light = GetComponent<Light>();
            _baseIntensity = _light.intensity;
            _seed = Random.value * 100f;
        }

        void Update()
        {
            float noise = Mathf.PerlinNoise(_seed, Time.time * speed) * 2f - 1f;
            _light.intensity = _baseIntensity * (1f + noise * amplitude);
        }
    }
}
