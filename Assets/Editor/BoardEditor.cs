using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BoardDef))]
public class BoardEditor : Editor
{
    Vector2 scrollPosition = Vector2.zero;  // To handle the scroll position

    public override void OnInspectorGUI()
    {
        // Get a reference to the BoardData target
        BoardDef board = (BoardDef)target;

        // Display the "Initialize Tiles" button first
        if (GUILayout.Button("Initialize Tiles (RESETS BOARD!!!)"))
        {
            board.InitializeTiles();
            EditorUtility.SetDirty(board);  // Mark the object as dirty so changes are saved
        }

        // Show the boardSize property
        board.boardSize = EditorGUILayout.Vector2IntField("Board Size", board.boardSize);

        // Check if tiles are initialized and the size matches
        if (board.tiles != null && board.tiles.Length == board.boardSize.x * board.boardSize.y)
        {
            GUILayout.Label("Tile Data Grid:");

            // Begin the scroll view for the grid
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(1000));  // Set a fixed height for the grid scroll view

            // Set the width of each tile (adjustable)
            float tileWidth = 100f;  // Width for each tile (adjust as needed)
            float totalGridWidth = tileWidth * board.boardSize.x;  // Total width of the grid

            // Begin a horizontal layout to handle scrolling
            GUILayout.BeginHorizontal();

            // Use a GUILayout.Width(totalGridWidth) to ensure that the full grid width is accounted for in the scroll view
            GUILayout.BeginVertical(GUILayout.Width(totalGridWidth));

            // Loop through the grid rows and columns (start y from boardSize.y - 1 to 0)
            for (int y = board.boardSize.y - 1; y >= 0; y--)  // Reversed loop for bottom-left origin
            {
                GUILayout.BeginHorizontal();  // Start a n*w row for the grid
                for (int x = 0; x < board.boardSize.x; x++)
                {
                    int index = x + y * board.boardSize.x;  // Calculate the 1D index for the 2D position
                    // Get the current tile
                    Tile tile = board.tiles[index];
                    
                    // Change the background color based on whether the tile is passable
                    if (!tile.isPassable)
                    {
                        GUI.backgroundColor = Color.black;  // Set background to black if not passable
                        tile.setupPlayer = Player.NONE;  // Automatically set tileSetup to NONE if not passable
                    }
                    else
                    {
                        GUI.backgroundColor = Color.white;  // Set background to default white if passable
                    }

                    GUI.backgroundColor = tile.setupPlayer switch
                    {
                        Player.RED => Color.red,
                        Player.BLUE => Color.blue,
                        _ => GUI.backgroundColor
                    };

                    // Display each tile with the specified width
                    GUILayout.BeginVertical("box", GUILayout.Width(tileWidth));  // Use GUILayout.Width to enforce the tile width

                    // Use GUILayout with fixed width for the internal label and enum dropdown
                    GUILayout.Label($"({x},{y})", GUILayout.Width(90));  // Reduce label width
                    // Set a fixed width for the enum dropdown to prevent tile from expanding
                    GUILayout.Label("Passable", GUILayout.Width(90));  // Adjust the width for the label
                    tile.isPassable = EditorGUILayout.Toggle(tile.isPassable, GUILayout.Width(20));  // Adjust the width for the toggle
                    tile.setupPlayer = (Player)EditorGUILayout.EnumPopup(tile.setupPlayer, GUILayout.Width(tileWidth));
                    
                    GUILayout.EndVertical();  // End the vertical layout for the tile
                }

                GUILayout.EndHorizontal();  // End the current row
            }

            GUILayout.EndVertical();  // End vertical layout for the grid content
            GUILayout.EndHorizontal();  // End horizontal layout for scrolling
            GUILayout.EndScrollView();  // End the scroll view
        }
        else
        {
            GUILayout.Label("Initialize tiles to display the grid.");
        }

        // Mark the object as dirty to ensure changes are saved
        if (GUI.changed)
        {
            EditorUtility.SetDirty(board);
        }
    }
}
