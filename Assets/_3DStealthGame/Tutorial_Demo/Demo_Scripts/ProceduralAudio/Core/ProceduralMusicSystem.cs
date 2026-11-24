using System.Collections;
using UnityEngine;

/// Conversión de nombres de nota a Hz.
public static class Notes
{
    static readonly int[] Semis = { 0, 2, 4, 5, 7, 9, 11 }; // C D E F G A B

    public static int Midi(string n)
    {
        int s = Semis["CDEFGAB".IndexOf(n[0])];
        int i = 1;
        if (n[i] == '#') { s++; i++; }
        else if (n[i] == 'b') { s--; i++; }
        int oct = int.Parse(n.Substring(i));
        return 12 * (oct + 1) + s;
    }

    public static float Hz(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);
    public static float Hz(string note) => Hz(Midi(note));
    public static float Hz(string note, int octave) => Hz(note + octave);
}

/// Música generativa de suspenso por capas (bajo pizzicato, pad de órgano, xilófono y percusión de latido),
/// con un tema distinto para cada ambiente del juego.
public class ProceduralMusicSystem : MonoBehaviour
{
    public static ProceduralMusicSystem Instance { get; private set; }

    [System.Serializable]
    public class Layer
    {
        public ProceduralAudioSO preset;
        [Range(1, 6)] public int voices = 2;
        [Range(0f, 1f)] public float volume = 1f;

        ProceduralAudioSynth[] pool;
        int idx;

        public void Init(Transform parent, string label)
        {
            pool = new ProceduralAudioSynth[voices];
            for (int i = 0; i < voices; i++)
            {
                var go = new GameObject($"{label}_{i}");
                go.transform.SetParent(parent);
                pool[i] = go.AddComponent<ProceduralAudioSynth>();
            }
        }

        public void Play(float hz, float lenSec, float master, float velocity = 1f)
        {
            if (preset == null || pool == null) return;
            float sustainMs = Mathf.Max(0f, lenSec * 1000f - preset.attack - preset.decay);
            pool[idx].PlayPreset(preset, hz, volume * master * velocity, sustainMs);
            idx = (idx + 1) % pool.Length;
        }

        public void StopAll()
        {
            if (pool == null) return;
            foreach (var s in pool) s.StopSound();
        }
    }

    // ---------- Temas por ambiente ----------
    struct Theme
    {
        public string name;
        public int tonic;         // MIDI de la tónica del bajo (38 = D2)
        public float bpm;
        public int[] chords;      // raíz de cada uno de los 4 compases, en semitonos sobre la tónica
        public string quality;    // 'm' menor, 'M' mayor, una letra por compás
        public bool minor;        // escala de blues menor (true) o pentatónica mayor (false)
        public int pattern;       // índice en BassPatterns

        public Theme(string n, int t, float b, int[] c, string q, bool m, int p)
        { name = n; tonic = t; bpm = b; chords = c; quality = q; minor = m; pattern = p; }
    }

    static readonly Theme[] Themes =
    {
        new Theme("Mansión",      38,  84f, new[]{0,8,5,7}, "mMmM", true, 3), // Re menor, calma tensa
        new Theme("Sótano",       33,  92f, new[]{0,1,0,6}, "mMmM", true, 3), // La menor, segunda menor y tritono
        new Theme("Cementerio",   40,  88f, new[]{0,8,6,7}, "mMMM", true, 0), // Mi menor
        new Theme("Persecución",  38, 132f, new[]{0,1,0,6}, "mMmM", true, 2), // Re menor, corcheas constantes
        new Theme("Jefe final",   36, 112f, new[]{0,3,8,6}, "mMMm", true, 1), // Do menor
    };

    // 16 pasos de semicorchea por compás; 'x' = golpe de bajo
    static readonly string[] BassPatterns =
    {
        "x...x...x..x..x.",   // 0: pulso disperso
        "x..x..x.x..x..x.",   // 1: rebotón
        "x.x.x.x.x.x.x.x.",   // 2: urgencia (persecución)
        "x.x...x.x.x...x."    // 3: sigiloso, notas cortas a contratiempo
    };

    static readonly int[] MinorBlues = { 0, 3, 5, 6, 7, 10 };   // la nota "6" es el tritono: aporta el susto
    static readonly int[] MajorPent = { 0, 2, 4, 7, 9 };

