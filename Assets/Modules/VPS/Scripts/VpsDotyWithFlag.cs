using UnityEngine;

namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// Doty planting a flag and disappearing.
    /// </summary>
    public class VpsDotyWithFlag : MonoBehaviour
    {
        public GameObject yeti;
        public Animator yetiAnimator;
        public GameObject flagColliderObject;

        [Header("Particles")]
        [SerializeField] GameObject poofParticles;

        private AudioManager audioManager;

        void Awake()
        {
            audioManager = SceneLookup.Get<AudioManager>();
        }

        // Fired by animation events on animation clip.
        public void TriggerDisappearSFX()
        {
            //Debug.Log("TriggerDisappearSFX animation event");
            audioManager.PlayAudioOnObject(AudioKeys.SFX_Doty_Disappear, targetObject: yeti);
        }

        public void TriggerParticles()
        {
            //Debug.Log("TriggerParticles animation event");
            poofParticles.SetActive(true);
        }
    }
}
