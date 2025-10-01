using UnityEngine;

[ExecuteAlways]
public class GridArranger : MonoBehaviour
{
    public Grid grid;

    public Vector2Int gridSize;

    void OnEnable() => Arrange();
    void OnTransformChildrenChanged() => Arrange();
    void OnValidate() => Arrange();

    public void Arrange()
    {
        if (grid == null)
        {
            return;
        }
        if (gridSize.x == 0 || gridSize.y == 0)
        {
            return;
        }
        foreach (Transform child in transform)
        {
            if (!child.gameObject.activeInHierarchy)
            {
                continue;
            }
            Vector2Int pos = new Vector2Int(child.GetSiblingIndex() % gridSize.x, child.GetSiblingIndex() / gridSize.x);
            child.position = grid.GetCellCenterWorld(new Vector3Int(pos.x, pos.y, 0));
        }
    }
    
}