    [Header("Capas (asignar un ProceduralAudioSO a cada una)")]
    public Layer bass = new Layer { voices = 2 };
    public Layer pad = new Layer { voices = 3 };
    public Layer lead = new Layer { voices = 3 };
    public Layer perc = new Layer { voices = 2 };

    [Header("Percusión de ruido (velocidad del ruido en Hz)")]
    public float kickHz = 55f;     // latido
    public float snareHz = 1200f;  // rasguño
    public float hatHz = 5000f;    // grillos

    [Header("Control")]
    public bool playOnStart = true;
    [Range(1, 5)] public int level = 1;
    [Range(0f, 1f)] public float musicVolume = 0.5f;
    [Tooltip("0 = calma, 1 = peligro máximo (te vieron, persecución)")]
    [Range(0f, 1f)] public float progress = 0f;
    public int seed = 7;

    // Estado del tema activo
    Theme theme;
    float[] rootHz;
    float[][] padHz;
    float[] leadHz;

    System.Random rng;
    double nextStepTime;
    int stepCount;
    int leadIdx = 3;
    bool running;

    // Ducking: baja la música unos instantes cuando suena un SFX importante
    float duck = 1f;
    float duckRecover = 0.5f;

    bool musicEnabled = true;
    public bool MusicEnabled
    {
        get => musicEnabled;
        set
        {
            musicEnabled = value;
            if (!value) StopMusic();
            else StartMusic();
        }
    }

    public float MusicVolume { get => musicVolume; set => musicVolume = Mathf.Clamp01(value); }
    public void SetProgress(float p) { progress = Mathf.Clamp01(p); }

    // Compatibilidad: lee el tempo del tema o lo reemplaza a mano. SetLevel() lo restablece.
    float bpmOverride = -1f;
    public float bpm
    {
        get => bpmOverride > 0f ? bpmOverride : (theme.bpm > 0f ? theme.bpm : Themes[0].bpm);
        set => bpmOverride = value;
    }

    void Awake()
    {
        if (Instance == null) Instance = this; else { Destroy(gameObject); return; }

        bass.Init(transform, "Bass");
        pad.Init(transform, "Pad");
        lead.Init(transform, "Lead");
        perc.Init(transform, "Perc");

        BuildTheme(level);
    }

    void Start()
    {
        if (playOnStart && musicEnabled) StartMusic();
    }

    /// <summary>Cambia al tema del ambiente (1 a 5) y reinicia la música con una breve pausa.</summary>
    public void SetLevel(int newLevel)
    {
        level = Mathf.Clamp(newLevel, 1, Themes.Length);
        progress = 0f;
        bpmOverride = -1f;
        BuildTheme(level);
        StopMusic();
        if (musicEnabled) StartMusic();
    }

    void BuildTheme(int lvl)
    {
        theme = Themes[Mathf.Clamp(lvl, 1, Themes.Length) - 1];

        rootHz = new float[4];
        padHz = new float[4][];
        for (int c = 0; c < 4; c++)
        {
            int o = theme.chords[c] > 6 ? theme.chords[c] - 12 : theme.chords[c]; // mantiene el bajo grave
            int b = theme.tonic + o;
            rootHz[c] = Notes.Hz(b);
            int third = theme.quality[c] == 'm' ? 3 : 4;
            int r = b + 12;
            padHz[c] = new[] { Notes.Hz(r), Notes.Hz(r + third), Notes.Hz(r + 7) };
        }

        int[] scale = theme.minor ? MinorBlues : MajorPent;
        leadHz = new float[scale.Length * 2];
        for (int i = 0; i < leadHz.Length; i++)
            leadHz[i] = Notes.Hz(theme.tonic + 24 + 12 * (i / scale.Length) + scale[i % scale.Length]);
        leadIdx = Mathf.Min(3, leadHz.Length - 1);
    }

    public void StartMusic()
    {
        if (!musicEnabled) return;
        rng = new System.Random(seed + level);   // cada ambiente tiene su melodía, repetible
        stepCount = 0;
        leadIdx = Mathf.Min(3, leadHz.Length - 1);
        nextStepTime = AudioSettings.dspTime + 0.25; // pausa breve: transición
        running = true;
    }

    public void StopMusic()
    {
        running = false;
        bass.StopAll(); pad.StopAll(); lead.StopAll(); perc.StopAll();
    }

