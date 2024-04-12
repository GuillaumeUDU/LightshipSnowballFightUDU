// Copyright 2022 Niantic, Inc. All Rights Reserved.
using UnityEngine;
using System;
using Niantic.ARVoyage.SnowballToss;
using Random = UnityEngine.Random;
using UDU;
using UnityEngine.UI;

namespace Niantic.ARVoyage
{
    /// <summary>
    /// Instantiated by SnowballMaker, used in SnowballToss and SnowballFight scenes.
    /// Includes support for networked-spawed snowballs in multiplayer game like SnowballFight.
    /// Hierarchy has a holder parent with a dynamic hold offset, 
    /// so the snowball can be held forward from the camera if view from first-person POV,
    /// or held directly on the camera if viewed from third-person POV, e.g. other players holding 
    /// a snowball in a multiplayer game like SnowballFight.
    /// Snowball can burst into smaller particles when colliding in the world, and leave a splat.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Snowball : MonoBehaviour
    {
        private const float snowballScale = 0.5f;
        public float scaleUpDuration = 1.5f;
        private const float DestroyDelay = 5f;

        private const int AR_MESH_LAYER = 9;
        private const int AR_PLANES_LAYER = 10;
        private const string ALLOW_SPLATS_TAG = "Allow Splats";

        // Passes angle, force and torque of the toss
        public readonly AppEvent<Snowball, float, Vector3, Vector3> EventLocallySpawnedSnowballTossed = new AppEvent<Snowball, float, Vector3, Vector3>();
        public readonly AppEvent<Snowball, Collision> EventSnowballCollided = new AppEvent<Snowball, Collision>();
        public readonly AppEvent<Snowball> EventSnowballExpiring = new AppEvent<Snowball>();

        // snowball model
        [SerializeField] public GameObject snowballModel;

        // hold the snowball in holder object
        [SerializeField] private GameObject snowballHolder;

        // use transform from this object as an (optional) hold offset
        [SerializeField] public GameObject snowballHoldOffset;

        // Prefab assets to instantiate on collision/destroy.
        [SerializeField] GameObject snowballSplatPrefab;
        [SerializeField] GameObject snowballBurstPrefab;

        // Should the snowball automatically handle its own collisions?
        [SerializeField] bool automaticallyProcessCollisions = true;

        [Header("VFX")]
        [SerializeField] TrailRenderer trail;
        [SerializeField] ParticleSystem sparkles;

        private float maxLifetime = 3f;
        private const float tossForce = 25f;
        //private const float holdOffsetTransitionOutDuration = 0.5f;

        public bool IsHeld { get; private set; } = true;
        private float timeTossed = 0f;
        private float expireTime = 0f;

        // Collision handling
        private Rigidbody snowballRigidbody;        // Cached Rigidbody component.
        public bool hasBurst { get; private set; } = false;          // Has the snowball burst?
        private float bounceDamper = .5f;       // Multiplier to dampen bounce force.
        private int maxImpacts = 1;             // Maximum allowed impacts before burst.
        private int impactCount = 0;            // Current number of impacts.
        private float lastImpact = 0;           // Moment of previous impact.

        private bool triggerPressed;
        private float consolePeakAcceleration;
        private float consoleCurrentAcceleration;
        private float peakTime;
        private float releasedTime;


        private AudioManager audioManager;

        public string SpawnerDescription { get; private set; }

        public bool Expired { get; private set; }

        public void Awake()
        {
            snowballRigidbody = this.GetComponent<Rigidbody>();

            audioManager = SceneLookup.Get<AudioManager>();

            ShowVFX(false);
        }

        private void Start()
        {
            EventsSystemHandler.Instance.onTriggerPressTriggerButton += TriggerButtonPressed;
            EventsSystemHandler.Instance.onTriggerReleaseTriggerButton += TriggerButtonReleased;
        }

        private void TriggerButtonPressed()
        {
            triggerPressed = true;
            consolePeakAcceleration = 0;
            peakTime = 0;
            releasedTime = 0;
        }

        private void TriggerButtonReleased()
        {
            triggerPressed = false;
        }

        public void InitSnowball(string spawnerDescription, Transform newParent = null)
        {
            this.SpawnerDescription = spawnerDescription;
            // Deactivate gravity/physics on snowball while initially held
            snowballRigidbody.isKinematic = true;
            snowballRigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;

            if (newParent != null)
            {
                transform.SetParent(newParent);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }

            // BubbleScale up the model
            snowballModel.transform.localScale = Vector3.zero;
            BubbleScaleUtil.ScaleUp(snowballModel, targetScale: snowballScale, duration: scaleUpDuration);

            // Start off held by player
            // Update() will immediately position snowball at held position
            IsHeld = true;
            hasBurst = false;
            impactCount = 0;
        }

        private void OnEnable()
        {
            if (Expired)
            {
                Debug.LogError(this + " got OnEnable for expired snowball. Deactivating");
                gameObject.SetActive(false);
                return;
            }
        }

        private void Update()
        {
            // If snowball is held
            if (IsHeld)
            {
                // in editor, assign latest values for local hold offset, 
                // in case the values are being updated realtime in inspector
#if UNITY_EDITOR
                UpdateSnowballHoldOffset();
#endif                
            }

            // If snowball is tossed
            else
            {
                // At start of toss, transition out any hold offset
                //if (Time.time < timeTossed + holdOffsetTransitionOutDuration)
                //{
                //    float offsetAmount = (timeTossed + holdOffsetTransitionOutDuration - Time.time) / holdOffsetTransitionOutDuration;
                //    UpdateSnowballHoldOffset(offsetAmount);
                //}

                // Eventually burst and destroy self
                if (Time.time > expireTime)
                {
                    Burst(transform.position, Vector3.up);
                    Expire(destroy: true);
                }
            }

            ControllerPickAcceleration();
            //Debug.Log("X: " + uduConsole.GetOrientation().eulerAngles.x);
        }

        private void ControllerPickAcceleration()
        {
            if (!triggerPressed) return;

            // Update the variable value
            consoleCurrentAcceleration = UDUGetters.GetAcceleration().magnitude;

            releasedTime = Time.time;

            if (triggerPressed && consoleCurrentAcceleration > consolePeakAcceleration)
            {
                // Update the max value if the current value is higher
                consolePeakAcceleration = consoleCurrentAcceleration;
                peakTime = Time.time;
            }
        }


        // offset position by the supplied gameObject's transform
        public void SetSnowballHoldOffset(Vector3 positionOffset)
        {
            if (snowballHoldOffset != null)
            {
                snowballHoldOffset.transform.localPosition = positionOffset;
                UpdateSnowballHoldOffset();
            }
        }

        private void UpdateSnowballHoldOffset(float offsetAmount = 1f)
        {
            if (snowballHoldOffset != null)
            {
                snowballHolder.transform.localPosition = snowballHoldOffset.transform.localPosition * offsetAmount;
            }
        }

        public void DetachFromParent()
        {
            // undo the hold offset
            this.transform.position += snowballHolder.transform.localPosition;
            snowballHolder.transform.localPosition = Vector3.zero;
            snowballHoldOffset.transform.localPosition = Vector3.zero;

            // detach
            this.transform.parent = null;
            IsHeld = false;
        }


        public void TossLocallySpawnedSnowball(float tossAngle)
        {
            Vector3 force = transform.forward * tossForce;
            Vector3 torque = transform.right * Random.Range(1, 3);
            TossSnowball(tossAngle, force, torque);

            EventLocallySpawnedSnowballTossed.Invoke(this, tossAngle, force, torque);
        }

        public void TossNetworkSpawnedSnowball(float tossAngle, Vector3 force, Vector3 torque)
        {
            DetachFromParent();

            TossSnowball(tossAngle, force, torque);
        }

        private void TossSnowball(float tossAngle, Vector3 force, Vector3 torque)
        {

            float tiltThreshold = 20.0f; // Add this line to define the tilt threshold in degree


            Quaternion currentOrientation = UDUGetters.GetOrientation();

            // Convert the current orientation quaternion to a rotation matrix
            Matrix4x4 rotationMatrix = Matrix4x4.Rotate(currentOrientation);

            // Extract rotations in radians
            float pitch = Mathf.Atan2(rotationMatrix.m21, rotationMatrix.m22); // Rotation around X-axis
            float yaw = Mathf.Asin(-rotationMatrix.m20); // Rotation around Y-axis
            float roll = Mathf.Atan2(rotationMatrix.m10, rotationMatrix.m00); // Rotation around Z-axis

            // Convert rotations to degrees
            pitch *= Mathf.Rad2Deg;
            yaw *= Mathf.Rad2Deg;
            roll *= Mathf.Rad2Deg;
            // Ondrej's experiments end

            //orientationOnRelease = currentOrientation.eulerAngles.x;

            timeTossed = Time.time;

            // Activate gravity/physics on snowball
            snowballRigidbody.isKinematic = false;
            snowballRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Toss snowball upward and forward with force
            Vector3 tossRotation = this.transform.eulerAngles;
            tossRotation.x -= tossAngle;

            this.transform.rotation = Quaternion.Euler(tossRotation);

            // This handle the power of the snowball on console acceleration
            if (releasedTime - peakTime > 1f) snowballRigidbody.AddForce(this.transform.forward * ConvertValue(UDUGetters.GetAcceleration().magnitude));
            else snowballRigidbody.AddForce(this.transform.forward * ConvertValue(consolePeakAcceleration));


            // Depending on the orientation of the console at release, we spin the ball.
            float convertedAngleValue = ConvertYawToCurve(yaw, tiltThreshold);

            Debug.Log("CURVE_TAG: Pitch: " + pitch + ", Yaw: " + yaw + ", Roll: " + roll);
            Debug.Log("CURVE_TAG: convertedAngleValue: " + convertedAngleValue);

            // Apply torque
            snowballRigidbody.AddTorque(this.transform.up * convertedAngleValue);

            // Set snowball lifetime duration
            expireTime = Time.time + maxLifetime;

            // Enable trail/sparkles.
            ShowVFX(true);

            // Throw SFX
            // Use PlayAudioAtPosition instead of PlayAudioOnObject, 
            // since snowball may burst before throw SFX is done
            audioManager.PlayAudioAtPosition(AudioKeys.SFX_SnowballThrow, this.gameObject.transform.position);
        }

        float ConvertYawToCurve(float yaw, float tiltThreshold)
        {
            // The target range for the curve, with 0 being no curve and ±0.2 being maximum curve in each direction.
            float targetMin = 0f;
            float targetMax = 0.2f;

            // Clamp the yaw value between -70 and 70.
            yaw = Mathf.Clamp(yaw, -70f, 70f);

            // Calculate the absolute yaw value
            float absYaw = Mathf.Abs(yaw);

            // If the absolute yaw is less than the threshold, no curve is applied.
            if (absYaw < tiltThreshold)
            {
                return 0f;
            }

            // Calculate the normalized value between 0 and 1 based on the yaw.
            float normalizedValue = (absYaw - tiltThreshold) / (70f - tiltThreshold);

            // Calculate the curve value.
            float curveValue = targetMax * normalizedValue;

            // If the original yaw was negative, make the curve negative.
            if (yaw < 0)
            {
                curveValue *= -1;
            }

            return curveValue;
        }


        // This convert console acceleration to snowball power
        float ConvertValue(float value)
        {
            // Minimum accel of the console
            float minValue = 900f;
            // Max accel of the console
            float maxValue = 5000f;

            // Minimum power of the snowball
            float minTargetValue = 5f;
            // Max power of the snowball
            float maxTargetValue = 30f;

            // Calculate the percentage of the original value within the range
            float percentage = (value - minValue) / (maxValue - minValue);

            // Map the percentage to the target range
            float targetValue = minTargetValue + (maxTargetValue - minTargetValue) * percentage;

            return targetValue;
        }

        void OnCollisionEnter(Collision collision)
        {
            //Debug.Log("Snowball OnCollisionEnter for " + this);

            // Filter out collisions if we've decided to burst already.
            if (hasBurst) return;

            EventSnowballCollided.Invoke(this, collision);

            if (automaticallyProcessCollisions)
            {
                HandleCollision(collision);
            }
        }

        // Default collision handling logic
        public void HandleCollision(Collision collision, bool destroy = true)
        {
            //Debug.Log("Handle collision for " + this);

            // If we hit anything other than environment layers,
            // then always destroy the snowball.
            if (!(collision.gameObject.layer == AR_MESH_LAYER ||
                collision.gameObject.layer == AR_PLANES_LAYER))
            {
                // Debug.LogFormat("Non-Environment Impact: GameObject: {0} Layer: {1}",
                //     collision.gameObject.name, collision.gameObject.layer);

                // Default layer shows secondary particles, others do not.
                bool showSecondaryParticles = (collision.gameObject.layer == 0) ? true : false;

                Burst(collision, showSecondaryParticles);
                LeaveSplat(collision);
                Expire(destroy);

                return;
            }

            // If we did hit the environment, try to filter out overly frequent collisions.
            if (Time.time - lastImpact < .125f) return;

            // Check to see if we have any additional bounces before deciding to destroy.
            if (impactCount < maxImpacts)
            {
                // Impacts remaining, zero the velocity and "toss" again from the impact point.
                ContactPoint contactPoint = collision.contacts[0];
                Vector3 randomVector = UnityEngine.Random.onUnitSphere * .1f;
                Vector3 bounceVector = (contactPoint.normal + randomVector).normalized;
                snowballRigidbody.velocity = Vector3.zero;

                // BOUNCE
                snowballRigidbody.AddForce(bounceVector * tossForce * bounceDamper);

                // SFX
                audioManager.PlayAudioAtPosition(AudioKeys.SFX_Snowball_Bump, this.gameObject.transform.position);

                LeaveSplat(collision);

                lastImpact = Time.time;
                impactCount++;
            }
            else
            {
                // No impacts left, destroy the snowball.
                Burst(collision, true);

                //LeaveSplat(collision);

                Expire(destroy);
            }
        }

        void LeaveSplat(Collision collision)
        {
            if (snowballSplatPrefab != null && collision.gameObject.tag == ALLOW_SPLATS_TAG)
            {

#if UNITY_EDITOR
                foreach (ContactPoint point in collision.contacts)
                {
                    Debug.DrawRay(point.point, point.normal * .1f, Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f), 10f);
                }
#endif

                ContactPoint contactPoint = collision.contacts[0];

                Vector3 position = contactPoint.point;
                Quaternion rotation =
                    Quaternion.AngleAxis(Random.Range(0, 360), contactPoint.normal) *
                    Quaternion.FromToRotation(Vector3.up, contactPoint.normal);

                Instantiate(snowballSplatPrefab, position, rotation);
            }
        }

