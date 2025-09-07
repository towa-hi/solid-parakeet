using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(CardHand))]
[CanEditMultipleObjects]
public class CardHandEditor : Editor
{
    int removeIndex;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();

        CardHand hand = (CardHand)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Testing", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Card"))
        {
            foreach (Object o in targets)
            {
                CardHand h = (CardHand)o;
                h.AddCard();
                EditorUtility.SetDirty(h);
            }
        }
        if (GUILayout.Button("Add 5 Cards"))
        {
            foreach (Object o in targets)
            {
                CardHand h = (CardHand)o;
                h.AddCards(5);
                EditorUtility.SetDirty(h);
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Remove Selected Card"))
        {
            foreach (Object o in targets)
            {
                CardHand h = (CardHand)o;
                h.RemoveCard(h.selectedCard);
                EditorUtility.SetDirty(h);
            }
        }
        removeIndex = EditorGUILayout.IntField("Index", removeIndex);
        if (GUILayout.Button("Remove At Index"))
        {
            foreach (Object o in targets)
            {
                CardHand h = (CardHand)o;
                h.RemoveAt(removeIndex);
                EditorUtility.SetDirty(h);
            }
        }
        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();
    }
}


