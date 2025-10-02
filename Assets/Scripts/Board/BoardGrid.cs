using System;
using Contract;
using UnityEngine;
#pragma warning disable
[ExecuteAlways]
public class BoardGrid : MonoBehaviour
{
    public bool showGizmos;
    public bool showLabels;
    bool isInitialized;
    //public SBoardDef boardDef;
    public float cellSize;
    public Vector2Int markerPos;
    public bool isHex;
    public Team team;
    // Constants for hex grid calculations
    public float HEX_INNER_RADIUS_MULTIPLIER = 0.866025404f; // √3/2

    public void SetBoard(bool isHex, Team team)
    {
        this.isHex = isHex;
        isInitialized = true;
        this.team = team;
    }

    public void ClearBoard()
    {
        isInitialized = false;
    }
    
    // void OnDrawGizmos()
    // {
    //     if (!isInitialized) return;
    //     if (!showGizmos) return;
    //     Gizmos.color = Color.white;
    //     
    //     // Draw all cells in the grid
    //     for (int x = 0; x < boardDef.boardSize.x; x++)
    //     {
    //         for (int y = 0; y < boardDef.boardSize.y; y++)
    //         {
    //             Vector2Int pos = new Vector2Int(x, y);
    //             Vector3 worldPos = CellToWorld(pos);
    //             DrawCircle(worldPos);
    //             if (boardDef.isHex)
    //             {
    //                 DrawHexGizmo(worldPos);
    //             }
    //             else
    //             {
    //                 DrawSquareGizmo(worldPos);
    //             }
    //         }
    //     }
    //     DrawMarkerCircle(CellToWorld(markerPos), Color.red);
    //     Vector2Int[] neighbors = Shared.GetNeighbors(markerPos, boardDef.isHex);
    //     DrawMarkerCircle(CellToWorld(neighbors[0]), Color.red);
    //     DrawMarkerCircle(CellToWorld(neighbors[1]), Color.magenta);
    //     DrawMarkerCircle(CellToWorld(neighbors[2]), Color.yellow);
    //     DrawMarkerCircle(CellToWorld(neighbors[3]), Color.green);
    //     if (boardDef.isHex)
    //     {
    //         DrawMarkerCircle(CellToWorld(neighbors[4]), Color.blue);
    //         DrawMarkerCircle(CellToWorld(neighbors[5]), Color.cyan);
    //     }
    // }
    
    public Vector3 offset = Vector3.zero;
    public int fontSize = 11;
    public float dotSizePx = 10;
    public Color color = Color.white;
    public Texture2D texture;
    
    GUIStyle style = new GUIStyle();

    // void OnGUI()
    // {
    //     if (!isInitialized) return;
    //     if (!showLabels) return;
    //     for (int x = 0; x < boardDef.boardSize.x; x++)
    //     {
    //         for (int y = 0; y < boardDef.boardSize.y; y++)
    //         {
    //             Vector2Int pos = new Vector2Int(x, y);
    //             Vector3 position = CellToWorld(pos);
    //             string message = $"{pos}";
    //             DrawText(position, message);
    //         }
    //     }
    // }
    void DrawText(Vector3 position, string message)
    {
        Vector3 screenPos = Camera.main.WorldToScreenPoint(position + offset);
        if (screenPos.z > 0)
        {
            screenPos.y = Screen.height - screenPos.y;
            style.fontSize = fontSize;
            style.normal.textColor = color;
            GUI.Label(new Rect(screenPos.x, screenPos.y, 200, 50), message);
            GUI.DrawTexture(new Rect(screenPos.x - dotSizePx / 2, screenPos.y - dotSizePx / 2,dotSizePx,dotSizePx), texture);
        }
    }
    
    // Convert grid coordinates to world position
    public Vector3 CellToWorld(Vector2Int pos)
    {
        // Adjust position by origin offset
        Vector2Int p = (team == Team.BLUE)
        ? new Vector2Int(9 - pos.x, 9 - pos.y)
        : pos;
        if (isHex)
        {
            
            float x = p.x * (cellSize * HEX_INNER_RADIUS_MULTIPLIER);
            float z = p.y * cellSize;
            if ((p.x & 1) != 0) z -= cellSize * 0.5f;
            return new Vector3(x, 0f, z) + transform.position;
        }
        else
        {
            return new Vector3(p.x * cellSize, 0f, p.y * cellSize) + transform.position;
        }
    }
    
    // The rest of the methods remain the same
    void DrawHexGizmo(Vector3 center)
    {
        Vector3[] vertices = GetHexVertices(center);
        
        for (int i = 0; i < 6; i++)
        {
            Gizmos.DrawLine(vertices[i], vertices[(i + 1) % 6]);
        }
    }

    public float hexSizeFactor;
    
    Vector3[] GetHexVertices(Vector3 center)
    {
        Vector3[] vertices = new Vector3[6];
        float innerRadius = cellSize * HEX_INNER_RADIUS_MULTIPLIER * hexSizeFactor;
        
        for (int i = 0; i < 6; i++)
        {
            float angle = i * 60f * Mathf.Deg2Rad;
            vertices[i] = center + new Vector3(
                cellSize * Mathf.Cos(angle) * hexSizeFactor,
                0,
                innerRadius * Mathf.Sin(angle)
            );
        }
        
        return vertices;
    }

    void DrawSquareGizmo(Vector3 center)
    {
        Vector3 halfSize = new Vector3(cellSize * 0.5f, 0, cellSize * 0.5f);
        
        Vector3[] corners = new Vector3[]
        {
            center + new Vector3(-halfSize.x, 0, -halfSize.z),
            center + new Vector3(halfSize.x, 0, -halfSize.z),
            center + new Vector3(halfSize.x, 0, halfSize.z),
            center + new Vector3(-halfSize.x, 0, halfSize.z)
        };

        for (int i = 0; i < 4; i++)
        {
            Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);
        }
    }

    public float circleRadius = 0.1f;
    
    void DrawCircle(Vector3 center)
    {
        Gizmos.color = Color.white;
        int segments = 32;
        float angleStep = 360f / segments;

        Vector3 previousPoint = center + new Vector3(circleRadius, 0, 0);
        for (int i = 1; i <= segments; i++)
        {
            float angle = Mathf.Deg2Rad * (i * angleStep);
            Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * circleRadius, 0, Mathf.Sin(angle) * circleRadius);
            Gizmos.DrawLine(previousPoint, newPoint);
            previousPoint = newPoint;
        }
    }
    
    void DrawMarkerCircle(Vector3 center, Color markerColor)
    {
        Gizmos.color = markerColor;
        int segments = 32;
        float angleStep = 360f / segments;
        float markerRadius = circleRadius * 1.1f;
        Vector3 previousPoint = center + new Vector3(markerRadius, 0, 0);
        for (int i = 1; i <= segments; i++)
        {
            float angle = Mathf.Deg2Rad * (i * angleStep);
            Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * markerRadius, 0, Mathf.Sin(angle) * markerRadius);
            Gizmos.DrawLine(previousPoint, newPoint);
            previousPoint = newPoint;
        }
    }
}
#pragma warning restore