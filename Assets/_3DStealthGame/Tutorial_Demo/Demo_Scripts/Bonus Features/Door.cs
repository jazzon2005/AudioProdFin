using System;
using UnityEngine;
using StealthGame.Audio;

namespace StealthGame
{
    public class Door : MonoBehaviour
    {
        public string KeyName = "key1";
    
        private void OnCollisionEnter(Collision other)
        {
            PlayerMovement player = other.gameObject.GetComponent<PlayerMovement>();

            if (player == null)
                return;

            if (player.OwnKey(KeyName))
            {
                // Obtiene el preset 3D directamente desde GameAudio antes de destruir la puerta
                if (GameAudio.Instance != null)
                {
                    ProceduralAudioSO openPreset = GameAudio.Instance.GetSpatialPreset(SpatialClip.DoorOpen);
                    SpatialOneShotTrigger.PlayAtPoint(openPreset, transform.position);
                }
                Destroy(gameObject);
            }
            else
            {
                // Cerradura bloqueada (2D)
                GameAudio.Instance?.Play(GameClip.DoorLocked);
            }
        }
    }
}