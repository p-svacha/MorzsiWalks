using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MenuButton : MonoBehaviour
{
    public Button Button;
    public Image Image;

    public void Select()
    {
        Image.color = Color.yellow;
    }

    public void Unselect()
    {
        Image.color = Color.white;
    }
}
