using System.Threading.Tasks;
using UnityEngine;

public class GameClient
{
    NetworkManager networkManager;
    
    public GameClient(NetworkManager inNetworkManager)
    {
        networkManager = inNetworkManager;
        networkManager.OnWelcomeReceived += OnWelcomeReceived;
        networkManager.OnEchoReceived += OnEchoReceived;
        networkManager.OnMoveReceived += OnMoveReceived;
        networkManager.OnErrorReceived += OnErrorReceived;
        networkManager.OnUpdateReceived += OnUpdateReceived;
    }

    void Update()
    {
        networkManager.ProcessIncomingMessages();
    }

    public async Task StartAsync(string serverIP, int serverPort, string alias)
    {
        await networkManager.ConnectToServerAsync(serverIP, serverPort, alias);
    }

    public void Stop()
    {
        networkManager.Disconnect();
    }
    
    void OnWelcomeReceived(string message)
    {
        Debug.Log("Server welcome: " + message);
    }
    
    void OnEchoReceived(string message)
    {
        Debug.Log("Server echo: " + message);
    }

    void OnMoveReceived(string message)
    {
        Debug.Log("Server move: " + message);
    }

    void OnErrorReceived(string message)
    {
        Debug.Log("Server error: " + message);
    }

    void OnUpdateReceived(string message)
    {
        Debug.Log("Server update: " + message);
    }
}
