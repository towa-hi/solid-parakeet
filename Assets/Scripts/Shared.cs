using System;
using System.Text.RegularExpressions;
using UnityEngine;

public static class Shared
{
    public static bool IsNicknameValid(string nickname)
    {
        // Check length constraints
        if (string.IsNullOrEmpty(nickname) || nickname.Length >= 16)
        {
            return false;
        }
        // Check for alphanumeric characters and spaces only
        return Regex.IsMatch(nickname, @"^[a-zA-Z0-9 ]+$");
    }
    
    public static bool IsPasswordValid(string password)
    {
        return true;
    }

    public static float EaseOutQuad(float t)
    {
        return t * (2 - t);
    }
    public static float EaseInOutQuad(float t) {
        return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
    }
    public static string ShortGuid(Guid guid)
    {
        return guid.ToString().Substring(0, 4);
    }
    
    public static UnityEngine.Vector2Int[] GetDirections(UnityEngine.Vector2Int pos, bool isHex)
    {
        if (isHex)
        {
            UnityEngine.Vector2Int[] neighbors = new UnityEngine.Vector2Int[6];
            bool oddCol = pos.x % 2 == 1; // Adjust for origin offset
            
            if (oddCol)
            {
                neighbors[0] = new UnityEngine.Vector2Int(0, 1);  // top
                neighbors[1] = new UnityEngine.Vector2Int(-1, 0);  // top right
                neighbors[2] = new UnityEngine.Vector2Int(-1, -1);  // bot right
                neighbors[3] = new UnityEngine.Vector2Int(0, -1); // bot
                neighbors[4] = new UnityEngine.Vector2Int(1, -1); // bot left
                neighbors[5] = new UnityEngine.Vector2Int(1, 0);  // top left
            }
            else
            {
                neighbors[0] = new UnityEngine.Vector2Int(0, 1);  // top
                neighbors[1] = new UnityEngine.Vector2Int(-1, 1);  // top right
                neighbors[2] = new UnityEngine.Vector2Int(-1, -0); // bot right
                neighbors[3] = new UnityEngine.Vector2Int(0, -1); // bot
                neighbors[4] = new UnityEngine.Vector2Int(1, 0);// bot left
                neighbors[5] = new UnityEngine.Vector2Int(1, 1); // top left
            }
            
            return neighbors;
        }
        else
        {
            return new UnityEngine.Vector2Int[]
            {
                UnityEngine.Vector2Int.up,
                UnityEngine.Vector2Int.right,
                UnityEngine.Vector2Int.down,
                UnityEngine.Vector2Int.left
            };
        }
    }
    
    public static UnityEngine.Vector2Int[] GetNeighbors(UnityEngine.Vector2Int pos, bool isHex)
    {
        UnityEngine.Vector2Int[] directions = GetDirections(pos, isHex);
        if (isHex)
        {
            UnityEngine.Vector2Int[] neighbors = new UnityEngine.Vector2Int[6];
            for (int i = 0; i < neighbors.Length; i++)
            {
                neighbors[i] = pos + directions[i];
            }
            return neighbors;
        }
        else
        {
            UnityEngine.Vector2Int[] neighbors = new UnityEngine.Vector2Int[4];
            for (int i = 0; i < neighbors.Length; i++)
            {
                neighbors[i] = pos + directions[i];
            }
            return neighbors;
        }
    }
    
    public static int OppTeam(int team)
    {
        return team switch
        {
            1 => 2,
            2 => 1,
            _ => team
        };
    }
}
