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
    Debug.Log($"Sending kit save request: {json}");
    Debug.Log($"Starting save to URL: {baseUrl}/elements/save");

    UnityWebRequest www = new UnityWebRequest(baseUrl + "/elements/save", "POST");
    byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
    www.uploadHandler = new UploadHandlerRaw(bodyRaw);
    www.downloadHandler = new DownloadHandlerBuffer();
    www.SetRequestHeader("Content-Type", "application/json");
    Debug.Log("About to send web request");
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
    Debug.Log($"Request complete - result: {www.result} code: {www.responseCode}");

    if (www.result == UnityWebRequest.Result.Success)
    {
        Debug.Log($"Server response: {www.downloadHandler.text}");
        KitSaveResponse response = JsonUtility.FromJson<KitSaveResponse>(
            www.downloadHandler.text);
        Debug.Log($"Kit saved successfully - profile ID: {response.profile_id}");
        callback?.Invoke(true, response.profile_id);
    }
    else
    {
        Debug.LogError($"Save failed - result: {www.result}");
        Debug.LogError($"Error: {www.error}");
        Debug.LogError($"Response code: {www.responseCode}");
        Debug.LogError($"Response body: {www.downloadHandler.text}");
        callback?.Invoke(false, www.error);
    }

    www.Dispose();
}

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