        void Burst(Collision collision, bool showSecondaryParticles = false)
        {
            ContactPoint contactPoint = collision.contacts[0];
            Burst(contactPoint.point, contactPoint.normal, showSecondaryParticles);
        }

        public void Burst(Vector3 position, Vector3 normal, bool showSecondaryParticles = false)
        {
            //Console outputs
            //if (uduConsole != null) uduConsole.SetVibrationAndStart("/spiffs/bd1_01.wav", false);

            // Always hide the VFX
            ShowVFX(false);

            if (snowballBurstPrefab != null && !hasBurst)
            {
                //Debug.Log("Burst snowball " + this + " isHeld? " + IsHeld);

                GameObject snowballBurstInstance = Instantiate(snowballBurstPrefab,
                                                                position,
                                                                Quaternion.FromToRotation(Vector3.up, normal));
                snowballBurstInstance.transform.localScale = transform.localScale;
                hasBurst = true;

                if (showSecondaryParticles)
                {
                    SnowballBurst snowballBurst = snowballBurstInstance.GetComponent<SnowballBurst>();
                    snowballBurst.TriggerSecondaryParticles();
                }

                // SFX
                audioManager.PlayAudioAtPosition(AudioKeys.SFX_Snowball_Bump, position);


            }
        }

        // Immediately deactivate this snowball, and schedule its delayed destruction if specified
        public void Expire(bool destroy)
        {
            if (!Expired)
            {
                EventSnowballExpiring.Invoke(this);
                Expired = true;

                ShowVFX(false);
                gameObject.SetActive(false);

                if (destroy)
                {
                    // Schedule this gameobject's destruction after 5 seconds, so any events can be handled
                    Destroy(gameObject, DestroyDelay);
                }
            }
        }

        private void ShowVFX(bool show)
        {
            trail.emitting = show;
            var emission = sparkles.emission;
            emission.enabled = show;
        }

        public override string ToString()
        {
            return "Snowball spawned by " + SpawnerDescription;
        }
    }
}