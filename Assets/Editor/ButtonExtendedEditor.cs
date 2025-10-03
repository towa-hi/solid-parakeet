using UnityEditor;
using UnityEditor.UI;

[CustomEditor(typeof(ButtonExtended), true)]
public class ButtonExtendedEditor : ButtonEditor
{
	SerializedProperty disabledTextColor;
	SerializedProperty text;
	SerializedProperty frame;
	SerializedProperty textAndFrameColor;
	SerializedProperty buttonClickType;
	protected override void OnEnable()
	{
		base.OnEnable();
		disabledTextColor = serializedObject.FindProperty("disabledTextColor");
		text = serializedObject.FindProperty("text");
		frame = serializedObject.FindProperty("frame");
		textAndFrameColor = serializedObject.FindProperty("textAndFrameColor");
		buttonClickType = serializedObject.FindProperty("buttonClickType");
	}

	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();
		serializedObject.Update();
		EditorGUILayout.PropertyField(text);
		EditorGUILayout.PropertyField(frame);
		EditorGUILayout.PropertyField(textAndFrameColor);
		EditorGUILayout.PropertyField(disabledTextColor);
		EditorGUILayout.PropertyField(buttonClickType);
		serializedObject.ApplyModifiedProperties();
	}
}
