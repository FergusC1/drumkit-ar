// MeasurementUI.cs
// Renders all on-screen UI using Unity's immediate-mode GUI (OnGUI).
// Displays lighting status, calibration state, measurements, and
// provides controls for all app features.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class MeasurementUI : MonoBehaviour
{
    [SerializeField] private GUISkin guiSkin;

    // --- Cached component references ---
    private AnchorPlacer anchorPlacer;
    private ARSessionManager sessionManager;
    private ElementTagger elementTagger;
    private GuidedSetupManager guidedSetupManager;

    // --- UI state ---
    private string measurementText = "";
    private string lightingText = "";
    private bool showElementPanel = false;
    private bool showProfileInput = false;
    private bool showSharePanel = false;

    // --- Session state ---
    private string lastSavedProfileId = "";    // Set on successful save, enables Share button
    private string generatedShareToken = "";   // Set on successful share link generation
    private string selectedViewLevel = "footprint";
    private string guidedProfileId = "";

    private void Start()
    {
        sessionManager = ARSessionManager.Instance;
        anchorPlacer = AnchorPlacer.Instance;
        elementTagger = ElementTagger.Instance ?? FindObjectOfType<ElementTagger>();

        StartCoroutine(DelayedARRestart());
    }

    // Resets the AR session after a short delay on scene load.
    // Fixes black screen issue when returning from the schematic view.
    private System.Collections.IEnumerator DelayedARRestart()
    {
        yield return new WaitForSeconds(1f);
        var arSession = FindObjectOfType<UnityEngine.XR.ARFoundation.ARSession>();
        if (arSession != null)
            arSession.Reset();
    }

    private void Update()
    {
        // Use null-coalescing to find instances that may not have been
        // available at Start() due to scene initialisation order
        sessionManager = sessionManager ?? ARSessionManager.Instance;
        anchorPlacer = anchorPlacer ?? AnchorPlacer.Instance;
        elementTagger = elementTagger ?? FindObjectOfType<ElementTagger>();
        guidedSetupManager = guidedSetupManager ?? FindObjectOfType<GuidedSetupManager>();

        UpdateLightingText();
        UpdateMeasurementText();
    }

    // Maps the current lighting condition enum to a human-readable string
    private void UpdateLightingText()
    {
        if (sessionManager == null) return;

        lightingText = sessionManager.CurrentLighting switch
        {
            ARSessionManager.LightingCondition.Good => "Lighting: Good",
            ARSessionManager.LightingCondition.Marginal => "Lighting: Marginal",
            ARSessionManager.LightingCondition.Poor => "Lighting: Too dark - tap torch button",
            _ => ""
        };
    }

    // Builds the measurement readout string from all placed anchor pairs.
    // Distance uses Euclidean distance, angle uses atan2 in the XZ plane.
    private void UpdateMeasurementText()
    {
        if (anchorPlacer == null) return;

        List<ARAnchor> anchors = anchorPlacer.GetPlacedAnchors();

        if (anchors.Count == 0) { measurementText = "Tap a surface to place a marker"; return; }
        if (anchors.Count == 1) { measurementText = "Marker placed - tap again to measure"; return; }

        measurementText = $"Markers: {anchors.Count}\n";
        for (int i = 0; i < anchors.Count - 1; i++)
        {
            for (int j = i + 1; j < anchors.Count; j++)
            {
                float distanceCm = Vector3.Distance(
                    anchors[i].transform.position,
                    anchors[j].transform.position) * 100f;

                Vector3 direction = anchors[j].transform.position - anchors[i].transform.position;
                float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

                measurementText += $"{i + 1}→{j + 1}: {distanceCm:F1}cm / {angle:F1}°\n";
            }
        }
    }

    private void OnGUI()
    {
        int padding = 20;
        int boxWidth = 350;
        int btnX = Screen.width - 140 - padding;
        int btnWidth = 140;

        // --- Lighting indicator (top left, colour coded) ---
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

        // --- Calibration status (below lighting indicator) ---
        string calibrationText = "";
        Color calibrationColor = Color.white;

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
                        ? "High" : "Medium";
                    calibrationText = $"{confidenceLevel} confidence - " +
                        $"error: {CalibrationManager.Instance.ErrorPercent:F1}% " +
                        $"({CalibrationManager.Instance.ConfidenceScore:P0})";
                    calibrationColor = CalibrationManager.Instance.ErrorPercent <= 10f
                        ? Color.green : Color.yellow;
                    break;
                case CalibrationManager.CalibrationState.Failed:
                    calibrationText = $"Calibration failed " +
                        $"({CalibrationManager.Instance.ErrorPercent:F1}% error) - try again";
                    calibrationColor = Color.red;
                    break;
            }
        }
        GUI.color = calibrationColor;
        GUI.Box(new Rect(padding, padding + 50, boxWidth, 40), calibrationText);
        GUI.color = Color.white;

        // --- Guided setup status (shown when a guided session is active) ---
        if (guidedSetupManager != null &&
            guidedSetupManager.State != GuidedSetupManager.GuidedSetupState.Idle)
        {
            string statusText = guidedSetupManager.State switch
            {
                GuidedSetupManager.GuidedSetupState.LoadingProfile => "Loading profile...",
                GuidedSetupManager.GuidedSetupState.PlacingOrigin =>
                    "Tap floor to place kit origin point",
                GuidedSetupManager.GuidedSetupState.Active =>
                    $"Place drums on blue markers - " +
                    $"{guidedSetupManager.GetTargetMarkers().FindAll(m => m.isPlaced).Count}" +
                    $"/{guidedSetupManager.GetTargetMarkers().Count} placed",
                GuidedSetupManager.GuidedSetupState.Complete => "Setup complete!",
                _ => ""
            };

            GUI.color = guidedSetupManager.State == GuidedSetupManager.GuidedSetupState.Complete
                ? Color.green : Color.cyan;
            GUI.Box(new Rect(padding, padding + 100, 400, 40), statusText);
            GUI.color = Color.white;
        }

        // --- Measurement readout (bottom left) ---
        GUI.Box(new Rect(padding, Screen.height - 140 - padding, boxWidth, 140), measurementText);

        // --- Tagged elements list (above measurement readout, bottom right) ---
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

        // --- Right side button column ---

        // Torch - toggles maximum screen brightness as a lighting aid
        if (GUI.Button(new Rect(btnX, padding, btnWidth, 50), "Torch"))
        {
            bool torchOn = PlayerPrefs.GetInt("torch", 0) == 0;
            PlayerPrefs.SetInt("torch", torchOn ? 1 : 0);
            sessionManager?.EnableTorch(torchOn);
        }

        // Scan progress bar - fills over requiredScanSeconds while ARCore tracks
        float progress = ARSessionManager.Instance?.ScanProgress ?? 0f;
        bool readyToCalibrate = ARSessionManager.Instance?.IsReadyToCalibrate ?? false;

        GUI.color = readyToCalibrate ? Color.green : Color.yellow;
        GUI.Box(new Rect(btnX, padding + 60, btnWidth, 30),
            readyToCalibrate ? "Ready to calibrate!" : $"Scanning: {progress:P0}");
        GUI.color = Color.grey;
        GUI.Box(new Rect(btnX, padding + 95, btnWidth, 20), "");
        GUI.color = readyToCalibrate ? Color.green : Color.yellow;
        if (progress > 0)
            GUI.Box(new Rect(btnX, padding + 95, btnWidth * progress, 20), "");
        GUI.color = Color.white;

        // Calibrate - locked until scan timer completes and no calibration is in progress
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
            // Draw as a non-interactive box when disabled
            GUI.color = Color.grey;
            GUI.Box(new Rect(btnX, padding + 125, btnWidth, 50), "Calibrate");
            GUI.color = Color.white;
        }

        // Clear - removes all anchors and element tags
        if (GUI.Button(new Rect(btnX, padding + 185, btnWidth, 50), "Clear"))
        {
            anchorPlacer?.ClearAllAnchors();
            elementTagger?.ClearAll();
        }

        // Tag Element - opens the element type selector panel
        if (GUI.Button(new Rect(btnX, padding + 245, btnWidth, 50),
            showElementPanel ? "Hide Elements" : "Tag Element"))
        {
            showElementPanel = !showElementPanel;
        }

        // Save Kit - posts current elements to the Railway backend
        if (GUI.Button(new Rect(btnX, padding + 305, btnWidth, 50), "Save Kit"))
        {
            var elements = elementTagger?.GetTaggedElements();
            if (elements != null && elements.Count > 0)
            {
                // Hardcoded test user - would be replaced by proper auth in production
                string testUserId = "16dd3dd7-0ea5-48eb-bc36-63a334bd6aa6";
                APIClient.Instance?.SaveKit(
                    "My Kit",
                    "Captured on " + System.DateTime.Now.ToString("dd/MM/yyyy"),
                    testUserId,
                    elements,
                    (success, profileId) =>
                    {
                        if (success)
                        {
                            lastSavedProfileId = profileId;
                            Debug.Log($"Kit saved - ID: {profileId}");
                        }
                        else
                            Debug.LogError($"Save failed: {profileId}");
                    }
                );
            }
        }

        // View Schematic - renders the tagged elements as a top-down 3D diagram
        if (GUI.Button(new Rect(btnX, padding + 365, btnWidth, 50), "View Schematic"))
        {
            var elements = elementTagger?.GetTaggedElements();
            if (elements != null && elements.Count > 0)
                SchematicRenderer.Instance?.ShowSchematic(elements);
        }

        // Guided Setup - loads a saved profile and places AR target markers
        if (GUI.Button(new Rect(btnX, padding + 425, btnWidth, 50),
            guidedSetupManager?.State == GuidedSetupManager.GuidedSetupState.Idle
                ? "Guided Setup" : "Stop Setup"))
        {
            if (guidedSetupManager?.State == GuidedSetupManager.GuidedSetupState.Idle)
                showProfileInput = !showProfileInput;
            else
                guidedSetupManager?.StopGuidedSetup();
        }

        // Share Kit - only visible after a successful save
        if (!string.IsNullOrEmpty(lastSavedProfileId))
        {
            if (GUI.Button(new Rect(btnX, padding + 485, btnWidth, 50), "Share Kit"))
            {
                showSharePanel = !showSharePanel;
                generatedShareToken = "";
            }
        }

        // --- Overlay panels ---

        // Element type selector panel
        if (showElementPanel && elementTagger != null)
        {
            int panelX = padding;
            int panelY = Screen.height / 2 - 200;
            GUI.Box(new Rect(panelX, panelY, 300, 400), "Select Element Type");

            int btnY = panelY + 30;
            foreach (ElementTagger.DrumElement element in
                System.Enum.GetValues(typeof(ElementTagger.DrumElement)))
            {
                if (GUI.Button(new Rect(panelX + 10, btnY, 280, 35),
                    elementTagger.GetElementIcon(element) + " " + element.ToString()))
                {
                    elementTagger.SetSelectedElement(element);
                    elementTagger.TagLastAnchor();
                    showElementPanel = false;
                }
                btnY += 38;
            }
        }

        // Guided setup profile ID input panel
        if (showProfileInput)
        {
            GUI.Box(new Rect(padding, Screen.height / 2 - 80, 400, 160), "Enter Profile ID");
            guidedProfileId = GUI.TextField(
                new Rect(padding + 10, Screen.height / 2 - 40, 380, 40), guidedProfileId);

            if (GUI.Button(new Rect(padding + 10, Screen.height / 2 + 20, 180, 50), "Load Profile"))
            {
                if (!string.IsNullOrEmpty(guidedProfileId))
                {
                    guidedSetupManager?.StartGuidedSetup(guidedProfileId);
                    showProfileInput = false;
                    anchorPlacer?.ClearAllAnchors();
                }
            }

            if (GUI.Button(new Rect(padding + 210, Screen.height / 2 + 20, 180, 50), "Cancel"))
                showProfileInput = false;
        }

        // Share link generation panel
        if (showSharePanel && !string.IsNullOrEmpty(lastSavedProfileId))
        {
            int panelX = padding;
            int panelY = Screen.height / 2 - 150;
            GUI.Box(new Rect(panelX, panelY, 360, 300), "Share Kit Profile");
            GUI.Label(new Rect(panelX + 10, panelY + 35, 340, 25), "Select view level:");

            // View levels map to the stakeholder types identified in requirements research
            string[] viewLevels = { "footprint", "technical", "inventory", "full" };
            string[] viewLabels =
            {
                "Footprint (promoters)",
                "Technical (sound engineers)",
                "Inventory (backline)",
                "Full (drummers)"
            };

            for (int i = 0; i < viewLevels.Length; i++)
            {
                GUI.color = selectedViewLevel == viewLevels[i] ? Color.cyan : Color.white;
                if (GUI.Button(new Rect(panelX + 10, panelY + 60 + (i * 42), 340, 35),
                    viewLabels[i]))
                    selectedViewLevel = viewLevels[i];
                GUI.color = Color.white;
            }

            if (GUI.Button(new Rect(panelX + 10, panelY + 235, 160, 45), "Generate Link"))
            {
                APIClient.Instance?.GenerateShareLink(
                    lastSavedProfileId,
                    selectedViewLevel,
                    (success, token) =>
                    {
                        if (success) generatedShareToken = token;
                    }
                );
            }

            if (GUI.Button(new Rect(panelX + 190, panelY + 235, 160, 45), "Close"))
                showSharePanel = false;

            if (!string.IsNullOrEmpty(generatedShareToken))
            {
                GUI.color = Color.green;
                GUI.Box(new Rect(panelX + 10, panelY + 255, 340, 35),
                    $"Token: {generatedShareToken}");
                GUI.color = Color.white;
            }
        }
    }
}