# Overlay of the builtin x64-windows triplet, which forces MSVC_VERSION to be
# used. Without this, vcpkg's own vcvarsall discovery picks the newest
# installed toolset.
include("${CMAKE_CURRENT_LIST_DIR}/../external/vcpkg/triplets/x64-windows.cmake")

file(READ "${CMAKE_CURRENT_LIST_DIR}/../MSVC_VERSION" ORION_MSVC_VERSION)
string(STRIP "${ORION_MSVC_VERSION}" ORION_MSVC_VERSION)
set(VCPKG_PLATFORM_TOOLSET_VERSION "${ORION_MSVC_VERSION}")

# vcpkg's compiler tracking hashes the cl.exe bytes, and a VS update might
# replace them inside the pinned toolset directory without renaming it (the
# toolset's "servicing version", Auxiliary\Microsoft.VC.ServicingVersion*.props).
# Hash the pin instead, so ABI hashes change only on MSVC_VERSION bumps.
set(VCPKG_DISABLE_COMPILER_TRACKING ON)
set(VCPKG_HASH_ADDITIONAL_FILES "${CMAKE_CURRENT_LIST_DIR}/../MSVC_VERSION")
