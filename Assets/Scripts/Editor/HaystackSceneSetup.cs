using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using HaystackSimulator.Runtime;

namespace HaystackSimulator.Editor
{
    [InitializeOnLoad]
    public static class HaystackSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/HaystackPlayground.unity";

        static HaystackSceneSetup()
        {
            EditorApplication.delayCall += EnsureSceneRegistered;
        }

        private static void EnsureSceneRegistered()
        {
            EditorBuildSettingsScene[] currentScenes = EditorBuildSettings.scenes;
            bool found = false;
            for (int i = 0; i < currentScenes.Length; i++)
            {
                if (currentScenes[i].path == ScenePath)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                EditorBuildSettingsScene[] newScenes = new EditorBuildSettingsScene[currentScenes.Length + 1];
                newScenes[0] = new EditorBuildSettingsScene(ScenePath, true);
                for (int i = 0; i < currentScenes.Length; i++)
                {
                    newScenes[i + 1] = currentScenes[i];
                }
                EditorBuildSettings.scenes = newScenes;
            }

            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (sceneAsset != null)
            {
                EditorSceneManager.playModeStartScene = sceneAsset;
            }
        }

        [MenuItem("Tools/Haystack/Setup & Rebuild Scene")]
        public static void BuildHaystackScene()
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Directional Sun Light
            GameObject lightGo = new GameObject("Sun_DirectionalLight");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1.0f, 0.94f, 0.82f); // Warm sun
            light.intensity = 1.35f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            // 2. Barn Ground & Environment Floor
            GameObject groundGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            groundGo.name = "Barn_StoneFloor";
            groundGo.transform.position = Vector3.zero;
            groundGo.transform.localScale = new Vector3(3f, 1f, 3f);
            
            Material floorMat = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                name = "M_BarnFloor",
                color = new Color(0.24f, 0.22f, 0.20f, 1.0f)
            };
            floorMat.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(floorMat, "Assets/Materials/M_BarnFloor.mat");
            groundGo.GetComponent<MeshRenderer>().sharedMaterial = floorMat;

            // 3. Wooden Barn Crate (Enclosure)
            GameObject cratePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Hay_Barn_Crate.fbx");
            if (cratePrefab != null)
            {
                GameObject crateInstance = (GameObject)PrefabUtility.InstantiatePrefab(cratePrefab);
                crateInstance.name = "Hay_Barn_Crate";
                crateInstance.transform.position = Vector3.zero;
                crateInstance.transform.rotation = Quaternion.identity;

                Material woodMat = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                {
                    name = "M_RusticWood",
                    color = new Color(0.42f, 0.28f, 0.18f, 1.0f)
                };
                woodMat.SetFloat("_Smoothness", 0.25f);
                AssetDatabase.CreateAsset(woodMat, "Assets/Materials/M_RusticWood.mat");

                Renderer[] renderers = crateInstance.GetComponentsInChildren<Renderer>();
                foreach (Renderer r in renderers)
                {
                    r.sharedMaterial = woodMat;
                }
            }

            // 4. Materials for Straw & Needle
            Shader strawShader = Shader.Find("Universal Render Pipeline/Custom/HayStrawGPUInstanced");
            Material strawMat = new Material(strawShader)
            {
                name = "M_HayStraw_GPUInstanced"
            };
            AssetDatabase.CreateAsset(strawMat, "Assets/Materials/M_HayStraw_GPUInstanced.mat");

            Shader needleShader = Shader.Find("Universal Render Pipeline/Custom/GoldenNeedleURP");
            Material needleMat = new Material(needleShader)
            {
                name = "M_GoldenNeedle"
            };
            AssetDatabase.CreateAsset(needleMat, "Assets/Materials/M_GoldenNeedle.mat");

            // 5. HaySimulationManager
            GameObject simGo = new GameObject("HaySimulationManager");
            HaySimulationManager sim = simGo.AddComponent<HaySimulationManager>();

            ComputeShader compShader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Shaders/HaySimulation.compute");
            SerializedObject serializedSim = new SerializedObject(sim);

            serializedSim.FindProperty("_simulationShader").objectReferenceValue = compShader;
            serializedSim.FindProperty("_strawMaterial").objectReferenceValue = strawMat;
            serializedSim.FindProperty("_needleMaterial").objectReferenceValue = needleMat;

            // Load FBX meshes
            GameObject strawObj = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Straw_LowPoly.fbx");
            if (strawObj != null)
            {
                MeshFilter mf = strawObj.GetComponentInChildren<MeshFilter>();
                if (mf != null)
                {
                    serializedSim.FindProperty("_strawMesh").objectReferenceValue = mf.sharedMesh;
                }
            }

            GameObject needleObj = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Needle_Golden.fbx");
            if (needleObj != null)
            {
                MeshFilter mf = needleObj.GetComponentInChildren<MeshFilter>();
                if (mf != null)
                {
                    serializedSim.FindProperty("_needleMesh").objectReferenceValue = mf.sharedMesh;
                }
            }

            serializedSim.ApplyModifiedProperties();

            // 6. Player Tactile Controller & Camera
            GameObject playerGo = new GameObject("Player_TactileRig");
            playerGo.transform.position = new Vector3(0f, 1.6f, -2.5f);
            playerGo.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

            GameObject camGo = new GameObject("Main Camera");
            camGo.transform.SetParent(playerGo.transform);
            camGo.transform.localPosition = Vector3.zero;
            camGo.transform.localRotation = Quaternion.Euler(22f, 0f, 0f);
            camGo.tag = "MainCamera";

            Camera cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 100f;
            cam.fieldOfView = 65f;
            camGo.AddComponent<UniversalAdditionalCameraData>();
            camGo.AddComponent<AudioListener>();

            HayTactileInteractor interactor = playerGo.AddComponent<HayTactileInteractor>();
            SerializedObject serializedInteractor = new SerializedObject(interactor);
            serializedInteractor.FindProperty("_playerCamera").objectReferenceValue = cam;

            // Attach Pitchfork model to Camera
            GameObject pitchforkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Hay_Pitchfork_Tool.fbx");
            if (pitchforkPrefab != null)
            {
                GameObject forkInst = (GameObject)PrefabUtility.InstantiatePrefab(pitchforkPrefab);
                forkInst.name = "Hay_Pitchfork_Handheld";
                forkInst.transform.SetParent(camGo.transform);
                forkInst.transform.localPosition = new Vector3(0.28f, -0.22f, 0.65f);
                forkInst.transform.localRotation = Quaternion.Euler(-15f, -25f, 10f);

                Material forkMat = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                {
                    name = "M_Pitchfork",
                    color = new Color(0.6f, 0.45f, 0.32f, 1.0f)
                };
                AssetDatabase.CreateAsset(forkMat, "Assets/Materials/M_Pitchfork.mat");
                Renderer[] fRends = forkInst.GetComponentsInChildren<Renderer>();
                foreach (Renderer r in fRends)
                {
                    r.sharedMaterial = forkMat;
                }

                serializedInteractor.FindProperty("_pitchforkVisual").objectReferenceValue = forkInst.transform;
            }
            serializedInteractor.ApplyModifiedProperties();

            // 7. Save Scene
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EnsureSceneRegistered();
            Debug.Log($"[HaystackSceneSetup] Complete! Scene saved at: {ScenePath} and registered in Build Settings.");
        }
    }
}
