using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(BoardData))]
public class BoardDataEditor : Editor
{
    // Scroll position to handle scrolling in the editor
    Vector2 scrollPosition = Vector2.zero;

    public override void OnInspectorGUI()
    {
        // Get a reference to the BoardData target
        BoardData boardData = (BoardData)target;

        // Display the "Initialize Tiles" button first
        if (GUILayout.Button("Initialize Tiles"))
        {
            boardData.InitializeTiles();
            EditorUtility.SetDirty(boardData);  // Mark the object as dirty so changes are saved
        }

        // Show the boardSize property
        boardData.boardSize = EditorGUILayout.Vector2IntField("Board Size", boardData.boardSize);

        // Check if tiles are initialized and the size matches
        if (boardData.tiles != null && boardData.tiles.Length == boardData.boardSize.x * boardData.boardSize.y)
        {
            GUILayout.Label("Tile Data Grid:");

            // Begin the scroll view for the grid
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(400));  // Set a fixed height for the grid scroll view

            // Calculate the width of the grid based on the number of tiles
            float tileWidth = 10f;  // Width for each tile
            float totalGridWidth = tileWidth * boardData.boardSize.x;  // Total width of the grid

            // Begin horizontal layout for the grid with the calculated width (forces horizontal scrolling)
            GUILayout.BeginHorizontal(GUILayout.Width(totalGridWidth));

            GUILayout.BeginVertical();  // Ensure the grid scrolls vertically as well

            for (int y = 0; y < boardData.boardSize.y; y++)
            {
                GUILayout.BeginHorizontal();  // Start a new row for the grid

                for (int x = 0; x < boardData.boardSize.x; x++)
                {
                    int index = x + y * boardData.boardSize.x;  // Calculate the 1D index for the 2D position

                    // Get the current tile
                    TileData tile = boardData.tiles[index];

                    // Display a small editable box for each tile
                    GUILayout.BeginVertical("box", GUILayout.Width(tileWidth));  // Define the box for each tile

                    // Show a label for tile position
                    GUILayout.Label($"Tile ({x}, {y})");

                    // Editable fields for each tile's properties (isPassable, tileName)
                    tile.isPassable = EditorGUILayout.Toggle("Passable", tile.isPassable);
                    //tile.tileName = EditorGUILayout.TextField("Name", tile.tileName);

                    GUILayout.EndVertical();  // End vertical box for the tile
                }

                GUILayout.EndHorizontal();  // End the current row
            }

            GUILayout.EndVertical();  // End vertical layout for the grid
            GUILayout.EndHorizontal();  // End horizontal layout with totalGridWidth

            GUILayout.EndScrollView();  // End the scroll view
        }
        else
        {
            GUILayout.Label("Initialize tiles to display the grid.");
        }

        // Mark the object as dirty to ensure changes are saved
        if (GUI.changed)
        {
            EditorUtility.SetDirty(boardData);
        }
    }
}