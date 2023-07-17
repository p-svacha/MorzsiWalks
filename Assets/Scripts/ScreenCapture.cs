using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScreenCapture : MonoBehaviour
{
    public Renderer ScreenGrabRenderer;
    private Texture2D DestinationTexture;
    private bool IsPerformingScreenGrab;

    void Start()
    {
        // Create a new Texture2D with the width and height of the screen, and cache it for reuse
        DestinationTexture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);

        // Make screenGrabRenderer display the texture.
        ScreenGrabRenderer.material.mainTexture = DestinationTexture;

        // Add the onPostRender callback
        Camera.onPostRender += OnPostRenderCallback;
    }

    void Update()
    {
        // When the user presses the Space key, perform the screen grab operation
        if (Input.GetKeyDown(KeyCode.Space))
        {
            IsPerformingScreenGrab = true;
        }
    }

    void OnPostRenderCallback(Camera cam)
    {
        Debug.Log("Camera callback: Camera name is " + cam.name);

        if (IsPerformingScreenGrab)
        {
            Debug.Log("Capturing screen");

            // Check whether the Camera that just finished rendering is the one you want to take a screen grab from
            if (cam == Camera.main)
            {
                Debug.Log("Capturing screen");

                // Define the parameters for the ReadPixels operation
                Rect captureArea = new Rect(110, 160, 1750, 810);
                int xPosToWriteTo = 0;
                int yPosToWriteTo = 0;
                bool updateMipMapsAutomatically = false;

                // Copy the pixels from the Camera's render target to the texture
                DestinationTexture.ReadPixels(captureArea, xPosToWriteTo, yPosToWriteTo, updateMipMapsAutomatically);

                // Upload texture data to the GPU, so the GPU renders the updated texture
                // Note: This method is costly, and you should call it only when you need to
                // If you do not intend to render the updated texture, there is no need to call this method at this point
                DestinationTexture.Apply();

                // Reset the isPerformingScreenGrab state
                IsPerformingScreenGrab = false;
            }
        }
    }

    // Remove the onPostRender callback
    void OnDestroy()
    {
        Camera.onPostRender -= OnPostRenderCallback;
    }
}
