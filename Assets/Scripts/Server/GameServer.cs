using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class GameServer
{
    public static GameServer instance;

    public event Action<GameState> GameStarted;
    public List<GameInstance> games;
    public GameServer()
    {
        instance = this;
    }
    
    public async Task ReqStartGameAsync(BoardDef boardDef, PlayerProfile inHostProfile, PlayerProfile inGuestProfile)
    {
        await Task.Delay(1000);
        GameInstance newGame = new GameInstance(boardDef, inHostProfile, inGuestProfile);
        games.Add(newGame);
        SendGameStarted(newGame);
    }

    void SendGameStarted(GameInstance game)
    {
        GameStarted?.Invoke(game.gameState);
    }
    
    public void CmdEndGame()
    {
        
    }

    public void CmdReceivePawnPlacement()
    {
        
    }
}
