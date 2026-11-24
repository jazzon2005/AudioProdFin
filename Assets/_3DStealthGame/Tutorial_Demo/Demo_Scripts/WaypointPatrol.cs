using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using StealthGame.Audio;

namespace StealthGame
{
    public class WaypointPatrol : MonoBehaviour
    {
        public float moveSpeed = 1.0f;
        public Transform[] waypoints;

        private ProceduralSpatialEmitter m_GhostEmitter;
        private Rigidbody m_RigidBody;
        int m_CurrentWaypointIndex;

        void Start ()
        {
            m_RigidBody = GetComponent<Rigidbody>();

            // Silenciar el AudioSource original del prefab de fantasma
            AudioSource oldSource = GetComponent<AudioSource>();
            if (oldSource != null) oldSource.enabled = false;

            // Crear un objeto hijo limpio para el emisor 3D
            GameObject emitterObj = new GameObject("Ghost_SpatialEmitter");
            emitterObj.transform.SetParent(transform);
            emitterObj.transform.localPosition = Vector3.zero;

            m_GhostEmitter = emitterObj.AddComponent<ProceduralSpatialEmitter>();
            m_GhostEmitter.minDistance = 1.5f;
            m_GhostEmitter.maxDistance = 10f;
            m_GhostEmitter.playOnStartLoop = true;

            if (GameAudio.Instance != null)
            {
                m_GhostEmitter.preset = GameAudio.Instance.GetSpatialPreset(SpatialClip.GhostHover);
                m_GhostEmitter.PlayLoop();
            }
        }

        void FixedUpdate ()
        {
            Transform currentWaypoint = waypoints[m_CurrentWaypointIndex];
            Vector3 currentToTarget = currentWaypoint.position - m_RigidBody.position;

            if (currentToTarget.magnitude < 0.1f)
            {
                //very close to the waypoint, get to the next waypoint
                m_CurrentWaypointIndex = (m_CurrentWaypointIndex + 1) % waypoints.Length;
            }
        
            //find the rotation to orient the rigidbody toward the waypoint
            Quaternion forwardRotation = Quaternion.LookRotation(currentToTarget);
            m_RigidBody.MoveRotation(forwardRotation);
        
            //move toward the waypoint at the set speed
            m_RigidBody.MovePosition(m_RigidBody.position + currentToTarget.normalized * moveSpeed * Time.deltaTime);
        }
    }
}