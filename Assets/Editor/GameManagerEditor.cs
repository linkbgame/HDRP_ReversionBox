#if UNITY_EDITOR
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

[CustomEditor(typeof(GameManager))]
public class GameManagerEditor : Editor
{
    private static readonly Regex LvNameRegex = new Regex(@"^Lv\s*(\d+)$", RegexOptions.IgnoreCase);

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8f);
        if (GUILayout.Button("Auto Bind Lv Buttons (Current Scene)"))
        {
            AutoBindLevelButtons((GameManager)target);
        }

        if (GUILayout.Button("Bind Menu Toggle Button"))
        {
            BindMenuToggleButton((GameManager)target);
        }

        if (GUILayout.Button("Bind Quit Button"))
        {
            BindQuitButton((GameManager)target);
        }

        if (GUILayout.Button("Bind All Common Buttons"))
        {
            GameManager gm = (GameManager)target;
            BindMenuToggleButton(gm);
            BindQuitButton(gm);
        }
    }

    [MenuItem("Tools/GameManager/Auto Bind Lv Buttons In Active Scene")]
    private static void AutoBindLvButtonsMenu()
    {
        GameManager gm = Object.FindObjectOfType<GameManager>();
        if (gm == null)
        {
            Debug.LogWarning("Scene has no GameManager.");
            return;
        }

        AutoBindLevelButtons(gm);
    }

    private static void AutoBindLevelButtons(GameManager gm)
    {
        Button[] buttons = Object.FindObjectsOfType<Button>(true);
        int bindCount = 0;

        foreach (Button btn in buttons)
        {
            Match m = LvNameRegex.Match(btn.name.Trim());
            if (!m.Success) continue;

            int levelNumber = int.Parse(m.Groups[1].Value);
            int buildIndex = levelNumber - 1;

            Undo.RecordObject(btn, "Auto Bind Lv Button");
            ClearAllClickListeners(btn);
            UnityEventTools.AddIntPersistentListener(btn.onClick, gm.LoadLevelByIndex, buildIndex);

            EditorUtility.SetDirty(btn);
            bindCount++;
        }

        EditorUtility.SetDirty(gm);
        Debug.Log($"Auto bind complete: {bindCount} buttons.");
    }
    /// <summary>
    /// 绑定菜单按钮
    /// </summary>
    private static void BindMenuToggleButton(GameManager gm)
    {
        string menuToggleName = GetSerializedString(gm, "menuToggleName");
        Button targetBtn = FindSceneButtonByName(menuToggleName);

        if (targetBtn == null)
        {
            Debug.LogWarning($"Menu toggle button not found: {menuToggleName}");
            return;
        }

        Undo.RecordObject(targetBtn, "Bind Menu Toggle Button");
        ClearAllClickListeners(targetBtn);
        UnityEventTools.AddPersistentListener(targetBtn.onClick, gm.ToggleMenu);

        EditorUtility.SetDirty(targetBtn);
        EditorUtility.SetDirty(gm);
        Debug.Log($"Bound menu toggle button: {targetBtn.name}");
    }

    private static void BindQuitButton(GameManager gm)
    {
        string quitButtonName = GetSerializedString(gm, "quitButtonName");
        Button targetBtn = FindSceneButtonByName(quitButtonName);

        if (targetBtn == null)
        {
            Debug.LogWarning($"Quit button not found: {quitButtonName}");
            return;
        }

        Undo.RecordObject(targetBtn, "Bind Quit Button");
        ClearAllClickListeners(targetBtn);
        UnityEventTools.AddPersistentListener(targetBtn.onClick, gm.QuitGame);

        EditorUtility.SetDirty(targetBtn);
        EditorUtility.SetDirty(gm);
        Debug.Log($"Bound quit button: {targetBtn.name}");
    }

    private static Button FindSceneButtonByName(string targetName)
    {
        Button[] buttons = Resources.FindObjectsOfTypeAll<Button>();
        foreach (Button btn in buttons)
        {
            if (!btn.gameObject.scene.IsValid()) continue;
            if (btn.gameObject.name != targetName) continue;
            return btn;
        }
        return null;
    }

    private static string GetSerializedString(GameManager gm, string propertyName)
    {
        SerializedObject so = new SerializedObject(gm);
        SerializedProperty p = so.FindProperty(propertyName);
        return p != null ? p.stringValue : string.Empty;
    }

    private static void ClearAllClickListeners(Button button)
    {
        button.onClick.RemoveAllListeners();

        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            UnityEventTools.RemovePersistentListener(button.onClick, i);
        }
    }
}
#endif
