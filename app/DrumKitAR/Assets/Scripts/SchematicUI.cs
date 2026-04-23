using UnityEngine;
using UnityEngine.SceneManagement;

public class SchematicUI : MonoBehaviour
{
    private SchematicRenderer schematicRenderer;
    private float rotationY = 0f;
    private float zoom = 5f;

    private void Start()
    {
        UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.Enable();
        schematicRenderer = SchematicRenderer.Instance ?? 
            FindObjectOfType<SchematicRenderer>();
    }

    private void Update()
    {
        if (Input.touchCount == 1)
        {
            var touch = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[0];
            if (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved)
            {
                rotationY += touch.delta.x * 0.5f;
                Camera.main.transform.rotation = Quaternion.Euler(90, rotationY, 0);
            }
        }

        if (Input.touchCount == 2)
        {
            var touch0 = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[0];
            var touch1 = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[1];

            float prevDist = Vector2.Distance(
                touch0.screenPosition - touch0.delta,
                touch1.screenPosition - touch1.delta);
            float currDist = Vector2.Distance(touch0.screenPosition, touch1.screenPosition);

            zoom -= (currDist - prevDist) * 0.01f;
            zoom = Mathf.Clamp(zoom, 1f, 15f);
            Camera.main.orthographicSize = zoom;
        }
    }

    private void OnGUI()
    {
        int padding = 20;
        int btnWidth = 140;
        int btnHeight = 50;

        GUI.Box(new Rect(padding, padding, 250, 40), "Kit Schematic - drag to rotate, pinch to zoom");

        if (GUI.Button(new Rect(padding, Screen.height - btnHeight - padding, btnWidth, btnHeight), 
            "Back to capture"))
        {
            SceneManager.LoadScene("MainScene");
        }
    }
}