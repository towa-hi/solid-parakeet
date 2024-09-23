using System;
using UnityEngine;

public class DebugText : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = Vector3.zero;
    public string message = "debug info";
    public int fontSize = 11;
    public float dotSizePx = 10;
    public Color color = Color.white;
    public Texture2D texture;
    
    Camera mainCamera;
    GUIStyle style = new GUIStyle();
    
    void Start()
    {
        mainCamera = Camera.main;
        if (target == null)
        {
            target = transform;
        }
    }

    void OnGUI()
    {
        Vector3 screenPos = mainCamera.WorldToScreenPoint(target.position + offset);
        if (screenPos.z > 0)
        {
            screenPos.y = Screen.height - screenPos.y;
            style.fontSize = fontSize;
            style.normal.textColor = color;
            GUI.Label(new Rect(screenPos.x, screenPos.y, 200, 50), message);
            GUI.DrawTexture(new Rect(screenPos.x - dotSizePx / 2, screenPos.y - dotSizePx / 2,dotSizePx,dotSizePx), texture);
        }
    }


}
