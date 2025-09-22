using UnityEditor;
using UnityEditor.Build;

public static class UseGameStoreDefine
{
    const string Define = "USE_GAME_STORE";

    [MenuItem("Tools/Game Store/Enable USE_GAME_STORE")] 
    public static void EnableDefine()
    {
        ToggleDefine(true);
    }

    [MenuItem("Tools/Game Store/Disable USE_GAME_STORE")] 
    public static void DisableDefine()
    {
        ToggleDefine(false);
    }

    static void ToggleDefine(bool enable)
    {
        BuildTargetGroup group = EditorUserBuildSettings.selectedBuildTargetGroup;
        var named = NamedBuildTarget.FromBuildTargetGroup(group);
        string defines = PlayerSettings.GetScriptingDefineSymbols(named);
        var set = new System.Collections.Generic.HashSet<string>((defines ?? string.Empty).Split(';'));
        if (enable)
        {
            set.Add(Define);
        }
        else
        {
            set.Remove(Define);
        }
        string result = string.Join(";", set);
        PlayerSettings.SetScriptingDefineSymbols(named, result);
        AssetDatabase.SaveAssets();
    }
}


