using System.Collections.Generic;
using UnityEngine;

public class GuiPawnSetupList : MonoBehaviour
{
    public List<GuiPawnSetupListEntry> entries = new();
    public GameObject body;
    public GameObject entryPrefab;

    void Start()
    {
        Initialize();
    }
    
    public void Initialize()
    {
        // TODO: replace this with something external
        SetupParameters fakeSetupParameters = new SetupParameters();
        foreach (var kvp in fakeSetupParameters.maxPawnsDictionary)
        {
            GameObject entryObject = Instantiate(entryPrefab, body.transform);
            GuiPawnSetupListEntry entry = entryObject.GetComponent<GuiPawnSetupListEntry>();
            entry.SetPawn(kvp.Key, kvp.Value);
        }
    }
}
