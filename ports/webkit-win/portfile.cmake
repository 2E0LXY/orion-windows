# --- Source acquisition ---
# The source is a pruned tarball published to GCS by the webkit repo's CI for
# every orion-windows commit: no test suites, no website content.
#
# scripts/fetch_webkit_src.py.cmd puts it in DOWNLOADS before vcpkg runs,
# because vcpkg builds its own PATH for portfiles and gcloud is not on it.
file(READ "${CMAKE_CURRENT_LIST_DIR}/WEBKIT_REF" WEBKIT_REF)
string(STRIP "${WEBKIT_REF}" WEBKIT_REF)

set(WEBKIT_ARCHIVE "${DOWNLOADS}/webkit-src-${WEBKIT_REF}.tar.zst")
if(NOT EXISTS "${WEBKIT_ARCHIVE}")
    message(FATAL_ERROR
        "No WebKit source tarball for ${WEBKIT_REF}.\n"
        "Run scripts/fetch_webkit_src.py.cmd, or push the ref to an "
        "*orion-windows* branch so its CI publishes one.")
endif()

vcpkg_extract_source_archive(SOURCE_PATH
    ARCHIVE "${WEBKIT_ARCHIVE}"
    SOURCE_BASE "${WEBKIT_REF}"
)

# WebKit requires Perl during the build (code generation scripts)
vcpkg_find_acquire_program(PERL)
get_filename_component(PERL_DIR "${PERL}" DIRECTORY)
vcpkg_add_to_path("${PERL_DIR}")

vcpkg_find_acquire_program(PYTHON3)
get_filename_component(PYTHON3_DIR "${PYTHON3}" DIRECTORY)
vcpkg_add_to_path("${PYTHON3_DIR}")

vcpkg_find_acquire_program(RUBY)
get_filename_component(RUBY_DIR "${RUBY}" DIRECTORY)
vcpkg_add_to_path("${RUBY_DIR}")

vcpkg_find_acquire_program(GPERF)
get_filename_component(GPERF_DIR "${GPERF}" DIRECTORY)
vcpkg_add_to_path("${GPERF_DIR}")

# WebKit on Windows requires clang-cl. Use the pinned version from
# vcpkg-tools.json instead of the VS-bundled one.
# Clang 21+ has a mangling regression that breaks WebKit:
# https://github.com/llvm/llvm-project/issues/191588
set(_tools_json "${CMAKE_CURRENT_LIST_DIR}/../../vcpkg-tools.json")
execute_process(
    COMMAND "${DOWNLOADS}/../vcpkg.exe" fetch clang-cl "--z-tool-data-file=${_tools_json}"
    OUTPUT_VARIABLE _clang_cl_output
    OUTPUT_STRIP_TRAILING_WHITESPACE
    RESULT_VARIABLE _fetch_result
)
if(NOT _fetch_result EQUAL 0)
    message(FATAL_ERROR "Failed to fetch pinned clang-cl via vcpkg.")
endif()
# vcpkg fetch prints the path on the last line
string(REGEX MATCH "[^\n]+$" CLANG_CL "${_clang_cl_output}")
get_filename_component(CLANG_CL_DIR "${CLANG_CL}" DIRECTORY)
vcpkg_add_to_path(PREPEND "${CLANG_CL_DIR}")
set(ENV{CC} "${CLANG_CL}")
set(ENV{CXX} "${CLANG_CL}")

# Tool paths (portfile-specific — tools come from vcpkg_find_acquire_program)
set(TOOL_OPTIONS
    "-DRuby_EXECUTABLE=${RUBY}"
    "-DGPERF_EXECUTABLE=${GPERF}"
)

# --- Configure + Build ---
# Debugger source paths: remap absolute paths in PDBs to repo-relative paths
# so the VS debugger can find WebKit source regardless of clone location.
file(RELATIVE_PATH _src_rel "${CURRENT_BUILDTREES_DIR}" "${SOURCE_PATH}")
string(APPEND VCPKG_CXX_FLAGS " /clang:-fdebug-prefix-map=${SOURCE_PATH}=../../${_src_rel}")
string(APPEND VCPKG_C_FLAGS " /clang:-fdebug-prefix-map=${SOURCE_PATH}=../../${_src_rel}")

vcpkg_cmake_configure(
    SOURCE_PATH "${SOURCE_PATH}"
    OPTIONS -C "${CMAKE_CURRENT_LIST_DIR}/webkit-options.cmake" ${TOOL_OPTIONS}
    MAYBE_UNUSED_VARIABLES CMAKE_NINJA_FORCE_RESPONSE_FILE
)

vcpkg_cmake_build()

set(REL_BUILD_DIR "${CURRENT_BUILDTREES_DIR}/${TARGET_TRIPLET}-rel")
set(DBG_BUILD_DIR "${CURRENT_BUILDTREES_DIR}/${TARGET_TRIPLET}-dbg")

# --- Manual install (WebKit has no install() targets on Windows) ---
# Allow EXEs in bin/ — WebKit child processes (WebProcess, NetworkProcess,
# GPUProcess) must be alongside DLLs at runtime.
set(VCPKG_POLICY_ALLOW_EXES_IN_BIN enabled)

# Exclude dependency DLLs (brotli, curl, icu, etc.) that CMake copies into
# bin/ for in-tree execution.  Those are installed by their own vcpkg ports;
# globbing *.dll causes file conflicts.  Filter by comparing against DLLs
# already installed by dependencies in CURRENT_INSTALLED_DIR.
file(GLOB _dep_dlls "${CURRENT_INSTALLED_DIR}/bin/*.dll")
set(_dep_dll_names "")
foreach(_dep ${_dep_dlls})
    get_filename_component(_name ${_dep} NAME)
    list(APPEND _dep_dll_names ${_name})
