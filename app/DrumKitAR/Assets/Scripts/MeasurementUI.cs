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
    private ElementTagger elementTagger;
    private Vector2 elementScrollPos;
    private bool showElementPanel = false;

private void Start()
{
    sessionManager = ARSessionManager.Instance;
    anchorPlacer = AnchorPlacer.Instance;
    elementTagger = ElementTagger.Instance ?? FindObjectOfType<ElementTagger>();

    StartCoroutine(DelayedARRestart());
}

private System.Collections.IEnumerator DelayedARRestart()
{
    yield return new WaitForSeconds(1f);
    var arSession = FindObjectOfType<UnityEngine.XR.ARFoundation.ARSession>();
    if (arSession != null)
    {
        arSession.Reset();
        Debug.Log("AR Session reset on scene load");
    }

    if (anchorPlacer == null)
        Debug.LogError("AnchorPlacer instance not found");
}

private void Update()
{
    sessionManager = sessionManager ?? ARSessionManager.Instance;
    anchorPlacer = anchorPlacer ?? AnchorPlacer.Instance;
    elementTagger = elementTagger ?? FindObjectOfType<ElementTagger>();

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

    // Lighting indicator - top left
    Color lightingColor = Color.white;
    if (sessionManager != null)
    {
        lightingColor = sessionManager.CurrentLighting switch
        {
            ARSessionManager.LightingCondition.Good => Color.green,
            ARSessionManager.LightingCondition.Marginal => Color.yellow,
            ARSessionManager.LightingCondition.Poor => Color.red,
            _ => Color.white
        };
    }
    GUI.color = lightingColor;
    GUI.Box(new Rect(padding, padding, boxWidth, 40), lightingText);
    GUI.color = Color.white;

    // Calibration status - below lighting
    Color calibrationColor = Color.white;
    string calibrationText = "";
    if (CalibrationManager.Instance != null)
    {
        switch (CalibrationManager.Instance.State)
        {
            case CalibrationManager.CalibrationState.Idle:
                calibrationText = "Not calibrated - press Calibrate when ready";
                calibrationColor = Color.yellow;
                break;
            case CalibrationManager.CalibrationState.WaitingForFirstAnchor:
                calibrationText = "Tap first corner of A4 sheet";
                calibrationColor = Color.cyan;
                break;
            case CalibrationManager.CalibrationState.WaitingForSecondAnchor:
                calibrationText = "Tap opposite corner of A4 sheet";
                calibrationColor = Color.cyan;
                break;
            case CalibrationManager.CalibrationState.Complete:
            string confidenceLevel = CalibrationManager.Instance.ErrorPercent <= 10f 
                ? "High" 
                : "Medium";
            calibrationText = $"{confidenceLevel} confidence - " +
                            $"error: {CalibrationManager.Instance.ErrorPercent:F1}% " +
                            $"({CalibrationManager.Instance.ConfidenceScore:P0})";
            calibrationColor = CalibrationManager.Instance.ErrorPercent <= 10f 
                ? Color.green 
                : Color.yellow;
            break;
            case CalibrationManager.CalibrationState.Failed:
                calibrationText = $"Calibration failed ({CalibrationManager.Instance.ErrorPercent:F1}% error) - try again";
                calibrationColor = Color.red;
                break;
        }
    }
    GUI.color = calibrationColor;
    GUI.Box(new Rect(padding, padding + 50, boxWidth, 40), calibrationText);
    GUI.color = Color.white;

    // Measurement readout - bottom left
    GUI.Box(new Rect(padding, Screen.height - 140 - padding, boxWidth, 140), measurementText);

    // Right side buttons and scan progress
    int btnX = Screen.width - 140 - padding;
    int btnWidth = 140;

    // Torch button
    if (GUI.Button(new Rect(btnX, padding, btnWidth, 50), "Torch"))
    {
        bool torchOn = PlayerPrefs.GetInt("torch", 0) == 0;
        PlayerPrefs.SetInt("torch", torchOn ? 1 : 0);
        sessionManager?.EnableTorch(torchOn);
    }

    // Scan progress
    float progress = ARSessionManager.Instance?.ScanProgress ?? 0f;
    bool readyToCalibrate = ARSessionManager.Instance?.IsReadyToCalibrate ?? false;

    string scanLabel = readyToCalibrate ? "Ready to calibrate!" : $"Scanning: {progress:P0}";
    GUI.color = readyToCalibrate ? Color.green : Color.yellow;
    GUI.Box(new Rect(btnX, padding + 60, btnWidth, 30), scanLabel);

    // Progress bar background
    GUI.color = Color.grey;
    GUI.Box(new Rect(btnX, padding + 95, btnWidth, 20), "");

    // Progress bar fill
    GUI.color = readyToCalibrate ? Color.green : Color.yellow;
    if (progress > 0)
        GUI.Box(new Rect(btnX, padding + 95, btnWidth * progress, 20), "");
    GUI.color = Color.white;

        // Calibrate button - greyed out until scan complete and not mid-calibration
    bool calibrateButtonEnabled = readyToCalibrate && 
        (CalibrationManager.Instance?.State == CalibrationManager.CalibrationState.Idle ||
        CalibrationManager.Instance?.State == CalibrationManager.CalibrationState.Failed ||
        CalibrationManager.Instance?.State == CalibrationManager.CalibrationState.Complete);

    if (calibrateButtonEnabled)
    {
        if (GUI.Button(new Rect(btnX, padding + 125, btnWidth, 50), "Calibrate"))
        {
            CalibrationManager.Instance?.StartCalibration();
            anchorPlacer?.ClearAllAnchors();
        }
    }
    else
    {
        GUI.color = Color.grey;
        GUI.Box(new Rect(btnX, padding + 125, btnWidth, 50), "Calibrate");
        GUI.color = Color.white;
    }

    // Clear button
    if (GUI.Button(new Rect(btnX, padding + 185, btnWidth, 50), "Clear"))
    {
        anchorPlacer?.ClearAllAnchors();
        elementTagger?.ClearAll();
    }
    // Element selector toggle button
    if (GUI.Button(new Rect(btnX, padding + 245, btnWidth, 50), 
        showElementPanel ? "Hide Elements" : "Tag Element"))
    {
        showElementPanel = !showElementPanel;
    }

    // Element selector panel
    if (showElementPanel && elementTagger != null)
    {
        int panelX = padding;
        int panelY = Screen.height / 2 - 200;
        int panelWidth = 300;
        int panelHeight = 400;

        GUI.Box(new Rect(panelX, panelY, panelWidth, panelHeight), "Select Element Type");

        int btnY = panelY + 30;
        foreach (ElementTagger.DrumElement element in 
            System.Enum.GetValues(typeof(ElementTagger.DrumElement)))
        {
            if (GUI.Button(new Rect(panelX + 10, btnY, panelWidth - 20, 35), 
                elementTagger.GetElementIcon(element) + " " + element.ToString()))
            {
                elementTagger.SetSelectedElement(element);
                elementTagger.TagLastAnchor();
                showElementPanel = false;
            }
            btnY += 38;
        }
    }

    // Tagged elements list - bottom right
    if (elementTagger != null)
    {
        var elements = elementTagger.GetTaggedElements();
        if (elements.Count > 0)
        {
            int listX = Screen.width - 200 - padding;
            int listY = Screen.height - (elements.Count * 25) - 80 - padding;
            GUI.Box(new Rect(listX, listY, 200, elements.Count * 25 + 30), "Kit elements:");
            for (int i = 0; i < elements.Count; i++)
            {
                GUI.Label(new Rect(listX + 10, listY + 30 + (i * 25), 180, 25),
                    $"{elementTagger.GetElementIcon(elements[i].elementType)} " +
                    $"{elements[i].label} " +
                    $"({elements[i].position.x * 100:F0}, {elements[i].position.z * 100:F0})cm");
            }
        }
    }
    // Save button
    if (GUI.Button(new Rect(btnX, padding + 305, btnWidth, 50), "Save Kit"))
    {
        var elements = elementTagger?.GetTaggedElements();
        if (elements != null && elements.Count > 0)
        {
            // Using test user ID - will be replaced with auth later
            string testUserId = "9b2288bb-0b27-45f2-9113-a9ea7f4ef100";
            APIClient.Instance?.SaveKit(
                "My Kit",
                "Captured on " + System.DateTime.Now.ToString("dd/MM/yyyy"),
                testUserId,
                elements,
                (success, profileId) =>
                {
                    if (success)
                        Debug.Log($"Kit saved successfully - ID: {profileId}");
                    else
                        Debug.LogError($"Failed to save kit: {profileId}");
                }
            );
        }
        else
        {
            Debug.LogWarning("No elements tagged - nothing to save");
        }
    }
    // View schematic button
    if (GUI.Button(new Rect(btnX, padding + 365, btnWidth, 50), "View Schematic"))
    {
        var elements = elementTagger?.GetTaggedElements();
        if (elements != null && elements.Count > 0)
        {
            SchematicRenderer.Instance?.ShowSchematic(elements);
        }
        else
        {
            Debug.LogWarning("No elements to show schematic for");
        }
    }
}
}