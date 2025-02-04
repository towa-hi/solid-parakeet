using System.Runtime.InteropServices;
using UnityEngine;
public class StellarManager : MonoBehaviour
{
    [DllImport("__Internal")]
    static extern void CheckFreighter();
    public async void ConnectToNetwork()
    {
        Debug.Log("StellarManager.ConnectToNetwork()");
#if UNITY_WEBGL
        CheckFreighter();
#endif
    }


    public void OnFreighterCheckComplete(int result)
    {
        Debug.Log($"StellarManager.OnFreighterCheckComplete() {result}");
    }
    
    
}
