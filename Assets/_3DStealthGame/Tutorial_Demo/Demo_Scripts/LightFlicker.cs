using UnityEngine;
using StealthGame.Audio;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace StealthGame
{
    public class LightFlicker : MonoBehaviour
    {
        public enum FlickerMode
        {
            Random,
            AnimationCurve
        }
    
        public Light flickeringLight;
        public Renderer flickeringRenderer;
        public FlickerMode flickerMode;
        public float lightIntensityMin = 1.25f;
        public float lightIntensityMax = 2.25f;
        public float flickerDuration = 0.075f;
        public AnimationCurve intensityCurve;

        [Range(0f, 1f)] public float crackleProbability = 0.25f;
        private ProceduralSpatialEmitter m_CrackleEmitter;

        Material m_FlickeringMaterial;
        Color m_EmissionColor;
        float m_Timer;
        float m_FlickerLightIntensity;
    
        static readonly int k_EmissionColorID = Shader.PropertyToID (k_EmissiveColorName);
    
        const string k_EmissiveColorName = "_EmissionColor";
        const string k_EmissionName = "_Emission";
        const float k_LightIntensityToEmission = 2f / 3f;

        void Start()
        {
            m_FlickeringMaterial = flickeringRenderer.material;
            m_FlickeringMaterial.EnableKeyword(k_EmissionName);
            m_EmissionColor = m_FlickeringMaterial.GetColor(k_EmissionColorID);

            // Crear un objeto hijo limpio para el emisor 3D
            GameObject emitterObj = new GameObject("Torch_SpatialEmitter");
            emitterObj.transform.SetParent(transform);
            emitterObj.transform.localPosition = Vector3.zero;

            m_CrackleEmitter = emitterObj.AddComponent<ProceduralSpatialEmitter>();
            m_CrackleEmitter.minDistance = 1f;
            m_CrackleEmitter.maxDistance = 8f;
            m_CrackleEmitter.playOnStartLoop = false;

            if (GameAudio.Instance != null)
            {
                m_CrackleEmitter.preset = GameAudio.Instance.GetSpatialPreset(SpatialClip.TorchCrackle);
            }
        }

        void Update()
        {
            m_Timer += Time.deltaTime;

            if (flickerMode == FlickerMode.Random)
            {
                if (m_Timer >= flickerDuration)
                {
                    ChangeRandomFlickerLightIntensity ();
                }
            }
            else if(flickerMode == FlickerMode.AnimationCurve)
            {
                ChangeAnimatedFlickerLightIntensity ();
            }
            
            flickeringLight.intensity = m_FlickerLightIntensity;
            m_FlickeringMaterial.SetColor (k_EmissionColorID, m_EmissionColor * m_FlickerLightIntensity * k_LightIntensityToEmission);
        }

        void ChangeRandomFlickerLightIntensity ()
        {
            m_FlickerLightIntensity = Random.Range(lightIntensityMin, lightIntensityMax);

            // Si la intensidad tiene un pico fuerte, emitimos un chispazo procedural 3D
            if (m_CrackleEmitter != null && m_CrackleEmitter.preset != null && Random.value < crackleProbability)
            {
                float randomFreq = m_CrackleEmitter.preset.baseFrequency * Random.Range(0.8f, 1.3f);
                m_CrackleEmitter.PlayOneShot(frequencyOverride: randomFreq, volumeScale: 0.6f);
            }

            m_Timer = 0f;
        }

        void ChangeAnimatedFlickerLightIntensity ()
        {
            m_FlickerLightIntensity = intensityCurve.Evaluate (m_Timer);

            if (m_Timer >= intensityCurve[intensityCurve.length - 1].time)
                m_Timer = intensityCurve[0].time;
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(LightFlicker))]
    public class LightFlickerEditor : Editor
    {
        SerializedProperty m_ScriptProp;
        SerializedProperty m_FlickeringLightProp;
        SerializedProperty m_FlickeringRendererProp;
        SerializedProperty m_FlickerModeProp;
        SerializedProperty m_LightIntensityMinProp;
        SerializedProperty m_LightIntensityMaxProp;
        SerializedProperty m_FlickerDurationProp;
        SerializedProperty m_IntensityCurveProp;
        SerializedProperty m_CrackleProbabilityProp;

        void OnEnable ()
        {
            m_ScriptProp = serializedObject.FindProperty ("m_Script");
            m_FlickeringLightProp = serializedObject.FindProperty ("flickeringLight");
            m_FlickeringRendererProp = serializedObject.FindProperty ("flickeringRenderer");
            m_FlickerModeProp = serializedObject.FindProperty ("flickerMode");
            m_LightIntensityMinProp = serializedObject.FindProperty ("lightIntensityMin");
            m_LightIntensityMaxProp = serializedObject.FindProperty ("lightIntensityMax");
            m_FlickerDurationProp = serializedObject.FindProperty ("flickerDuration");
            m_IntensityCurveProp = serializedObject.FindProperty ("intensityCurve");
            m_CrackleProbabilityProp = serializedObject.FindProperty ("crackleProbability");
        }

        public override void OnInspectorGUI ()
        {
            serializedObject.Update ();

            GUI.enabled = false;
            EditorGUILayout.PropertyField (m_ScriptProp);
            GUI.enabled = true;
        
            EditorGUILayout.PropertyField (m_FlickeringLightProp);
            EditorGUILayout.PropertyField (m_FlickeringRendererProp);
            EditorGUILayout.PropertyField (m_FlickerModeProp);

            if (m_FlickerModeProp.enumValueIndex == 0)
            {
                EditorGUILayout.PropertyField (m_LightIntensityMinProp);
                EditorGUILayout.PropertyField (m_LightIntensityMaxProp);
                EditorGUILayout.PropertyField (m_FlickerDurationProp);
            }
            else if (m_FlickerModeProp.enumValueIndex == 1)
            {
                EditorGUILayout.PropertyField (m_IntensityCurveProp);
            }

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField (m_CrackleProbabilityProp);

            serializedObject.ApplyModifiedProperties ();
        }
    }
#endif
}