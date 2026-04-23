// APIClient.cs
// Handles all HTTP communication between the Unity app and the FastAPI backend.
// Uses Unity's UnityWebRequest with coroutines for non-blocking async requests.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;

public class APIClient : MonoBehaviour
{
    [Header("API Settings")]
    [SerializeField] private string baseUrl = "https://drumkit-ar-production.up.railway.app";

    public static APIClient Instance { get; private set; }

    // Exposed so other scripts (e.g. GuidedSetupManager) can construct URLs
    public string GetBaseUrl() => baseUrl;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // --- Serialisable data models ---
    // These classes mirror the JSON structure expected by the FastAPI backend.
    // JsonUtility requires fields to be public and the class to be marked Serializable.

    [System.Serializable]
    public class ElementData
    {
        public string element_type;
        public string label;
        public float pos_x_cm;
        public float pos_y_cm;
        public float pos_z_cm;
        public float angle_deg;
        public float height_cm;
    }

    [System.Serializable]
    public class KitSaveRequest
    {
        public string profile_name;
        public string description;
        public float stage_width_cm;
        public float stage_depth_cm;
        public string owner_id;
        public List<ElementData> elements;
    }

    [System.Serializable]
    public class KitSaveResponse
    {
        public bool success;
        public string profile_id;
        public int elements_saved;
    }

    [System.Serializable]
    public class ShareLinkRequest
    {
        public string profile_id;
        public string view_level;
        public int expires_hours;
    }

    [System.Serializable]
    public class ShareLinkResponse
    {
        public string token;
        public string view_level;
        public string share_url;
    }

    // --- Kit Save ---

    // Public entry point - starts the coroutine and returns result via callback.
    // Callback signature: (success, profileId or errorMessage)
    public void SaveKit(string profileName, string description,
        string ownerId, List<ElementTagger.TaggedElement> taggedElements,
        System.Action<bool, string> callback)
    {
        StartCoroutine(SaveKitCoroutine(profileName, description,
            ownerId, taggedElements, callback));
    }

    private IEnumerator SaveKitCoroutine(string profileName, string description,
        string ownerId, List<ElementTagger.TaggedElement> taggedElements,
        System.Action<bool, string> callback)
    {
        // Build the request body, converting Unity world positions (metres)
        // to centimetres to match the backend schema
        KitSaveRequest request = new KitSaveRequest
        {
            profile_name = profileName,
            description = description,
            stage_width_cm = 0f,
            stage_depth_cm = 0f,
            owner_id = ownerId,
            elements = new List<ElementData>()
        };

        foreach (var elem in taggedElements)
        {
            request.elements.Add(new ElementData
            {
                element_type = ElementTypeToString(elem.elementType),
                label = elem.label,
                pos_x_cm = elem.position.x * 100f,
                pos_y_cm = elem.position.y * 100f,
                pos_z_cm = elem.position.z * 100f,
                angle_deg = elem.angleDeg,
                height_cm = elem.heightCm
            });
        }

        string json = JsonUtility.ToJson(request);

        UnityWebRequest www = new UnityWebRequest(baseUrl + "/elements/save", "POST");
        www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");

        // Wrap SendWebRequest in try-catch as it can throw on Android
        // if the network is unavailable or the URL is malformed
        UnityWebRequestAsyncOperation operation = null;
        try
        {
            operation = www.SendWebRequest();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SendWebRequest exception: {e.Message}");
            callback?.Invoke(false, e.Message);
            yield break;
        }

        yield return operation;

        if (www.result == UnityWebRequest.Result.Success)
        {
            KitSaveResponse response = JsonUtility.FromJson<KitSaveResponse>(
                www.downloadHandler.text);
            Debug.Log($"Kit saved - profile ID: {response.profile_id}");
            callback?.Invoke(true, response.profile_id);
        }
        else
        {
            Debug.LogError($"Save failed: {www.result} {www.responseCode} - {www.error}");
            callback?.Invoke(false, www.error);
        }

        www.Dispose();
    }

    // --- Share Link Generation ---

    // Public entry point - callback returns (success, token or errorMessage)
    public void GenerateShareLink(string profileId, string viewLevel,
        System.Action<bool, string> callback)
    {
        StartCoroutine(GenerateShareLinkCoroutine(profileId, viewLevel, callback));
    }

    private IEnumerator GenerateShareLinkCoroutine(string profileId, string viewLevel,
        System.Action<bool, string> callback)
    {
        ShareLinkRequest request = new ShareLinkRequest
        {
            profile_id = profileId,
            view_level = viewLevel,
            expires_hours = 24
        };

        string json = JsonUtility.ToJson(request);

        UnityWebRequest www = new UnityWebRequest(baseUrl + "/shares/generate", "POST");
        www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");

        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            ShareLinkResponse response = JsonUtility.FromJson<ShareLinkResponse>(
                www.downloadHandler.text);
            Debug.Log($"Share link generated - token: {response.token}");
            callback?.Invoke(true, response.token);
        }
        else
        {
            Debug.LogError($"Share link failed: {www.error}");
            callback?.Invoke(false, www.error);
        }

        www.Dispose();
    }

    // Converts the DrumElement enum to the snake_case string expected by the backend.
    // A default fallback of snare_drum is used if an unrecognised value is passed.
    private string ElementTypeToString(ElementTagger.DrumElement element)
    {
        return element switch
        {
            ElementTagger.DrumElement.KickDrum => "kick_drum",
            ElementTagger.DrumElement.SnareDrum => "snare_drum",
            ElementTagger.DrumElement.HiHat => "hi_hat",
            ElementTagger.DrumElement.RideCymbal => "ride_cymbal",
            ElementTagger.DrumElement.CrashCymbal => "crash_cymbal",
            ElementTagger.DrumElement.FloorTom => "floor_tom",
            ElementTagger.DrumElement.RackTom => "rack_tom",
            ElementTagger.DrumElement.Splash => "splash",
            ElementTagger.DrumElement.China => "china",
            ElementTagger.DrumElement.DrumThrone => "drum_throne",
            _ => "snare_drum"
        };
    }
}