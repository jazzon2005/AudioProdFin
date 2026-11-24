using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using StealthGame.Audio;

namespace StealthGame
{
    public class PlayerMovement : MonoBehaviour
    {
        public InputAction MoveAction;

        public float walkSpeed = 1.0f;
        public float turnSpeed = 20f;

        [Header("Audio Procedural de Pasos")]
        public float stepInterval = 0.42f;
        private float m_StepTimer = 0f;
        private ProceduralSpatialEmitter m_FootstepEmitter;

        Animator m_Animator;
        Rigidbody m_Rigidbody;
        Vector3 m_Movement;
        Quaternion m_Rotation = Quaternion.identity;
    
        // DEMO
        private List<string> m_OwnedKeys = new();

        void Start ()
        {
            m_Animator = GetComponent<Animator> ();
            m_Rigidbody = GetComponent<Rigidbody> ();

            // Desactivar cualquier AudioSource viejo del prefab original para evitar conflictos
            AudioSource oldSource = GetComponent<AudioSource>();
            if (oldSource != null) oldSource.enabled = false;

            // Crear un objeto hijo limpio para el emisor 3D (evita conflictos con AudioListener o múltiples AudioSources)
            GameObject emitterObj = new GameObject("Footstep_SpatialEmitter");
            emitterObj.transform.SetParent(transform);
            emitterObj.transform.localPosition = Vector3.zero;

            m_FootstepEmitter = emitterObj.AddComponent<ProceduralSpatialEmitter>();
            m_FootstepEmitter.minDistance = 2f;
            m_FootstepEmitter.maxDistance = 15f;
            m_FootstepEmitter.playOnStartLoop = false;

            if (GameAudio.Instance != null)
            {
                m_FootstepEmitter.preset = GameAudio.Instance.GetSpatialPreset(SpatialClip.FootstepWood);
            }
        
            MoveAction.Enable();
        }

        void FixedUpdate ()
        {
            var pos = MoveAction.ReadValue<Vector2>();
        
            float horizontal = pos.x;
            float vertical = pos.y;
        
            m_Movement.Set(horizontal, 0f, vertical);
            m_Movement.Normalize ();

            bool hasHorizontalInput = !Mathf.Approximately (horizontal, 0f);
            bool hasVerticalInput = !Mathf.Approximately (vertical, 0f);
            bool isWalking = hasHorizontalInput || hasVerticalInput;
            m_Animator.SetBool ("IsWalking", isWalking);
        
            // Audio Procedural de Pasos sincronizado con la caminata
            if (isWalking)
            {
                m_StepTimer -= Time.fixedDeltaTime;
                if (m_StepTimer <= 0f)
                {
                    if (m_FootstepEmitter != null && m_FootstepEmitter.preset != null)
                    {
                        float pitchVariation = m_FootstepEmitter.preset.baseFrequency * Random.Range(0.92f, 1.08f);
                        m_FootstepEmitter.PlayOneShot(frequencyOverride: pitchVariation);
                    }
                    m_StepTimer = stepInterval;
                }
            }
            else
            {
                // Al detenerse, dejamos listo el timer para que el primer paso suene al instante al volver a caminar
                m_StepTimer = 0.05f;
            }

            Vector3 desiredForward = Vector3.RotateTowards (transform.forward, m_Movement, turnSpeed * Time.deltaTime, 0f);
            m_Rotation = Quaternion.LookRotation (desiredForward);
        
            m_Rigidbody.MoveRotation (m_Rotation);
            m_Rigidbody.MovePosition (m_Rigidbody.position + m_Movement * walkSpeed * Time.deltaTime);
        }

        public void AddKey(string keyName)
        {
            m_OwnedKeys.Add(keyName);
        }

        public bool OwnKey(string keyName)
        {
            return m_OwnedKeys.Contains(keyName);
        }
    }
}