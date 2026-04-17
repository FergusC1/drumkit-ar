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
            UpdateLightingCondition();
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
    if (cameraManager != null)
    {
        cameraManager.requestedLightEstimation = enable 
            ? UnityEngine.XR.ARSubsystems.CameraLightEstimation.AmbientIntensity 
            : UnityEngine.XR.ARSubsystems.CameraLightEstimation.None;
    }

    using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
    using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
    {
        activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
        {
            using (AndroidJavaObject cameraManagerAndroid = 
                activity.Call<AndroidJavaObject>("getSystemService", "camera"))
            {
                try
                {
                    string[] cameraIds = cameraManagerAndroid.Call<string[]>("getCameraIdList");
                    cameraManagerAndroid.Call("setTorchMode", cameraIds[0], enable);
                }
                catch (AndroidJavaException e)
                {
                    Debug.LogWarning($"Torch not available: {e.Message}");
                }
            }
        }));
    }
}
}