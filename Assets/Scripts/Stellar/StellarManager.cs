using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
public class StellarManager : MonoBehaviour
{
    [DllImport("__Internal")] 
    static extern void JSCheckFreighter(string contractAddressPtr, string contractFunctionPtr, string dataPtr, int transactionTimeoutSec, int pingFrequencyMS);
    
    public event Action<bool> OnWaiting;
    TaskCompletionSource<int> connectToNetworkTaskSource;

    public string contract = "CCSNEMSRSVXPDSGIR2KX6KGLKHDSSNSRDUH34S4AMYOG47YVRD5YIIKT";
    
    public async Task<int> ConnectToNetwork()
    {
        if (connectToNetworkTaskSource != null && !connectToNetworkTaskSource.Task.IsCompleted)
        {
            Debug.LogError("StellarManager.ConnectToNetwork is already in progress, returning existing task");
            return await connectToNetworkTaskSource.Task;
        }
        Debug.Log("StellarManager.ConnectToNetwork()");
#if UNITY_WEBGL
        connectToNetworkTaskSource = new TaskCompletionSource<int>();
        JSCheckFreighter(
            contract,
            "register",
            "newusername",
            120,
            2000);
        int result = await connectToNetworkTaskSource.Task;
        Debug.Log($"StellarManager.ConnectToNetwork() finished with result {result}");
        return result;
#else
        return -999;
#endif
    }
    
    public void ConnectToNetworkComplete(int result)
    {
        if (connectToNetworkTaskSource != null)
        {
            connectToNetworkTaskSource.TrySetResult(result);
        }
        Debug.Log($"StellarManager.OnFreighterCheckComplete() {result}");
    }
}
