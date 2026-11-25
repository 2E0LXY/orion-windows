1>2# : vim: set ft=python: ^
'''
@echo off & cd %~dp0 & bin\\uv.exe run python "%~dpf0" %* & exit /b ::'''

# Regenerate Visual Studio browsing projects for WebKit and vcpkg
# dependency source so that Solution Explorer can navigate source files
# with IntelliSense while actual building goes through vcpkg + ninja.
#
# Usage:
#   update_wk_solution.py.cmd           - regenerate from configured source
#
# Requires ../webkit (local clone) to exist.

import os
import subprocess
import sys
import uuid
import xml.etree.ElementTree as ET
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent
BUILDTREES = REPO_ROOT / "external" / "vcpkg" / "buildtrees" / "webkit-win"
TOOLS = REPO_ROOT / "external" / "vcpkg" / "downloads" / "tools"
VS_OUT = BUILDTREES / "VS"
DEPS_VS_OUT = REPO_ROOT / "build" / "VS" / "Dependencies"

BROWSE_DEPS = ["libuv", "libzip", "pugixml", "reflectcpp", "yyjson"]
SOURCE_EXTS = {".c", ".cc", ".cpp", ".cxx", ".h", ".hh", ".hpp", ".hxx", ".inc"}


def resolve_webkit_source() -> Path:
    local = REPO_ROOT / ".." / "webkit"
    if (local / "Source" / "WebKit" / "CMakeLists.txt").exists():
        return local.resolve()

    print("WebKit source not found. Clone ../webkit first.")
    sys.exit(1)


def find_first(pattern: Path, relative_exe: str) -> Path | None:
    """Return <dir>/<relative_exe> for the first dir matching pattern where it exists."""
    if not pattern.parent.exists():
        return None
    for candidate in sorted(pattern.parent.glob(pattern.name)):
        if candidate.is_dir():
            exe = candidate / relative_exe
            if exe.exists():
                return exe
    return None


def resolve_tools() -> dict[str, Path]:
    perl = find_first(TOOLS / "perl" / "*", "perl/bin/perl.exe")
    python = find_first(TOOLS / "python" / "*", "python.exe")
    ruby = find_first(TOOLS / "ruby" / "*", "bin/ruby.exe")
    gperf = TOOLS / "gperf" / "bin" / "gperf.exe"

    tools = {}
    for name, path in [("Perl", perl), ("Python", python), ("Ruby", ruby),
                       ("Gperf", gperf if gperf.exists() else None)]:
        if not path:
            print(f"{name} not found under {TOOLS}. Run vcpkg install first.")
            sys.exit(1)
        tools[name] = path
    return tools


def resolve_dep_source(dep: str) -> Path | None:
    src_root = REPO_ROOT / "external" / "vcpkg" / "buildtrees" / dep / "src"
    if not src_root.exists():
        return None
    cleans = sorted(src_root.glob("*.clean"))
    return cleans[0] if cleans else None


def generate_dep_browse_project(dep: str, src: Path) -> Path | None:
    files = sorted(
        p for p in src.rglob("*")
        if p.is_file() and p.suffix.lower() in SOURCE_EXTS
    )
    if not files:
        return None

    headers = [f for f in files if f.suffix.lower() in {".h", ".hh", ".hpp", ".hxx", ".inc"}]
    sources = [f for f in files if f not in set(headers)]

    proj_guid = str(uuid.uuid5(uuid.NAMESPACE_URL, f"vcpkg-browse://{dep}")).upper()

    ns = "http://schemas.microsoft.com/developer/msbuild/2003"
    ET.register_namespace("", ns)
    project = ET.Element("Project", DefaultTargets="Build", ToolsVersion="17.0")

    configs = ET.SubElement(project, "ItemGroup", Label="ProjectConfigurations")
    for cfg in ("Debug", "Release"):
        pc = ET.SubElement(configs, "ProjectConfiguration", Include=f"{cfg}|x64")
        ET.SubElement(pc, "Configuration").text = cfg
        ET.SubElement(pc, "Platform").text = "x64"

    pg = ET.SubElement(project, "PropertyGroup", Label="Globals")
    ET.SubElement(pg, "ProjectGuid").text = f"{{{proj_guid}}}"
    ET.SubElement(pg, "RootNamespace").text = dep
    ET.SubElement(pg, "Keyword").text = "MakeFileProj"

    ET.SubElement(project, "Import", Project="$(VCTargetsPath)\\Microsoft.Cpp.default.props")

    for cfg in ("Debug", "Release"):
        pg2 = ET.SubElement(project, "PropertyGroup",
                            Label="Configuration",
                            Condition=f"'$(Configuration)|$(Platform)'=='{cfg}|x64'")
        ET.SubElement(pg2, "ConfigurationType").text = "Makefile"
        ET.SubElement(pg2, "NMakeBuildCommandLine").text = "echo browse-only"
        ET.SubElement(pg2, "NMakeReBuildCommandLine").text = "echo browse-only"
        ET.SubElement(pg2, "NMakeCleanCommandLine").text = "echo browse-only"
        ET.SubElement(pg2, "NMakeIncludeSearchPath").text = str(
            REPO_ROOT / "vcpkg_installed" / "x64-windows" / "include"
        )

    ET.SubElement(project, "Import", Project="$(VCTargetsPath)\\Microsoft.Cpp.props")

    if sources:
        ig = ET.SubElement(project, "ItemGroup")
        for f in sources:
            ET.SubElement(ig, "ClCompile", Include=str(f))

    if headers:
        ig = ET.SubElement(project, "ItemGroup")
        for f in headers:
            ET.SubElement(ig, "ClInclude", Include=str(f))

    ET.SubElement(project, "Import", Project="$(VCTargetsPath)\\Microsoft.Cpp.targets")

    out_dir = DEPS_VS_OUT / dep
    out_dir.mkdir(parents=True, exist_ok=True)
    out_path = out_dir / f"{dep}.vcxproj"

    tree = ET.ElementTree(project)
    ET.indent(tree, space="  ")
    tree.write(out_path, xml_declaration=True, encoding="utf-8")
    return out_path


