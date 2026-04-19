using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class CalibrationManager : MonoBehaviour
{
    [Header("Calibration Settings")]
    [SerializeField] private float knownWidthCm = 29.7f;
    [SerializeField] private float knownHeightCm = 21.0f;
    [SerializeField] private float acceptableErrorPercent = 10f;

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

        // Compare against A4 diagonal (35.9cm) or width (29.7cm) depending on placement
        float knownDistanceCm = knownWidthCm;
        ErrorPercent = Mathf.Abs(MeasuredDistanceCm - knownDistanceCm) / knownDistanceCm * 100f;

        if (ErrorPercent <= acceptableErrorPercent)
        {
            ConfidenceScore = 1f - (ErrorPercent / acceptableErrorPercent);
            State = CalibrationState.Complete;
            IsCalibrated = true;
            Debug.Log($"Calibration complete - measured: {MeasuredDistanceCm:F1}cm, " +
                      $"expected: {knownDistanceCm}cm, " +
                      $"error: {ErrorPercent:F1}%, " +
                      $"confidence: {ConfidenceScore:F2}");
        }
        else
        {
            ConfidenceScore = 0f;
            State = CalibrationState.Failed;
            IsCalibrated = false;
            Debug.LogWarning($"Calibration failed - error too high: {ErrorPercent:F1}%");
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