using UnityEngine;

public class PlayerProfile
{
    public bool isHost;
    public Player player;
    
    public PlayerProfile(bool inIsHost, Player inPlayer)
    {
        isHost = inIsHost;
        player = inPlayer;
    }
    
}
