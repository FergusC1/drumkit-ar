// CalibrationManager.cs
// Measures ARCore accuracy by comparing a user-placed measurement against
// a known real-world reference object (an A4 sheet, 29.7cm x 21.0cm).
// Produces a confidence score that is stored with each measurement session.

using UnityEngine;

public class CalibrationManager : MonoBehaviour
{
    [Header("Calibration Settings")]
    [SerializeField] private float knownWidthCm = 29.7f;   // A4 width in cm
    [SerializeField] private float knownHeightCm = 21.0f;  // A4 height in cm (unused, available for diagonal calibration)
    [SerializeField] private float acceptableErrorPercent = 25f; // Max error before calibration is marked failed

    public enum CalibrationState
    {
        Idle,
        WaitingForFirstAnchor,
        WaitingForSecondAnchor,
        Complete,
        Failed
    }

    // Read-only public properties expose calibration results to other scripts
    public CalibrationState State { get; private set; } = CalibrationState.Idle;
    public float ConfidenceScore { get; private set; } = 0f;
    public float MeasuredDistanceCm { get; private set; } = 0f;
    public float ErrorPercent { get; private set; } = 0f;
    public bool IsCalibrated { get; private set; } = false;

    public static CalibrationManager Instance { get; private set; }

    private Vector3 firstAnchorPosition;
    private Vector3 secondAnchorPosition;
    private bool firstAnchorPlaced = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // Resets state and begins waiting for the user to place two anchors
    public void StartCalibration()
    {
        State = CalibrationState.WaitingForFirstAnchor;
        firstAnchorPlaced = false;
        IsCalibrated = false;
        ConfidenceScore = 0f;
        Debug.Log("Calibration started - place first anchor on corner of A4 sheet");
    }

    // Called by AnchorPlacer each time an anchor is placed during calibration.
    // First call records the first corner, second call triggers the calculation.
    public void RegisterAnchorForCalibration(Vector3 position)
    {
        if (State == CalibrationState.WaitingForFirstAnchor)
        {
            firstAnchorPosition = position;
            firstAnchorPlaced = true;
            State = CalibrationState.WaitingForSecondAnchor;
            Debug.Log("First anchor placed - tap opposite corner of A4 sheet");
        }
        else if (State == CalibrationState.WaitingForSecondAnchor && firstAnchorPlaced)
        {
            secondAnchorPosition = position;
            CalculateCalibration();
        }
    }

    // Compares the measured distance between the two anchors against the
    // known A4 width to produce an error percentage and confidence score.
    // Results are banded into High (<10% error), Medium (<25%), or Failed.
    private void CalculateCalibration()
    {
        // Unity world units are metres - convert to cm for comparison
        float measuredMetres = Vector3.Distance(firstAnchorPosition, secondAnchorPosition);
        MeasuredDistanceCm = measuredMetres * 100f;

        Debug.Log($"First anchor: {firstAnchorPosition}");
        Debug.Log($"Second anchor: {secondAnchorPosition}");
        Debug.Log($"Measured: {MeasuredDistanceCm:F1}cm, Expected: {knownWidthCm}cm");

        float knownDistanceCm = knownWidthCm;
        ErrorPercent = Mathf.Abs(MeasuredDistanceCm - knownDistanceCm) / knownDistanceCm * 100f;

        // Confidence score is a linear scale from 0 (100% error) to 1 (0% error)
        ConfidenceScore = Mathf.Clamp01(1f - (ErrorPercent / 100f));

        if (ErrorPercent <= 10f)
        {
            State = CalibrationState.Complete;
            IsCalibrated = true;
            Debug.Log($"High confidence - error: {ErrorPercent:F1}%");
        }
        else if (ErrorPercent <= 25f)
        {
            State = CalibrationState.Complete;
            IsCalibrated = true;
            Debug.Log($"Medium confidence - error: {ErrorPercent:F1}%");
        }
        else
        {
            // Error too high - likely caused by poor plane detection or
            // anchors placed before ARCore has stabilised the scene map
            State = CalibrationState.Failed;
            IsCalibrated = false;
            Debug.LogWarning($"Calibration failed - error: {ErrorPercent:F1}%");
        }
    }

    // Resets all calibration state - called when starting a new session
    public void Reset()
    {
        State = CalibrationState.Idle;
        firstAnchorPlaced = false;
        IsCalibrated = false;
        ConfidenceScore = 0f;
        MeasuredDistanceCm = 0f;
        ErrorPercent = 0f;
    }
}