"""Validate release tags and inspect the package before publishing."""
import argparse
import os
from pathlib import Path
import re
import xml.etree.ElementTree as ET
from zipfile import ZipFile


def version_from_tag(tag, project_version):
    pattern = r"v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?"
    match = re.fullmatch(pattern, tag)
    if not match:
        raise ValueError("Use a tag such as v0.1.0 or v0.2.0-rc.1")
    prerelease = match.group(4)
    if prerelease and any(item.isdigit() and len(item) > 1 and item.startswith("0") for item in prerelease.split(".")):
        raise ValueError("Numeric prerelease identifiers cannot have leading zeros")
    version = tag[1:]
    if version != project_version:
        raise ValueError(f"Tag version {version} does not match project Version {project_version}")
    return version, prerelease is not None


def inspect_manifest(data, expected_version):
    root = ET.fromstring(data)
    namespace = {"n": "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"}
    # NuGet uses several schema versions; work with the namespace in this manifest.
    if root.tag.startswith("{"):
        namespace["n"] = root.tag[1:].split("}", 1)[0]
        prefix = "n:"
    else:
        prefix = ""
    metadata = root.find(f"{prefix}metadata", namespace)
    if metadata is None:
        raise ValueError("Package metadata is missing")
    if metadata.findtext(f"{prefix}id", namespaces=namespace) != "FormatParse":
        raise ValueError("Unexpected package ID")
    if metadata.findtext(f"{prefix}version", namespaces=namespace) != expected_version:
        raise ValueError("Package version does not match the release tag")
    if metadata.findall(f".//{prefix}dependency", namespace):
        raise ValueError("The runtime package must not have NuGet dependencies")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    version = commands.add_parser("version")
    version.add_argument("tag")
    version.add_argument("--project", type=Path, default=Path("src/FormatParse/FormatParse.csproj"))
    project_version_command = commands.add_parser("project-version")
    project_version_command.add_argument("--project", type=Path, default=Path("src/FormatParse/FormatParse.csproj"))
    package = commands.add_parser("package")
    package.add_argument("path", type=Path)
    package.add_argument("version")
    args = parser.parse_args()
    if args.command in ("version", "project-version"):
        project_version = ET.parse(args.project).findtext(".//Version")
        tag = args.tag if args.command == "version" else f"v{project_version}"
        value, prerelease = version_from_tag(tag, project_version)
        print(f"Release version: {value}")
        if os.environ.get("GITHUB_OUTPUT"):
            with open(os.environ["GITHUB_OUTPUT"], "a", encoding="utf-8") as output:
                output.write(f"version={value}\nprerelease={str(prerelease).lower()}\n")
    else:
        with ZipFile(args.path) as archive:
            manifests = [name for name in archive.namelist() if name.endswith(".nuspec")]
            if len(manifests) != 1:
                raise ValueError("Expected one package manifest")
            inspect_manifest(archive.read(manifests[0]), args.version)
            required = {"README.md", "LICENSE", "NOTICE", "lib/net8.0/FormatParse.dll", "lib/net8.0/FormatParse.xml"}
            if not required.issubset(archive.namelist()):
                raise ValueError("The package is missing required files")
        print("Package ID, version, files, and zero-dependency policy verified.")


if __name__ == "__main__":
    main()
