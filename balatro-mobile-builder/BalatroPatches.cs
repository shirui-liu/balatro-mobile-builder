using BalatroMobileBuilder.Properties;
using System.Text;
using System.Text.RegularExpressions;

namespace BalatroMobileBuilder
{
    public static class BalatroPatches
    {
        /**
         * <summary>
         * A "BalatroPatch" is actually a list of .patch files that will
         * be applied to different files. An id and a name must be specified for
         * each BalatroPatch.
         * </summary>
         */
        public static List<BalatroPatch> patchList = [
            new BalatroPatch("commonfixes", "Common Fixes", new Dictionary<string, string> {
                { "commonfixes_globals", "globals.lua" },
                { "commonfixes_button_callbacks", "functions/button_callbacks.lua" } }, true, true),
            new BalatroPatch("shaderfixes", "Shader Fixes", new Dictionary<string, string> {
                { "shaderfixes_background", "resources/shaders/background.fs" },
                { "shaderfixes_booster", "resources/shaders/booster.fs" },
                { "shaderfixes_crt", "resources/shaders/CRT.fs" },
                { "shaderfixes_debuff", "resources/shaders/debuff.fs" },
                { "shaderfixes_dissolve", "resources/shaders/dissolve.fs" },
                { "shaderfixes_flame", "resources/shaders/flame.fs" },
                { "shaderfixes_flash", "resources/shaders/flash.fs" },
                { "shaderfixes_foil", "resources/shaders/foil.fs" },
                { "shaderfixes_gold_seal", "resources/shaders/gold_seal.fs" },
                { "shaderfixes_holo", "resources/shaders/holo.fs" },
                { "shaderfixes_hologram", "resources/shaders/hologram.fs" },
                { "shaderfixes_negative", "resources/shaders/negative.fs" },
                { "shaderfixes_negative_shine", "resources/shaders/negative_shine.fs" },
                { "shaderfixes_played", "resources/shaders/played.fs" },
                { "shaderfixes_polychrome", "resources/shaders/polychrome.fs" },
                { "shaderfixes_skew", "resources/shaders/skew.fs" },
                { "shaderfixes_splash", "resources/shaders/splash.fs" },
                { "shaderfixes_vortex", "resources/shaders/vortex.fs" },
                { "shaderfixes_voucher", "resources/shaders/voucher.fs" } }, true, true),

            new BalatroPatch("externalstorage", "External Storage", new Dictionary<string, string> {
                { "externalstorage", "conf.lua" } }, false, true),

            new BalatroPatch("fpscap", "FPS cap", new Dictionary<string, string> {
                { "fpscap", "main.lua" } }),
            new BalatroPatch("landscape", "Landscape", new Dictionary<string, string> {
                { "landscape", "functions/button_callbacks.lua" } }),
            new BalatroPatch("fullscreen", "Fullscreen", new Dictionary<string, string> {
                { "fullscreen", "functions/button_callbacks.lua" } }),
            new BalatroPatch("highdpi", "High DPI", new Dictionary<string, string> {
                { "highdpi_conf", "conf.lua" },
                { "highdpi_button_callbacks", "functions/button_callbacks.lua" } }),

            new BalatroPatch("crtdisable", "Disable CRT", new Dictionary<string, string> {
                { "crtdisable", "game.lua" } }, false)
        ];

        public static bool applyPatch(BalatroPatch patch, BalatroZip balaZip) {
            bool err = false;
            foreach (var patchInfo in patch.pathAssignedPatches) {
                // Get file content
                string filePath = $"{balaZip.extractPath}/{patchInfo.Value}";
                string fileContent = File.ReadAllText(filePath, Encoding.UTF8);

                // Get patch file from assembly resources
                object? resource = Resources.ResourceManager.GetObject(patchInfo.Key);
                ArgumentNullException.ThrowIfNull(resource);
                string patchFile = UTF8Encoding.UTF8.GetString((byte[])resource);

                // Patch the specified file
                List<UnifiedPatch.Hunk> hunks = UnifiedPatch.parseText(patchFile);
                (fileContent, bool[] results) = UnifiedPatch.apply(fileContent, hunks);
                if (results.Contains(false) && patch.id == "highdpi") {
                    (fileContent, bool applied) = applyHighDpiFallback(patchInfo.Value, fileContent);
                    results = [applied];
                }
                File.WriteAllText(filePath, fileContent);

                err |= results.Contains(false);
            }
            return err;
        }

        private static (string, bool) applyHighDpiFallback(string relativePath, string fileContent) {
            if (relativePath == "conf.lua") {
                if (Regex.IsMatch(fileContent, @"(?m)^\s*t\.window\.usedpiscale\s*="))
                    return (fileContent, true);

                Match end = Regex.Match(fileContent, @"(?m)^end\s*$", RegexOptions.RightToLeft);
                if (!end.Success)
                    return (fileContent, false);

                string lineEnding = fileContent.Contains("\r\n") ? "\r\n" : "\n";
                string insertion = $"\tt.window.usedpiscale = false{lineEnding}";
                return (fileContent.Insert(end.Index, insertion), true);
            }

            if (relativePath == "functions/button_callbacks.lua") {
                const string highDpiExpression = "highdpi = (love.system.getOS() == 'OS X' or love.system.getOS() == 'Android' or love.system.getOS() == 'iOS')";
                Match highDpi = Regex.Match(fileContent, @"(?m)^(\s*)highdpi\s*=.*$");
                if (highDpi.Success)
                    return (fileContent.Remove(highDpi.Index, highDpi.Length).Insert(highDpi.Index, highDpi.Groups[1].Value + highDpiExpression), true);

                Match display = Regex.Match(fileContent, @"(?m)^(\s*display\s*=.*)$");
                if (!display.Success)
                    return (fileContent, false);

                string lineEnding = fileContent.Contains("\r\n") ? "\r\n" : "\n";
                string insertion = display.Groups[1].Value + lineEnding + display.Groups[1].Value[..^display.Groups[1].Value.TrimStart().Length] + highDpiExpression;
                return (fileContent.Remove(display.Index, display.Length).Insert(display.Index, insertion), true);
            }

            return (fileContent, false);
        }

        public static void setReleaseMode(bool value, BalatroZip balaZip) {
            string luaCode = File.ReadAllText($"{balaZip.extractPath}/conf.lua");
            luaCode = Regex.Replace(luaCode, @"_RELEASE_MODE\s*=.+", $"_RELEASE_MODE = {value.ToString().ToLower()}");
            File.WriteAllText($"{balaZip.extractPath}/conf.lua", luaCode);
        }
    }

    public readonly struct BalatroPatch
    {
        public readonly string id;
        public readonly string name;
        public readonly Dictionary<string, string> pathAssignedPatches;
        public readonly bool defaultPromptAns;
        public readonly bool hidden;

        public BalatroPatch(string id, string name, Dictionary<string, string> pathAssignedPatches, bool defaultPromptAns = true, bool hidden = false) {
            this.id = id;
            this.name = name;
            this.pathAssignedPatches = pathAssignedPatches;
            this.defaultPromptAns = defaultPromptAns;
            this.hidden = hidden;
        }
    }
}
