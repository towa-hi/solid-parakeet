using UnityEngine;
using UnityEditor;

[ExecuteAlways]
[RequireComponent(typeof(Grid))]
public class GridVisualizerDebug : MonoBehaviour
{
    public Color gridColor = Color.white;
    public float cellSizeMultiplier = 1f;
    public float circleRadius = 0.1f;

    private Grid grid;
    
    public const float hexSizeFactor = 1.1547f;
    
    private void OnEnable()
    {
        grid = GetComponent<Grid>();
    }

    private void OnDrawGizmos()
    {
        if (grid == null || !this.isActiveAndEnabled)
            return;

        Gizmos.color = gridColor;

        Vector3Int minCell = new Vector3Int(0, 0, 0);
        Vector3Int maxCell = new Vector3Int(10, 10, 0);

        for (int x = minCell.x; x <= maxCell.x; x++)
        {
            for (int y = minCell.y; y <= maxCell.y; y++)
            {
                Vector3 cellCenter = grid.GetCellCenterWorld(new Vector3Int(x, y, 0));
                DrawCircle(cellCenter);
                if (grid.cellLayout == GridLayout.CellLayout.Hexagon)
                {
                    DrawHexagon(cellCenter, x, y);
                }
                else
                {
                    DrawSquare(cellCenter);
                }
            }
        }
    }

    private void DrawCircle(Vector3 center)
    {
        int segments = 32;
        float angleStep = 360f / segments;

        Vector3 previousPoint = center + Swizzle(new Vector3(circleRadius, 0, 0));
        for (int i = 1; i <= segments; i++)
        {
            float angle = Mathf.Deg2Rad * (i * angleStep);
            Vector3 newPoint = center + Swizzle(new Vector3(Mathf.Cos(angle) * circleRadius, Mathf.Sin(angle) * circleRadius, 0));
            Gizmos.DrawLine(previousPoint, newPoint);
            previousPoint = newPoint;
        }
    }

    private void DrawSquare(Vector3 center)
    {
        Vector3 size = grid.cellSize * cellSizeMultiplier;
        Vector3 halfSize = size / 2f;

        Vector3[] vertices = new Vector3[4]
        {
            center + Swizzle(new Vector3(-halfSize.x, -halfSize.y, 0)),
            center + Swizzle(new Vector3(-halfSize.x,  halfSize.y, 0)),
            center + Swizzle(new Vector3( halfSize.x,  halfSize.y, 0)),
            center + Swizzle(new Vector3( halfSize.x, -halfSize.y, 0)),
        };

        for (int i = 0; i < 4; i++)
        {
            Gizmos.DrawLine(vertices[i], vertices[(i + 1) % 4]);
        }
    }

    private void DrawHexagon(Vector3 center, int gridX, int gridY)
    {
        // Unity's hex grid uses pointy-top hexagons
        float size = grid.cellSize.x * cellSizeMultiplier * hexSizeFactor;
        
        // For pointy-top hexagons:
        // width = 2 * size * 0.5
        // height = 2 * size * 0.5773502692f (approximately sqrt(3)/3)
        float width = size;
        float height = size * 0.8660254038f; // sqrt(3)/2

        Vector3[] vertices = new Vector3[6];
        for (int i = 0; i < 6; i++)
        {
            // Start at 30 degrees for pointy-top orientation
            float angle = Mathf.Deg2Rad * (60 * i + 30);
            vertices[i] = center + Swizzle(new Vector3(
                width * 0.5f * Mathf.Cos(angle),
                height * 0.5f * Mathf.Sin(angle),
                0));
        }

        for (int i = 0; i < 6; i++)
        {
            Gizmos.DrawLine(vertices[i], vertices[(i + 1) % 6]);
        }
    }

    private Vector3 Swizzle(Vector3 position)
    {
        switch (grid.cellSwizzle)
        {
            case Grid.CellSwizzle.XYZ: return position;
            case Grid.CellSwizzle.XZY: return new Vector3(position.x, position.z, position.y);
            case Grid.CellSwizzle.YXZ: return new Vector3(position.y, position.x, position.z);
            case Grid.CellSwizzle.YZX: return new Vector3(position.y, position.z, position.x);
            case Grid.CellSwizzle.ZXY: return new Vector3(position.z, position.x, position.y);
            case Grid.CellSwizzle.ZYX: return new Vector3(position.z, position.y, position.x);
            default: return position;
        }
    }
}