using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace TabletopMayhem.Editor
{
    public static class QuestProjectSetup
    {
        const string ScenePath = "Assets/TabletopMayhem/Scenes/TabletopMayhem.unity";
        static readonly string[] RequiredFeatures = {
            "com.unity.openxr.feature.metaquest",
            "com.unity.openxr.feature.input.oculustouch",
            "com.unity.openxr.feature.arfoundation-meta-session",
            "com.unity.openxr.feature.arfoundation-meta-camera",
            "com.unity.openxr.feature.arfoundation-meta-bounding-boxes",
            "com.unity.openxr.feature.arfoundation-meta-mesh",
            "com.unity.openxr.feature.arfoundation-meta-anchor",
            "com.unity.openxr.feature.compositionlayers",
            "MetaOpenXR-OpenXRLifeCycle"
        };

        [MenuItem("Puff/1. Open Game Scene")]
        public static void OpenScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Puff/2. Configure Quest 3")]
        public static void ConfigureQuest()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before configuring Quest.");
            Directory.CreateDirectory("Assets/XR/Settings"); AssetDatabase.Refresh();
            XRGeneralSettingsPerBuildTarget perTarget;
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out perTarget) || perTarget == null)
            {
                const string path = "Assets/XR/Settings/XRGeneralSettingsPerBuildTarget.asset";
                perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(path);
                if (perTarget == null)
                {
                    perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                    AssetDatabase.CreateAsset(perTarget, path);
                }
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            }
            if (!perTarget.HasSettingsForBuildTarget(BuildTargetGroup.Android)) perTarget.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
            if (!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android)) perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            var general = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            general.InitManagerOnStart = true;
            var manager = perTarget.ManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            manager.automaticLoading = true; manager.automaticRunning = true;
            if (!XRPackageMetadataStore.AssignLoader(manager, "UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Android))
                throw new BuildFailedException("Unable to assign OpenXR Android loader. Check Package Manager errors.");
            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
            var xr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (xr == null) throw new BuildFailedException("OpenXR settings could not be created.");
            var planeFeature = FeatureHelpers.GetFeatureWithIdForBuildTarget(BuildTargetGroup.Android, "com.unity.openxr.feature.arfoundation-meta-plane");
            if (planeFeature != null) { planeFeature.enabled = false; EditorUtility.SetDirty(planeFeature); }
            foreach (string id in RequiredFeatures)
            {
                var feature = FeatureHelpers.GetFeatureWithIdForBuildTarget(BuildTargetGroup.Android, id);
                if (feature == null) throw new BuildFailedException("Required Quest feature not found: " + id);
                feature.enabled = true; EditorUtility.SetDirty(feature);
            }
            xr.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            xr.depthSubmissionMode = OpenXRSettings.DepthSubmissionMode.None;
            EditorUtility.SetDirty(xr); EditorUtility.SetDirty(manager); EditorUtility.SetDirty(general); EditorUtility.SetDirty(perTarget);
            PlayerSettings.companyName = "Tabletop Mayhem";
            PlayerSettings.productName = "Puff";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.tabletopmayhem.quest");
            PlayerSettings.bundleVersion = "3.3.0"; PlayerSettings.Android.bundleVersionCode = 25;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.stereoRenderingPath = StereoRenderingPath.Instancing;
            // Enable the Input System without requiring the legacy Input Manager on Android.
            var playerSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var inputHandler = playerSettings.FindProperty("activeInputHandler");
            if (inputHandler != null) { inputHandler.intValue = 1; playerSettings.ApplyModifiedPropertiesWithoutUndo(); }
            QualitySettings.antiAliasing = 4; QualitySettings.vSyncCount = 0;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Quest configured: OpenXR, Meta passthrough, Touch controllers, ARM64/IL2CPP and Vulkan. Run XR Project Validation, then build.");
        }

        [MenuItem("Puff/3. Build Quest APK")]
        public static void BuildQuest()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new BuildFailedException("Install Android Build Support, SDK/NDK and OpenJDK for this Editor through Unity Hub.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                // Target changes reload scripts; a separate invocation avoids trying to build during that reload.
                if (Application.isBatchMode) throw new BuildFailedException("Pass -buildTarget Android to the Unity command line.");
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                Debug.Log("Switched to Android. After scripts finish compiling, run Puff > Build Quest APK again.");
                return;
            }
            ConfigureQuest();
            Directory.CreateDirectory("Builds");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = "Builds/PuffQuest.apk",
                target = BuildTarget.Android, options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Quest build failed: " + report.summary.result);
            Debug.Log("Quest APK created: " + Path.GetFullPath("Builds/PuffQuest.apk"));
        }
    }
}
