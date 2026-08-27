using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SceneUIManager))]
public sealed class SceneUIManagerEditor : Editor
{
    private SerializedProperty script;
    private SerializedProperty settingsUI;
    private SerializedProperty managedUIObjects;
    private SerializedProperty managedUIIds;
    private SerializedProperty allowMultipleManagedUIs;
    private SerializedProperty keepCursorVisibleInScene;
    private SerializedProperty fallbackClosedCursorLockMode;
    private SerializedProperty fallbackClosedCursorVisible;
    private SerializedProperty notifyInputManager;
    private SerializedProperty playerTag;
    private SerializedProperty playerLayerName;
    private SerializedProperty settingsApplyFadeDuration;
    private SerializedProperty placementModeUIRoot;
    private SerializedProperty placementCancelGridManager;


    private void OnEnable()
    {
        script = serializedObject.FindProperty("m_Script");
        settingsUI = serializedObject.FindProperty("settingsUI");
        managedUIObjects = serializedObject.FindProperty("managedUIObjects");
        managedUIIds = serializedObject.FindProperty("managedUIIds");
        allowMultipleManagedUIs = serializedObject.FindProperty("allowMultipleManagedUIs");
        keepCursorVisibleInScene =
            serializedObject.FindProperty("keepCursorVisibleInScene");
        fallbackClosedCursorLockMode =
            serializedObject.FindProperty("fallbackClosedCursorLockMode");
        fallbackClosedCursorVisible =
            serializedObject.FindProperty("fallbackClosedCursorVisible");
        notifyInputManager = serializedObject.FindProperty("notifyInputManager");
        playerTag = serializedObject.FindProperty("playerTag");
        playerLayerName = serializedObject.FindProperty("playerLayerName");
        settingsApplyFadeDuration =
            serializedObject.FindProperty("settingsApplyFadeDuration");
        placementModeUIRoot = serializedObject.FindProperty("placementModeUIRoot");
        placementCancelGridManager =
            serializedObject.FindProperty("placementCancelGridManager");

    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(script);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("UI References", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            settingsUI,
            new GUIContent("Settings UI", "게임 시작 시 자동으로 닫히는 설정 UI 루트입니다."));
        EditorGUILayout.PropertyField(
            allowMultipleManagedUIs,
            new GUIContent(
                "Allow Multiple Managed UIs",
                "체크하면 여러 UI를 동시에 열 수 있고, 해제하면 마지막으로 연 UI만 유지합니다."));
        EditorGUILayout.PropertyField(
            keepCursorVisibleInScene,
            new GUIContent(
                "Keep Cursor Visible In Scene",
                "체크하면 Managed UI가 모두 닫힌 상태에서도 이 씬의 커서를 계속 표시합니다."));

        if (!allowMultipleManagedUIs.boolValue)
        {
            EditorGUILayout.HelpBox(
                "동시 열림 제한이 활성화되었습니다. 새 UI가 열리면 기존 Managed UI는 자동으로 닫힙니다.",
                MessageType.Info);
        }

        if (keepCursorVisibleInScene.boolValue)
        {
            EditorGUILayout.HelpBox(
                "이 씬에서는 Managed UI가 모두 닫혀도 커서가 잠기거나 숨겨지지 않습니다.",
                MessageType.Info);
        }

        DrawManagedUIList();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Closed Cursor Fallback", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            fallbackClosedCursorLockMode,
            new GUIContent("Fallback Closed Cursor Lock Mode"));
        EditorGUILayout.PropertyField(
            fallbackClosedCursorVisible,
            new GUIContent("Fallback Closed Cursor Visible"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Player Input", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(notifyInputManager);
        EditorGUILayout.PropertyField(playerTag);
        EditorGUILayout.PropertyField(playerLayerName);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Settings Apply Fade", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            settingsApplyFadeDuration,
            new GUIContent(
                "Settings Apply Fade Duration",
                "적용 후 환경설정 하위 창이 완전히 닫힐 때까지의 시간입니다."));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("배치 모드 연동 (HDY 요청)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            placementModeUIRoot,
            new GUIContent(
                "Placement Mode UI Root",
                "여기 등록한 오브젝트를 닫을 때는 SetActive(false) 대신 GridManager.ChangePlacementMode()를 호출합니다. 배치 모드 UI가 없으면 비워두세요."));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(
            "배치 모드 ESC 취소 연동 (HDY 요청 - PanelManager 흡수)",
            EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            placementCancelGridManager,
            new GUIContent(
                "Placement Cancel Grid Manager",
                "(멤) ESC를 눌렀을 때 열려 있는 Managed UI가 없고 이 GridManager가 배치 모드 중이면 CancelPlacement()를 호출합니다. 배치 모드 개념이 없는 씬이면 비워두세요."));


        serializedObject.ApplyModifiedProperties();
    }

