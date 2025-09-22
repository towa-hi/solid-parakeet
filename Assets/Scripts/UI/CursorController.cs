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
        ViewEventBus.OnSetupCursorToolChanged += UpdateCursor;
    }
    
    void OnDestroy()
    {
        ViewEventBus.OnSetupCursorToolChanged -= UpdateCursor;
    }
    
    void Start()
    {
        ChangeCursor(CursorType.DEFAULT);
    }

    static void ChangeCursor(CursorType cursorType)
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
    
    public static void UpdateCursor(SetupInputTool tool)
    {
        switch (tool)
        {
            case SetupInputTool.NONE:
                ChangeCursor(CursorType.DEFAULT);
                break;
            case SetupInputTool.ADD:
                ChangeCursor(CursorType.PLUS);
                break;
            case SetupInputTool.REMOVE:
                ChangeCursor(CursorType.MINUS);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public static void UpdateCursor(MoveInputTool tool)
    {
        switch (tool)
        {
            case MoveInputTool.NONE:
                ChangeCursor(CursorType.DEFAULT);
                break;
            case MoveInputTool.SELECT:
                ChangeCursor(CursorType.PLUS);
                break;
            case MoveInputTool.TARGET:
                ChangeCursor(CursorType.TARGET);
                break;
            case MoveInputTool.CLEAR_SELECT:
                ChangeCursor(CursorType.MINUS);
                break;
            case MoveInputTool.CLEAR_MOVEPAIR:
                ChangeCursor(CursorType.MINUS);
                break;
            default:
                throw new ArgumentOutOfRangeException();
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