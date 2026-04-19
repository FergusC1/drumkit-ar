using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class CalibrationManager : MonoBehaviour
{
    [Header("Calibration Settings")]
    [SerializeField] private float knownWidthCm = 29.7f;
    [SerializeField] private float knownHeightCm = 21.0f;
    [SerializeField] private float acceptableErrorPercent = 25f;

    public enum CalibrationState
    {
        Idle,
        WaitingForFirstAnchor,
        WaitingForSecondAnchor,
        Complete,
        Failed
    }

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

    public void StartCalibration()
    {
        State = CalibrationState.WaitingForFirstAnchor;
        firstAnchorPlaced = false;
        IsCalibrated = false;
        ConfidenceScore = 0f;
        Debug.Log("Calibration started - place first anchor on corner of A4 sheet");
    }

    public void RegisterAnchorForCalibration(Vector3 position)
    {
        if (State == CalibrationState.WaitingForFirstAnchor)
        {
            firstAnchorPosition = position;
            firstAnchorPlaced = true;
            State = CalibrationState.WaitingForSecondAnchor;
            Debug.Log("First calibration anchor placed - place second anchor on opposite corner");
        }
        else if (State == CalibrationState.WaitingForSecondAnchor && firstAnchorPlaced)
        {
            secondAnchorPosition = position;
            CalculateCalibration();
        }
    }

    private void CalculateCalibration()
{
         float measuredMetres = Vector3.Distance(firstAnchorPosition, secondAnchorPosition);
        MeasuredDistanceCm = measuredMetres * 100f;

        Debug.Log($"First anchor: {firstAnchorPosition}");
        Debug.Log($"Second anchor: {secondAnchorPosition}");
        Debug.Log($"Raw distance metres: {measuredMetres}");
        Debug.Log($"Measured cm: {MeasuredDistanceCm}");

        float knownDistanceCm = knownWidthCm;
        ErrorPercent = Mathf.Abs(MeasuredDistanceCm - knownDistanceCm) / knownDistanceCm * 100f;

        ConfidenceScore = Mathf.Clamp01(1f - (ErrorPercent / 100f));

        if (ErrorPercent <= 10f)
        {
            State = CalibrationState.Complete;
            IsCalibrated = true;
            Debug.Log($"High confidence calibration - error: {ErrorPercent:F1}%");
        }
        else if (ErrorPercent <= 25f)
        {
            State = CalibrationState.Complete;
            IsCalibrated = true;
            Debug.Log($"Medium confidence calibration - error: {ErrorPercent:F1}%");
        }
        else
        {
            State = CalibrationState.Failed;
            IsCalibrated = false;
            Debug.LogWarning($"Low confidence - error: {ErrorPercent:F1}%");
        }
    }

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