    private void DrawManagedUIList()
    {
        SynchronizeManagedUIArrays();

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(
            $"Managed UI Objects ({managedUIObjects.arraySize})",
            EditorStyles.boldLabel);

        if (managedUIObjects.arraySize == 0)
        {
            EditorGUILayout.HelpBox(
                "ESC 및 플레이어 입력 상태를 관리할 UI 루트를 추가하세요.",
                MessageType.None);
        }

        int removeIndex = -1;

        for (int i = 0; i < managedUIObjects.arraySize; i++)
        {
            SerializedProperty element = managedUIObjects.GetArrayElementAtIndex(i);
            SerializedProperty idElement = managedUIIds.GetArrayElementAtIndex(i);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(
                idElement,
                new GUIContent("ID", "씬이 바뀌어도 동일하게 사용할 고유 ID입니다."));

            if (GUILayout.Button("삭제", GUILayout.Width(48f)))
            {
                removeIndex = i;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.PropertyField(
                element,
                new GUIContent($"Panel {i + 1}"),
                true);
            EditorGUILayout.EndVertical();
        }

        if (removeIndex >= 0)
        {
            RemoveArrayElement(managedUIObjects, removeIndex);
            RemoveArrayElement(managedUIIds, removeIndex);
        }

        if (GUILayout.Button("+ UI 추가", GUILayout.Height(23f)))
        {
            int newIndex = managedUIObjects.arraySize;
            managedUIObjects.arraySize++;
            managedUIObjects.GetArrayElementAtIndex(newIndex).objectReferenceValue = null;
            managedUIIds.arraySize++;
            managedUIIds.GetArrayElementAtIndex(newIndex).stringValue = string.Empty;
        }

        DrawManagedUIIdWarnings();
    }

    private void SynchronizeManagedUIArrays()
    {
        while (managedUIIds.arraySize < managedUIObjects.arraySize)
        {
            int index = managedUIIds.arraySize;
            managedUIIds.arraySize++;

            GameObject target = managedUIObjects
                .GetArrayElementAtIndex(index)
                .objectReferenceValue as GameObject;
            managedUIIds.GetArrayElementAtIndex(index).stringValue =
                target != null ? target.name : string.Empty;
        }

        while (managedUIIds.arraySize > managedUIObjects.arraySize)
        {
            managedUIIds.DeleteArrayElementAtIndex(managedUIIds.arraySize - 1);
        }
    }

    private void DrawManagedUIIdWarnings()
    {
        HashSet<string> usedIds =
            new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < managedUIIds.arraySize; i++)
        {
            string rawId = managedUIIds.GetArrayElementAtIndex(i).stringValue;
            string id = string.IsNullOrWhiteSpace(rawId)
                ? string.Empty
                : rawId.Trim();
            GameObject target = managedUIObjects
                .GetArrayElementAtIndex(i)
                .objectReferenceValue as GameObject;

            if (target != null && id.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    $"Panel {i + 1}의 ID가 비어 있습니다.",
                    MessageType.Warning);
                continue;
            }

            if (id.Length > 0 && !usedIds.Add(id))
            {
                EditorGUILayout.HelpBox(
                    $"중복된 Managed UI ID입니다: {id}",
                    MessageType.Error);
            }
        }
    }

    private static void RemoveArrayElement(SerializedProperty array, int index)
    {
        int previousSize = array.arraySize;
        array.DeleteArrayElementAtIndex(index);

        // ObjectReference 배열은 첫 삭제에서 값만 null이 될 수 있으므로 한 번 더 제거합니다.
        if (array.arraySize == previousSize)
        {
            array.DeleteArrayElementAtIndex(index);
        }
    }
}
