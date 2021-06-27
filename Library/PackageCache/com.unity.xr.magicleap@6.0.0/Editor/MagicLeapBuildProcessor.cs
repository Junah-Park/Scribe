using System.Linq;

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;

using UnityEngine;
using UnityEngine.XR.Management;

using UnityEngine.XR.MagicLeap;

namespace UnityEditor.XR.MagicLeap
{
    public class MagicLeapBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        private string[] runtimePluginNames = new string[] { "UnityMagicLeap.elf", "UnityMagicLeap.so" };
        private string[] remotingPluginNames = new string[] { "UnityMagicLeap.dll", "UnityMagicLeap.dylib" };

        void CleanOldSettings()
        {
            UnityEngine.Object[] preloadedAssets = PlayerSettings.GetPreloadedAssets();
            if (preloadedAssets == null)
                return;

            var oldSettings = from s in preloadedAssets
                where (s != null) && (s.GetType() == typeof(MagicLeapSettings))
                select s;

            if (oldSettings.Any())
            {
                var assets = preloadedAssets.ToList();
                foreach (var s in oldSettings)
                {
                    assets.Remove(s);
                }

                PlayerSettings.SetPreloadedAssets(assets.ToArray());
            }
        }

        public bool ShouldIncludeRuntimePluginsInBuild(string path)
        {
            // Return false if not on platform Lumin
#if PLATFORM_LUMIN
            XRGeneralSettings generalSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget));
            if (generalSettings == null)
                return false;

            foreach (var loader in generalSettings.Manager.loaders)
            {
                if (loader is MagicLeapLoader)
                    return true;
            }
#endif // PLATFORM_LUMIN

            return false;
        }

        // Remoting is only intended to work in the editor so builds are disallowed to have the libraries
        public bool ShouldIncludeRemotingPluginsInBuild(string path) => false;

        void AssignNativePluginIncludeInBuildDelegates()
        {
            // For each plugin within the project, check if it is a plugin generated by this
            // package and assign the Include in build delegate to prevent magic leap libraries
            // from being included on other platforms
            var allPlugins = PluginImporter.GetAllImporters();
            foreach (var plugin in allPlugins)
            {
                if (plugin.isNativePlugin)
                {
                    foreach (var pluginName in runtimePluginNames)
                    {
                        if (plugin.assetPath.Contains(pluginName))
                        {
                            plugin.SetIncludeInBuildDelegate(ShouldIncludeRuntimePluginsInBuild);
                            break;
                        }
                    }

                    foreach (var pluginName in remotingPluginNames)
                    {
                        if (plugin.assetPath.Contains(pluginName))
                        {
                            plugin.SetIncludeInBuildDelegate(ShouldIncludeRemotingPluginsInBuild);
                            break;
                        }
                    }
                }
            }
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            // Assign each library a "ShouldIncludeInBuild" delegate to indicate whether the plugin
            // should be placed in a build on a specific platform.  As of right now it's only important
            // for runtime on the device but that could change to have standalone include remoting libs
            AssignNativePluginIncludeInBuildDelegates();

            // Always remember to cleanup preloaded assets after build to make sure we don't
            // dirty later builds with assets that may not be needed or are out of date.
            CleanOldSettings();

            MagicLeapSettings settings = null;
            EditorBuildSettings.TryGetConfigObject(MagicLeapConstants.kSettingsKey, out settings);
            if (settings == null)
                return;

#if PLATFORM_LUMIN
            MagicLeapImageDatabaseBuildProcessor.BuildImageTrackingAssets();
#endif

            UnityEngine.Object[] preloadedAssets = PlayerSettings.GetPreloadedAssets();

            if (!preloadedAssets.Contains(settings))
            {
                var assets = preloadedAssets.ToList();
                assets.Add(settings);
                PlayerSettings.SetPreloadedAssets(assets.ToArray());
            }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            // Always remember to cleanup preloaded assets after build to make sure we don't
            // dirty later builds with assets that may not be needed or are out of date.
            CleanOldSettings();
        }
    }
}