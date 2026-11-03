#!/usr/bin/env python3
"""G3: fresh regression plus R5 source/API preservation and assembly migration evidence.

K3 independently verifies all rebuilt consumers, deployment and missing dependencies.
Requires git history containing the pinned V2 revision and a restored solution.
"""
import argparse
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import subprocess
import sys
import tarfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
V2_REVISION = "fa8dbb38c4809b028151167887d5d72968d100a8"
spec = importlib.util.spec_from_file_location("v2_runner", ROOT / "eng/Test-FhirV2Context.py")
v2 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(v2)
baseline = v2.baseline


def historical(path):
    return subprocess.check_output(["git", "show", f"{V2_REVISION}:{path}"], cwd=ROOT)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", default="artifacts/fhir-versions/v3/run")
    args = parser.parse_args()
    output = (ROOT / args.output).resolve()
    if output == (ROOT / "artifacts").resolve() or not output.is_relative_to((ROOT / "artifacts").resolve()) or output.exists():
        parser.error("Use a fresh directory strictly under artifacts/")
    output.mkdir(parents=True)
    summary = {"schemaVersion": 1, "gate": "G3", "status": "Pending", "v2Revision": V2_REVISION,
               "migrationPolicy": "rebuild-all-no-forwarders", "completeR4Support": False}
    previous_ui = os.environ.get("DOTNET_CLI_UI_LANGUAGE")
    # Match K3: localized WriteCodeFragment comments affect PDB checksums and PE bytes.
    os.environ["DOTNET_CLI_UI_LANGUAGE"] = "en-US"
    summary["buildUiLanguage"] = "en-US"
    try:
        archive = subprocess.check_output(["git", "archive", "--format=tar", V2_REVISION, "Generated/R5"], cwd=ROOT)
        original = {}
        with tarfile.open(fileobj=io.BytesIO(archive)) as tar:
            for member in tar:
                if member.isfile() and member.name.endswith(".cs"):
                    original[member.name] = hashlib.sha256(tar.extractfile(member).read()).hexdigest()
        current = {p.relative_to(ROOT).as_posix(): baseline.sha(p) for p in (ROOT / "Generated/R5").rglob("*.cs")}
        if len(original) != 852 or current != original:
            raise ValueError("R5 generated C# shape/source drift from V2")
        summary["preservedGeneratedSources"] = len(current)
        for path in ("Tests/Architecture/ApprovedR5ModelApi.txt", "Tests/Architecture/ApprovedModelContractsApi.txt"):
            if historical(path).replace(b"\r\n", b"\n") != (ROOT / path).read_bytes().replace(b"\r\n", b"\n"):
                raise ValueError(f"Existing model/SPI API changed: {path}")
        approved = historical("Tests/Architecture/ApprovedPublicApi.txt").decode().splitlines()
        actual = (ROOT / "Tests/Architecture/ApprovedPublicApi.txt").read_text(encoding="utf-8").splitlines()
        kept = []
        skipping = False
        for line in actual:
            if line.startswith("TYPE "):
                skipping = "MyFhirSdk.ModelMetadata.R5.R5ModelProvider |" in line
            if not skipping:
                kept.append(line)
        if kept != approved:
            raise ValueError("Existing aggregate SDK/Runtime/model API changed beyond the new R5 provider")
        summary["existingApiPreserved"] = True
        sys.argv = ["Test-FhirV2Context.py", "--output", str(output / "context")]
        v2.main()
        context = json.loads((output / "context/summary.json").read_text(encoding="utf-8"))
        for name in ("sourceRevision", "sourceFingerprint", "createdAtUtc", "os", "sdkVersion", "configuration", "targetFramework",
                     "testCounts", "runtimeContract", "testedImplementationHashes", "apiSnapshots"):
            summary[name] = context[name]
        cases = []
        for path in (output / "context/test-results").glob("*.trx"):
            for result in ET.parse(path).getroot().findall(".//{*}UnitTestResult"):
                if result.attrib["testName"].startswith("MyFhirSdk.Tests.Architecture.V3."):
                    cases.append({"test": result.attrib["testName"], "outcome": result.attrib["outcome"]})
        if len(cases) < 4 or any(c["outcome"] != "Passed" for c in cases):
            raise ValueError("Missing or failed V3 ownership/dependency/API cases")
        baseline.write(output / "v3-test-cases.json", cases)
        summary["v3Tests"] = {"total": len(cases), "passed": len(cases)}
        summary["modelSupportApiSha256"] = baseline.sha(ROOT / "Tests/Architecture/ApprovedModelSupportApi.txt")
        summary["toolVersion"] = json.loads((ROOT / "CodeGen/Policy/runtime-contract.json").read_text())["compatibility"]["toolVersion"]
        summary["definingAssemblies"] = {"R5PrimitivesTypesResourcesAndSimpleQuantity": "MyFhirSdk.R5.Models",
            "R5Provider": "MyFhirSdk.R5.Models", "SharedCodecsAndRuleImplementations": "MyFhirSdk.ModelSupport",
            "ContextAndEngines": "MyFhirSdk", "NeutralSpi": "MyFhirSdk.ModelContracts", "Bootstrap": "MyFhirSdk.Runtime"}
        summary["status"] = "Passed"
        print(f"G3 Passed: {len(cases)} V3 cases; {summary['testCounts']}; 852 generated sources unchanged")
    except Exception as error:
        summary["status"] = "Failed"
        summary["error"] = str(error).replace(str(ROOT), "<checkout>")
        raise
    finally:
        baseline.write(output / "summary.json", summary)
        if previous_ui is None:
            os.environ.pop("DOTNET_CLI_UI_LANGUAGE", None)
        else:
            os.environ["DOTNET_CLI_UI_LANGUAGE"] = previous_ui


if __name__ == "__main__":
    main()
