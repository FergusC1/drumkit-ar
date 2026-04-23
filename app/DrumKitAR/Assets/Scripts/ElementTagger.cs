// ElementTagger.cs
// Associates drum element types with placed AR anchors.
// The user places an anchor then selects an element type from the UI panel,
// which calls TagLastAnchor() to label the most recently placed anchor.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class ElementTagger : MonoBehaviour
{
    // All supported drum kit element types
    public enum DrumElement
    {
        KickDrum,
        SnareDrum,
        HiHat,
        RideCymbal,
        CrashCymbal,
        FloorTom,
        RackTom,
        Splash,
        China,
        DrumThrone
    }

    // Stores all data for a single tagged element.
    // Serializable so Unity can inspect it and JsonUtility can serialise it.
    [System.Serializable]
    public class TaggedElement
    {
        public DrumElement elementType;
        public string label;
        public Vector3 position;   // World position in metres (converted to cm on save)
        public float angleDeg;     // Bearing angle in the horizontal plane
        public float heightCm;     // Height above floor in centimetres
        public ARAnchor anchor;    // Reference to the underlying AR anchor
        public GameObject marker;
    }

    public static ElementTagger Instance { get; private set; }

    private List<TaggedElement> taggedElements = new List<TaggedElement>();
    private DrumElement selectedElement = DrumElement.SnareDrum; // Default selection
    private bool taggingMode = false;
    private AnchorPlacer anchorPlacer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        anchorPlacer = AnchorPlacer.Instance;
    }

    public void SetSelectedElement(DrumElement element) => selectedElement = element;
    public void SetTaggingMode(bool active) => taggingMode = active;
    public bool IsTaggingMode() => taggingMode;

    // Tags the most recently placed anchor with the currently selected element type.
    // Checks for duplicate tagging so the same anchor cannot be tagged twice.
    public void TagLastAnchor()
    {
        List<ARAnchor> anchors = anchorPlacer?.GetPlacedAnchors();
        if (anchors == null || anchors.Count == 0) return;

        ARAnchor lastAnchor = anchors[anchors.Count - 1];

        // Prevent the same anchor being tagged more than once
        foreach (var existing in taggedElements)
            if (existing.anchor == lastAnchor) return;

        Vector3 pos = lastAnchor.transform.position;
        float angle = Mathf.Atan2(
            lastAnchor.transform.forward.x,
            lastAnchor.transform.forward.z) * Mathf.Rad2Deg;

        taggedElements.Add(new TaggedElement
        {
            elementType = selectedElement,
            label = selectedElement.ToString(),
            position = pos,
            angleDeg = angle,
            heightCm = pos.y * 100f,
            anchor = lastAnchor
        });

        Debug.Log($"Tagged as {selectedElement} at {pos}");
    }

    // Read-only access to the tagged element list for MeasurementUI and APIClient
    public List<TaggedElement> GetTaggedElements() => taggedElements;

    // Clears all tags - called by the Clear button in MeasurementUI
    public void ClearAll() => taggedElements.Clear();

    // Returns a two-letter short code for display in the schematic and element list
    public string GetElementIcon(DrumElement element)
    {
        return element switch
        {
            DrumElement.KickDrum    => "KD",
            DrumElement.SnareDrum   => "SD",
            DrumElement.HiHat       => "HH",
            DrumElement.RideCymbal  => "RD",
            DrumElement.CrashCymbal => "CC",
            DrumElement.FloorTom    => "FT",
            DrumElement.RackTom     => "RT",
            DrumElement.Splash      => "SP",
            DrumElement.China       => "CH",
            DrumElement.DrumThrone  => "TH",
            _                       => "??"
        };
    }
}