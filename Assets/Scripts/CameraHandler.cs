using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// This is the default controls for handling camera movement on the world map.
/// Attach this script to the main camera.
/// </summary>
public class CameraHandler : MonoBehaviour
{
    public Camera Camera { get; private set; }

    protected static float ZOOM_SPEED = 2f; // Mouse Wheel Speed
    protected static float DRAG_SPEED = 0.05f; // Middle Mouse Drag Speed
    protected static float PAN_SPEED = 20f; // WASD Speed
    protected static float MIN_CAMERA_SIZE = 2f;
    protected static float MAX_CAMERA_SIZE = 50f;
    protected bool IsLeftMouseDown;
    protected bool IsRightMouseDown;
    protected bool IsMouseWheelDown;

    // Size
    private float CameraHeightWorld => Camera.orthographicSize;
    private float CameraWidthWorld => Camera.orthographicSize * Camera.aspect;

    // Bounds
    protected float MinX, MinY, MaxX, MaxY;

    public void SetPosition(Vector2 pos)
    {
        transform.position = new Vector3(pos.x, pos.y, transform.position.z);
    }

    public void SetZoom(float zoom)
    {
        Camera.orthographicSize = zoom;
    }

    private void Start()
    {
        Camera = GetComponent<Camera>();
        SetBounds(-100, -100, 100, 100);
    }


    private void Update()
    {
        // Scroll
        if (Input.mouseScrollDelta.y != 0)
        {
            Camera.orthographicSize += -Input.mouseScrollDelta.y * ZOOM_SPEED;

            // Zoom Boundaries
            if (Camera.orthographicSize < MIN_CAMERA_SIZE) Camera.orthographicSize = MIN_CAMERA_SIZE;
            if (Camera.orthographicSize > MAX_CAMERA_SIZE) Camera.orthographicSize = MAX_CAMERA_SIZE;
        }

        // Dragging with right/middle mouse button
        if (Input.GetKeyDown(KeyCode.Mouse2)) IsMouseWheelDown = true;
        if (Input.GetKeyUp(KeyCode.Mouse2)) IsMouseWheelDown = false;
        if (Input.GetKeyDown(KeyCode.Mouse1)) IsRightMouseDown = true;
        if (Input.GetKeyUp(KeyCode.Mouse1)) IsRightMouseDown = false;
        if (IsMouseWheelDown)
        {
            float speed = DRAG_SPEED * Camera.orthographicSize;
            float canvasScaleFactor = GameObject.Find("Canvas").GetComponent<Canvas>().scaleFactor;
            transform.position += new Vector3(-Input.GetAxis("Mouse X") * speed / canvasScaleFactor, -Input.GetAxis("Mouse Y") * speed / canvasScaleFactor, 0f);
        }

        // Panning with WASD
        if(Input.GetKey(KeyCode.W)) transform.position += new Vector3(0f, PAN_SPEED * Time.deltaTime, 0f);
        if(Input.GetKey(KeyCode.A)) transform.position += new Vector3(-PAN_SPEED * Time.deltaTime, 0f, 0f);
        if(Input.GetKey(KeyCode.S)) transform.position += new Vector3(0f, -PAN_SPEED * Time.deltaTime, 0f);
        if(Input.GetKey(KeyCode.D)) transform.position += new Vector3(PAN_SPEED * Time.deltaTime, 0f, 0f);

        // Drag triggers
        if (Input.GetKeyDown(KeyCode.Mouse0) && !IsLeftMouseDown)
        {
            IsLeftMouseDown = true;
            OnLeftMouseDragStart();
        }
        if (Input.GetKeyUp(KeyCode.Mouse0) && IsLeftMouseDown)
        {
            IsLeftMouseDown = false;
            OnLeftMouseDragEnd();
        }

        // Bounds
        float realMinX = MinX + CameraWidthWorld - 1f;
        float realMaxX = MaxX - CameraWidthWorld + 1f;
        float realMinY = MinY + CameraHeightWorld - 1f;
        float realMaxY = MaxY - CameraHeightWorld + 1f;
        if (transform.position.x < realMinX) transform.position = new Vector3(realMinX, transform.position.y, transform.position.z);
        if (transform.position.x > realMaxX) transform.position = new Vector3(realMaxX, transform.position.y, transform.position.z);
        if (transform.position.y < realMinY) transform.position = new Vector3(transform.position.x, realMinY, transform.position.z);
        if (transform.position.y > realMaxY) transform.position = new Vector3(transform.position.x, realMaxY, transform.position.z);
    }

    public void SetBounds(float minX, float minY, float maxX, float maxY)
    {
        MinX = minX;
        MinY = minY;
        MaxX = maxX;
        MaxY = maxY;
    }

    #region Triggers

    protected virtual void OnLeftMouseDragStart() { }

    protected virtual void OnLeftMouseDragEnd() { }

    #endregion
}
