using UnityEngine;

namespace Keepfall.Core.Config
{
    /// <summary>
    /// Loads the bundled remote-config defaults from <c>Resources/remote-config.defaults.json</c>
    /// into a <see cref="RemoteConfig"/>. This is the thin UnityEngine seam over the otherwise
    /// engine-free <see cref="RemoteConfig"/>, called once at the composition root. The
    /// Firebase Remote Config wiring (which calls <see cref="RemoteConfig.ApplyOverridesFromJson"/>
    /// on fetch completion) lives in the Monetization/Funnel bootstrap, not here.
    /// </summary>
    public static class RemoteConfigLoader
    {
        /// <summary>Resources path (no extension) of the bundled defaults file.</summary>
        public const string DefaultsResourcePath = "remote-config.defaults";

        /// <summary>
        /// Builds a <see cref="RemoteConfig"/> seeded with the bundled defaults. If the file is
        /// missing, returns a config with no defaults — callers still get their hard-coded
        /// fallbacks (which mirror source-of-truth), so the game never breaks on a missing file.
        /// </summary>
        public static RemoteConfig CreateWithBundledDefaults()
        {
            var config = new RemoteConfig();
            TextAsset asset = Resources.Load<TextAsset>(DefaultsResourcePath);
            if (asset == null)
            {
                Debug.LogWarning(
                    $"[RemoteConfig] Bundled defaults '{DefaultsResourcePath}' not found in " +
                    "Resources; using built-in fallbacks.");
                return config;
            }

            config.LoadDefaultsFromJson(asset.text);
            return config;
        }
    }
}
