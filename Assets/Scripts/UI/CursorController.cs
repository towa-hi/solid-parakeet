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
        ViewEventBus.OnStateUpdated += HandleStateUpdated;
    }
    
    void OnDestroy()
    {
        ViewEventBus.OnStateUpdated -= HandleStateUpdated;
    }
    
    void Start()
    {
        ChangeCursor(CursorType.DEFAULT);
    }

    static void HandleStateUpdated(GameSnapshot snapshot)
    {
        var mode = snapshot.Mode;
        var net = snapshot.Net;
        var ui = snapshot.Ui ?? LocalUiState.Empty;
        var tool = UiSelectors.ComputeCursorTool(mode, net, ui);
        UpdateCursor(tool);
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
    
    public static void UpdateCursor(CursorInputTool tool)
    {
        switch (tool)
        {
            case CursorInputTool.NONE:
                ChangeCursor(CursorType.DEFAULT);
                break;
            case CursorInputTool.SETUP_SET_RANK:
                ChangeCursor(CursorType.PLUS);
                break;
            case CursorInputTool.SETUP_UNSET_RANK:
                ChangeCursor(CursorType.MINUS);
                break;
            case CursorInputTool.MOVE_SELECT:
                ChangeCursor(CursorType.PLUS);
                break;
            case CursorInputTool.MOVE_TARGET:
                ChangeCursor(CursorType.TARGET);
                break;
            case CursorInputTool.MOVE_CLEAR:
                ChangeCursor(CursorType.MINUS);
                break;
            case CursorInputTool.MOVE_CLEAR_MOVEPAIR:
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