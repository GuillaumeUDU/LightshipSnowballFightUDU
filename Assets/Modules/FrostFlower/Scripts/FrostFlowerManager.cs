using Niantic.ARVoyage.Vps;

using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

namespace Niantic.ARVoyage.FrostFlower
{
    public enum FrostFlowerLocationState
    {
        Unvisited,
        Planted,
        Harvested
    }

    /// <summary>
    /// Serializable class for frost flower save data.
    /// </summary>
    [System.Serializable]
    public class FrostFlowerSaveData
    {
        public FrostFlowerLocationState locationState = FrostFlowerLocationState.Unvisited;
        public long timestamp;
        public bool notificationShown = false;

        public void UpdateTimestamp()
        {
            timestamp = System.DateTimeOffset.Now.ToUnixTimeSeconds();
        }

        public long GetAgeInSeconds()
        {
            long currentTimeStamp = System.DateTimeOffset.Now.ToUnixTimeSeconds();
            return currentTimeStamp - timestamp;
        }
    }

    /// <summary>
    /// High-level manager class for Frost Flower VPS locations. It handles most
    /// aspects of targeting and planting seeds and all associated animation and
    /// visual effects. It is also responsible for handling the save/load of
    /// frost flower data between user visits to a given location.
    /// </summary>
    public class FrostFlowerManager : MonoBehaviour
    {
        public AppEvent RespawnComplete;
        public AppEvent<Transform> SeedPlanted;

        [Header("Content")]
        [SerializeField] Transform cameraFollower;
        [SerializeField] public Transform seedTransform;
        [SerializeField] GameObject seedPrefab;
        [SerializeField] Transform seedContainer;

        [SerializeField] GameObject burstPrefab;

        [SerializeField] GameObject flowerHeroPrefabA;
        [SerializeField] GameObject flowerHeroPrefabB;
        [SerializeField] GameObject[] flowerSecondaryPrefabs;
        [SerializeField] GameObject[] flowerMossPrefabs;
        [SerializeField] Transform saveRoot;

        [Header("Arc")]
        [SerializeField] LineRenderer lineRenderer;
        [SerializeField] int segments = 32;
        [SerializeField] float widthMultiplier = .01f;
        [SerializeField] SpriteRenderer reticle;

        [Header("Debug")]
        [SerializeField] Button spawnButton;
        [SerializeField] Button clearButton;
        [SerializeField] Button saveButton;
        [SerializeField] Button loadButton;

        public bool HasValidTarget { get; private set; } = false;
        public bool PlantingEnabled { get; set; } = false;

        private Vector3 currentTargetPosition;
        private Vector3 currentTargetNormal;

        private Coroutine launchSeedRoutine;
        private Coroutine showSeedRoutine;

        private Transform rootTransform;

        private AudioManager audioManager;

        private FrostFlowerLocationState locationState = FrostFlowerLocationState.Unvisited;

        public void Awake()
        {
            // Default root transform to this gameobject unless told otherwise.
            rootTransform = transform;
            audioManager = SceneLookup.Get<AudioManager>();
            SceneLookup.Add(this, persistAcrossScenes: false);
        }

        void OnDestroy()
        {
            // Remove from the SceneLookup when destroyed
            SceneLookup.Remove(this);
        }

        void Update()
        {
            // Update camera follower position/orientation.
            cameraFollower.position = Camera.main.transform.position;
            cameraFollower.eulerAngles = Camera.main.transform.eulerAngles;
        }

