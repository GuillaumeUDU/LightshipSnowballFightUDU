using Niantic.ARDK.AR;
using Niantic.ARDK.AR.Awareness.Semantics;
using Niantic.ARDK.Configuration;
using Niantic.ARDK.Extensions;

using Niantic.ARVoyage.Loading;
using Niantic.ARVoyage.Vps;

using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

namespace Niantic.ARVoyage
{
    /// <summary>
    /// Manager class for the placement utility. It allows for dynamically
    /// instantiating and positioning objects in an AR scene with various
    /// related hooks and utilities.
    /// </summary>
    public class PlacementManager : MonoBehaviour
    {
        [Header("ARDK")]
        [SerializeField] ARDepthManager arDepthManager;
        [SerializeField] ARSemanticSegmentationManager arSemanticSegmentationManager;

        [Header("Selection")]
        [SerializeField] Button selectButton;
        [SerializeField] Button deselectButton;
        [SerializeField] Button repositionButton;
        [SerializeField] Button deleteButton;

        [Header("Status")]
        [SerializeField] Text selectionText;
        [SerializeField] Text distanceText;

        [Header("Place")]
        [SerializeField] Dropdown placeDropdown;
        [SerializeField] Button placeButton;
        [SerializeField] List<GameObject> placePrefabs;

        [Header("Transform")]
        [SerializeField] PlacementControl transformRotationY;
        [SerializeField] PlacementControl transformPositionY;
        [SerializeField] PlacementControl transformScale;
        [SerializeField] PlacementControl transformPositionXZ;

        private Vector3 startRotation;
        private Vector3 startPosition;
        private Vector3 startScale;

        [Header("Depth")]
        [SerializeField] Toggle toggleShowDepth;
        [SerializeField] Dropdown depthOcclusionDropdown;
        [SerializeField] Dropdown depthInterpolationDropdown;
        // [SerializeField] Dropdown depthSegmentationDropdown;

        [SerializeField] RectTransform depthSegmentationContainer;
        [SerializeField] GameObject depthSegmentationTemplate;

        private HashSet<string> depthSuppressionChannels = new HashSet<string>();

        [Header("Other")]
        [SerializeField] Transform reticle;
        [SerializeField] Transform gizmo;
        [SerializeField] Transform target;

