using UnityEngine;

namespace StealthGame.Audio
{
    /// <summary>
    /// Observa la distancia entre el jugador y los enemigos (Fantasmas y Gárgolas)
    /// para modular dinámicamente el progreso/tensión de ProceduralMusicSystem.
    /// </summary>
    public class StealthTensionController : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Transform del jugador (JohnLemon). Si se deja vacío, lo buscará automáticamente.")]
        public Transform playerTransform;

        [Header("Parámetros de Detección de Tensión")]
        [Tooltip("Distancia a la que empieza a subir la tensión musical.")]
        public float maxDetectionRadius = 10f;

        [Tooltip("Distancia crítica donde la tensión llega al 100% (1.0).")]
        public float minDangerRadius = 2.5f;

        [Tooltip("Suavizado de la transición de tensión.")]
        public float smoothSpeed = 3f;

        private Observer[] _enemies;
        private float _currentTension = 0f;

        private void Start()
        {
            if (playerTransform == null)
            {
                var playerObj = FindFirstObjectByType<PlayerMovement>();
                if (playerObj != null) playerTransform = playerObj.transform;
            }

            // En Haunted House, tanto fantasmas como gárgolas tienen el componente Observer (cono de visión)
            _enemies = FindObjectsByType<Observer>(FindObjectsSortMode.None);
        }

        private void Update()
        {
            if (playerTransform == null || ProceduralMusicSystem.Instance == null) return;

            float closestDistance = float.MaxValue;

            // Buscar el enemigo más cercano al jugador
            if (_enemies != null)
            {
                foreach (var enemy in _enemies)
                {
                    if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;

                    float dist = Vector3.Distance(playerTransform.position, enemy.transform.position);
                    if (dist < closestDistance)
                    {
                        closestDistance = dist;
                    }
                }
            }

            // Calcular tensión normalizada (0 = seguro, 1 = peligro máximo)
            float targetTension = 0f;
            if (closestDistance < maxDetectionRadius)
            {
                targetTension = Mathf.InverseLerp(maxDetectionRadius, minDangerRadius, closestDistance);
            }

            // Suavizar el cambio para que la música acelere de forma orgánica
            _currentTension = Mathf.Lerp(_currentTension, targetTension, Time.deltaTime * smoothSpeed);
            ProceduralMusicSystem.Instance.SetProgress(_currentTension);
        }
    }
}