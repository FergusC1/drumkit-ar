// AnchorPlacer.cs
// Handles touch-based anchor placement on ARCore-detected surfaces.
// Anchors are used for spatial measurement throughout the app.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;

// Alias resolves ambiguity between legacy UnityEngine.Touch
// and the new Input System's EnhancedTouch.Touch
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class AnchorPlacer : MonoBehaviour
{
    [Header("AR Components")]
    [SerializeField] private ARRaycastManager raycastManager;
    [SerializeField] private ARAnchorManager anchorManager;

    [Header("Anchor Marker")]
    [SerializeField] private GameObject anchorMarkerPrefab;

    private List<ARRaycastHit> hits = new List<ARRaycastHit>();
    private List<ARAnchor> placedAnchors = new List<ARAnchor>();
    private List<GameObject> placedMarkers = new List<GameObject>();

    // Singleton allows other scripts to access this instance without
    // needing a direct Inspector reference
    public static AnchorPlacer Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // Enhanced touch support must be explicitly enabled with the new Input System
    private void OnEnable() => EnhancedTouchSupport.Enable();
    private void OnDisable() => EnhancedTouchSupport.Disable();

    private void Update()
    {
        if (Touch.activeTouches.Count == 0) return;

        var touch = Touch.activeTouches[0];
        if (touch.phase != UnityEngine.InputSystem.TouchPhase.Began) return;

        Vector2 pos = touch.screenPosition;

        // Unity's OnGUI doesn't block touch events on UI elements,
        // so we manually check if the tap landed on a UI region
        if (IsOnUI(pos)) return;

        PlaceAnchorAtTouch(pos);
    }

    // Returns true if the screen position is within a known UI area.
    // Coordinates match the layout defined in MeasurementUI.cs.
    private bool IsOnUI(Vector2 screenPos)
    {
        int btnX = Screen.width - 140 - 20;

        if (screenPos.x > btnX) return true; // Right button column
        if (screenPos.y > Screen.height - 100 && screenPos.x < 370) return true; // Top-left status boxes
        if (screenPos.x < 320 && screenPos.y < Screen.height - 140 &&
            screenPos.y > Screen.height / 2 - 200) return true; // Element selector panel

        return false;
    }

    // Raycasts against ARCore detected planes and places an anchor at the hit point.
    // Forwards the position to CalibrationManager or GuidedSetupManager if active.
    private void PlaceAnchorAtTouch(Vector2 touchPosition)
    {
        // Block placement in poor lighting to avoid inaccurate measurements
        if (ARSessionManager.Instance != null &&
            ARSessionManager.Instance.CurrentLighting == ARSessionManager.LightingCondition.Poor)
        {
            Debug.LogWarning("Lighting too poor for accurate placement");
            return;
        }

        // PlaneEstimated included to allow hits on partially-mapped surfaces,
        // improving responsiveness on devices slow to detect planes (e.g. A36)
        bool hitDetected = raycastManager.Raycast(touchPosition, hits,
            TrackableType.PlaneWithinBounds |
            TrackableType.PlaneWithinPolygon |
            TrackableType.PlaneEstimated);

        if (!hitDetected) return;

        // Sort by distance - ARCore may return a distant plane before a closer one
        hits.Sort((a, b) => a.distance.CompareTo(b.distance));
        Pose hitPose = hits[0].pose;

        // ARAnchor keeps the object locked to the real-world surface
        // as ARCore continues to refine its spatial map
        GameObject anchorObj = new GameObject("Anchor");
        anchorObj.transform.position = hitPose.position;
        anchorObj.transform.rotation = hitPose.rotation;

        ARAnchor anchor = anchorObj.AddComponent<ARAnchor>();
        placedAnchors.Add(anchor);

        if (anchorMarkerPrefab != null)
            placedMarkers.Add(Instantiate(anchorMarkerPrefab, hitPose.position, hitPose.rotation));

        // Forward to calibration if a calibration session is in progress
        if (CalibrationManager.Instance != null &&
            CalibrationManager.Instance.State != CalibrationManager.CalibrationState.Idle &&
            CalibrationManager.Instance.State != CalibrationManager.CalibrationState.Complete)
        {
            CalibrationManager.Instance.RegisterAnchorForCalibration(hitPose.position);
        }

        // If guided setup is waiting for an origin point, use this tap as the origin
        // and return early so it isn't also added as a measurement anchor
        if (GuidedSetupManager.Instance != null &&
            GuidedSetupManager.Instance.State == GuidedSetupManager.GuidedSetupState.PlacingOrigin)
        {
            GuidedSetupManager.Instance.PlaceOrigin(hitPose.position);
            return;
        }

        if (placedAnchors.Count >= 2)
            CalculateDistances();
    }

    // Calculates distance (cm) and bearing angle between every anchor pair.
    // O(n²) complexity is acceptable for the expected 5-15 anchors per session.
    private void CalculateDistances()
    {
        for (int i = 0; i < placedAnchors.Count - 1; i++)
        {
            for (int j = i + 1; j < placedAnchors.Count; j++)
            {
                // Unity world units are metres, multiply by 100 for centimetres
                float distanceCm = Vector3.Distance(
                    placedAnchors[i].transform.position,
                    placedAnchors[j].transform.position) * 100f;

                Vector3 direction = placedAnchors[j].transform.position -
                    placedAnchors[i].transform.position;
                float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

                Debug.Log($"Anchor {i}→{j}: {distanceCm:F1}cm at {angle:F1}°");
            }
        }
    }

    // Destroys all anchors and markers, resetting to a clean state
    public void ClearAllAnchors()
    {
        foreach (var anchor in placedAnchors)
            if (anchor != null) Destroy(anchor.gameObject);

        foreach (var marker in placedMarkers)
            if (marker != null) Destroy(marker);

        placedAnchors.Clear();
        placedMarkers.Clear();
    }

    // Read-only access to placed anchors for MeasurementUI and GuidedSetupManager
    public List<ARAnchor> GetPlacedAnchors() => placedAnchors;
}