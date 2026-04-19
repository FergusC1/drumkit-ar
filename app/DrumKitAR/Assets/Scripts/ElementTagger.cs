using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class ElementTagger : MonoBehaviour
{
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

    [System.Serializable]
    public class TaggedElement
    {
        public DrumElement elementType;
        public string label;
        public Vector3 position;
        public float angleDeg;
        public float heightCm;
        public ARAnchor anchor;
        public GameObject marker;
    }

    public static ElementTagger Instance { get; private set; }

    private List<TaggedElement> taggedElements = new List<TaggedElement>();
    private DrumElement selectedElement = DrumElement.SnareDrum;
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

    public void SetSelectedElement(DrumElement element)
    {
        selectedElement = element;
    }

    public void SetTaggingMode(bool active)
    {
        taggingMode = active;
    }

    public bool IsTaggingMode() => taggingMode;

    public void TagLastAnchor()
    {
        List<ARAnchor> anchors = anchorPlacer?.GetPlacedAnchors();
        if (anchors == null || anchors.Count == 0) return;

        ARAnchor lastAnchor = anchors[anchors.Count - 1];

        // Check if already tagged
        foreach (var existing in taggedElements)
        {
            if (existing.anchor == lastAnchor) return;
        }

        Vector3 pos = lastAnchor.transform.position;
        Vector3 forward = lastAnchor.transform.forward;
        float angle = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

        TaggedElement tagged = new TaggedElement
        {
            elementType = selectedElement,
            label = selectedElement.ToString(),
            position = pos,
            angleDeg = angle,
            heightCm = pos.y * 100f,
            anchor = lastAnchor
        };

        taggedElements.Add(tagged);
        Debug.Log($"Tagged anchor as {selectedElement} at {pos}");
    }

    public List<TaggedElement> GetTaggedElements() => taggedElements;

    public void ClearAll()
    {
        taggedElements.Clear();
    }

    public string GetElementIcon(DrumElement element)
    {
        return element switch
        {
            DrumElement.KickDrum => "KD",
            DrumElement.SnareDrum => "SD",
            DrumElement.HiHat => "HH",
            DrumElement.RideCymbal => "RD",
            DrumElement.CrashCymbal => "CC",
            DrumElement.FloorTom => "FT",
            DrumElement.RackTom => "RT",
            DrumElement.Splash => "SP",
            DrumElement.China => "CH",
            DrumElement.DrumThrone => "TH",
            _ => "??"
        };
    }
}