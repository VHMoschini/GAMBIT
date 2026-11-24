using System.Collections.Generic;
using System.IO;
using Prototipo.Core;
using Prototipo.Visual;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Prototipo.Editor
{
    /// <summary>
    /// Ferramenta de Editor para gerar os ScriptableObjects padrao e montar/abrir a cena do prototipo em 1 clique.
    /// </summary>
    public static class PrototypeSceneSetup
    {
        private const string RootFolder = "Assets/prototipo";
        private const string DataFolder = "Assets/prototipo/Data";
        private const string ScenesFolder = "Assets/prototipo/Scenes";
        private const string ScenePath = "Assets/prototipo/Scenes/Prototipo.unity";

        private const string RedAssetPath = "Assets/prototipo/Data/Peca_Vermelha.asset";
        private const string BlueAssetPath = "Assets/prototipo/Data/Peca_Azul.asset";
        private const string YellowAssetPath = "Assets/prototipo/Data/Peca_Amarela.asset";
        private const string ConfigAssetPath = "Assets/prototipo/Data/Config_Prototipo.asset";

        [MenuItem("GAMBIT/Prototipo/Criar e Abrir Cena do Prototipo", priority = 10)]
        public static void CreateAndOpenPrototypeScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EnsureFolders();

            PieceBlueprint red = CreateOrUpdateBlueprint(RedAssetPath, DefaultPieceCatalog.CreateRedSpec());
            PieceBlueprint blue = CreateOrUpdateBlueprint(BlueAssetPath, DefaultPieceCatalog.CreateBlueSpec());
            PieceBlueprint yellow = CreateOrUpdateBlueprint(YellowAssetPath, DefaultPieceCatalog.CreateYellowSpec());

            List<PieceBlueprint> blueprints = new List<PieceBlueprint> { red, blue, yellow };
            PrototypeConfig config = CreateOrUpdateConfig(ConfigAssetPath, blueprints);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            ConfigureSceneLightingAndCamera();

            GameObject controllerGo = new GameObject("PrototipoController");
            PrototypeGameController controller = controllerGo.AddComponent<PrototypeGameController>();
            controller.SetConfig(config);

            SerializedObject serializedController = new SerializedObject(controller);
            SerializedProperty configProp = serializedController.FindProperty("config");
            if (configProp != null)
            {
                configProp.objectReferenceValue = config;
                serializedController.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Selection.activeGameObject = controllerGo;

            Debug.Log(
                "[PrototypeSceneSetup] Cena do prototipo criada e salva em '" + ScenePath + "'.\n" +
                "Assets gerados em '" + DataFolder + "': Peca_Vermelha, Peca_Azul, Peca_Amarela e Config_Prototipo.\n" +
                "Pressione Play no Unity Editor para jogar (Setup das capsulas -> Batalha em turnos com AP).");
        }

        [MenuItem("GAMBIT/Prototipo/Abrir Cena do Prototipo", priority = 11)]
        public static void OpenPrototypeScene()
        {
            SceneAsset existingScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (existingScene == null)
            {
                CreateAndOpenPrototypeScene();
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            PrototypeGameController controller = Object.FindAnyObjectByType<PrototypeGameController>();
            if (controller != null)
            {
                Selection.activeGameObject = controller.gameObject;
            }

            Debug.Log("[PrototypeSceneSetup] Cena '" + ScenePath + "' aberta. Pressione Play para iniciar o prototipo.");
        }

        [MenuItem("GAMBIT/Prototipo/Build Windows + Steam (steam_appid.txt)", priority = 20)]
        public static void BuildWindowsSteamPrototype()
        {
            SceneAsset existingScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (existingScene == null)
            {
                CreateAndOpenPrototypeScene();
            }

            string buildFolder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "PrototipoSteam"));
            Directory.CreateDirectory(buildFolder);
            string exePath = Path.Combine(buildFolder, "GAMBIT_Prototipo.exe");

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                PrototypeSteamBuildProcessor.WriteAppIdFile(buildFolder);
                Debug.Log(
                    "[PrototypeSceneSetup] Build Steam concluida com sucesso em:\n" + exePath +
                    "\n'steam_appid.txt' (480) ja esta na pasta! Adicione GAMBIT_Prototipo.exe na Steam como jogo nao-Steam ou execute com a Steam aberta.");
                EditorUtility.RevealInFinder(exePath);
            }
            else
            {
                Debug.LogError("[PrototypeSceneSetup] Falha na build Windows: " + report.summary.result);
            }
        }

        private static PieceBlueprint CreateOrUpdateBlueprint(string assetPath, PieceSpec spec)
        {
            PieceBlueprint blueprint = AssetDatabase.LoadAssetAtPath<PieceBlueprint>(assetPath);
            if (blueprint == null)
            {
                blueprint = ScriptableObject.CreateInstance<PieceBlueprint>();
                blueprint.ConfigureFromSpec(spec);
                AssetDatabase.CreateAsset(blueprint, assetPath);
            }
            else
            {
                blueprint.ConfigureFromSpec(spec);
                EditorUtility.SetDirty(blueprint);
            }

            return blueprint;
        }

        private static PrototypeConfig CreateOrUpdateConfig(string assetPath, List<PieceBlueprint> blueprints)
        {
            PrototypeConfig config = AssetDatabase.LoadAssetAtPath<PrototypeConfig>(assetPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<PrototypeConfig>();
                config.ConfigureDefaults(blueprints);
                AssetDatabase.CreateAsset(config, assetPath);
            }
            else
            {
                config.ConfigureDefaults(blueprints);
                EditorUtility.SetDirty(config);
            }

            return config;
        }

        private static void ConfigureSceneLightingAndCamera()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                mainCamera = Object.FindAnyObjectByType<Camera>();
            }

            if (mainCamera != null)
            {
                mainCamera.transform.position = new Vector3(3.45f, 9.20f, -3.10f);
                mainCamera.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
                mainCamera.clearFlags = CameraClearFlags.SolidColor;
                mainCamera.backgroundColor = new Color(0.08f, 0.10f, 0.14f, 1f);
            }

            Light directionalLight = Object.FindAnyObjectByType<Light>();
            if (directionalLight != null)
            {
                directionalLight.type = LightType.Directional;
                directionalLight.transform.rotation = Quaternion.Euler(52f, -32f, 0f);
                directionalLight.intensity = 1.25f;
                directionalLight.color = new Color(1.00f, 0.97f, 0.92f, 1f);
                directionalLight.shadows = LightShadows.Soft;
            }
        }

        private static void EnsureFolders()
        {
            EnsureSingleFolder("Assets", "prototipo", RootFolder);
            EnsureSingleFolder(RootFolder, "Data", DataFolder);
            EnsureSingleFolder(RootFolder, "Scenes", ScenesFolder);
        }

        private static void EnsureSingleFolder(string parentPath, string folderName, string fullPath)
        {
            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                AssetDatabase.CreateFolder(parentPath, folderName);
            }
        }
    }
}
