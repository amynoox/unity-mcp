"""Compile the real updater/version sources without starting a Unity Editor."""
import json
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[2]

# Only Unity platform APIs are doubled. PackageVersion and ApplicationPaths must
# come from the package, with their real namespaces, so missing imports fail.
PLATFORM = r'''
namespace UnityEngine
{
    public static class Application { public static string dataPath; }
}
namespace UnityEditor
{
    public static class AssetDatabase
    {
        public static string[] FindAssets(string filter) => new string[0];
        public static string GUIDToAssetPath(string guid) => string.Empty;
    }
}
namespace UnityEditor.PackageManager
{
    public sealed class PackageInfo
    {
        public string name;
        public string resolvedPath;
        public static PackageInfo FindForAssetPath(string path) => null;
    }
}
'''

PROBE = r'''
using System;
using System.IO;
using KitWright.Editor.MCP.Server;
class Probe
{
    static int Main(string[] args)
    {
        UnityEngine.Application.dataPath = Path.Combine(args[0], "Assets");
        if (UpdateChecker.CurrentState.CurrentVersion != "4.5.6")
            throw new Exception("The updater must read the real version service.");
        UpdateChecker.MaybeCheckForUpdatesInBackground();
        UpdateChecker.UpdateToLatestFromWindow();
        var state = UpdateChecker.CurrentState;
        if (state.CurrentVersion != "4.5.6" || state.HasUpdateAvailable ||
            state.IsChecking || state.IsUpdating || state.UpdateStarted)
            throw new Exception("Project-managed updates must stay inactive.");
        Console.WriteLine("Real updater and version-service compilation passed.");
        return 0;
    }
}
'''


class UpdateCheckerCompilationTests(unittest.TestCase):
    def test_real_updater_and_version_sources_compile_and_run(self):
        dotnet = shutil.which("dotnet")
        if not dotnet:
            self.skipTest("A local .NET SDK is required for the compilation fixture.")
        result = subprocess.run([dotnet, "--list-sdks"], text=True,
                                capture_output=True, check=True, timeout=30)
        sdks = []
        for line in result.stdout.splitlines():
            match = re.fullmatch(r"(\d+\.\d+\.\d+) \[(.+)\]", line.strip())
            if match:
                sdks.append((tuple(map(int, match[1].split('.'))), match[1], Path(match[2])))
        if not sdks:
            self.skipTest("A stable local .NET SDK is required.")
        version, label, sdk_root = max(sdks)
        compiler = sdk_root / label / "Roslyn" / "bincore" / "csc.dll"
        packs = sdk_root.parent / "packs" / "Microsoft.NETCore.App.Ref"
        references = sorted(
            (p for p in packs.glob("*") if re.fullmatch(r"\d+\.\d+\.\d+", p.name)
             and int(p.name.split('.')[0]) == version[0]),
            key=lambda p: tuple(map(int, p.name.split('.'))))
        self.assertTrue(references, "The SDK's matching reference assemblies are required.")
        framework = "net" + ".".join(map(str, version[:2]))
        # SDK feature bands (e.g. 9.0.320) use the major/minor target net9.0.
        reference_dir = references[-1] / "ref" / framework
        self.assertTrue(compiler.is_file(), str(compiler))
        self.assertTrue(reference_dir.is_dir(), str(reference_dir))

        with tempfile.TemporaryDirectory(prefix="mcp-updater-compile-") as name:
            work = Path(name)
            (work / "Assets").mkdir()
            (work / "package.json").write_text('{"version":"4.5.6"}', encoding="utf-8")
            platform = work / "Platform.cs"
            platform.write_text(PLATFORM, encoding="utf-8")
            probe = work / "Probe.cs"
            probe.write_text(PROBE, encoding="utf-8")
            output = work / "Probe.dll"
            sources = [ROOT / "Editor/MCP/Server/UpdateChecker.cs",
                       ROOT / "Editor/Services/PackageVersion.cs",
                       ROOT / "Editor/Services/ApplicationPaths.cs", platform, probe]
            arguments = ["-nologo", "-nostdlib+", "-langversion:7.3",
                         "-target:exe", '-out:"' + str(output) + '"']
            arguments += ['-r:"' + str(p) + '"' for p in reference_dir.glob("*.dll")]
            arguments += ['"' + str(p) + '"' for p in sources]
            response = work / "compile.rsp"
            response.write_text("\n".join(arguments), encoding="utf-8")
            compiled = subprocess.run([dotnet, "exec", str(compiler), "-noconfig", "@" + str(response)],
                                      text=True, capture_output=True, timeout=60)
            self.assertEqual(compiled.returncode, 0, compiled.stdout + compiled.stderr)
            output.with_suffix(".runtimeconfig.json").write_text(json.dumps({
                "runtimeOptions": {"tfm": framework, "framework": {
                    "name": "Microsoft.NETCore.App",
                    "version": ".".join(map(str, version[:2])) + ".0"}}}), encoding="utf-8")
            ran = subprocess.run([dotnet, str(output), str(work)], text=True,
                                 capture_output=True, timeout=30)
            self.assertEqual(ran.returncode, 0, ran.stdout + ran.stderr)


if __name__ == "__main__":
    unittest.main()
