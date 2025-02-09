using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
public class StellarManager : MonoBehaviour
{
    [DllImport("__Internal")] 
    static extern void JSCheckFreighter(string contractAddressPtr, string contractFunctionPtr, string dataPtr, int transactionTimeoutSec, int pingFrequencyMS);
    
    
    TaskCompletionSource<int> freighterCheckTaskSource;
    TaskCompletionSource<bool> setFreighterAllowedTaskSource;
    
    public async Task<int> ConnectToNetwork()
    {
        Debug.Log("StellarManager.ConnectToNetwork()");
#if UNITY_WEBGL
        freighterCheckTaskSource = new TaskCompletionSource<int>();
        JSCheckFreighter(
            "CDSEFFTMRY3F4Y2C5J3KV7G7VEHGWJIWWWYE4BG2VAY34FII3KHNQ4GT",
            "hello",
            "data",
            120,
            2000);
        
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
}
