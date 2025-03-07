
using System.Runtime.InteropServices;
using UnityEngine;

public class SignString: MonoBehaviour
{
    [DllImport("__Internal")]
    private static extern string SignStringJS(string message);

    public static string Sign(string message)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
            return SignStringJS(message);
#else
        return "Signing only available in WebGL build";
#endif
    }
}
