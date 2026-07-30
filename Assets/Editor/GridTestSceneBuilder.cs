using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GridTestSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/GridTest.unity";

    [MenuItem("Tools/Grid/Build GridTest Scene")]
    public static void BuildGridTestScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateCameraAndLight();
        CreateGridSystem();
        CreateFloor();
        CreateWalls();
        CreateGameplayObjects();
        CreateHelper();

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[GridTestSceneBuilder] Scene saved to " + ScenePath);
    }

    private static void CreateCameraAndLight()
    {
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.AddComponent<Camera>();
        camGo.AddComponent<AudioListener>();
        camGo.transform.position = new Vector3(0f, 8f, -8f);
        camGo.transform.rotation = Quaternion.Euler(45f, 0f, 0f);

        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    private static void CreateGridSystem()
    {
        var gridSystem = new GameObject("GridSystem");
        gridSystem.AddComponent<GridBoard>();
        gridSystem.AddComponent<GridBootstrapper>();
    }

    private static GameObject CreateFloor()
    {
        var planePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlaneGroup_7_7.prefab");
        var planeGroup = (GameObject)PrefabUtility.InstantiatePrefab(planePrefab);
        planeGroup.name = "PlaneCubes";
        planeGroup.transform.position = new Vector3(0f, -1f, 0f);
        return planeGroup;
    }

    private static void CreateWalls()
    {
        var wallsRoot = new GameObject("Walls");
        var wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Wall.prefab");
        int wallLayer = LayerMask.NameToLayer("Wall");
        int min = -3;
        int max = 3;

        for (int x = min; x <= max; x++)
        {
            for (int z = min; z <= max; z++)
            {
                bool border = x == min || x == max || z == min || z == max;
                if (!border)
                {
                    continue;
                }

                var wall = (GameObject)PrefabUtility.InstantiatePrefab(wallPrefab);
                wall.transform.SetParent(wallsRoot.transform, false);
                wall.transform.position = new Vector3(x, 0.5f, z);
                wall.transform.localScale = new Vector3(1f, 2f, 1f);
                wall.layer = wallLayer;
            }
        }
    }

    private static void CreateGameplayObjects()
    {
        var boxGroup = new GameObject("BoxGroup");

        InstantiateAt("Assets/Prefabs/Player.prefab", "Player", new Vector3(0f, 0f, -1f), null);
        InstantiateAt("Assets/Prefabs/Yellow Box.prefab", "Yellow Box", new Vector3(1f, 0f, -1f), boxGroup.transform);
        InstantiateAt("Assets/Prefabs/Blue Box.prefab", "Blue Box", new Vector3(-1f, 0f, -1f), boxGroup.transform);
        InstantiateAt("Assets/Prefabs/TriggerPlate.prefab", "TriggerPlate", new Vector3(0f, 0f, 0f), null);
        InstantiateAt("Assets/Prefabs/ExitGate.prefab", "ExitGate", new Vector3(0f, 0f, 1f), null);
    }

    private static void CreateHelper()
    {
        var helper = new GameObject("GridTestHelper");
        helper.AddComponent<GridTestReporter>();
    }

    private static GameObject InstantiateAt(string prefabPath, string objectName, Vector3 position, Transform parent)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = objectName;
        if (parent != null)
        {
            instance.transform.SetParent(parent, false);
        }

        instance.transform.position = position;
        return instance;
    }
}
