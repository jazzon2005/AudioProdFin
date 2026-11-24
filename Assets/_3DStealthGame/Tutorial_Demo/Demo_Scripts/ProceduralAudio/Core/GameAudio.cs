using System.Collections;
using UnityEngine;

namespace StealthGame.Audio
{
    /// <summary>
    /// Controlador principal de audio 2D/Global para Stealth Game: Haunted House.
    /// Gestiona efectos de interfaz, alertas, llaves y la música procedural dinámica.
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        private const string MusicStatusKey = "stealth.music.status";
        private const string SfxStatusKey = "stealth.sfx.status";

        public static GameAudio Instance { get; private set; }

        [Header("Presets Procedurales 2D (Mapeados 1 a 1 con GameClip)")]
        [Tooltip("Asigna en el Inspector un ProceduralAudioSO para cada evento global del juego.")]
        public ProceduralAudioSO[] SfxPresets;

        [Header("Presets Procedurales 3D (Mapeados 1 a 1 con SpatialClip)")]
        [Tooltip("Asigna aquí los sonidos espaciales (pasos, fantasmas, velas, puertas) desde un solo lugar.")]
        public ProceduralAudioSO[] SpatialPresets;

        private bool _musicEnabled = true;
        public bool MusicEnabled
        {
            get => _musicEnabled;
            set
            {
                _musicEnabled = value;
                if (ProceduralMusicSystem.Instance != null)
                {
                    ProceduralMusicSystem.Instance.MusicEnabled = value;
                }
                PlayerPrefs.SetInt(MusicStatusKey, value ? 0 : 1);
            }
        }

        private bool _sfxEnabled = true;
        public bool SfxEnabled
        {
            get => _sfxEnabled;
            set
            {
                _sfxEnabled = value;
                PlayerPrefs.SetInt(SfxStatusKey, value ? 0 : 1);
            }
        }

        private ProceduralAudioSO _fallbackPreset;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                EnsureAudioListener();
                // Desactivamos ValidateAndFixPresets() para que las ranuras vacías (None) queden en silencio
                // ValidateAndFixPresets();
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            SafeInitializePlayerPrefs();
        }

        /// <summary>
        /// Devuelve el ProceduralAudioSO 3D configurado centralmente para que los emisores espaciales lo usen en runtime.
        /// </summary>
        public ProceduralAudioSO GetSpatialPreset(SpatialClip clip)
        {
            int index = (int)clip;
            if (SpatialPresets != null && index >= 0 && index < SpatialPresets.Length && SpatialPresets[index] != null)
            {
                return SpatialPresets[index];
            }
            return _fallbackPreset;
        }

        /// <summary>
        /// Reproduce un efecto de sonido 2D global (UI, Alertas, Llaves, Victoria/Derrota).
        /// </summary>
        public ProceduralAudioSynth Play(GameClip clip, float delay = 0f)
        {
            if (!enabled || !SfxEnabled) return null;

            int index = (int)clip;
            if (SfxPresets == null || index < 0 || index >= SfxPresets.Length || SfxPresets[index] == null)
            {
                Debug.LogWarning($"[GameAudio] Falta el preset para: {clip}");
                return null;
            }

            ProceduralAudioSO preset = SfxPresets[index];

            if (delay > 0f)
            {
                StartCoroutine(PlayDelayedRoutine(preset, delay));
                return null;
            }

            if (ProceduralAudioManager.Instance == null)
            {
                Debug.LogError("[GameAudio] No se encontró ProceduralAudioManager en la escena.");
                return null;
            }

            return ProceduralAudioManager.Instance.PlayPreset(preset);
        }

        private IEnumerator PlayDelayedRoutine(ProceduralAudioSO preset, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (enabled && SfxEnabled && ProceduralAudioManager.Instance != null)
            {
                ProceduralAudioManager.Instance.PlayPreset(preset);
            }
        }

        private void EnsureAudioListener()
        {
            if (FindFirstObjectByType<AudioListener>() == null)
            {
                if (Camera.main != null)
                    Camera.main.gameObject.AddComponent<AudioListener>();
                else
                    gameObject.AddComponent<AudioListener>();
            }
        }

        private void ValidateAndFixPresets()
        {
            if (_fallbackPreset == null)
            {
                _fallbackPreset = ScriptableObject.CreateInstance<ProceduralAudioSO>();
                _fallbackPreset.baseFrequency = 440f;
                _fallbackPreset.amplitude = 0.25f;
                _fallbackPreset.useADSR = true;
                _fallbackPreset.sustainTime = 80f;
            }

            // Validar array 2D
            int enumSize2D = System.Enum.GetNames(typeof(GameClip)).Length;
            if (SfxPresets == null || SfxPresets.Length < enumSize2D)
            {
                System.Array.Resize(ref SfxPresets, enumSize2D);
            }
            for (int i = 0; i < SfxPresets.Length; i++)
            {
                if (SfxPresets[i] == null) SfxPresets[i] = _fallbackPreset;
            }

            // Validar array 3D
            int enumSize3D = System.Enum.GetNames(typeof(SpatialClip)).Length;
            if (SpatialPresets == null || SpatialPresets.Length < enumSize3D)
            {
                System.Array.Resize(ref SpatialPresets, enumSize3D);
            }
            for (int i = 0; i < SpatialPresets.Length; i++)
            {
                if (SpatialPresets[i] == null) SpatialPresets[i] = _fallbackPreset;
            }
        }

        private void SafeInitializePlayerPrefs()
        {
#if UNITY_EDITOR
            PlayerPrefs.SetInt(SfxStatusKey, 0);
            PlayerPrefs.SetInt(MusicStatusKey, 0);
            PlayerPrefs.Save();
#else
            if (!PlayerPrefs.HasKey(MusicStatusKey)) PlayerPrefs.SetInt(MusicStatusKey, 0);
            if (!PlayerPrefs.HasKey(SfxStatusKey)) PlayerPrefs.SetInt(SfxStatusKey, 0);
#endif
            MusicEnabled = PlayerPrefs.GetInt(MusicStatusKey) == 0;
            SfxEnabled = PlayerPrefs.GetInt(SfxStatusKey) == 0;
        }
    }

    /// <summary>
    /// Eventos sonoros globales (2D) de Stealth Game: Haunted House.
    /// </summary>
    public enum GameClip
    {
        MenuButtonClick,    // Clic en Start / Exit
        KeyCollect,         // Recoger llave en el escenario
        DoorLocked,         // Intentar abrir puerta sin llave
        PlayerSpotted,      // Alerta cuando te detecta una gárgola o fantasma
        LevelWin,           // Escapar de la casa embrujada
        LevelLose           // Ser atrapado
    }

    /// <summary>
    /// Eventos sonoros espaciales (3D) centralizados para los objetos de la escena.
    /// </summary>
    public enum SpatialClip
    {
        FootstepWood,       // Pasos de John Lemon
        GhostHover,         // Zumbido continuo de los fantasmas
        TorchCrackle,       // Chispazos de las velas/antorchas
        DoorOpen            // Rechinido al abrir una puerta con llave
    }
}