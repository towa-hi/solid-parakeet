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
        ViewEventBus.OnSetupHoverChanged += HandleSetupHover;
        ViewEventBus.OnMoveHoverChanged += HandleMoveHover;
        ViewEventBus.OnClientModeChanged += HandleClientModeChanged;
    }
    
    void OnDestroy()
    {
        ViewEventBus.OnSetupHoverChanged -= HandleSetupHover;
        ViewEventBus.OnMoveHoverChanged -= HandleMoveHover;
        ViewEventBus.OnClientModeChanged -= HandleClientModeChanged;
    }
    
    void Start()
    {
        ChangeCursor(CursorType.DEFAULT);
    }

    static void HandleSetupHover(Vector2Int pos, bool isMyTurn, SetupInputTool tool)
    {
        if (!isMyTurn) { ChangeCursor(CursorType.DISABLED); return; }
        UpdateCursor(tool);
    }

    static void HandleMoveHover(Vector2Int pos, bool isMyTurn, MoveInputTool tool, System.Collections.Generic.HashSet<Vector2Int> _)
    {
        if (!isMyTurn) { ChangeCursor(CursorType.DISABLED); return; }
        UpdateCursor(tool);
    }

    static void HandleClientModeChanged(ClientMode mode, GameNetworkState net, LocalUiState ui)
    {
        if (mode == ClientMode.Resolve || mode == ClientMode.Finished || mode == ClientMode.Aborted)
        {
            ChangeCursor(CursorType.DEFAULT);
        }
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