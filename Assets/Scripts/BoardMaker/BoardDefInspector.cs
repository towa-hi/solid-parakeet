using System.Linq;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(BoardDef))]
public class BoardDefInspector : Editor
{
    public override void OnInspectorGUI()
    {
        BoardDef boardDef = (BoardDef)target;
        if (GUILayout.Button("Initialize"))
        {
            EditorInitialize(boardDef);
            EditorUtility.SetDirty(boardDef);
        }
        
        base.OnInspectorGUI();
        
        int maxPawnSum = boardDef.maxPawns.Sum(maxPawns => maxPawns.max);
        EditorGUILayout.LabelField($"maxPawnSum: {maxPawnSum}");
        int redSetupTiles = boardDef.tiles.Count(tile => tile.setupTeam == Team.RED);
        EditorGUILayout.LabelField($"redSetupTiles: {redSetupTiles}");
        int blueSetupTiles = boardDef.tiles.Count(tile => tile.setupTeam == Team.BLUE);
        EditorGUILayout.LabelField($"blueSetupTiles: {blueSetupTiles}");
        
        if (GUILayout.Button("Init Max Pawns"))
        {
            boardDef.maxPawns = EditorInitializeMaxPawns();
            EditorUtility.SetDirty(boardDef);
        }
    }
    
    static void EditorInitialize(BoardDef boardDef)
    {
        boardDef.boardSize = new Vector2Int(10, 10);
        boardDef.tiles = new Tile[boardDef.boardSize.x * boardDef.boardSize.y];
        int index = 0;
        for (int y = 0; y < boardDef.boardSize.y; y++)
        {
            for (int x = 0; x < boardDef.boardSize.x; x++)
            {
                Vector2Int currentPos = new(x, y);
                Tile tile = new()
                {
                    pos = currentPos,
                    isPassable = true,
                    setupTeam = 0,
                    autoSetupZone = 0,
                };
                boardDef.tiles[index] = tile;
                index++;
            }
        }
    }
    
    static SMaxPawnsPerRank[] EditorInitializeMaxPawns()
    {
        return new[]
        {
            new SMaxPawnsPerRank(Rank.THRONE, 1),
            new SMaxPawnsPerRank(Rank.ASSASSIN, 1),
            new SMaxPawnsPerRank(Rank.SCOUT, 8),
            new SMaxPawnsPerRank(Rank.SEER, 5),
            new SMaxPawnsPerRank(Rank.GRUNT, 4),
            new SMaxPawnsPerRank(Rank.KNIGHT, 4),
            new SMaxPawnsPerRank(Rank.WRAITH, 4),
            new SMaxPawnsPerRank(Rank.REAVER, 3),
            new SMaxPawnsPerRank(Rank.HERALD, 2),
            new SMaxPawnsPerRank(Rank.CHAMPION, 1),
            new SMaxPawnsPerRank(Rank.WARLORD, 1),
            new SMaxPawnsPerRank(Rank.TRAP, 6),
            new SMaxPawnsPerRank(Rank.UNKNOWN, 0),
        };
    }
}
