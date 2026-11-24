using UnityEngine;

namespace StealthGame.Audio
{
    /// <summary>
    /// Componente de audio procedural 3D completamente desacoplado del sistema 2D.
    /// Se adjunta directamente a objetos del mundo (Fantasmas, Velas, Puertas, Jugador)
    /// y utiliza el motor espacial nativo de Unity para atenuación por distancia y paneo estéreo.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class ProceduralSpatialEmitter : MonoBehaviour
    {
        [Header("Configuración Procedural 3D")]
        [Tooltip("Preset de síntesis que generará este objeto en el espacio 3D.")]
        public ProceduralAudioSO preset;

        [Tooltip("Si está activo, el sonido se emitirá continuamente en bucle (ideal para fantasmas o antorchas).")]
        public bool playOnStartLoop = false;

        [Header("Ajustes Espaciales (Área de Acción)")]
        [Tooltip("Distancia a la que el sonido se escucha al 100% de volumen.")]
        public float minDistance = 1.5f;

        [Tooltip("Distancia máxima a partir de la cual el sonido cae a 0%.")]
        public float maxDistance = 12f;

        private AudioSource _audioSource;
        private float _sampleRate;

        // Estado interno del sintetizador
        private float _currentFrequency;
        private float _currentAmplitude;
        private WaveformType _currentWaveform;
        private float[] _currentHarmonics = new float[10];

        private bool _currentUseLFO;
        private float _currentLfoRate;
        private float _currentLfoIndex;

        private float _sweepStartMul = 1f;
        private float _sweepTime = 0f;
        private float _sweepElapsed = 0f;

        private enum AdsrStage { Idle, Attack, Decay, Sustain, Release }
        private AdsrStage _stage = AdsrStage.Idle;
        private float _envelopeValue = 0f;
        private float _releaseStartValue = 0f;
        private float _currentSustainTimer = 0f;
        private bool _isPlaying = false;
        private bool _isLoopingMode = false;

        private float _phase = 0f;
        private float _lfoTime = 0f;

        private void Awake()
        {
            _sampleRate = AudioSettings.outputSampleRate;
            _audioSource = GetComponent<AudioSource>();

            // Configuramos el AudioSource de Unity en modo 3D puro
            _audioSource.playOnAwake = false;
            _audioSource.loop = true;
            _audioSource.spatialBlend = 1.0f; // 100% 3D Posicional
            _audioSource.rolloffMode = AudioRolloffMode.Linear; // Caída clara hasta 0 en maxDistance
            _audioSource.minDistance = minDistance;
            _audioSource.maxDistance = maxDistance;
        }

        private void Start()
        {
            if (playOnStartLoop && preset != null)
            {
                PlayLoop();
            }
        }

        /// <summary>
        /// Dispara el sonido procedural una vez (ideal para pasos, crujidos de fuego o abrir puertas).
        /// </summary>
        public void PlayOneShot(float frequencyOverride = -1f, float volumeScale = 1f)
        {
            if (preset == null) return;

            _isLoopingMode = false;
            LoadPresetParameters(preset, frequencyOverride, volumeScale);

            _stage = AdsrStage.Attack;
            _envelopeValue = 0f;
            _currentSustainTimer = preset.sustainTime / 1000f;
            _isPlaying = true;

            if (!_audioSource.isPlaying) _audioSource.Play();
        }

        /// <summary>
        /// Inicia el sonido procedural de forma continua (ideal para fantasmas patrullando).
        /// </summary>
        public void PlayLoop(float frequencyOverride = -1f, float volumeScale = 1f)
        {
            if (preset == null) return;

            _isLoopingMode = true;
            LoadPresetParameters(preset, frequencyOverride, volumeScale);

            _stage = AdsrStage.Attack;
            _envelopeValue = 0f;
            _isPlaying = true;

            if (!_audioSource.isPlaying) _audioSource.Play();
        }

        /// <summary>
        /// Detiene suavemente el sonido pasando a la fase Release.
        /// </summary>
        public void StopEmitter()
        {
            _isLoopingMode = false;
            if (_stage != AdsrStage.Idle)
            {
                _stage = AdsrStage.Release;
                _releaseStartValue = _envelopeValue;
            }
            else
            {
                _isPlaying = false;
            }
        }

        public void SetLiveFrequency(float newFreq)
        {
            _currentFrequency = newFreq;
        }

        private void LoadPresetParameters(ProceduralAudioSO so, float freqOverride, float volScale)
        {
            _audioSource.minDistance = minDistance;
            _audioSource.maxDistance = maxDistance;

            _currentFrequency = (freqOverride > 0f) ? freqOverride : so.baseFrequency;
            _currentAmplitude = so.amplitude * volScale;
            _currentWaveform = so.waveform;

            _currentUseLFO = so.useLFO;
            _currentLfoRate = so.lfoRate;
            _currentLfoIndex = so.LFOIndex;

            _sweepStartMul = so.pitchSweepMultiplier;
            _sweepTime = so.pitchSweepTime / 1000f;
            _sweepElapsed = 0f;

            if (so.harmonicLevels != null && so.harmonicLevels.Length == 10)
            {
                System.Array.Copy(so.harmonicLevels, _currentHarmonics, 10);
            }

            _phase = 0f;
            _lfoTime = 0f;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (preset == null || (!_isPlaying && _stage == AdsrStage.Idle))
            {
                for (int i = 0; i < data.Length; i++) data[i] = 0f;
                return;
            }

            float dt = 1f / _sampleRate;

            for (int i = 0; i < data.Length; i += channels)
            {
                float sweepMul = 1f;
                if (_sweepTime > 0f && _sweepElapsed < _sweepTime)
                {
                    sweepMul = Mathf.Lerp(_sweepStartMul, 1f, _sweepElapsed / _sweepTime);
                    _sweepElapsed += dt;
                }

                _phase += _currentFrequency * sweepMul * dt;
                _phase -= Mathf.Floor(_phase);

                float sample = GetSample(_currentWaveform, _phase);
                float env = EvaluateEnvelope(dt);

                sample = Mathf.Clamp(sample * env * _currentAmplitude, -1f, 1f);

                for (int j = 0; j < channels; j++)
                {
                    data[i + j] = sample;
                }

                _lfoTime += dt;
            }
        }

        private float EvaluateEnvelope(float dt)
        {
            if (!preset.useADSR && !_isLoopingMode)
            {
                _currentSustainTimer -= dt;
                if (_currentSustainTimer <= 0f)
                {
                    _isPlaying = false;
                    _stage = AdsrStage.Idle;
                    return 0f;
                }
                return 1f;
            }

            switch (_stage)
            {
                case AdsrStage.Attack:
                    _envelopeValue += dt / Mathf.Max(0.0001f, preset.attack / 1000f);
                    if (_envelopeValue >= 1f)
                    {
                        _envelopeValue = 1f;
                        _stage = AdsrStage.Decay;
                    }
                    break;

                case AdsrStage.Decay:
                    _envelopeValue -= dt / Mathf.Max(0.0001f, preset.decay / 1000f);
                    if (_envelopeValue <= preset.sustainLevel)
                    {
                        _envelopeValue = preset.sustainLevel;
                        _stage = AdsrStage.Sustain;
                    }
                    break;

                case AdsrStage.Sustain:
                    _envelopeValue = preset.sustainLevel;
                    if (!_isLoopingMode)
                    {
                        _currentSustainTimer -= dt;
                        if (_currentSustainTimer <= 0f)
                        {
                            _stage = AdsrStage.Release;
                            _releaseStartValue = _envelopeValue;
                        }
                    }
                    break;

                case AdsrStage.Release:
                    _envelopeValue -= dt * (_releaseStartValue / Mathf.Max(0.0001f, preset.release / 1000f));
                    if (_envelopeValue <= 0f)
                    {
                        _envelopeValue = 0f;
                        _stage = AdsrStage.Idle;
                        _isPlaying = false;
                    }
                    break;
            }

            return _envelopeValue;
        }

        private float GetSample(WaveformType type, float ph)
        {
            float cycles = ph;
            if (_currentUseLFO)
            {
                cycles += _currentLfoIndex * Mathf.Sin(2f * Mathf.PI * _currentLfoRate * _lfoTime) / (2f * Mathf.PI);
            }

            switch (type)
            {
                case WaveformType.Sine: return Mathf.Sin(2f * Mathf.PI * cycles);
                case WaveformType.Square: return Mathf.Sign(Mathf.Sin(2f * Mathf.PI * cycles));
                case WaveformType.Triangle: return Mathf.PingPong(cycles * 2f, 1f) * 2f - 1f;
                case WaveformType.Sawtooth: return 2f * (cycles - Mathf.Floor(cycles + 0.5f));
                case WaveformType.Additive:
                    float val = 0f;
                    for (int i = 0; i < _currentHarmonics.Length; i++)
                    {
                        if (_currentHarmonics[i] > 0f)
                            val += _currentHarmonics[i] * Mathf.Sin(2f * Mathf.PI * (i + 1) * cycles);
                    }
                    return val / 2.5f;
                default: return 0f;
            }
        }
    }
}