using System.Collections.Generic;
using UnityEngine;

public class SchematicRenderer : MonoBehaviour
{
    [Header("Schematic Settings")]
    [SerializeField] private float scaleFactor = 0.01f;
    [SerializeField] private Camera schematicCamera;
    [SerializeField] private GameObject schematicPanel;

    private List<ElementTagger.TaggedElement> elements;
    private List<GameObject> renderedObjects = new List<GameObject>();
    private float rotationY = 0f;
    private float zoom = 5f;
    private bool isVisible = false;

    public static SchematicRenderer Instance { get; private set; }

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
        if (schematicCamera != null)
        {
            schematicCamera.transform.position = new Vector3(0, 10, 0);
            schematicCamera.transform.rotation = Quaternion.Euler(90, 0, 0);
            schematicCamera.orthographic = true;
            schematicCamera.orthographicSize = zoom;
            schematicCamera.gameObject.SetActive(false);
        }

        if (schematicPanel != null)
            schematicPanel.SetActive(false);
    }

    private void Update()
    {
        if (!isVisible) return;

        if (UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count == 1)
        {
            var touch = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[0];
            if (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved)
            {
                rotationY += touch.delta.x * 0.5f;
                schematicCamera.transform.rotation = Quaternion.Euler(90, rotationY, 0);
            }
        }

        if (UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count == 2)
        {
            var touch0 = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[0];
            var touch1 = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[1];

            float prevDist = Vector2.Distance(
                touch0.screenPosition - touch0.delta,
                touch1.screenPosition - touch1.delta);
            float currDist = Vector2.Distance(
                touch0.screenPosition, touch1.screenPosition);

            zoom -= (currDist - prevDist) * 0.01f;
            zoom = Mathf.Clamp(zoom, 1f, 15f);
            schematicCamera.orthographicSize = zoom;
        }
    }

   private Camera arCamera;

    public void ShowSchematic(List<ElementTagger.TaggedElement> kitElements)
    {
        elements = kitElements;
        Debug.Log($"ShowSchematic called with {kitElements.Count} elements");
        RenderKit();
        isVisible = true;

        // Find and disable AR camera component but keep gameobject active
        arCamera = GameObject.Find("Main Camera")?.GetComponent<Camera>();
        if (arCamera != null)
        {
            arCamera.enabled = false;
            Debug.Log("AR camera disabled");
        }
        else
        {
            Debug.LogError("Main Camera not found");
        }

        if (schematicCamera != null)
        {
            schematicCamera.gameObject.SetActive(true);
            schematicCamera.enabled = true;
            Debug.Log($"Schematic camera activated: {schematicCamera.name}");
        }
        else
        {
            Debug.LogError("Schematic camera is null");
        }
    }

    public void HideSchematic()
    {
        isVisible = false;
        ClearRendered();

        if (schematicCamera != null)
        {
            schematicCamera.enabled = false;
            schematicCamera.gameObject.SetActive(false);
        }

        // Re-enable AR camera component
        if (arCamera != null)
        {
            arCamera.enabled = true;
            Debug.Log("AR camera re-enabled");
        }
        else
        {
            arCamera = GameObject.Find("Main Camera")?.GetComponent<Camera>();
            if (arCamera != null)
            {
                arCamera.enabled = true;
                Debug.Log("AR camera found and re-enabled");
            }
        }

        Debug.Log("Schematic hidden, AR camera restored");
    }
    private void RenderKit()
    {
        ClearRendered();
        if (elements == null || elements.Count == 0) return;

        Vector3 centre = CalculateCentre();

        foreach (var element in elements)
        {
            Vector3 worldPos = new Vector3(
                element.position.x - centre.x,
                0,
                element.position.z - centre.z
            );
            renderedObjects.Add(CreateElementMarker(element, worldPos));
        }
    }

    private Vector3 CalculateCentre()
    {
        Vector3 sum = Vector3.zero;
        foreach (var e in elements)
            sum += e.position;
        return sum / elements.Count;
    }

    private GameObject CreateElementMarker(ElementTagger.TaggedElement element, Vector3 position)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.transform.position = position;
        marker.transform.localScale = GetElementScale(element.elementType);
        marker.transform.rotation = Quaternion.Euler(0, element.angleDeg, 0);

        Renderer rend = marker.GetComponent<Renderer>();
        rend.material.color = GetElementColor(element.elementType);

        GameObject label = new GameObject("Label");
        label.transform.parent = marker.transform;
        label.transform.localPosition = new Vector3(0, 1.5f, 0);

        TextMesh text = label.AddComponent<TextMesh>();
        text.text = GetElementIcon(element.elementType);
        text.fontSize = 24;
        text.alignment = TextAlignment.Center;
        text.anchor = TextAnchor.MiddleCenter;
        text.color = Color.white;

        return marker;
    }

    public void OnGUI()
    {
        if (!isVisible) return;

        int padding = 20;
        GUI.Box(new Rect(padding, padding, 300, 40), 
            "Kit Schematic - drag to rotate, pinch to zoom");

        if (GUI.Button(new Rect(padding, Screen.height - 70 - padding, 160, 50), 
            "Back to capture"))
        {
            HideSchematic();
        }
    }

    private void ClearRendered()
    {
        foreach (var obj in renderedObjects)
            if (obj != null) Destroy(obj);
        renderedObjects.Clear();
    }

    private Vector3 GetElementScale(ElementTagger.DrumElement element)
    {
        return element switch
        {
            ElementTagger.DrumElement.KickDrum => new Vector3(0.9f, 0.3f, 0.9f),
            ElementTagger.DrumElement.SnareDrum => new Vector3(0.5f, 0.3f, 0.5f),
            ElementTagger.DrumElement.FloorTom => new Vector3(0.7f, 0.3f, 0.7f),
            ElementTagger.DrumElement.RackTom => new Vector3(0.5f, 0.3f, 0.5f),
            ElementTagger.DrumElement.HiHat => new Vector3(0.4f, 0.05f, 0.4f),
            ElementTagger.DrumElement.RideCymbal => new Vector3(0.55f, 0.05f, 0.55f),
            ElementTagger.DrumElement.CrashCymbal => new Vector3(0.45f, 0.05f, 0.45f),
            ElementTagger.DrumElement.Splash => new Vector3(0.3f, 0.05f, 0.3f),
            ElementTagger.DrumElement.China => new Vector3(0.45f, 0.05f, 0.45f),
            ElementTagger.DrumElement.DrumThrone => new Vector3(0.3f, 0.5f, 0.3f),
            _ => new Vector3(0.5f, 0.3f, 0.5f)
        };
    }

    private Color GetElementColor(ElementTagger.DrumElement element)
    {
        return element switch
        {
            ElementTagger.DrumElement.KickDrum => new Color(0.8f, 0.2f, 0.2f),
            ElementTagger.DrumElement.SnareDrum => new Color(0.2f, 0.6f, 0.8f),
            ElementTagger.DrumElement.FloorTom => new Color(0.8f, 0.5f, 0.2f),
            ElementTagger.DrumElement.RackTom => new Color(0.8f, 0.7f, 0.2f),
            ElementTagger.DrumElement.HiHat => new Color(0.7f, 0.7f, 0.7f),
            ElementTagger.DrumElement.RideCymbal => new Color(0.9f, 0.9f, 0.6f),
            ElementTagger.DrumElement.CrashCymbal => new Color(0.9f, 0.8f, 0.5f),
            ElementTagger.DrumElement.Splash => new Color(0.8f, 0.9f, 0.5f),
            ElementTagger.DrumElement.China => new Color(0.6f, 0.8f, 0.5f),
            ElementTagger.DrumElement.DrumThrone => new Color(0.4f, 0.4f, 0.4f),
            _ => Color.white
        };
    }

    private string GetElementIcon(ElementTagger.DrumElement element)
    {
        return element switch
        {
            ElementTagger.DrumElement.KickDrum => "KD",
            ElementTagger.DrumElement.SnareDrum => "SD",
            ElementTagger.DrumElement.HiHat => "HH",
            ElementTagger.DrumElement.RideCymbal => "RD",
            ElementTagger.DrumElement.CrashCymbal => "CC",
            ElementTagger.DrumElement.FloorTom => "FT",
            ElementTagger.DrumElement.RackTom => "RT",
            ElementTagger.DrumElement.Splash => "SP",
            ElementTagger.DrumElement.China => "CH",
            ElementTagger.DrumElement.DrumThrone => "TH",
            _ => "??"
        };
    }
}