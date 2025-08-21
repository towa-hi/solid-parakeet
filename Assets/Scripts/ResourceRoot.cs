using System.Collections.Generic;
using System.Linq;
using UnityEngine;
[CreateAssetMenu(fileName = "ResourceRoot", menuName = "Scriptable Objects/ResourceRoot")]

public class ResourceRoot : ScriptableObject
{
    static ResourceRoot instance;

    static ResourceRoot Instance
    {
        get
        {
            if (instance == null)
            {
                // Try to load an asset named "ResourceRoot" placed under any Resources/ folder
                instance = Resources.Load<ResourceRoot>("ResourceRoot");
                if (instance == null)
                {
                    ResourceRoot[] all = Resources.LoadAll<ResourceRoot>(string.Empty);
                    if (all is { Length: > 0 })
                    {
                        instance = all[0];
                    }
                }
            }
            return instance;
        }
    }

    // Static convenience accessors
    public static DefaultSettings DefaultSettings => Instance != null ? Instance.defaultSettings : null;
    public static List<PawnDef> OrderedPawnDefs => Instance != null ? Instance.orderedPawnDefs : null;
    public static List<BoardDef> BoardDefs => Instance != null ? Instance.boardDefs : null;

    public static PawnDef GetPawnDefFromRank(Rank? maybeRank)
    {
        Rank rank = Rank.UNKNOWN;
        if (maybeRank.HasValue)
        {
            rank = maybeRank.Value;
        }
        Debug.Log($"is rank null {rank}");
        Debug.Log($"OrderedPawnDefs length {OrderedPawnDefs.Count}");
        return OrderedPawnDefs.FirstOrDefault(def => def.rank == rank);
    }
    
    public DefaultSettings defaultSettings;
    public List<PawnDef> orderedPawnDefs;
    public List<BoardDef> boardDefs;
}
