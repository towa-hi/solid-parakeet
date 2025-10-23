using UnityEngine;

public class ArenaTiles : MonoBehaviour
{
    public TileModel tileL;
    public TileModel tileR;

    public bool show;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    public void SetShow(bool inShow)
    {
        Debug.Log($"ArenaTiles: SetShow {inShow}");
        show = inShow;
        gameObject.SetActive(show);
    }
}
