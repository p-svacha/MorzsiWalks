using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class GoogleMapUrlCreator
{
    // ---------------------------------------------------------------------------------------------------
    // How to use this class:
    // 1. Chose coordiantes for the sector you want to add to the background map.
    // 2. Call GetUrlForSector for the chosen coordinates.
    // 3. Copy the link that is returned to the console and paste it into the browser.
    // 4. Make sure "Beschriftungen" is turned off in google maps (bottom left corner > Ebenen > Mehr > ganz unten)
    // 5. Use snipping tool and select the are starting in the BOTTOM RIGHT where the "-" and "^^" meet, so that no UI element is visible.
    // 6. Drag the area to the top to the search bor and to the left to the "Ebenen" icon.
    // 7. Save the image to Resources/BackgroundMaps/Snipptes
    // 8. When you're done use Photoshop or whatever to merge them together.
    // ---------------------------------------------------------------------------------------------------

    // https://www.google.ch/maps/@47.5471437,7.6202267,189m/data=!3m1!1e3?entry=ttu
    private const float CENTER_X = 7.620215f;
    private const float CENTER_Y = 47.5471191f;
    private const float ZOOM_LEVEL = 159;

    private const float X_STEP = 0.0045144f;
    private const float Y_STEP = -0.0014104f;

    public static string GetUrlForSector(int x, int y)
    {
        float xCoordinates = CENTER_X + x * X_STEP;
        float yCoordinates = CENTER_Y + y * Y_STEP;
        return "https://www.google.ch/maps/@" + yCoordinates + "," + xCoordinates + "," + ZOOM_LEVEL + "m/data=!3m1!1e3?entry=ttu";
    }
}
