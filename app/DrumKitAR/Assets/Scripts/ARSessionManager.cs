// ARSessionManager.cs
// Manages the ARCore session lifecycle, lighting estimation, and scan timer.
// Acts as the central AR state provider for other scripts in the app.

using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class ARSessionManager : MonoBehaviour
{
    [Header("AR Components")]
    [SerializeField] private ARSession arSession;
    [SerializeField] private ARCameraManager cameraManager;

    [Header("Scan Timer")]
    [SerializeField] private float requiredScanSeconds = 30f; // Time before calibration is unlocked

    public float ScanProgress { get; private set; } = 0f;
    public bool IsReadyToCalibrate { get; private set; } = false;
    private float scanTimer = 0f;

    [Header("Lighting Thresholds")]
    [SerializeField] private float goodLightThreshold = 0.5f;
    [SerializeField] private float poorLightThreshold = 0.2f;

    public enum LightingCondition { Good, Marginal, Poor }
    public LightingCondition CurrentLighting { get; private set; }
    public float CurrentLightIntensity { get; private set; }

    private bool torchEnabled = false;

    public static ARSessionManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // Persist across scene loads so AR state is not lost
        // when switching between MainScene and other views
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        StartCoroutine(CheckARAvailability());

        if (cameraManager != null)
            cameraManager.frameReceived += OnCameraFrameReceived;

        RequestCameraPermission();
    }

    private void OnEnable()
    {
        // Brief delay before resetting to allow the scene to fully load
        StartCoroutine(RestartARSession());
    }

    private IEnumerator RestartARSession()
    {
        yield return new WaitForSeconds(0.5f);
        if (arSession != null)
            arSession.Reset();
    }

    private void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks when the object is destroyed
        if (cameraManager != null)
            cameraManager.frameReceived -= OnCameraFrameReceived;
    }

    // Requests camera permission on first launch.
    // ARCore requires explicit permission on Android 6.0+.
    private void RequestCameraPermission()
    {
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(
            UnityEngine.Android.Permission.Camera))
        {
            UnityEngine.Android.Permission.RequestUserPermission(
                UnityEngine.Android.Permission.Camera);
        }
    }

    private void Update()
    {
        UpdateLightingCondition();
        UpdateScanTimer();
    }

    // Increments the scan timer while ARCore is actively tracking.
    // Calibration is locked until the required scan time is reached,
    // reducing measurement error from an unstabilised AR session.
    private void UpdateScanTimer()
    {
        if (IsReadyToCalibrate) return;

        if (ARSession.state == ARSessionState.SessionTracking)
        {
            scanTimer += Time.deltaTime;
            ScanProgress = Mathf.Clamp01(scanTimer / requiredScanSeconds);

            if (scanTimer >= requiredScanSeconds)
            {
                IsReadyToCalibrate = true;
                Debug.Log("Environment scanned - ready to calibrate");
            }
        }
    }

    // Receives per-frame light estimation data from ARCore via the camera manager.
    // averageBrightness is a normalised value (0-1) derived from the camera image.
    private void OnCameraFrameReceived(ARCameraFrameEventArgs args)
    {
        if (args.lightEstimation.averageBrightness.HasValue)
        {
            CurrentLightIntensity = args.lightEstimation.averageBrightness.Value;
            UpdateLightingCondition();
        }
    }

    // Checks whether ARCore is supported on this device before the session starts.
    // Logs an error if the device does not meet ARCore requirements.
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

    // Maps the raw brightness float to a three-state enum for use in the UI
    // and anchor placement logic
    private void UpdateLightingCondition()
    {
        if (CurrentLightIntensity >= goodLightThreshold)
            CurrentLighting = LightingCondition.Good;
        else if (CurrentLightIntensity >= poorLightThreshold)
            CurrentLighting = LightingCondition.Marginal;
        else
            CurrentLighting = LightingCondition.Poor;
    }

    // Adjusts screen brightness as a torch workaround.
    // The Android Camera2 API torch conflicts with ARCore's camera usage,
    // so maximum screen brightness is used instead for illuminating dark spaces.
    public void EnableTorch(bool enable)
    {
        torchEnabled = enable;
        try
        {
            using (AndroidJavaClass unityPlayer =
                new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity =
                unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject window =
                activity.Call<AndroidJavaObject>("getWindow"))
            using (AndroidJavaObject layoutParams =
                window.Call<AndroidJavaObject>("getAttributes"))
            {
                // -1.0f restores the system default brightness
                layoutParams.Set("screenBrightness", enable ? 1.0f : -1.0f);

                activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    window.Call("setAttributes", layoutParams);
                }));
            }
        }
        catch (AndroidJavaException e)
        {
            Debug.LogWarning($"Torch failed: {e.Message}");
        }
    }
}