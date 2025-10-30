using System.Runtime.InteropServices;
using UnityEngine;

public static class ClipboardUtility
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void JS_CopyTextToClipboard(string text);
#endif

    public static void Copy(string text)
    {
        string safeText = text ?? string.Empty;
#if UNITY_WEBGL && !UNITY_EDITOR
        JS_CopyTextToClipboard(safeText);
#else
        GUIUtility.systemCopyBuffer = safeText;
#endif
    }
}