        void LateUpdate()
        {
            // Show/hide seed container.
            seedContainer.gameObject.SetActive(PlantingEnabled);

            // Check for valid planting locations on the AR Mesh.
            LayerMask layerMask = LayerMask.GetMask("AR Mesh");
            Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
            RaycastHit hit;

            if (PlantingEnabled && Physics.Raycast(ray, out hit, 10, layerMask))
            {
                // Secondary raycast to look for blockers.
                RaycastHit blockerHit;
                if (Physics.Raycast(ray, out blockerHit, 10, ~layerMask))
                {
                    HasValidTarget = false;
                }
                else
                {
                    HasValidTarget = true;
                }

                // Target.
                currentTargetPosition = hit.point;
                currentTargetNormal = hit.normal;

                // Update line renderer.
                {
                    float distance = Vector3.Distance(seedTransform.position, hit.point);
                    float height = distance * .25f;

                    Vector3[] linePositions = new Vector3[segments];
                    for (int i = 0; i < segments; i++)
                    {
                        linePositions[i] = SampleParabola(seedTransform.position, hit.point, height, i / (float)segments);
                    }

                    reticle.transform.parent.position = hit.point + (hit.normal * .05f);
                    reticle.gameObject.SetActive(true);

                    if (HasValidTarget)
                    {
                        reticle.transform.parent.rotation = Quaternion.Lerp(reticle.transform.parent.rotation, Quaternion.FromToRotation(Vector3.up, hit.normal), Time.deltaTime / .15f);
                    }

                    lineRenderer.positionCount = segments;
                    lineRenderer.SetPositions(linePositions);
                    lineRenderer.widthMultiplier = widthMultiplier;
                    lineRenderer.enabled = true;

                    // Reticle/Line fade
                    Color targetColor = Color.white;
                    targetColor.a = (HasValidTarget) ? 1 : 0;
                    reticle.color = Color.Lerp(reticle.color, targetColor, Time.deltaTime / .1f);
                    lineRenderer.material.color = Color.Lerp(lineRenderer.material.color, targetColor, Time.deltaTime / .1f);
                }

            }
            else
            {
                // Line renderer.
                lineRenderer.enabled = false;
                reticle.gameObject.SetActive(false);

                // Default state.
                HasValidTarget = false;
            }
        }

        // Clicks off the animated routines for throwing the seed
        // and growing new plants.
        public void Spawn(float reshowDelay = 0f)
        {
            if (HasValidTarget && launchSeedRoutine == null)
            {
                // Spawn and launch a seed
                Debug.DrawLine(Camera.main.transform.position, currentTargetPosition, Color.blue, 4);
                launchSeedRoutine = StartCoroutine(LaunchSeedRoutine(currentTargetPosition, currentTargetNormal));

                // Reshow held seed
                ShowSeed(delay: reshowDelay);
            }
        }

        // Calls the internal method to clear spawned art assets.
        public void Clear()
        {
            ClearAll();
        }

        // Returns an object for saving the state of this FF location.
        public FrostFlowerSaveData GetSaveData()
        {
            FrostFlowerSaveData saveData = new FrostFlowerSaveData();

            saveData.UpdateTimestamp();
            saveData.locationState = locationState;

            return saveData;
        }

        // Stores a state flag for this location based on actions the
        // user has taken so far.
        public void SetLocationState(FrostFlowerLocationState locationState)
        {
            this.locationState = locationState;
        }

        // Clicks of respawn animation routine.
        public void RespawnPlants(List<Transform> transforms)
        {
            StartCoroutine(RespawnPlantsRoutine(transforms));
        }

        // Routine for animated respawn of previously planted plants.
        private IEnumerator RespawnPlantsRoutine(List<Transform> transforms)
        {
            yield return new WaitForSeconds(1f);

            int spawnCount = 0;
            foreach (Transform transform in transforms)
            {
                StartCoroutine(SpawnPrimaryRoutine(transform, 2, true, spawnCount));
                spawnCount++;
            }

            float respawnWait = (transforms.Count - 1) + 3.15f;
            respawnWait += 3f;  // Additional delay after growth.

            yield return new WaitForSeconds(respawnWait);
            RespawnComplete?.Invoke();
        }

        // Position the held seed in worldspace. 
        public void SetSeedOffset(Vector3 position)
        {
            seedContainer.transform.localPosition = position;
        }

        // Clear all spawned plant art.
        private void ClearAll()
        {
            foreach (Transform child in saveRoot)
            {
                GameObject.Destroy(child.gameObject);
            }
        }

