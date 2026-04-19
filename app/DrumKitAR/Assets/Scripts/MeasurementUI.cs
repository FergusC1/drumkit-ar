using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class MeasurementUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GUISkin guiSkin;

    private AnchorPlacer anchorPlacer;
    private ARSessionManager sessionManager;
    private string measurementText = "";
    private string lightingText = "";

   private void Start()
{
    anchorPlacer = AnchorPlacer.Instance;
    sessionManager = ARSessionManager.Instance;
    
    if (sessionManager == null)
        Debug.LogError("ARSessionManager instance not found");
    if (anchorPlacer == null)
        Debug.LogError("AnchorPlacer instance not found");
}

private void Update()
{
    sessionManager = sessionManager ?? ARSessionManager.Instance;
    anchorPlacer = anchorPlacer ?? AnchorPlacer.Instance;
    
    UpdateLightingText();
    UpdateMeasurementText();
}

    private void UpdateLightingText()
    {
        if (sessionManager == null) return;

        switch (sessionManager.CurrentLighting)
        {
            case ARSessionManager.LightingCondition.Good:
                lightingText = "Lighting: Good";
                break;
            case ARSessionManager.LightingCondition.Marginal:
                lightingText = "Lighting: Marginal";
                break;
            case ARSessionManager.LightingCondition.Poor:
                lightingText = "Lighting: Too dark - tap torch button";
                break;
        }
    }

    private void UpdateMeasurementText()
    {
        if (anchorPlacer == null) return;

        List<ARAnchor> anchors = anchorPlacer.GetPlacedAnchors();

        if (anchors.Count == 0)
        {
            measurementText = "Tap a surface to place a marker";
            return;
        }

        if (anchors.Count == 1)
        {
            measurementText = "Marker placed - tap again to measure";
            return;
        }

        measurementText = $"Markers: {anchors.Count}\n";
        for (int i = 0; i < anchors.Count - 1; i++)
        {
            for (int j = i + 1; j < anchors.Count; j++)
            {
                float distanceCm = Vector3.Distance(
                    anchors[i].transform.position,
                    anchors[j].transform.position
                ) * 100f;

                Vector3 direction = anchors[j].transform.position - anchors[i].transform.position;
                float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

                measurementText += $"{i+1}→{j+1}: {distanceCm:F1}cm / {angle:F1}°\n";
            }
        }
    }

    private void OnGUI()
    {
        int padding = 20;
        int boxWidth = 350;
        int boxHeight = 120;

        // Lighting indicator - top of screen
        Color lightingColor = sessionManager?.CurrentLighting switch
        {
            ARSessionManager.LightingCondition.Good => Color.green,
            ARSessionManager.LightingCondition.Marginal => Color.yellow,
            ARSessionManager.LightingCondition.Poor => Color.red,
            _ => Color.white
        };

        GUI.color = lightingColor;
        GUI.Box(new Rect(padding, padding, boxWidth, 40), lightingText);
        GUI.color = Color.white;
        // Calibration status
        string calibrationText = "";
        Color calibrationColor = Color.white;

        if (CalibrationManager.Instance != null)
        {
            switch (CalibrationManager.Instance.State)
            {
                case CalibrationManager.CalibrationState.Idle:
                    calibrationText = "Not calibrated";
                    calibrationColor = Color.yellow;
                    break;
                case CalibrationManager.CalibrationState.WaitingForFirstAnchor:
                    calibrationText = "Place marker on first corner of A4 sheet";
                    calibrationColor = Color.cyan;
                    break;
                case CalibrationManager.CalibrationState.WaitingForSecondAnchor:
                    calibrationText = "Place marker on opposite corner of A4 sheet";
                    calibrationColor = Color.cyan;
                    break;
                case CalibrationManager.CalibrationState.Complete:
                    calibrationText = $"Calibrated - confidence: {CalibrationManager.Instance.ConfidenceScore:P0} " +
                                    $"(error: {CalibrationManager.Instance.ErrorPercent:F1}%)";
                    calibrationColor = Color.green;
                    break;
                case CalibrationManager.CalibrationState.Failed:
                    calibrationText = $"Calibration failed - error too high " +
                                    $"({CalibrationManager.Instance.ErrorPercent:F1}%). Try again";
                    calibrationColor = Color.red;
                    break;
            }
        }

        GUI.color = calibrationColor;
        GUI.Box(new Rect(padding, padding + 50, boxWidth, 40), calibrationText);
        GUI.color = Color.white;

        // Calibrate button
        if (GUI.Button(new Rect(Screen.width - 120 - padding, padding + 70, 120, 60), "Calibrate"))
        {
            CalibrationManager.Instance?.StartCalibration();
            anchorPlacer?.ClearAllAnchors();
        }
        // Measurement readout - bottom of screen
        GUI.Box(new Rect(padding, Screen.height - boxHeight - padding, boxWidth, boxHeight), measurementText);

        // Torch toggle button
        if (GUI.Button(new Rect(Screen.width - 120 - padding, padding, 120, 60), "Torch"))
        {
            bool torchOn = !PlayerPrefs.HasKey("torch") || PlayerPrefs.GetInt("torch") == 0;
            PlayerPrefs.SetInt("torch", torchOn ? 1 : 0);
            sessionManager?.EnableTorch(torchOn);
        }

        // Clear button
        if (GUI.Button(new Rect(Screen.width - 120 - padding, Screen.height - 80 - padding, 120, 60), "Clear"))
        {
            anchorPlacer?.ClearAllAnchors();
            measurementText = "";
        }
    }
}