def generate_dep_browse_projects():
    generated = []
    for dep in BROWSE_DEPS:
        src = resolve_dep_source(dep)
        if not src:
            print(f"  {dep}: source not found (skipped)")
            continue
        path = generate_dep_browse_project(dep, src)
        if path:
            print(f"  {dep}: {path.relative_to(REPO_ROOT)}")
            generated.append((dep, path))
        else:
            print(f"  {dep}: no source files found (skipped)")
    return generated


def main():
    src = resolve_webkit_source()
    print(f"Source: {src}")

    tools = resolve_tools()
    ruby_root = tools["Ruby"].parent.parent  # <ruby>/bin/ruby.exe → <ruby>

    env = os.environ.copy()
    # Add perl + python to PATH so CMake's find_package can locate them.
    extra_paths = [str(tools["Perl"].parent), str(tools["Python"].parent)]
    env["PATH"] = os.pathsep.join(extra_paths + [env.get("PATH", "")])

    # Forward slashes in toolchain paths because CMake is happier with them
    # when embedded directly into -D flags on Windows.
    script_dir_fs = str(REPO_ROOT).replace("\\", "/")
    toolchain = f"{script_dir_fs}/external/vcpkg/scripts/buildsystems/vcpkg.cmake"
    installed = f"{script_dir_fs}/vcpkg_installed"

    VS_OUT.parent.mkdir(parents=True, exist_ok=True)

    cmd = [
        "cmake", "-S", str(src), "-B", str(VS_OUT),
        "-G", "Visual Studio 18", "-A", "x64", "-T", "ClangCL",
        "-DCMAKE_BUILD_TYPE=Debug",
        f"-DCMAKE_TOOLCHAIN_FILE={toolchain}",
        "-DVCPKG_MANIFEST_INSTALL=OFF",
        f"-DVCPKG_INSTALLED_DIR={installed}",
        "-DVCPKG_TARGET_TRIPLET=x64-windows",
        "-DPORT=Win",
        "-DENABLE_EXPERIMENTAL_FEATURES=ON",
        f"-DPERL_EXECUTABLE={tools['Perl']}",
        "-DPython_FIND_REGISTRY=NEVER",
        f"-DPython_EXECUTABLE={tools['Python']}",
        f"-DRuby_ROOT={ruby_root}",
        f"-DRuby_EXECUTABLE={tools['Ruby']}",
        f"-DGPERF_EXECUTABLE={tools['Gperf']}",
    ]
    print("Running cmake...")
    result = subprocess.run(cmd, env=env, cwd=str(REPO_ROOT))
    if result.returncode != 0:
        sys.exit(result.returncode)

    # Generate browse-only projects for vcpkg dependencies.
    print("Generating dependency browse projects...")
    generate_dep_browse_projects()

    # Sync project GUIDs so the solution's ProjectReferences keep matching.
    fix_script = REPO_ROOT / "fix_project_uuids.py.cmd"
    if fix_script.exists():
        print("Running fix_project_uuids...")
        result = subprocess.run(
            ["cmd", "/c", str(fix_script)], env=env, cwd=str(REPO_ROOT)
        )
        if result.returncode != 0:
            sys.exit(result.returncode)


if __name__ == "__main__":
    main()
