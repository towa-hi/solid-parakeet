using System;
using System.Threading.Tasks;
using UnityEngine;

public class GameServer
{
    public static GameServer instance;

    public GameState gameState;

    public PlayerProfile hostProfile;
    public PlayerProfile guestProfile;

    public event Action<GameState> GameStarted;
    public GameServer()
    {
        instance = this;
    }
    
    public async Task ReqStartGameAsync(BoardDef boardDef, PlayerProfile inHostProfile, PlayerProfile inGuestProfile)
    {
        await Task.Delay(5000);
        gameState = new GameState(boardDef);
        hostProfile = inHostProfile;
        guestProfile = inGuestProfile;
        SendGameStarted();
    }

    void SendGameStarted()
    {
        GameStarted?.Invoke(gameState);
    }
    
    public void CmdEndGame()
    {
        
    }

    public void CmdReceivePawnPlacement()
    {
        
    }
}
