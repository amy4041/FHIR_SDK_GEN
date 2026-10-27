#!/usr/bin/env python3
"""Explicit read-only smoke against a specified server and pre-existing synthetic Patient."""
import argparse
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
from urllib.parse import urlsplit
import re

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "eng"))
import FhirDelivery as delivery


def configuration(release, environment):
    prefix = f"MYFHIRSDK_{release}_"
    endpoint = environment.get(prefix + "INTEGRATION_BASE_URL", "").strip()
    patient = environment.get(prefix + "PATIENT_ID", "").strip()
    address = urlsplit(endpoint)
    if (address.scheme not in ("https", "http") or not address.hostname or address.username is not None
            or address.password is not None or address.query or address.fragment
            or not re.fullmatch(r"[A-Za-z0-9.-]{1,64}", patient)):
        raise ValueError("Provide the selected version's base URL and valid synthetic Patient logical ID")
    return {prefix + "INTEGRATION_BASE_URL": endpoint, prefix + "PATIENT_ID": patient,
            prefix + "INTEGRATION_BEARER_TOKEN": environment.get(prefix + "INTEGRATION_BEARER_TOKEN", "")}


def validate_server_report(release, report, deployed, manifest):
    delivery.validate_execution(release, report, deployed)
    for assembly in report["assemblies"]:
        dll = assembly["name"] + ".dll"
        if assembly["identity"] != manifest["assemblyIdentities"][dll] or delivery.sha(deployed / dll) != manifest["files"]["sdk/" + dll]:
            raise ValueError("Server smoke used different delivery assemblies")
    remote = report["releases"][0].get("externalServer", {})
    if remote != {"status": "Passed", "operation": "GET metadata; GET Patient/{id}", "versionChecked": True}:
        raise ValueError("Actual version-checked server read did not pass")