        // Handle visibility of held seed.
        public void ShowSeed(float delay = 0f)
        {
            // hide seed
            seedTransform.localScale = Vector3.zero;

            // scale up seed
            if (showSeedRoutine != null) StopCoroutine(showSeedRoutine);
            showSeedRoutine = StartCoroutine(ShowSeedRoutine(delay));
        }

        // Animated reveal of the held seed.
        IEnumerator ShowSeedRoutine(float delay = 0f)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            yield return BubbleScaleUtil.ScaleUp(seedTransform.gameObject, 1f, 1);
            showSeedRoutine = null;
        }

        // Handles animation of the seed toward the targeted destination
        // and begins the process of growing plants.
        IEnumerator LaunchSeedRoutine(Vector3 spawnPoint, Vector3 spawnNormal)
        {
            audioManager.PlayAudioNonSpatial(AudioKeys.SFX_Flower_SeedFlick);

            GameObject seedInstance = Instantiate(seedPrefab, seedTransform.position, seedTransform.rotation, transform);

            Vector3 startPosition = seedInstance.transform.position;
            Vector3 endPosition = spawnPoint;

            float distance = Vector3.Distance(startPosition, endPosition);
            float height = distance * .25f;
            float duration = distance * .2f;

            yield return InterpolationUtil.LinearInterpolation(seedInstance, seedInstance,
                duration,
                onUpdate: (t) =>
                 {
                     seedInstance.transform.position = SampleParabola(startPosition, endPosition, height, t);
                 }
            );

            Destroy(seedInstance);
            launchSeedRoutine = null;

            // Rotation based on normal
            Quaternion spawnRotation = Quaternion.FromToRotation(Vector3.up, spawnNormal);

            // Instantiate temporary burst.
            GameObject burstInstance = Instantiate(burstPrefab, transform);
            burstInstance.transform.position = spawnPoint;
            burstInstance.transform.rotation = spawnRotation;
            Destroy(burstInstance, 2);
            audioManager.PlayAudioAtPosition(AudioKeys.SFX_Flower_SeedImpact, spawnPoint);

            // Container transform
            Transform spawnParent = new GameObject("Spawn Point").transform;
            spawnParent.transform.SetParent(saveRoot);
            spawnParent.transform.position = spawnPoint;
            spawnParent.transform.rotation = spawnRotation;

            // Fire planted event after instantiation.
            Debug.Log("Seed planted.");
            SeedPlanted?.Invoke(spawnParent);

            // Spawn Plants.
            yield return StartCoroutine(SpawnPrimaryRoutine(spawnParent));
        }

        // Creates and manages the primary "hero" plants.
        IEnumerator SpawnPrimaryRoutine(Transform spawnParent, int multiplier = 1, bool hasBloomed = false, float delay = 0)
        {
            // Handle delay.
            if (delay > 0) yield return new WaitForSeconds(delay);

            // Container transform

            audioManager.PlayAudioOnObject(AudioKeys.SFX_Plant_Grow,
                                            targetObject: spawnParent.gameObject);

            // Instantiate main flower.
            GameObject prefab = (!hasBloomed) ? flowerHeroPrefabA : flowerHeroPrefabB;
            GameObject instance = Instantiate(prefab, spawnParent);

            // Animate primary flower.
            Animator animator = instance.GetComponentInChildren<Animator>();
            if (animator != null) animator.speed = .3f;

            // Cache the main camera since it can be deactivated while this is being cleaned up
            Transform mainCameraTransform = Camera.main.transform;

            // Wait to be visible.
            yield return new WaitWhile(() =>
            {
                Vector3 toTransform = Vector3.Normalize(instance.transform.position - mainCameraTransform.position);
                float angle = Vector3.Dot(mainCameraTransform.forward, toTransform);

                bool transformHidden = angle < .75f;

                return transformHidden;
            });

            Debug.Log("Flower visible!");

            // Spawn secondary flowers and moss.
            yield return StartCoroutine(SpawnSecondaryRoutine(spawnParent, 64 * multiplier, .5f * multiplier));
        }

