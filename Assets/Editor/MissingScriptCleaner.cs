#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class MissingScriptCleaner
{
    private const string SessionKey = "MissingScriptCleaner.RanForSession";

    static MissingScriptCleaner()
    {
        EditorApplication.delayCall += RunOncePerSession;
    }

    [MenuItem("Tools/Cleanup/Remove Missing Scripts")]
    public static void RunManual()
    {
        RunCleanup(showDialog: true);
    }

    private static void RunOncePerSession()
    {
        if (SessionState.GetBool(SessionKey, false))
            return;

        SessionState.SetBool(SessionKey, true);
        RunCleanup(showDialog: false);
    }

    private static void RunCleanup(bool showDialog)
    {
        int removed = 0;
        removed += CleanScenes();
        removed += CleanPrefabs();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (showDialog)
        {
            EditorUtility.DisplayDialog(
                "Cleanup concluido",
                removed > 0
                    ? $"Foram removidos {removed} componentes com Missing Script."
                    : "Nenhum Missing Script foi encontrado.",
                "OK");
        }
        else if (removed > 0)
        {
            Debug.Log($"[MissingScriptCleaner] Removidos {removed} componentes com Missing Script.");
        }
    }

    private static int CleanScenes()
    {
        int removed = 0;
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });

        foreach (string guid in sceneGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            bool dirty = false;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                removed += RemoveMissingRecursive(root, ref dirty);
            }

            if (dirty)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        return removed;
    }

    private static int CleanPrefabs()
    {
        int removed = 0;
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });

        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            bool dirty = false;

            removed += RemoveMissingRecursive(root, ref dirty);

            if (dirty)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }

            PrefabUtility.UnloadPrefabContents(root);
        }

        return removed;
    }

    private static int RemoveMissingRecursive(GameObject gameObject, ref bool dirty)
    {
        int removed = 0;
        int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
        if (missingCount > 0)
        {
            Undo.RegisterCompleteObjectUndo(gameObject, "Remove Missing Scripts");
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(gameObject);
            dirty = true;
            removed += missingCount;
        }

        Transform transform = gameObject.transform;
        for (int i = 0; i < transform.childCount; i++)
        {
            removed += RemoveMissingRecursive(transform.GetChild(i).gameObject, ref dirty);
        }

        return removed;
    }
}
#endif
