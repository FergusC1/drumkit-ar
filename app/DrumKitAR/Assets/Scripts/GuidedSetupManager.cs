using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;

public class GuidedSetupManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float snapDistanceCm = 15f;
    [SerializeField] private GameObject targetMarkerPrefab;

    public enum GuidedSetupState
    {
        Idle,
        LoadingProfile,
        PlacingOrigin,
        Active,
        Complete
    }

    public GuidedSetupState State { get; private set; } = GuidedSetupState.Idle;
    public static GuidedSetupManager Instance { get; private set; }

    private List<TargetMarker> targetMarkers = new List<TargetMarker>();
    private Vector3 originPosition;
    private bool originPlaced = false;
    private string currentProfileId = "";

    [System.Serializable]
    public class TargetMarker
    {
        public string label;
        public string elementType;
        public Vector3 targetPosition;
        public float targetAngle;
        public GameObject markerObject;
        public bool isPlaced = false;
    }

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

    public void StartGuidedSetup(string profileId)
    {
        currentProfileId = profileId;
        State = GuidedSetupState.LoadingProfile;
        ClearMarkers();
        StartCoroutine(LoadProfile(profileId));
    }

    private IEnumerator LoadProfile(string profileId)
    {
        string url = $"{APIClient.Instance.GetBaseUrl()}/elements/profile/{profileId}";
        Debug.Log($"Loading profile from: {url}");

        UnityWebRequest www = UnityWebRequest.Get(url);
        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            Debug.Log($"Profile loaded: {www.downloadHandler.text}");
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

    private void CreateTargetMarkers(List<ElementResponse> elements)
    {
        foreach (var elem in elements)
        {
            TargetMarker marker = new TargetMarker
            {
                label = elem.label,
                elementType = elem.element_type,
                targetPosition = new Vector3(
                    elem.pos_x_cm * 0.01f,
                    0,
                    elem.pos_z_cm * 0.01f
                ),
                targetAngle = elem.angle_deg
            };
            targetMarkers.Add(marker);
        }
    }

    public void PlaceOrigin(Vector3 position)
    {
        if (State != GuidedSetupState.PlacingOrigin) return;

        originPosition = position;
        originPlaced = true;
        State = GuidedSetupState.Active;

        foreach (var marker in targetMarkers)
        {
            Vector3 worldPos = originPosition + marker.targetPosition;
            worldPos.y = originPosition.y;
            SpawnMarker(marker, worldPos);
        }

        Debug.Log($"Origin placed at {position}, {targetMarkers.Count} markers spawned");
    }

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

        Renderer rend = obj.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material.color = new Color(0f, 0.8f, 1f, 0.6f);
        }

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

    private void CheckPlacedAnchors()
    {
        var placedAnchors = AnchorPlacer.Instance?.GetPlacedAnchors();
        if (placedAnchors == null || placedAnchors.Count == 0) return;

        int placedCount = 0;
        foreach (var marker in targetMarkers)
        {
            float closestDist = float.MaxValue;

            foreach (var anchor in placedAnchors)
            {
                float dist = Vector3.Distance(
                    anchor.transform.position, marker.markerObject.transform.position);
                if (dist < closestDist)
                    closestDist = dist;
            }

            float distCm = closestDist * 100f;
            bool isClose = distCm <= snapDistanceCm;

            if (marker.markerObject != null)
            {
                Renderer rend = marker.markerObject.GetComponent<Renderer>();
                if (rend != null)
                {
                    rend.material.color = isClose
                        ? new Color(0f, 1f, 0f, 0.6f)
                        : new Color(0f, 0.8f, 1f, 0.6f);
                }

                TextMesh text = marker.markerObject
                    .GetComponentInChildren<TextMesh>();
                if (text != null)
                {
                    text.text = isClose
                        ? $"{marker.label}\nGood!"
                        : $"{marker.label}\n{distCm:F0}cm";
                }
            }

            if (isClose) placedCount++;
        }

        if (placedCount == targetMarkers.Count && targetMarkers.Count > 0)
        {
            State = GuidedSetupState.Complete;
            Debug.Log("All elements placed - setup complete!");
        }
    }

    public void StopGuidedSetup()
    {
        State = GuidedSetupState.Idle;
        ClearMarkers();
        originPlaced = false;
    }

    private void ClearMarkers()
    {
        foreach (var marker in targetMarkers)
        {
            if (marker.markerObject != null)
                Destroy(marker.markerObject);
        }
        targetMarkers.Clear();
    }

    public List<TargetMarker> GetTargetMarkers() => targetMarkers;
    public GuidedSetupState GetState() => State;
}