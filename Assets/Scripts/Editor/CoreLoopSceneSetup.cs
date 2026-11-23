#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 유니티 에디터 컴파일 시 또는 메뉴 클릭 시 현재 열려 있는 씬(SampleScene)에
/// GameManagers(TownDungeonLoopManager) 및 DebugUI(CoreLoopDebugUI) 오브젝트를
/// 자동으로 생성하고 아이템 에셋을 매핑한 뒤 씬을 저장하는 에디터 유틸리티
/// </summary>
[InitializeOnLoad]
public static class CoreLoopSceneSetup
{
    private const string PrefKey = "CoreLoopSceneSetup_Executed";

    static CoreLoopSceneSetup()
    {
        EditorApplication.delayCall += () =>
        {
            SetupActiveScene(false);
        };
    }

    [MenuItem("MysteryDungeon/Setup CoreLoop Scene & Save")]
    public static void ManualSetup()
    {
        SetupActiveScene(true);
    }

    public static void SetupActiveScene(bool forceSave)
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        if (!activeScene.isLoaded) return;

        bool isModified = false;

        // 1. GameManagers 오브젝트 & TownDungeonLoopManager 컴포넌트 생성 및 확인
        var loopManager = Object.FindFirstObjectByType<TownDungeonLoopManager>();
        GameObject managersObj = null;
        if (loopManager == null)
        {
            managersObj = GameObject.Find("GameManagers");
            if (managersObj == null)
            {
                managersObj = new GameObject("GameManagers");
                Undo.RegisterCreatedObjectUndo(managersObj, "Create GameManagers");
            }
            loopManager = managersObj.GetComponent<TownDungeonLoopManager>();
            if (loopManager == null)
            {
                loopManager = managersObj.AddComponent<TownDungeonLoopManager>();
                Undo.RegisterCreatedObjectUndo(loopManager, "Add TownDungeonLoopManager");
            }
            isModified = true;
            Debug.Log("[CoreLoopSceneSetup] GameManagers 오브젝트에 TownDungeonLoopManager 컴포넌트 부착 완료.");
        }
        else
        {
            managersObj = loopManager.gameObject;
        }

        // 2. DebugUI 오브젝트 & CoreLoopDebugUI 컴포넌트 생성 및 확인
        var debugUI = Object.FindFirstObjectByType<CoreLoopDebugUI>();
        GameObject debugUIObj = null;
        if (debugUI == null)
        {
            debugUIObj = GameObject.Find("DebugUI");
            if (debugUIObj == null)
            {
                debugUIObj = new GameObject("DebugUI");
                Undo.RegisterCreatedObjectUndo(debugUIObj, "Create DebugUI");
            }
            debugUI = debugUIObj.GetComponent<CoreLoopDebugUI>();
            if (debugUI == null)
            {
                debugUI = debugUIObj.AddComponent<CoreLoopDebugUI>();
                Undo.RegisterCreatedObjectUndo(debugUI, "Add CoreLoopDebugUI");
            }
            isModified = true;
            Debug.Log("[CoreLoopSceneSetup] DebugUI 오브젝트에 CoreLoopDebugUI 컴포넌트 부착 완료.");
        }
        else
        {
            debugUIObj = debugUI.gameObject;
        }

        // 3. 테스트용 아이템 에셋 매핑 및 초기 골드/가방 설정 보장
        var item101 = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Resources/Items/101_오랭열매.asset");
        var item102 = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Resources/Items/102_탈출구슬.asset");
        var item201 = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Resources/Items/201_철검.asset");

        if (loopManager != null)
        {
            var loopSo = new SerializedObject(loopManager);
            var goldProp = loopSo.FindProperty("accountData.gold");
            if (goldProp != null && goldProp.intValue < 5000)
            {
                goldProp.intValue = 5000;
                isModified = true;
            }
            var maxSlotsProp = loopSo.FindProperty("maxBagSlots");
            if (maxSlotsProp != null && maxSlotsProp.intValue != 20)
            {
                maxSlotsProp.intValue = 20;
                isModified = true;
            }
            if (isModified)
            {
                loopSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(loopManager);
            }
        }

        if (debugUI != null)
        {
            var so = new SerializedObject(debugUI);
            var prop101 = so.FindProperty("item101_OranBerry");
            var prop102 = so.FindProperty("item102_EscapeOrb");
            var prop201 = so.FindProperty("item201_IronSword");

            if (prop101 != null && prop101.objectReferenceValue != item101)
            {
                prop101.objectReferenceValue = item101;
                isModified = true;
            }
            if (prop102 != null && prop102.objectReferenceValue != item102)
            {
                prop102.objectReferenceValue = item102;
                isModified = true;
            }
            if (prop201 != null && prop201.objectReferenceValue != item201)
            {
                prop201.objectReferenceValue = item201;
                isModified = true;
            }

            if (isModified)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(debugUI);
            }
        }

        // 4. 변경사항 저장
        if (isModified || forceSave)
        {
            if (managersObj != null) EditorUtility.SetDirty(managersObj);
            if (debugUIObj != null) EditorUtility.SetDirty(debugUIObj);
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log($"[CoreLoopSceneSetup] 씬 배치 및 아이템 매핑 설정 완료. 씬 저장됨: {activeScene.path}");
        }
    }
}
#endif
