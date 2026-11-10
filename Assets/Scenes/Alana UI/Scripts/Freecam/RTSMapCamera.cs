using UnityEngine;

public class RTSMapCamera : MonoBehaviour
{
    public float moveSpeed = 50f;
    public float zoomSpeed = 20f;
    public float minHeight = 5f;
    public float maxHeight = 100f;

    void Update()
    {
        // 1. Keyboard Panning (WASD or Arrow keys)
        float moveX = Input.GetAxis("Horizontal"); // A/D
        float moveZ = Input.GetAxis("Vertical");   // W/S

        Vector3 moveDir = new Vector3(moveX, 0f, moveZ).normalized;
        transform.Translate(moveDir * moveSpeed * Time.deltaTime, Space.World);

        // 2. Smooth Zooming (Mouse Scroll Wheel)
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        Vector3 zoomDir = new Vector3(0, -scroll * zoomSpeed, 0);
        
        // Clamp height constraints over the Mars heightmap
        Vector3 nextPos = transform.position + zoomDir;
        if (nextPos.y >= minHeight && nextPos.y <= maxHeight)
        {
            transform.position = nextPos;
        }
    }
}