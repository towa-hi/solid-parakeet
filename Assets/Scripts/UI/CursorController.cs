using System;
using UnityEngine;

public class CursorController : MonoBehaviour
{
    public static Texture2D currentCursor;
    public Texture2D defaultCursor;
    public Texture2D plusCursor;
    public Texture2D targetCursor;
    public Texture2D minusCursor;
    public Texture2D disabledCursor;
    public Vector2 hotspot = Vector2.zero;
    public CursorMode cursorMode = CursorMode.Auto;

    public static CursorController instance;

    void Awake()
    {
        instance = this;
    }
    
    void Start()
    {
        ChangeCursor(CursorType.DEFAULT);
    }

    public static void ChangeCursor(CursorType cursorType)
    {
        Texture2D newCursor;
        switch (cursorType)
        {
            case CursorType.DEFAULT:
                newCursor = instance.defaultCursor;
                break;
            case CursorType.PLUS:
                newCursor = instance.plusCursor;
                break;
            case CursorType.TARGET:
                newCursor = instance.targetCursor;
                break;
            case CursorType.MINUS:
                newCursor = instance.minusCursor;
                break;
            case CursorType.DISABLED:
                newCursor = instance.disabledCursor;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(cursorType), cursorType, null);
        }
        if (newCursor != currentCursor)
        {
            currentCursor = newCursor;
            Cursor.SetCursor(currentCursor, instance.hotspot, instance.cursorMode);
        }
    }
}

public enum CursorType
{
    DEFAULT,
    PLUS,
    TARGET,
    MINUS,
    DISABLED,
}