        void Awake()
        {
            // Hide gizmo.
            gizmo.gameObject.SetActive(false);

            // General handlers.
            {
                selectButton.onClick.AddListener(() =>
                {
                    Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
                    RaycastHit hit;

                    if (Physics.Raycast(ray, out hit))
                    {
                        Debug.Log(hit.transform.gameObject.name);
                        target = hit.transform;

                        gizmo.SetParent(target, false);
                        gizmo.gameObject.SetActive(true);
                    }
                });

                deselectButton.onClick.AddListener(() =>
                {
                    gizmo.gameObject.SetActive(false);
                    gizmo.transform.SetParent(null, false);

                    target = null;
                });

                repositionButton.onClick.AddListener(() =>
                {
                    if (target == null) return;

                    Vector3 placePosition = arDepthManager.DepthBufferProcessor.GetWorldPosition(Screen.width / 2, Screen.height / 2);
                    target.transform.position = placePosition;
                });

                deleteButton.onClick.AddListener(() =>
                {
                    gizmo.gameObject.SetActive(false);
                    gizmo.transform.SetParent(null, false);

                    if (target != null) Destroy(target.gameObject);
                    target = null;
                });
            }

            // Place handlers.
            {
                placeDropdown.ClearOptions();
                foreach (GameObject prefab in placePrefabs)
                {
                    placeDropdown.options.Add(new Dropdown.OptionData(prefab.name));
                }
                placeDropdown.RefreshShownValue();

                placeButton.onClick.AddListener(() =>
                {
                    int index = placeDropdown.value;
                    Debug.Log(placePrefabs[index].name);

                    Vector3 placePosition = arDepthManager.DepthBufferProcessor.GetWorldPosition(Screen.width / 2, Screen.height / 2);

                    GameObject instance = Instantiate(placePrefabs[index], placePosition, Quaternion.identity);
                    target = instance.transform;

                    gizmo.SetParent(target, false);
                    gizmo.gameObject.SetActive(true);

                    Debug.Log("Placed model at: " + instance.transform.position);
                });
            }

            // Transform handlers.
            {
                // RotationY

                transformRotationY.PlacementBegin.AddListener(() =>
                {
                    if (target == null) return;
                    startRotation = target.transform.rotation.eulerAngles;
                });

                transformRotationY.PlacementUpdate.AddListener((delta) =>
                {
                    if (target == null) return;
                    target.transform.rotation = Quaternion.Euler(startRotation.x, startRotation.y + (delta.x / 10f), startRotation.z);
                });

                // PositionY

                transformPositionY.PlacementBegin.AddListener(() =>
                {
                    if (target == null) return;
                    startPosition = target.transform.position;
                });

                transformPositionY.PlacementUpdate.AddListener((delta) =>
                {
                    if (target == null) return;
                    target.transform.position = new Vector3(startPosition.x, startPosition.y + (delta.y / 100f), startPosition.z);
                });

                // Position XZ

                transformPositionXZ.PlacementBegin.AddListener(() =>
                {
                    if (target == null) return;
                    startPosition = target.transform.position;
                });

                transformPositionXZ.PlacementUpdate.AddListener((delta) =>
                {
                    if (target == null) return;

                    Vector3 cameraForward = Camera.main.transform.forward;
                    cameraForward.y = 0;
                    cameraForward.Normalize();

                    Vector3 cameraRight = Camera.main.transform.right;
                    cameraRight.y = 0;
                    cameraRight.Normalize();

                    target.transform.position = startPosition + (cameraForward * (delta.y / 100f) + (cameraRight * (delta.x / 100f)));
                });

                // Scale

                transformScale.PlacementBegin.AddListener(() =>
                {
                    if (target == null) return;
                    startScale = target.transform.localScale;
                });

                transformScale.PlacementUpdate.AddListener((delta) =>
                {
                    if (target == null) return;
                    target.transform.localScale = new Vector3(startScale.x + (delta.x / 100f), startScale.y + (delta.x / 100f), startScale.z + (delta.x / 100f));
                });
            }

            // Depth Handlers
            {
                toggleShowDepth.onValueChanged.AddListener((value) =>
                {
                    Debug.Log("Toggle depth: " + value);
                    arDepthManager.ToggleDebugVisualization(value);
                });

                // Interpolation.
                depthInterpolationDropdown.ClearOptions();
                foreach (InterpolationMode interpolationMode in System.Enum.GetValues(typeof(InterpolationMode)))
                {
                    depthInterpolationDropdown.options.Add(new Dropdown.OptionData("InterpolationMode." + interpolationMode.ToString()));
                }

                depthInterpolationDropdown.value = (int)arDepthManager.DepthBufferProcessor.InterpolationMode;
                depthInterpolationDropdown.RefreshShownValue();

                depthInterpolationDropdown.onValueChanged.AddListener((value) =>
                {
                    arDepthManager.DepthBufferProcessor.InterpolationMode = (InterpolationMode)value;
                });

                // Occlusion.
                depthOcclusionDropdown.ClearOptions();
                foreach (ARDepthManager.OcclusionMode occlusionMode in System.Enum.GetValues(typeof(ARDepthManager.OcclusionMode)))
                {
                    depthOcclusionDropdown.options.Add(new Dropdown.OptionData("OcclusionMode." + occlusionMode.ToString()));
                }

                depthOcclusionDropdown.value = (int)arDepthManager.OcclusionTechnique;
                depthOcclusionDropdown.RefreshShownValue();

                depthOcclusionDropdown.onValueChanged.AddListener((value) =>
                {
                    arDepthManager.OcclusionTechnique = (ARDepthManager.OcclusionMode)value;
                });

                // Semantic segmentation.
                arSemanticSegmentationManager.SemanticBufferInitialized += (args) =>
                {
                    ISemanticBuffer semanticBuffer = args.Sender.AwarenessBuffer;

                    // Populate list.
                    foreach (string channel in arSemanticSegmentationManager.SemanticBufferProcessor.Channels)
                    {
                        GameObject instance = Instantiate(depthSegmentationTemplate, depthSegmentationContainer);

                        Toggle toggle = instance.GetComponent<Toggle>();
                        if (toggle != null)
                        {
                            // Default "ground" channel to active.
                            if (channel == "ground")
                            {
                                toggle.SetIsOnWithoutNotify(true);
                                depthSuppressionChannels.Add("ground");
                            }

                            toggle.onValueChanged.AddListener((value) =>
                            {
                                // Manage set.
                                if (depthSuppressionChannels.Contains(channel) && value == false)
                                {
                                    depthSuppressionChannels.Remove(channel);
                                }
                                else if (!depthSuppressionChannels.Contains(channel) && value == true)
                                {
                                    depthSuppressionChannels.Add(channel);
                                }

                                // Print values.
                                Debug.Log("Updating suppression channels.");
                                foreach (string segmentationChannel in depthSuppressionChannels)
                                {
                                    Debug.Log("Suppressing: " + segmentationChannel);
                                }

                                // Create array.
                                string[] stringArray = new string[depthSuppressionChannels.Count];
                                depthSuppressionChannels.CopyTo(stringArray);

#if SEGMENTATION_HACK
                                arSemanticSegmentationManager.UpdateChannels(stringArray);
#endif
                            });
                        }

                        Text textField = instance.GetComponentInChildren<Text>();
                        if (textField != null) textField.text = "Suppress: " + channel;
                    }
                };
            }
        }

        void Update()
        {

            // Reticle
            {
                Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit))
                {
                    reticle.position = hit.point;
                }
                else
                {
                    reticle.position = arDepthManager.DepthBufferProcessor.GetWorldPosition(Screen.width / 2, Screen.height / 2);
                }
            }

            // Status
            {
                string selection = ((target == null) ? "N/A" : target.gameObject.name);

                string distance;
                if (target == null)
                {
                    distance = Vector3.Distance(Camera.main.transform.position, reticle.position).ToString("0.00") + "m";
                }
                else
                {
                    distance = Vector3.Distance(Camera.main.transform.position, target.transform.position).ToString("0.00") + "m";
                }

                selectionText.text = "Selection\n" + selection;
                distanceText.text = "Distance\n" + distance;
            }

        }

    }
}