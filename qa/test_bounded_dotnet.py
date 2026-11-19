"""Check SDK argument containment without starting dotnet, MSBuild, or an app."""
import json
from pathlib import Path
import subprocess
import tempfile
import unittest


class BoundedDotNetTests(unittest.TestCase):
    def invoke(self, operation, additional=(), exit_code=0):
        wrapper = Path(__file__).resolve().parents[1] / "scripts" / "Invoke-BoundedDotNet.ps1"
        with tempfile.TemporaryDirectory(prefix="worldseed-bound-arguments-") as directory:
            project = Path(directory) / "Probe.csproj"
            project.write_text('<Project Sdk="Microsoft.NET.Sdk" />', encoding="utf-8")
            quote = lambda value: "'" + str(value).replace("'", "''") + "'"
            arguments = ",".join(quote(value) for value in additional)
            harness = f"""$ErrorActionPreference = 'Stop'
$env:DOTNET_PROCESSOR_COUNT = '7'
$global:taskCapture = $null
function dotnet {{
    $global:taskCapture = @{{ arguments = @($args); processorCount = $env:DOTNET_PROCESSOR_COUNT; nodeReuse = $env:MSBUILDDISABLENODEREUSE; buildServer = $env:DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER }}
    $global:LASTEXITCODE = {exit_code}
}}
try {{
    & {quote(wrapper)} -Operation {quote(operation)} -Project {quote(project)} -AdditionalArguments @({arguments})
    @{{ call = $global:taskCapture; restoredCount = $env:DOTNET_PROCESSOR_COUNT }} | ConvertTo-Json -Depth 5 -Compress
}} catch {{
    @{{ call = $global:taskCapture; restoredCount = $env:DOTNET_PROCESSOR_COUNT; error = $_.Exception.Message }} | ConvertTo-Json -Depth 5 -Compress
}}
"""
            completed = subprocess.run(["powershell.exe", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", harness], capture_output=True, text=True, check=True)
            return json.loads(completed.stdout.strip())

    def test_every_operation_limits_workers_and_restores_environment(self):
        for operation in ("Restore", "Build", "Test", "Publish"):
            with self.subTest(operation=operation):
                result = self.invoke(operation)
                self.assertNotIn("error", result)
                call = result["call"]
                self.assertEqual("2", call["processorCount"])
                self.assertEqual("1", call["nodeReuse"])
                self.assertEqual("1", call["buildServer"])
                self.assertEqual("7", result["restoredCount"])
                for argument in ("-m:1", "-nr:false", "-p:BuildInParallel=false", "-p:UseSharedCompilation=false"):
                    self.assertIn(argument, call["arguments"])
                self.assertIn("--disable-parallel" if operation == "Restore" else "--no-restore", call["arguments"])

    def test_worker_overrides_are_refused_before_launch(self):
        for argument in ("-m:100", "-nr:true", "-p:BuildInParallel=true", "--restore"):
            with self.subTest(argument=argument):
                result = self.invoke("Build", (argument,))
                self.assertIsNone(result["call"])
                self.assertIn("Worker controls", result["error"])

    def test_child_failure_restores_environment_and_stops(self):
        result = self.invoke("Build", exit_code=9)
        self.assertIn("exit code 9", result["error"])
        self.assertEqual("7", result["restoredCount"])

    def test_normal_test_arguments_are_forwarded_unchanged(self):
        result = self.invoke("Test", ("--filter", "FullyQualifiedName~SomeTests", "--results-directory", "a path with spaces"))
        self.assertNotIn("error", result)
        self.assertIn("a path with spaces", result["call"]["arguments"])


if __name__ == "__main__":
    unittest.main()
