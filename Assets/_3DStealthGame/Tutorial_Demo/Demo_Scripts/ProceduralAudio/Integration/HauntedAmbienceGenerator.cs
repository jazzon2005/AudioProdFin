using UnityEngine;

namespace StealthGame.Audio
{
    /// <summary>
    /// Genera sonidos ambientales procedurales (viento, crujidos de madera) en puntos 3D aleatorios
    /// alrededor del jugador para construir la atmósfera de la casa embrujada.
    /// </summary>
    public class HauntedAmbienceGenerator : MonoBehaviour
    {
        [Header("Presets Ambientales")]
        [Tooltip("Asigna aquí los ScriptableObjects de viento, crujidos o susurros.")]
        public ProceduralAudioSO[] ambientPresets;

        [Header("Frecuencia y Distancia")]
        public float minIntervalSeconds = 4f;
        public float maxIntervalSeconds = 9f;
        public float spawnRadius = 5f;
        public bool debugLogs = false;

        private Transform _playerTransform;
        private ProceduralSpatialEmitter _ambientEmitter;
        private float _timer = 1.5f; // Primer sonido rápido al iniciar para verificar funcionamiento

        private void Start()
        {
            // Creamos un emisor 3D dedicado exclusivamente para los sonidos ambientales
            GameObject emitterObj = new GameObject("Ambient_3D_Emitter");
            emitterObj.transform.SetParent(transform);
            _ambientEmitter = emitterObj.AddComponent<ProceduralSpatialEmitter>();
            _ambientEmitter.minDistance = 4f;
            _ambientEmitter.maxDistance = 25f;
        }

        private void Update()
        {
            if (ambientPresets == null || ambientPresets.Length == 0) return;

            // Búsqueda dinámica por si AudioSystem viene desde MainMenu via DontDestroyOnLoad
            if (_playerTransform == null)
            {
                var player = FindFirstObjectByType<PlayerMovement>();
                if (player != null)
                {
                    _playerTransform = player.transform;
                    _timer = 1.5f; 
                }
                else
                {
                    return; // Aún estamos en el menú principal
                }
            }

            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                TriggerRandomAmbientSound();
                ResetTimer();
            }
        }

        private void TriggerRandomAmbientSound()
        {
            // Construir lista solo con los presets asignados (ignorando ranuras vacías/None)
            var validPresets = new System.Collections.Generic.List<ProceduralAudioSO>();
            foreach (var p in ambientPresets)
            {
                if (p != null) validPresets.Add(p);
            }

            if (validPresets.Count == 0) return;

            ProceduralAudioSO chosenPreset = validPresets[Random.Range(0, validPresets.Count)];

            // Posicionar el emisor en un ángulo aleatorio alrededor del jugador (garantizando distancia válida)
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float dist = Random.Range(1.5f, spawnRadius);
            Vector3 offset = new Vector3(Mathf.Cos(angle) * dist, 1.5f, Mathf.Sin(angle) * dist);
            Vector3 randomPos = _playerTransform.position + offset;

            _ambientEmitter.transform.position = randomPos;
            _ambientEmitter.preset = chosenPreset;

            // Variar ligeramente el tono (pitch) cada vez para que nunca suene repetitivo
            float randomPitch = chosenPreset.baseFrequency * Random.Range(0.85f, 1.15f);
            _ambientEmitter.PlayOneShot(frequencyOverride: randomPitch, volumeScale: 1f);

            if (debugLogs)
            {
                Debug.Log($"[Ambience] Reproduciendo '{chosenPreset.name}' a {dist:F1}m del jugador.");
            }
        }

        private void ResetTimer()
        {
            _timer = Random.Range(minIntervalSeconds, maxIntervalSeconds);
        }
    }
}