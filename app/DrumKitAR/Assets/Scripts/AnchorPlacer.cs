using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

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

   private void Update()
{
    if (Input.touchCount > 0)
    {
        Touch touch = Input.GetTouch(0);
        Debug.Log($"Touch detected - phase: {touch.phase} position: {touch.position}");
        
        if (touch.phase != TouchPhase.Began) return;
        PlaceAnchorAtTouch(touch.position);
    }
}

private void PlaceAnchorAtTouch(Vector2 touchPosition)
{
    Debug.Log($"Attempting placement at {touchPosition}");

    if (ARSessionManager.Instance != null &&
        ARSessionManager.Instance.CurrentLighting == ARSessionManager.LightingCondition.Poor)
    {
        Debug.LogWarning("Lighting too poor for accurate placement");
        return;
    }

    bool hitDetected = raycastManager.Raycast(touchPosition, hits, 
        TrackableType.PlaneWithinBounds | TrackableType.PlaneWithinPolygon | TrackableType.PlaneEstimated);
    Debug.Log($"Raycast hit detected: {hitDetected}, hits count: {hits.Count}");

    if (!hitDetected)
    {
        Debug.Log("No surface detected at touch point");
        return;
    }

    Pose hitPose = hits[0].pose;

    GameObject anchorObj = new GameObject("Anchor");
    anchorObj.transform.position = hitPose.position;
    anchorObj.transform.rotation = hitPose.rotation;

    ARAnchor anchor = anchorObj.AddComponent<ARAnchor>();
    placedAnchors.Add(anchor);

    if (anchorMarkerPrefab != null)
    {
        GameObject marker = Instantiate(anchorMarkerPrefab, hitPose.position, hitPose.rotation);
        placedMarkers.Add(marker);
    }

    Debug.Log($"Anchor placed at {hitPose.position}. Total anchors: {placedAnchors.Count}");

    if (placedAnchors.Count >= 2)
        CalculateDistances();
}
    private void CalculateDistances()
    {
        for (int i = 0; i < placedAnchors.Count - 1; i++)
        {
            for (int j = i + 1; j < placedAnchors.Count; j++)
            {
                float distanceMetres = Vector3.Distance(
                    placedAnchors[i].transform.position,
                    placedAnchors[j].transform.position
                );
                float distanceCm = distanceMetres * 100f;

                Vector3 direction = placedAnchors[j].transform.position - placedAnchors[i].transform.position;
                float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

                Debug.Log($"Anchor {i} to Anchor {j}: {distanceCm:F1}cm, angle: {angle:F1} degrees");
            }
        }
    }

    public void ClearAllAnchors()
    {
        foreach (var anchor in placedAnchors)
        {
            if (anchor != null)
                Destroy(anchor.gameObject);
        }
        foreach (var marker in placedMarkers)
        {
            if (marker != null)
                Destroy(marker);
        }
        placedAnchors.Clear();
        placedMarkers.Clear();
        Debug.Log("All anchors cleared");
    }

    public List<ARAnchor> GetPlacedAnchors() => placedAnchors;
}