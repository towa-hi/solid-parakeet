using UnityEngine;

public class Card : MonoBehaviour
{
    public CardSorting sorting;

    public void Initialize(int index)
    {
        sorting.AssignUniqueStencilRef();
    }
}
