using System;
using UnityEngine;
using StealthGame.Audio;

namespace StealthGame
{
    public class Key : MonoBehaviour
    {
        public string KeyName = "key1";

        private void OnTriggerEnter(Collider other)
        {
            PlayerMovement player = other.gameObject.GetComponent<PlayerMovement>();

            //this wasn't a player
            if (player == null)
                return;
        
            GameAudio.Instance?.Play(GameClip.KeyCollect);
            player.AddKey(KeyName);
            Destroy(gameObject);
        }
    }
}