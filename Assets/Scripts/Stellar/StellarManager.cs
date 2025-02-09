using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
public class StellarManager : MonoBehaviour
{
    [DllImport("__Internal")] 
    static extern void JSCheckFreighter();

    [DllImport("__Internal")]
    static extern void JSSetFreighterAllowed();
    
    
    TaskCompletionSource<int> freighterCheckTaskSource;
    TaskCompletionSource<bool> setFreighterAllowedTaskSource;
    
    public async Task<int> ConnectToNetwork()
    {
        Debug.Log("StellarManager.ConnectToNetwork()");
#if UNITY_WEBGL
        freighterCheckTaskSource = new TaskCompletionSource<int>();
        JSCheckFreighter();
        int result = await freighterCheckTaskSource.Task;
        Debug.Log($"StellarManager.ConnectToNetwork() finished with result {result}");
        
        return result;
#else
        return -3;
#endif
    }
    
    public void OnFreighterCheckComplete(int result)
    {
        if (freighterCheckTaskSource != null)
        {
            freighterCheckTaskSource.TrySetResult(result);
        }
        Debug.Log($"StellarManager.OnFreighterCheckComplete() {result}");
    }

    public async Task<bool> SetFreighterAllowed()
    {
        Debug.Log("StellarManager.SetFreighterAllowed()");
        setFreighterAllowedTaskSource = new();
        JSSetFreighterAllowed();
        bool result = await setFreighterAllowedTaskSource.Task;
        Debug.Log($"StellarManager.SetFreighterAllowed() finished with result {result}");
        return result;
    }

    public void OnSetFreighterAllowedComplete(int result)
    {
        if (result == -1)
        {
            Debug.Log("StellarManager.OnSetFreighterAllowedComplete() rejected");
        }
    }
}
