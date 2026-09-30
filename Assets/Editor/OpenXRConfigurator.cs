using System;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace VRTraining.Editor
{
    [InitializeOnLoad]
    public static class OpenXRConfigurator
    {
        static OpenXRConfigurator()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EnsureOpenXRConfigured();
                }
            };
        }

        [MenuItem("VR Training/Configure OpenXR & VR Settings", priority = 2)]
        public static void EnsureOpenXRConfigured()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            try
            {
                ConfigureXRGeneralSettings();
                EnableOpenXRInteractionProfiles();
                Debug.Log("[VR Training] OpenXR и профили контроллеров успешно настроены!");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VR Training] Настройка OpenXR завершилась с предупреждением: {ex.Message}");
            }
        }

        private static void ConfigureXRGeneralSettings()
        {
            XRGeneralSettingsPerBuildTarget buildTargetSettings = null;
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out buildTargetSettings);

            if (buildTargetSettings == null)
            {
                buildTargetSettings = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, buildTargetSettings, true);
            }

            BuildTargetGroup group = BuildTargetGroup.Standalone;
            XRGeneralSettings generalSettings = buildTargetSettings.SettingsForBuildTarget(group);

            if (generalSettings == null)
            {
                generalSettings = ScriptableObject.CreateInstance<XRGeneralSettings>();
                buildTargetSettings.SetSettingsForBuildTarget(group, generalSettings);
            }

            XRManagerSettings managerSettings = generalSettings.Manager;
            if (managerSettings == null)
            {
                managerSettings = ScriptableObject.CreateInstance<XRManagerSettings>();
                generalSettings.Manager = managerSettings;
            }

            // Назначаем OpenXRLoader в качестве активного загрузчика XR
            string openXRLoaderTypeName = typeof(OpenXRLoader).FullName;
            XRPackageMetadataStore.AssignLoader(managerSettings, openXRLoaderTypeName, group);

            generalSettings.InitManagerOnStart = true;

            EditorUtility.SetDirty(managerSettings);
            EditorUtility.SetDirty(generalSettings);
            EditorUtility.SetDirty(buildTargetSettings);
            AssetDatabase.SaveAssets();
        }

        private static void EnableOpenXRInteractionProfiles()
        {
            BuildTargetGroup group = BuildTargetGroup.Standalone;
            FeatureHelpers.RefreshFeatures(group);

            string[] featureIds = new string[]
            {
                "com.unity.openxr.feature.input.oculustouch",
                "com.unity.openxr.feature.input.metaquestplus",
                "com.unity.openxr.feature.input.metaquestpro",
                "com.unity.openxr.feature.input.valveindex",
                "com.unity.openxr.feature.input.htcvive",
                "com.unity.openxr.feature.input.microsoftmotioncontroller",
                "com.unity.openxr.feature.input.hpreverb",
                "com.unity.openxr.feature.input.khrsimpleprofile"
            };

            foreach (var id in featureIds)
            {
                var feature = FeatureHelpers.GetFeatureWithIdForBuildTarget(group, id);
                if (feature != null && !feature.enabled)
                {
                    feature.enabled = true;
                    EditorUtility.SetDirty(feature);
                    Debug.Log($"[VR Training] Включен профиль OpenXR: {id}");
                }
            }

            AssetDatabase.SaveAssets();
        }
    }
}