def remote_failure(stdout, stderr):
    # Only persist our fixed diagnostic vocabulary, never arbitrary child output.
    allowed = {"MetadataRequest": {"RequestFailed"}, "MetadataVersion": {"VersionMismatch"},
               "PatientRead": {"RequestFailed"}, "PatientIdentity": {"PatientMissing", "PatientIdMismatch"}}
    for line in reversed((stdout + "\n" + stderr).splitlines()):
        if not line.startswith("D5_SERVER_FAILURE ") or len(line) > 512:
            continue
        try:
            detail = json.loads(line[len("D5_SERVER_FAILURE "):])
            if (isinstance(detail, dict) and isinstance(detail.get("stage"), str)
                    and isinstance(detail.get("code"), str)
                    and detail["code"] in allowed.get(detail["stage"], set())):
                return detail["stage"], detail["code"]
        except (ValueError, TypeError):
            pass
    return "ServerExecution", "ExecutionFailed"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bundle", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--release", required=True, choices=delivery.VERSIONS)
    parser.add_argument("--synthetic-patient", required=True, action="store_true", help="Confirm the configured pre-existing Patient is synthetic and GET operations are permitted")
    args = parser.parse_args()
    bundle, output = args.bundle.resolve(), args.output.resolve()
    artifacts = (ROOT / "artifacts").resolve()
    if output.exists() or output == artifacts or not output.is_relative_to(artifacts) or output.is_relative_to(bundle):
        parser.error("Use fresh evidence under artifacts/, outside the bundle")
    selected = configuration(args.release, os.environ)
    manifest, manifest_hash = delivery.read_manifest(bundle)
    delivery.validate_manifest(bundle, manifest)
    if args.release not in manifest["compositions"]:
        parser.error("Selected single-version example is missing from bundle")
    output.mkdir(parents=True)
    summary = {"schemaVersion": 1, "gate": "D5-server", "status": "Running", "release": args.release,
               "fhirVersion": delivery.VERSIONS[args.release], "operation": "GET metadata; GET Patient/{id}",
               "syntheticPatientConfirmed": True, "createdAtUtc": datetime.now(timezone.utc).isoformat(),
               "sourceRevision": manifest["sourceRevision"], "sourceFingerprint": manifest["sourceFingerprint"],
               "workingTreeDirty": manifest["workingTreeDirty"],
               "deliveryManifestSha256": manifest_hash}
    phase, failure_code, exit_code = "StageSources", "SourceStagingFailed", None
    try:
        with tempfile.TemporaryDirectory(prefix="FhirServerSmoke-") as temporary:
            work = Path(temporary).resolve()
            if work.is_relative_to(ROOT):
                raise ValueError("Server consumer must run outside repository")
            spec = delivery.composition(args.release)
            names = {"global.json", "NuGet.Config", "examples/SdkReferences.props"}
            names.update("sdk/" + name for name in spec["requiredDlls"])
            names.update(n for n in manifest["files"] if n.startswith(("examples/Shared/", f"examples/{args.release}/")))
            for name in names:
                target = delivery.safe_path(work, name)
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(delivery.safe_path(bundle, name), target)
            env = {k: v for k, v in os.environ.items() if not k.startswith("MYFHIRSDK_") and k not in
                   ("DOTNET_ADDITIONAL_DEPS", "DOTNET_SHARED_STORE", "DOTNET_STARTUP_HOOKS")}
            env.update(NUGET_PACKAGES=str(work / "packages"), DOTNET_CLI_HOME=str(work / "cli-home"),
                       NUGET_HTTP_CACHE_PATH=str(work / "http-cache"), DOTNET_CLI_UI_LANGUAGE="en-US",
                       DOTNET_CLI_USE_MSBUILD_SERVER="0", MSBUILDDISABLENODEREUSE="1")
            deployed = work / "publish"
            phase, failure_code = "Publish", "PublishFailed"
            built = subprocess.run(["dotnet", "publish", spec["exampleProject"], "-c", "Release", "--self-contained", "false",
                                    "-p:UseAppHost=false", "-p:NuGetAudit=false", "-o", str(deployed)],
                                   cwd=work, env=env, capture_output=True, encoding="utf-8", errors="replace", timeout=600)
            if built.returncode:
                exit_code = built.returncode
                raise ValueError("Server example publish failed")
            # Credentials are provided only to the explicit execution, never build/tool processes.
            phase, failure_code = "ServerExecution", "ExecutionFailed"
            result = subprocess.run(["dotnet", str(deployed / (args.release + "Example.dll"))], cwd=deployed,
                                    env=env | selected, capture_output=True, encoding="utf-8", errors="replace", timeout=120)
            if result.returncode:
                exit_code = result.returncode
                phase, failure_code = remote_failure(result.stdout, result.stderr)
                raise ValueError("Server read failed; verify endpoint version, synthetic ID and authentication")
            phase, failure_code = "ReportParsing", "InvalidExecutionReport"
            report = json.loads(result.stdout.strip().splitlines()[-1])
            phase, failure_code = "ReportValidation", "ExecutionEvidenceMismatch"
            validate_server_report(args.release, report, deployed, manifest)
            assembly_hashes = {n: delivery.sha(deployed / n) for n in spec["requiredDlls"]}
            phase, failure_code = "Cleanup", "WorkspaceCleanupFailed"
        phase, failure_code = "DeliveryIntegrity", "DeliveryIntegrityFailed"
        delivery.validate_manifest(bundle, manifest)
        phase, failure_code = "ManifestIntegrity", "ManifestChanged"
        delivery.assert_manifest_unchanged(bundle, manifest_hash)
        summary.update(status="Passed", versionChecked=True, patientRead=True, outsideRepository=True,
                       assemblyHashes=assembly_hashes)
    except Exception as error:
        # Remote payloads, request details and secrets are deliberately not persisted.
        if isinstance(error, subprocess.TimeoutExpired):
            failure_code = "Timeout"
        elif isinstance(error, FileNotFoundError):
            failure_code = "ExecutableNotFound" if phase in ("Publish", "ServerExecution") else "FileMissing"
        summary.update(status="Failed", failureStage=phase, errorCode=failure_code,
                       error="Server verification failed; see failureStage and errorCode")
        if exit_code is not None:
            summary["processExitCode"] = exit_code
    (output / "summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(summary, indent=2))
    return 0 if summary["status"] == "Passed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