endforeach()

# Release binaries
if(NOT DEFINED VCPKG_BUILD_TYPE OR VCPKG_BUILD_TYPE STREQUAL "release")
    # DLLs — only WebKit's own (exclude dependency DLLs)
    file(GLOB _all_rel_dlls "${REL_BUILD_DIR}/bin/*.dll")
    set(release_dlls "")
    foreach(_dll ${_all_rel_dlls})
        get_filename_component(_name ${_dll} NAME)
        if(NOT _name IN_LIST _dep_dll_names)
            list(APPEND release_dlls ${_dll})
        endif()
    endforeach()
    if(release_dlls)
        file(INSTALL ${release_dlls} DESTINATION "${CURRENT_PACKAGES_DIR}/bin")
    endif()

    # EXEs (WebProcess, NetworkProcess, GPUProcess, MiniBrowser, etc.)
    file(GLOB release_exes "${REL_BUILD_DIR}/bin/*.exe")
    file(INSTALL ${release_exes} DESTINATION "${CURRENT_PACKAGES_DIR}/bin")

    # Import libraries
    file(GLOB release_libs "${REL_BUILD_DIR}/lib/*.lib")
    file(INSTALL ${release_libs} DESTINATION "${CURRENT_PACKAGES_DIR}/lib")

    # PDB files
    file(GLOB release_pdbs_bin "${REL_BUILD_DIR}/bin/*.pdb")
    file(GLOB release_pdbs_lib "${REL_BUILD_DIR}/lib/*.pdb")
    if(release_pdbs_bin)
        file(INSTALL ${release_pdbs_bin} DESTINATION "${CURRENT_PACKAGES_DIR}/bin")
    endif()
    if(release_pdbs_lib)
        file(INSTALL ${release_pdbs_lib} DESTINATION "${CURRENT_PACKAGES_DIR}/lib")
    endif()

    # Resources
    if(EXISTS "${REL_BUILD_DIR}/bin/WebKit.resources")
        file(INSTALL "${REL_BUILD_DIR}/bin/WebKit.resources/"
             DESTINATION "${CURRENT_PACKAGES_DIR}/share/webkit/WebKit.resources")
    endif()
endif()

# Debug binaries
if(NOT DEFINED VCPKG_BUILD_TYPE OR VCPKG_BUILD_TYPE STREQUAL "debug")
    # DLLs — only WebKit's own (exclude dependency DLLs)
    file(GLOB _dep_dbg_dlls "${CURRENT_INSTALLED_DIR}/debug/bin/*.dll")
    set(_dep_dbg_dll_names "")
    foreach(_dep ${_dep_dbg_dlls})
        get_filename_component(_name ${_dep} NAME)
        list(APPEND _dep_dbg_dll_names ${_name})
    endforeach()
    file(GLOB _all_dbg_dlls "${DBG_BUILD_DIR}/bin/*.dll")
    set(debug_dlls "")
    foreach(_dll ${_all_dbg_dlls})
        get_filename_component(_name ${_dll} NAME)
        if(NOT _name IN_LIST _dep_dbg_dll_names)
            list(APPEND debug_dlls ${_dll})
        endif()
    endforeach()
    if(debug_dlls)
        file(INSTALL ${debug_dlls} DESTINATION "${CURRENT_PACKAGES_DIR}/debug/bin")
    endif()

    file(GLOB debug_exes "${DBG_BUILD_DIR}/bin/*.exe")
    file(INSTALL ${debug_exes} DESTINATION "${CURRENT_PACKAGES_DIR}/debug/bin")

    file(GLOB debug_libs "${DBG_BUILD_DIR}/lib/*.lib")
    file(INSTALL ${debug_libs} DESTINATION "${CURRENT_PACKAGES_DIR}/debug/lib")

    file(GLOB debug_pdbs_bin "${DBG_BUILD_DIR}/bin/*.pdb")
    file(GLOB debug_pdbs_lib "${DBG_BUILD_DIR}/lib/*.pdb")
    if(debug_pdbs_bin)
        file(INSTALL ${debug_pdbs_bin} DESTINATION "${CURRENT_PACKAGES_DIR}/debug/bin")
    endif()
    if(debug_pdbs_lib)
        file(INSTALL ${debug_pdbs_lib} DESTINATION "${CURRENT_PACKAGES_DIR}/debug/lib")
    endif()
endif()

# Headers (from release build; both configs share the same public API)
set(HEADER_BUILD_DIR "${REL_BUILD_DIR}")
if(DEFINED VCPKG_BUILD_TYPE AND VCPKG_BUILD_TYPE STREQUAL "debug")
    set(HEADER_BUILD_DIR "${DBG_BUILD_DIR}")
endif()

file(INSTALL "${HEADER_BUILD_DIR}/JavaScriptCore/Headers/"
     DESTINATION "${CURRENT_PACKAGES_DIR}/include")
file(INSTALL "${HEADER_BUILD_DIR}/bmalloc/Headers/"
     DESTINATION "${CURRENT_PACKAGES_DIR}/include")
file(INSTALL "${HEADER_BUILD_DIR}/WebCore/PrivateHeaders/"
     DESTINATION "${CURRENT_PACKAGES_DIR}/include")
file(INSTALL "${HEADER_BUILD_DIR}/WTF/Headers/"
     DESTINATION "${CURRENT_PACKAGES_DIR}/include")
file(INSTALL "${HEADER_BUILD_DIR}/WebKit/Headers/"
     DESTINATION "${CURRENT_PACKAGES_DIR}/include")

set(VCPKG_POLICY_SKIP_COPYRIGHT_CHECK enabled)
