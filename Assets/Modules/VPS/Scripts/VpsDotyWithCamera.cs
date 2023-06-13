using UnityEngine;

namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// Doty appearing with a poof, and offering a photo camera.
    /// </summary>
    public class VpsDotyWithCamera : MonoBehaviour
    {
        public GameObject yeti;
        public Animator yetiAnimator;
        public GameObject photoCamera;
        public GameObject photoCameraColliderObject;

        [Header("Dynamic Look")]
        [Range(0, 1f)] public float lookWeight = 1;

        [SerializeField] Transform neckJoint;
        [SerializeField] Transform waistJoint;
        private Transform lookJoint;
        private Vector3 lookAngleOffset = default;

        [SerializeField] Transform cameraTargetTransform;
        [SerializeField] Transform cameraContainer;

        private Vector3 lazyTarget;

        [Header("Particles")]
        [SerializeField] GameObject poofParticles;

        private AudioManager audioManager;

        void Awake()
        {
            // Bail if we don't have a camera;
            if (Camera.main == null) return;

            lazyTarget = Camera.main.transform.position;

            audioManager = SceneLookup.Get<AudioManager>();

            lookJoint = neckJoint;
            lookAngleOffset = new Vector3(0, 0, 0);
        }

        public void ShowCamera()
        {
            //Debug.Log("ShowCamera animation event");
            photoCamera.SetActive(true);
        }

        // Fired by animation events on animation clip.
        public void TriggerParticles()
        {
            //Debug.Log("TriggerParticles animation event");
            poofParticles.SetActive(true);
        }

        public void SwitchLookTarget()
        {
            InterpolationUtil.EasedInterpolation(this, this, InterpolationUtil.EaseInOutCubic, .5f, onUpdate: (t) =>
            {
                lookWeight = 1 - t;
            }, onComplete: () =>
            {
                lookJoint = waistJoint;
                lookAngleOffset = new Vector3(15, 0, 0);
                InterpolationUtil.EasedInterpolation(this, this, InterpolationUtil.EaseInOutCubic, .5f, onUpdate: (t) =>
                {
                    lookWeight = t;
                });
            });
        }

        void LateUpdate()
        {
            // Bail if we don't have a camera;
            if (Camera.main == null) return;

            // Only pay attention to the camera when on the front side of doty.
            Vector3 newTarget;
            if (Vector3.Dot(transform.forward, Camera.main.transform.position - transform.position) > 0)
            {
                newTarget = Camera.main.transform.position;
            }
            else
            {
                newTarget = lookJoint.position + transform.forward;
            }

            // SmoothDamp lerp an invisible target toward the new target.
            Vector3 velocity = Vector3.zero;
            float smoothTime = 0.1F;
            float maxSpeed = 8;
            lazyTarget = Vector3.SmoothDamp(lazyTarget, newTarget, ref velocity, smoothTime, maxSpeed);

            // Force lookjoint to aim directly at target.
            Quaternion originalRotation = lookJoint.transform.localRotation;
            lookJoint.transform.LookAt(lazyTarget);
            Vector3 constrainedRotation = lookJoint.localRotation.eulerAngles + lookAngleOffset;

            // Constrain rotation as necessary.
            float rangeY = 60;
            float rangeX = 60;

            if (constrainedRotation.y < 180 && constrainedRotation.y > rangeY) constrainedRotation.y = rangeY;
            if (constrainedRotation.y > 180 && constrainedRotation.y < 360 - rangeY) constrainedRotation.y = 360 - rangeY;

            if (constrainedRotation.x < 180 && constrainedRotation.x > rangeX) constrainedRotation.x = rangeX;
            if (constrainedRotation.x > 180 && constrainedRotation.x < 360 - rangeX) constrainedRotation.x = 360 - rangeX;
            constrainedRotation.z = 0;

            // Apply constrained rotation.
            lookJoint.localRotation = Quaternion.Lerp(originalRotation, Quaternion.Euler(constrainedRotation), lookWeight);

            // Update camera container.
            cameraContainer.position = cameraTargetTransform.position;
            cameraContainer.rotation = cameraTargetTransform.rotation;
        }
    }
}