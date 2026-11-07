"""Read-only configuration inventory. Never starts tests, services, or generators."""
import ast
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET


def main():
    root = Path(__file__).resolve().parent.parent
    try:
        import tomllib as toml
    except ImportError:
        try:
            import tomli as toml
        except ImportError:
            print("BLOCKED: use Python 3.11+ or the already installed tomli parser; no package installation was attempted.")
            return 1
    checks = []

    def check(label, condition, detail=""):
        checks.append({"check": label, "passed": bool(condition), "detail": detail})

    agent_path = root / ".codex/agents/qa_engineer.toml"
    agent = toml.loads(agent_path.read_text(encoding="utf-8"))
    config = toml.loads((root / ".codex/config.toml").read_text(encoding="utf-8"))
    check("standalone-agent-schema", agent.get("name") == "qa_engineer" and all(isinstance(agent.get(key), str) and agent[key].strip() for key in ("name", "description", "developer_instructions")))
    check("inherited-permissions", not {"sandbox_mode", "approval_policy", "mcp_servers", "model_provider"}.intersection(agent))
    check("three-agent-settings", config.get("agents", {}).get("enabled") is True and config["agents"].get("max_concurrent_threads_per_session") == 2)
    for name in ("SUBAGENT.md", "REPORT_TEMPLATE.md", "infrastructure-location.json"):
        check("handoff-" + name, (root / "qa" / name).is_file())
    location = json.loads((root / "qa/infrastructure-location.json").read_text(encoding="utf-8"))
    harness = root
    check("focused-harness-location", location.get("focusedHarness") == "tests/WorldSeed.Qa.Tests")
    for name in ("WorldSeed.slnx", "tests/WorldSeed.Qa.Tests/WorldSeed.Qa.Tests.csproj", "tests/WorldSeed.Qa.Tests/TestAppBuilder.cs", "tests/WorldSeed.Qa.Tests/SchemaChangeClarificationWindowTests.cs"):
        check("focused-harness-" + name, (root / name).is_file(), str(root))
    if not all(c["passed"] for c in checks):
        print(json.dumps({"status": "BLOCKED", "project": str(root), "harness": str(harness), "checks": checks}, indent=2))
        return 1
    for path in sorted((root / "qa").glob("*.py")):
        ast.parse(path.read_text(encoding="utf-8-sig"), filename=str(path))
    solution = ET.parse(root / "WorldSeed.slnx")
    for project in solution.findall(".//Project"):
        path = root / project.attrib["Path"]
        check("project-" + path.parent.name, path.exists())
        ET.parse(path)
    test_source = (root / "tests/WorldSeed.Qa.Tests/SchemaChangeClarificationWindowTests.cs").read_text(encoding="utf-8")
    bootstrap_source = (root / "tests/WorldSeed.Qa.Tests/TestAppBuilder.cs").read_text(encoding="utf-8")
    check("isolated-desktop-data-config", "WORLDSEED_DATA_DIRECTORY" in bootstrap_source and "OfflineHandler" in test_source)
    check("rendered-dialog-test", "CaptureRenderedFrame" in test_source)
    result = {"status": "PASS" if all(c["passed"] for c in checks) else "FAIL", "project": str(root), "harness": str(harness),
              "applicationQaExecuted": False, "servicesStarted": False, "testDataGenerated": False,
              "baselineNote": "Focused dialog rendering creates unapproved candidates only; this inventory never approves baselines.", "checks": checks}
    print(json.dumps(result, indent=2))
    return 0 if result["status"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
