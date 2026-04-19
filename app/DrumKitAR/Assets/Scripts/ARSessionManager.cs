using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class ARSessionManager : MonoBehaviour
{
    [Header("AR Components")]
    [SerializeField] private ARSession arSession;
    [SerializeField] private ARCameraManager cameraManager;

    [Header("Lighting Thresholds")]
    [SerializeField] private float goodLightThreshold = 0.5f;
    [SerializeField] private float poorLightThreshold = 0.2f;

    public enum LightingCondition { Good, Marginal, Poor }
    public LightingCondition CurrentLighting { get; private set; }
    public float CurrentLightIntensity { get; private set; }

    public static ARSessionManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

 private void Start()
{
    StartCoroutine(CheckARAvailability());

    if (cameraManager != null)
        cameraManager.frameReceived += OnCameraFrameReceived;

    RequestCameraPermission();
}

private void RequestCameraPermission()
{
    if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(
        UnityEngine.Android.Permission.Camera))
    {
        UnityEngine.Android.Permission.RequestUserPermission(
            UnityEngine.Android.Permission.Camera);
    }
}

    private void OnDestroy()
    {
        if (cameraManager != null)
            cameraManager.frameReceived -= OnCameraFrameReceived;
    }

   private void OnCameraFrameReceived(ARCameraFrameEventArgs args)
{
    if (args.lightEstimation.averageBrightness.HasValue)
    {
        CurrentLightIntensity = args.lightEstimation.averageBrightness.Value;
        Debug.Log($"Light intensity: {CurrentLightIntensity}");
        UpdateLightingCondition();
    }
    else
    {
        Debug.Log("No brightness value available this frame");
    }
}

    private IEnumerator CheckARAvailability()
    {
        if (ARSession.state == ARSessionState.None ||
            ARSession.state == ARSessionState.CheckingAvailability)
        {
            yield return ARSession.CheckAvailability();
        }

        if (ARSession.state == ARSessionState.Unsupported)
            Debug.LogError("ARCore not supported on this device");
        else
            Debug.Log("ARCore is supported and ready");
    }

    private void UpdateLightingCondition()
    {
        if (CurrentLightIntensity >= goodLightThreshold)
            CurrentLighting = LightingCondition.Good;
        else if (CurrentLightIntensity >= poorLightThreshold)
            CurrentLighting = LightingCondition.Marginal;
        else
            CurrentLighting = LightingCondition.Poor;
    }

    private bool torchEnabled = false;

public void EnableTorch(bool enable)
{
    torchEnabled = enable;
    try
    {
        using (AndroidJavaClass unityPlayer = 
            new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (AndroidJavaObject activity = 
            unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        using (AndroidJavaObject window = activity.Call<AndroidJavaObject>("getWindow"))
        using (AndroidJavaObject layoutParams = 
            window.Call<AndroidJavaObject>("getAttributes"))
        {
            if (enable)
                layoutParams.Set("screenBrightness", 1.0f);
            else
                layoutParams.Set("screenBrightness", -1.0f);

            activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
            {
                window.Call("setAttributes", layoutParams);
            }));
        }
        Debug.Log($"Screen brightness torch: {enable}");
    }
    catch (AndroidJavaException e)
    {
        Debug.LogWarning($"Torch failed: {e.Message}");
    }
}
}