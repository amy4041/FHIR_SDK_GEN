#!/usr/bin/env python3
"""Verify current D5 delivery; real-server acceptance remains a separate operation."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
import os
from pathlib import Path
import platform
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "eng"))
import FhirDelivery as delivery

spec = importlib.util.spec_from_file_location("d5_source", ROOT / "eng/Export-FhirVersionBaseline.py")
source = importlib.util.module_from_spec(spec)
spec.loader.exec_module(source)


def validate_offline(manifest, report, manifest_hash):
    if set(manifest["compositions"]) != set(delivery.COMPOSITIONS):
        raise ValueError("D5 requires R4, R5, Dual and R4TwCore")
    if (report.get("status") != "Passed" or report.get("deliveryManifestSha256") != manifest_hash
            or report.get("sourceRevision") != manifest["sourceRevision"]
            or report.get("sourceFingerprint") != manifest["sourceFingerprint"]
            or report.get("externalServer") != "NotRun"
            or any(report.get(k) is not True for k in ("outsideRepository", "isolatedCache", "inheritedEndpointsDisabled"))):
        raise ValueError("Incomplete or mismatched offline delivery evidence")
    if set(report.get("examples", {})) != set(delivery.COMPOSITIONS):
        raise ValueError("Missing deployment composition")
    for name, record in report["examples"].items():
        expected = delivery.composition(name)
        if (record.get("status") != "Passed" or record.get("requiredDlls") != expected["requiredDlls"]
                or record.get("versions") != expected["fhirVersions"]
                or any(record.get(k) is not True for k in ("dllReferencesOnly", "publishedExecution", "onlyRequiredInputs"))):
            raise ValueError("Incomplete deployment: " + name)
        missing = record.get("missingDependencies", [])
        if (len(missing) != len(expected["requiredDlls"])
                or {r["assembly"] for r in missing} != set(expected["requiredDlls"])
                or any(not r.get("exitCode") or r.get("diagnosed") is not True for r in missing)):
            raise ValueError("Incomplete missing dependency checks: " + name)
    if set(report.get("generation", {})) != set(delivery.VERSIONS):
        raise ValueError("Missing installed-tool generation")
    for record in report["generation"].values():
        if (record.get("status") != "Passed" or record.get("sourceCount", 0) <= 0
                or any(record.get(k) is not True for k in ("repeatedGenerationMatches", "matchesCommittedModels"))):
            raise ValueError("Incomplete installed-tool generation")
    install = report.get("canonicalInstall", {})
    if (install.get("emptyInitialCache") is not True or install.get("localFeedOnly") is not True
            or install.get("toolPackageSha256") != manifest["toolPackageSha256"]
            or install.get("compilerReferenceSha256") != manifest["runtimeCompilerReferenceSha256"]):
        raise ValueError("Installed tool differs from delivery")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--bundle", type=Path, help="Current clean all-composition bundle; export fresh when omitted")
    parser.add_argument("--tool-package", type=Path, help="Canonical package for a new export")
    parser.add_argument("--allow-working-tree", action="store_true", help="Development preview only; clean-source acceptance stays Pending")
    args = parser.parse_args()
    output = args.output.resolve()
    artifacts = (ROOT / "artifacts").resolve()
    if output.exists() or output == artifacts or not output.is_relative_to(artifacts):
        parser.error("Use a fresh directory strictly under artifacts/")
    if args.bundle and (output.is_relative_to(args.bundle.resolve()) or args.tool_package):
        parser.error("Existing bundle requires separate evidence and cannot use --tool-package")
    revision = source.run(["git", "rev-parse", "HEAD"])
    dirty = bool(source.run(["git", "status", "--porcelain"]))
    if dirty and not args.allow_working_tree:
        parser.error("D5 acceptance requires clean committed sources; --allow-working-tree is preview only")
    fingerprint = source.fingerprint(source.source_inventory())
    output.mkdir(parents=True)
    summary = {"schemaVersion": 1, "gate": "D5-offline", "status": "Running", "os": platform.system(),
               "createdAtUtc": datetime.now(timezone.utc).isoformat(), "sourceRevision": revision,
               "sourceFingerprint": fingerprint, "workingTreeDirty": dirty,
               "cleanSourceAcceptance": "Pending" if dirty else "Passed", "offlineAcceptance": "Pending",
               "externalServerAcceptance": "Pending", "seriesCloseout": "Pending"}
    env = {k: v for k, v in os.environ.items() if not k.startswith("MYFHIRSDK_")}
    env["DOTNET_CLI_UI_LANGUAGE"] = "en-US"

    def run(name, arguments):
        result = subprocess.run(arguments, cwd=ROOT, env=env, capture_output=True, encoding="utf-8", errors="replace", timeout=1800)
        (output / (name + ".log")).write_text(result.stdout + result.stderr, encoding="utf-8")
        if result.returncode:
            raise ValueError("D5 command failed: " + name)

    try:
        bundle = args.bundle.resolve() if args.bundle else output / "bundle"
        if not args.bundle:
            command = [sys.executable, "eng/Export-FhirDelivery.py", "--output", str(bundle)]
            if args.tool_package:
                command += ["--tool-package", str(args.tool_package.resolve())]
            run("export", command)
        manifest, manifest_hash = delivery.read_manifest(bundle)
        delivery.validate_manifest(bundle, manifest)
        if (manifest["sourceRevision"] != revision or manifest["sourceFingerprint"] != fingerprint
                or manifest["workingTreeDirty"] != dirty):
            raise ValueError("Bundle must describe the exact current sources and cleanliness")
        summary["deliveryManifestSha256"] = manifest_hash
        run("external", [sys.executable, "eng/Test-FhirDelivery.py", "--bundle", str(bundle), "--output", str(output / "external")])
        report = json.loads((output / "external/summary.json").read_text(encoding="utf-8"))
        validate_offline(manifest, report, summary["deliveryManifestSha256"])
        delivery.validate_manifest(bundle, manifest)
        delivery.assert_manifest_unchanged(bundle, manifest_hash)
        if (source.fingerprint(source.source_inventory()) != fingerprint
                or source.run(["git", "rev-parse", "HEAD"]) != revision
                or bool(source.run(["git", "status", "--porcelain"])) != dirty):
            raise ValueError("Sources changed during D5 acceptance")
        summary.update(offlineAcceptance="Passed", status="Pending" if dirty else "Passed")
    except Exception as error:
        summary.update(status="Failed", error=str(error))
    source.write(output / "summary.json", summary)
    source.write(output / "evidence-files.json", delivery.inventory(output))
    print(json.dumps(summary, indent=2))
    return 1 if summary["status"] == "Failed" else 0


if __name__ == "__main__":
    raise SystemExit(main())