        // Raycasts "down" around the hero plant with an increasing radius
        // to place smaller secondary plants and weeds.
        IEnumerator SpawnSecondaryRoutine(Transform spawnParent, int desiredCount, float spawnRadius)
        {
            // Attempt to spawn children.
            int maxAttempts = desiredCount * 2;

            int placedCount = 0;
            int attempts = 0;

            float theta = Random.value * Mathf.PI * 2;
            while (placedCount < desiredCount && attempts < maxAttempts)
            {
                if (spawnParent == null) yield break;

                float altitude = .25f;
                float progress = attempts / (float)maxAttempts;

                theta += (Mathf.PI / 4f) * (Random.value * Mathf.PI / 4f);
                float radius = .1f + (spawnRadius * progress);

                // Increase outliers towards edges.
                if (Random.value < (.25f * progress)) radius *= 1.125f;

                Vector2 randomPoint;// = Random.insideUnitCircle * spawnRadius;
                randomPoint.x = (radius) * Mathf.Cos(theta);
                randomPoint.y = (radius) * Mathf.Sin(theta);

                Vector3 raycastOrigin = spawnParent.TransformPoint(new Vector3(randomPoint.x, altitude, randomPoint.y));
                Ray ray = new Ray(raycastOrigin, -spawnParent.up);
                Debug.DrawRay(ray.origin, ray.direction, Color.red, 1f);

                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, 1) && (hit.collider.gameObject.layer == LayerMask.NameToLayer("AR Mesh")))
                {
                    Debug.DrawLine(raycastOrigin, hit.point, Color.green, 5f);

                    // Weight selection toward moss.
                    float plantProbability = .2f * Mathf.Pow(Mathf.Sin(progress * Mathf.PI), 4);

                    GameObject secondaryPrefab;
                    if (Random.value >= plantProbability)
                    {
                        secondaryPrefab = flowerMossPrefabs[Mathf.RoundToInt(progress * (flowerMossPrefabs.Length - 1))];
                    }
                    else
                    {
                        secondaryPrefab = flowerSecondaryPrefabs[Random.Range(0, flowerSecondaryPrefabs.Length)];
                    }

                    // Instantiate and place.
                    Quaternion secondaryOrientation = Quaternion.FromToRotation(Vector3.up, hit.normal);
                    GameObject secondaryInstance = Instantiate(secondaryPrefab, hit.point, secondaryOrientation * Quaternion.Euler(0, Random.Range(0, 360), 0));

                    secondaryInstance.transform.SetParent(spawnParent);
                    //secondaryInstance.transform.localScale = Vector3.one * (.6f + (1 - progress) * .4f);

                    Animator animator = secondaryInstance.GetComponentInChildren<Animator>();
                    if (animator != null) animator.speed = Random.Range(.75f, 1.25f);

                    //

                    placedCount++;
                }

                attempts++;

                if (attempts % 2 == 0) yield return null;
            }

        }

        // Utility method for calculating a parabolic arc for the seed.
        Vector3 SampleParabola(Vector3 start, Vector3 end, float height, float t)
        {
            System.Func<float, float> f = (x) => -1 * height * x * x + 1 * height * x;

            Vector3 current = Vector3.Lerp(start, end, t);
            return new Vector3(current.x, f(t) + current.y, current.z);
        }

        // Handles award badge presentation.
        public bool BadgeJustUnlocked()
        {
            // If garden had been visited before, and we haven't unlocked badge, unlock it
            if (locationState != FrostFlowerLocationState.Unvisited &&
                !SaveUtil.IsBadgeUnlocked(VpsSceneManager.FrostFlowerBadgeKey))
            {
                // BADGE UNLOCKED
                SaveUtil.SaveBadgeUnlocked(VpsSceneManager.FrostFlowerBadgeKey);
                return true;
            }

            return false;
        }

    }
}