    /// <summary>Baja la música (amount 0 a 1) y la recupera en 'seconds'. Úsalo con sustos o eventos grandes.</summary>
    public void Duck(float amount, float seconds)
    {
        duck = Mathf.Min(duck, 1f - Mathf.Clamp01(amount));
        duckRecover = Mathf.Max(0.05f, seconds);
    }

    void Update()
    {
        duck = Mathf.MoveTowards(duck, 1f, Time.unscaledDeltaTime / duckRecover);

        if (!running) return;

        // El peligro acelera el tempo (hasta +12 %)
        float bpmNow = bpm * Mathf.Lerp(1f, 1.12f, progress);
        double stepDur = 60.0 / bpmNow / 4.0;
        double now = AudioSettings.dspTime;
        int guard = 0;
        while (now >= nextStepTime && guard++ < 4)
        {
            Step(stepCount, (float)stepDur);
            stepCount++;
            nextStepTime += stepDur;
        }
        if (guard >= 4) nextStepTime = now + stepDur; // recuperación tras un hitch
    }

    void Step(int count, float stepDur)
    {
        int bar = (count / 16) % 4;
        int s = count % 16;
        float m = musicVolume * duck;

        // PAD (órgano): acorde largo al inicio de cada compás
        if (s == 0)
        {
            float barSec = stepDur * 16f;
            foreach (float hz in padHz[bar]) pad.Play(hz, barSec * 0.9f, m);
        }

        // BAJO (pizzicato): patrón del tema, con octava y quinta
        if (BassPatterns[theme.pattern][s] == 'x')
        {
            float mul = (s % 8 == 6) ? 2f : (s % 8 == 3) ? 1.5f : 1f;
            bass.Play(rootHz[bar] * mul, stepDur * 1.5f, m);
        }

        // XILÓFONO: caminata aleatoria por la escala de blues; esparcido en calma, denso con peligro
        float baseProb = Mathf.Lerp(0.15f, 0.35f, (level - 1) / (float)(Themes.Length - 1));
        if (s % 2 == 0 && rng.NextDouble() < Mathf.Lerp(baseProb, 0.70f, progress))
        {
            leadIdx = Mathf.Clamp(leadIdx + rng.Next(-2, 3), 0, leadHz.Length - 1);
            float vel = (s % 4 == 0) ? 1f : 0.75f;
            lead.Play(leadHz[leadIdx], stepDur * 2f, m, vel);
        }

        // PERCUSIÓN: latido (lub-dub) siempre; con peligro el latido se duplica
        if (s == 0) perc.Play(kickHz, stepDur, m);
        if (s == 3) perc.Play(kickHz, stepDur, m, 0.7f);
        if (progress >= 0.5f)
        {
            if (s == 8) perc.Play(kickHz, stepDur, m);
            if (s == 11) perc.Play(kickHz, stepDur, m, 0.7f);
        }
        // rasguños a contratiempo desde el segundo ambiente
        if (level >= 2 && (s == 6 || s == 14)) perc.Play(snareHz, stepDur, m, 0.5f);
        // grillos: aparecen con el peligro
        if (progress > 0.1f && s % 2 == 1 && rng.NextDouble() < Mathf.InverseLerp(0.1f, 0.6f, progress))
            perc.Play(hatHz, stepDur * 0.5f, m, 0.4f);
    }

    /// <summary>Compatibilidad con la versión anterior: equivale a PlayWinStinger.</summary>
    public void PlayCadence() { PlayWinStinger(); }

    /// <summary>Victoria: para la música y resuelve en mayor (alivio tras tanto menor).</summary>
    public void PlayWinStinger()
    {
        StopMusic();
        StartCoroutine(Stinger(new[] { 0, 4, 7, 12, 16, 19 }, theme.tonic + 24, 0.09f));
    }

    /// <summary>Derrota: para la música y baja por semitonos, tipo trombón triste.</summary>
    public void PlayLoseStinger()
    {
        StopMusic();
        StartCoroutine(Stinger(new[] { 12, 11, 10, 8 }, theme.tonic + 24, 0.2f));
    }

    IEnumerator Stinger(int[] semis, int rootMidi, float gap)
    {
        foreach (int semi in semis)
        {
            lead.Play(Notes.Hz(rootMidi + semi), gap * 3f, musicVolume);
            yield return new WaitForSeconds(gap);
        }
    }
}