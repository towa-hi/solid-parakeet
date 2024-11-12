using System.Collections.Generic;
using UnityEngine;

public class RadialMenu : MonoBehaviour
{ 
    public GameObject radialEntry;
    public RectTransform parent;

    public float radius = 100f;

    List<string> menuOptions = new() {"None"};
    void Start()
    {
        List<KeyValuePair<PawnDef, int>> pawnList = Globals.GetOrderedPawnList();
        foreach (var kvp in pawnList)
        {
            menuOptions.Add(kvp.Key.pawnName);
        }
        Debug.Log(menuOptions);
        int totalOptions = menuOptions.Count;
        float angleStep = 360f / totalOptions;
        float startAngle = 90f;
        for (int i = 0; i < totalOptions; i++)
        {
            float angle = startAngle + (i * angleStep);
            float angleRad = angle * Mathf.Deg2Rad;
            // Calculate position based on radius and angle
            Vector2 pos = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)) * radius;

            // Instantiate the menu item
            GameObject menuItem = Instantiate(radialEntry, parent);

            // Set the position
            RectTransform rt = menuItem.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = pos;
            }

            RadialMenuEntry entry = menuItem.GetComponent<RadialMenuEntry>();
            if (menuOptions[i] == "None")
            {
                entry.Initialize(null);
            }
            else
            {
                entry.Initialize(pawnList[i - 1].Key);
            }
        }
    }
    

}
