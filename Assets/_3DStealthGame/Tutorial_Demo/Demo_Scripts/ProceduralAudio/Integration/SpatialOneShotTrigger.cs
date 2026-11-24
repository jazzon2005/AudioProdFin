using UnityEngine;

namespace StealthGame.Audio
{
    /// <summary>
    /// Utilidad estática para reproducir un sonido procedural 3D en una posición exacta
    /// cuando el objeto emisor va a ser destruido inmediatamente (ej. al abrir una puerta).
    /// </summary>
    public static class SpatialOneShotTrigger
    {
        public static void PlayAtPoint(ProceduralAudioSO preset, Vector3 position, float minDist = 2f, float maxDist = 15f)
        {
            if (preset == null) return;

            GameObject tempObj = new GameObject($"TempSpatialAudio_{preset.name}");
            tempObj.transform.position = position;

            ProceduralSpatialEmitter emitter = tempObj.AddComponent<ProceduralSpatialEmitter>();
            emitter.preset = preset;
            emitter.minDistance = minDist;
            emitter.maxDistance = maxDist;
            emitter.PlayOneShot();

            // Calcular duración total de la envolvente ADSR para destruir el objeto limpiamente al terminar
            float totalDurationSec = (preset.attack + preset.decay + preset.sustainTime + preset.release) / 1000f;
            Object.Destroy(tempObj, Mathf.Max(0.2f, totalDurationSec + 0.1f));
        }
    }
}