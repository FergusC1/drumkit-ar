// GuidedSetupManager.cs
// Loads a saved kit profile from the backend and places AR target markers
// at the stored element positions, allowing a drummer to reproduce a known
// layout in a new physical space.
//
// Flow: Load profile → place origin tap → markers spawn → user places drums
//       near markers → markers turn green when within snap distance → complete

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class GuidedSetupManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float snapDistanceCm = 15f;      // Distance at which a marker turns green
    [SerializeField] private GameObject targetMarkerPrefab;    // Optional custom marker prefab

    public enum GuidedSetupState
    {
        Idle,
        LoadingProfile,  // Awaiting API response
        PlacingOrigin,   // Waiting for user to tap the floor to set the kit origin
        Active,          // Markers placed, user is positioning drums
        Complete         // All elements snapped
    }

    public GuidedSetupState State { get; private set; } = GuidedSetupState.Idle;
    public static GuidedSetupManager Instance { get; private set; }

    private List<TargetMarker> targetMarkers = new List<TargetMarker>();
    private Vector3 originPosition;
    private bool originPlaced = false;
    private string currentProfileId = "";

    // Represents a single target position in the guided layout
    [System.Serializable]
    public class TargetMarker
    {
        public string label;
        public string elementType;
        public Vector3 targetPosition;  // Stored relative to origin, in metres
        public float targetAngle;
        public GameObject markerObject;
        public bool isPlaced = false;
    }

    // Mirrors the element JSON returned by GET /elements/profile/{id}
    [System.Serializable]
    public class ElementResponse
    {
        public string id;
        public string profile_id;
        public string element_type;
        public string label;
        public float pos_x_cm;
        public float pos_y_cm;
        public float pos_z_cm;
        public float angle_deg;
        public float height_cm;
    }

    // Wrapper class needed because JsonUtility cannot deserialise a root-level array
    [System.Serializable]
    public class ElementListResponse
    {
        public List<ElementResponse> elements;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // Entry point - clears any existing markers and begins loading the profile
    public void StartGuidedSetup(string profileId)
    {
        currentProfileId = profileId;
        State = GuidedSetupState.LoadingProfile;
        ClearMarkers();
        StartCoroutine(LoadProfile(profileId));
    }

    // Fetches kit elements from the backend and creates TargetMarker data objects.
    // The JSON array is wrapped in a root object before deserialisation because
    // JsonUtility requires a top-level object, not a bare array.
    private IEnumerator LoadProfile(string profileId)
    {
        string url = $"{APIClient.Instance.GetBaseUrl()}/elements/profile/{profileId}";

        UnityWebRequest www = UnityWebRequest.Get(url);
        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            // Wrap raw array response so JsonUtility can deserialise it
            string json = "{\"elements\":" + www.downloadHandler.text + "}";
            ElementListResponse response = JsonUtility.FromJson<ElementListResponse>(json);

            if (response?.elements != null && response.elements.Count > 0)
            {
                CreateTargetMarkers(response.elements);
                State = GuidedSetupState.PlacingOrigin;
                Debug.Log($"Loaded {response.elements.Count} elements - tap to place origin");
            }
            else
            {
                Debug.LogError("No elements found in profile");
                State = GuidedSetupState.Idle;
            }
        }
        else
        {
            Debug.LogError($"Failed to load profile: {www.error}");
            State = GuidedSetupState.Idle;
        }

        www.Dispose();
    }

    // Converts element positions from centimetres to Unity metres and
    // stores them as relative offsets from the origin (set at PlaceOrigin time)
    private void CreateTargetMarkers(List<ElementResponse> elements)
    {
        foreach (var elem in elements)
        {
            targetMarkers.Add(new TargetMarker
            {
                label = elem.label,
                elementType = elem.element_type,
                targetPosition = new Vector3(
                    elem.pos_x_cm * 0.01f,
                    0,
                    elem.pos_z_cm * 0.01f),
                targetAngle = elem.angle_deg
            });
        }
    }

    // Called by AnchorPlacer when the first tap is received in PlacingOrigin state.
    // Spawns all target markers offset from this world position.
    public void PlaceOrigin(Vector3 position)
    {
        if (State != GuidedSetupState.PlacingOrigin) return;

        originPosition = position;
        originPlaced = true;
        State = GuidedSetupState.Active;

        foreach (var marker in targetMarkers)
        {
            // Place markers at stored offsets from origin, keeping the floor height consistent
            Vector3 worldPos = originPosition + marker.targetPosition;
            worldPos.y = originPosition.y;
            SpawnMarker(marker, worldPos);
        }

        Debug.Log($"Origin at {position}, {targetMarkers.Count} markers spawned");
    }

    // Spawns a marker object at the given world position.
    // Uses the assigned prefab if available, otherwise falls back to a cylinder primitive.
    private void SpawnMarker(TargetMarker marker, Vector3 position)
    {
        GameObject obj;
        if (targetMarkerPrefab != null)
        {
            obj = Instantiate(targetMarkerPrefab, position,
                Quaternion.Euler(0, marker.targetAngle, 0));
        }
        else
        {
            obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            obj.transform.position = position;
            obj.transform.localScale = new Vector3(0.3f, 0.05f, 0.3f);
            obj.transform.rotation = Quaternion.Euler(0, marker.targetAngle, 0);
        }

        // Blue by default, turns green when a drum is placed within snap distance
        Renderer rend = obj.GetComponent<Renderer>();
        if (rend != null)
            rend.material.color = new Color(0f, 0.8f, 1f, 0.6f);

        GameObject labelObj = new GameObject("Label");
        labelObj.transform.parent = obj.transform;
        labelObj.transform.localPosition = new Vector3(0, 2f, 0);

        TextMesh text = labelObj.AddComponent<TextMesh>();
        text.text = marker.label;
        text.fontSize = 20;
        text.alignment = TextAlignment.Center;
        text.anchor = TextAnchor.MiddleCenter;
        text.color = Color.white;

        marker.markerObject = obj;
    }

    private void Update()
    {
        if (State != GuidedSetupState.Active) return;
        CheckPlacedAnchors();
    }

    // Each frame, checks whether any placed anchor is within snap distance of each marker.
    // Updates marker colour and label text to provide visual feedback.
    // Transitions to Complete when all markers have been snapped.
    private void CheckPlacedAnchors()
    {
        var placedAnchors = AnchorPlacer.Instance?.GetPlacedAnchors();
        if (placedAnchors == null || placedAnchors.Count == 0) return;

        int placedCount = 0;

        foreach (var marker in targetMarkers)
        {
            // Find the closest anchor to this marker
            float closestDist = float.MaxValue;
            foreach (var anchor in placedAnchors)
            {
                float dist = Vector3.Distance(
                    anchor.transform.position, marker.markerObject.transform.position);
                if (dist < closestDist) closestDist = dist;
            }

            float distCm = closestDist * 100f;
            bool isClose = distCm <= snapDistanceCm;

            if (marker.markerObject != null)
            {
                // Green when snapped, blue when not
                var rend = marker.markerObject.GetComponent<Renderer>();
                if (rend != null)
                    rend.material.color = isClose
                        ? new Color(0f, 1f, 0f, 0.6f)
                        : new Color(0f, 0.8f, 1f, 0.6f);

                // Show distance in cm until snapped, then confirm with "Good!"
                var text = marker.markerObject.GetComponentInChildren<TextMesh>();
                if (text != null)
                    text.text = isClose
                        ? $"{marker.label}\nGood!"
                        : $"{marker.label}\n{distCm:F0}cm";
            }

            if (isClose) placedCount++;
        }

        if (placedCount == targetMarkers.Count && targetMarkers.Count > 0)
        {
            State = GuidedSetupState.Complete;
            Debug.Log("All elements placed - setup complete!");
        }
    }

    // Resets the guided setup session and destroys all marker objects
    public void StopGuidedSetup()
    {
        State = GuidedSetupState.Idle;
        ClearMarkers();
        originPlaced = false;
    }

    private void ClearMarkers()
    {
        foreach (var marker in targetMarkers)
            if (marker.markerObject != null)
                Destroy(marker.markerObject);
        targetMarkers.Clear();
    }

    public List<TargetMarker> GetTargetMarkers() => targetMarkers;
    public GuidedSetupState GetState() => State;
}