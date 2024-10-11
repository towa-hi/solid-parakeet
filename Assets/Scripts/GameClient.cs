using System;
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

    public async Task CreateNewGameAsync(int[] password)
    {
        if (password.Length != 5)
        {
            Debug.LogError("Password must consist of exactly five integers");
            return;
        }
        byte[] passwordBytes = new byte[20];
        for (int i = 0; i < 5; i++)
        {
            byte[] intBytes = BitConverter.GetBytes(password[i]);
            Array.Copy(intBytes, 0, passwordBytes, i * 4, 4);
        }

        await networkManager.SendMessageAsync(MessageType.NEWGAME, passwordBytes);
        Debug.Log("Sent NEWGAME request to server.");
    }
    public void Stop()
    {
        networkManager.Disconnect();
    }
    
    void OnWelcomeReceived(string message)
    {
        Debug.Log("Server welcome: " + message);
        GameManager.instance.SetIsLoading(false);
        PasswordModal.instance.Show(true);
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
