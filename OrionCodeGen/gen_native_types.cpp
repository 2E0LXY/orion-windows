// Generates OrionBackend/generated/native_types.h (C++) and
// Orion/Interop/Generated/NativeTypes.cs (C#) from DTO definitions
// in the orion-linux core module using reflectcpp type-level reflection.
//
// Usage: OrionCodeGen.exe <solution_dir>

#include <cstdint>
#include <direct.h>
#include <fstream>
#include <iostream>
#include <map>
#include <string>
#include <string_view>
#include <utility>
#include <vector>

// reflectcpp — header-only type-level reflection
#include <rfl/named_tuple_t.hpp>
#include <rfl/Tuple.hpp>
#include <rfl/enums.hpp>

// DTO headers from the orion-linux core module
#include <core/dto/history_entry_dto.h>
#include <core/dto/bookmark_dto.h>
#include <core/dto/bookmark_folder_dto.h>
#include <core/dto/reading_list_entry_dto.h>
#include <core/dto/settings_dto.h>

// Windows-side wire types (browser engine boundary)
#include "../OrionBackend/context_menu_types.h"
#include "../OrionBackend/media_permission_types.h"

// ============================================================
// Wire type mapping: C++ type → (C wire type, C# wire type)
// ============================================================

struct WireTypeInfo {
    std::string_view c_type;   // used in the C++ wire struct  (e.g. "int64_t")
    std::string_view cs_type;  // used in the C# wire struct and record struct base (e.g. "Int64")
};

// Primary template: clear compile error for unsupported types.
template <typename T>
struct ToWireType {
    static_assert(sizeof(T) == 0,
        "Unsupported DTO field type — add a specialization in OrionCodeGen/gen_native_types.cpp");
};

template <> struct ToWireType<int64_t>      { static constexpr WireTypeInfo info{"int64_t",  "Int64"}; };
template <> struct ToWireType<int32_t>      { static constexpr WireTypeInfo info{"int32_t",  "Int32"}; };
template <> struct ToWireType<uint64_t>     { static constexpr WireTypeInfo info{"uint64_t", "UInt64"}; };
template <> struct ToWireType<uint32_t>     { static constexpr WireTypeInfo info{"uint32_t", "UInt32"}; };
template <> struct ToWireType<int16_t>      { static constexpr WireTypeInfo info{"int16_t",  "Int16"}; };
template <> struct ToWireType<uint16_t>     { static constexpr WireTypeInfo info{"uint16_t", "UInt16"}; };
template <> struct ToWireType<int8_t>       { static constexpr WireTypeInfo info{"int8_t",   "SByte"}; };
template <> struct ToWireType<uint8_t>      { static constexpr WireTypeInfo info{"uint8_t",  "Byte"}; };
// bool: use int8_t/Byte for reliable blittable layout (C# bool in StructLayout
// defaults to 4-byte BOOL marshaling which would misalign the struct)
template <> struct ToWireType<bool>         { static constexpr WireTypeInfo info{"int8_t",   "Byte"}; };
template <> struct ToWireType<float>        { static constexpr WireTypeInfo info{"float",    "float"}; };
template <> struct ToWireType<double>       { static constexpr WireTypeInfo info{"double",   "double"}; };
template <> struct ToWireType<std::string>  { static constexpr WireTypeInfo info{"char*",    "IntPtr"}; };
template <> struct ToWireType<std::wstring> { static constexpr WireTypeInfo info{"wchar_t*", "IntPtr"}; };
template <> struct ToWireType<const char*>  { static constexpr WireTypeInfo info{"const char*", "IntPtr"}; };

// Enum classes: map to their underlying integer type
template <> struct ToWireType<orion::BookmarkFolderType> { static constexpr WireTypeInfo info{"int32_t", "Int32"}; };

// Unwrap StrongTypedef<T, Tag> to the same wire types as T
template <typename T, typename Tag>
struct ToWireType<orion::StrongTypedef<T, Tag>> : ToWireType<T> {};

// ============================================================
// Strong typedef detection
// ============================================================

// Extract the tag type name from __FUNCSIG__ without requiring a complete type.
// MSVC expands __FUNCSIG__ to e.g.:
//   "std::string __cdecl tag_cs_name<struct orion::OpaqueSimpleProfileId>(void)"
// We parse out "ProfileId" by stripping the "struct"/namespace prefix and
// the "OpaqueSimple" prefix that DECLARE_STRONG_TYPEDEF adds.
template <typename Tag>
std::string tag_cs_name()
{
    std::string_view sig = __FUNCSIG__;
    auto start = sig.find('<');
    auto end   = sig.rfind('>');
    if (start == std::string_view::npos || end == std::string_view::npos) return {};
    std::string raw(sig.substr(start + 1, end - start - 1));
    if (raw.starts_with("struct ")) raw = raw.substr(7);
    if (auto pos = raw.rfind("::"); pos != std::string::npos)
        raw = raw.substr(pos + 2);
    if (raw.starts_with("OpaqueSimple")) raw = raw.substr(12);
    return raw;
}

template <typename T>
struct StrongTypedefTraits {
    static constexpr bool is_strong = false;
};

template <typename T, typename Tag>
struct StrongTypedefTraits<orion::StrongTypedef<T, Tag>> {
    static constexpr bool is_strong = true;
    static std::string cs_name()      { return tag_cs_name<Tag>(); }
    static std::string_view cs_type() { return ToWireType<T>::info.cs_type; }
};

// ============================================================
// Field collection
// ============================================================

// Tracks a C# strong typedef to emit: e.g. {name="ProfileId", cs_type="Int64"}
struct RecordStructDef {
    std::string cs_type;
};

struct FieldDef {
    std::string name;
    std::string c_type;    // C++ wire type
    std::string cs_type;   // C# wire type
};

template <typename Fields, size_t... Is>
void collect_fields(
    std::index_sequence<Is...>,
    std::vector<FieldDef>& fields,
    std::map<std::string, RecordStructDef>& record_structs)
{
    ([&]() {
        using F = rfl::tuple_element_t<Is, Fields>;
        using T = typename F::Type;
        const auto& wire = ToWireType<T>::info;

        fields.push_back({
            std::string(F::name()),
            std::string(wire.c_type),
            std::string(wire.cs_type),
        });

        if constexpr (StrongTypedefTraits<T>::is_strong) {
            std::string cs_name = StrongTypedefTraits<T>::cs_name();
            if (!record_structs.contains(cs_name))
                record_structs[cs_name] = { std::string(StrongTypedefTraits<T>::cs_type()) };
        }
    }(), ...);
}

template <typename Dto>
void get_fields(
    std::vector<FieldDef>& fields,
    std::map<std::string, RecordStructDef>& record_structs)
{
    using NT     = rfl::named_tuple_t<Dto>;
    using Fields = typename NT::Fields;
    collect_fields<Fields>(
        std::make_index_sequence<rfl::tuple_size_v<Fields>>{},
        fields, record_structs);
}

// ============================================================
// Enum definitions (C# only — C++ already has the originals)
// ============================================================

struct EnumValueDef {
    std::string name;
    int value;
};

struct EnumDef {
    std::string name;
    std::vector<EnumValueDef> values;
    bool flags = false;  // emit [Flags] on the C# enum
};

// Reflect a C++ enum into an EnumDef using reflectcpp. C enums prefix each
// enumerator with the enum name (one shared enumerator namespace); strip
// that prefix so C# reads WebContextMenuCommand.Back.
template <typename E>
EnumDef reflect_enum(const char* cs_name, bool flags = false)
{
    constexpr auto entries = rfl::get_underlying_enumerator_array<E>();
    EnumDef def;
    def.name = cs_name;
    def.flags = flags;
    for (const auto& [name, value] : entries) {
        std::string cs_value_name{name};
        if (cs_value_name.starts_with(def.name))
            cs_value_name = cs_value_name.substr(def.name.size());
        def.values.push_back({std::move(cs_value_name), static_cast<int>(value)});
    }
    return def;
}

// ============================================================
// Output writers — write all structs into a single file each
// ============================================================

struct NativeStruct {
    std::string name;
    std::vector<FieldDef> fields;
    bool cs_only = false;  // C++ already has the original (plain C header)
};

void write_cpp_header(
    std::ostream& out,
    const std::vector<NativeStruct>& structs)
{
    out << "#pragma once\n"
           "// <auto-generated>\n"
           "// Generated from DTO definitions by OrionCodeGen.\n"
           "// Do not edit manually. Rebuild the project to regenerate.\n"
           "// </auto-generated>\n"
           "\n"
           "#include <cstdint>\n"
           "\n";

    for (const auto& s : structs) {
        if (s.cs_only) continue;
        out << "struct " << s.name << "\n{\n";
        for (const auto& f : s.fields)
            out << "    " << f.c_type << " " << f.name << ";\n";
        out << "};\n\n";
    }
}

void write_cs_file(
    std::ostream& out,
    const std::vector<NativeStruct>& structs,
    const std::map<std::string, RecordStructDef>& record_structs,
    const std::vector<EnumDef>& enums)
{
    out << "// <auto-generated>\n"
           "// Generated from DTO definitions by OrionCodeGen.\n"
           "// Do not edit manually. Rebuild the project to regenerate.\n"
           "// </auto-generated>\n"
           "\n"
           "using System;\n"
           "using System.Runtime.InteropServices;\n"
           "\n"
           "namespace Orion\n"
           "{\n";

    // Emit C# enums mirrored from the C++ originals
    for (const auto& e : enums) {
        if (e.flags)
            out << "    [Flags]\n";
        out << "    public enum " << e.name << "\n    {\n";
        for (size_t i = 0; i < e.values.size(); i++) {
            out << "        " << e.values[i].name << " = " << e.values[i].value;
            if (i + 1 < e.values.size()) out << ",";
            out << "\n";
        }
        out << "    }\n\n";
    }

    // Emit a C# record struct for each strong typedef encountered
    for (const auto& [name, def] : record_structs)
        out << "    public readonly record struct " << name
            << "(" << def.cs_type << " Value);\n";

    if (!record_structs.empty())
        out << "\n";

    for (const auto& s : structs) {
        out << "    [StructLayout(LayoutKind.Sequential)]\n"
            << "    internal struct " << s.name << "\n"
            << "    {\n";
        for (const auto& f : s.fields)
            out << "        public " << f.cs_type << " " << f.name << ";\n";
        out << "    }\n\n";
    }

    out << "}\n";
}

// ============================================================
// Entry point
// ============================================================

int main(int argc, char* argv[])
{
    if (argc < 2) {
        std::cerr << "Usage: OrionCodeGen.exe <solution_dir>\n";
        return 1;
    }

    std::string sol_dir = argv[1];
    for (char& c : sol_dir) { if (c == '\\') c = '/'; }
    if (!sol_dir.empty() && sol_dir.back() != '/') sol_dir += '/';

    std::map<std::string, RecordStructDef> record_structs; // shared — deduplicates across DTOs
    std::vector<NativeStruct> structs;
    std::vector<EnumDef> enums;

    // ---- Enums mirrored from orion-linux (reflected automatically) ----
    enums.push_back(reflect_enum<orion::settings::TabLayout>("TabLayout"));
    enums.push_back(reflect_enum<orion::settings::SidebarPage>("SidebarPage"));
    enums.push_back(reflect_enum<orion::settings::SearchEngine>("SearchEngine"));

    // ---- Context menu types (OrionBackend/context_menu_types.h) ----
    enums.push_back(reflect_enum<WebContextMenuTypeFlags>("WebContextMenuTypeFlags", /*flags=*/true));
    enums.push_back(reflect_enum<WebContextMenuEditFlags>("WebContextMenuEditFlags", /*flags=*/true));
    enums.push_back(reflect_enum<WebContextMenuMediaType>("WebContextMenuMediaType"));
    enums.push_back(reflect_enum<WebContextMenuCommand>("WebContextMenuCommand"));

    // ---- Media permission types (OrionBackend/media_permission_types.h) ----
    enums.push_back(reflect_enum<WebMediaPermission>("WebMediaPermission", /*flags=*/true));

    {
        NativeStruct s; s.name = "HistoryEntryNative";
        get_fields<orion::HistoryEntryDto>(s.fields, record_structs);
        structs.push_back(std::move(s));
    }
    {
        NativeStruct s; s.name = "BookmarkFolderNative";
        get_fields<orion::BookmarkFolderDto>(s.fields, record_structs);
        structs.push_back(std::move(s));
    }
    {
        NativeStruct s; s.name = "BookmarkNative";
        get_fields<orion::BookmarkDto>(s.fields, record_structs);
        structs.push_back(std::move(s));
    }
    {
        NativeStruct s; s.name = "ReadingListEntryNative";
        get_fields<orion::ReadingListEntryDto>(s.fields, record_structs);
        structs.push_back(std::move(s));
    }
    {
        // C# mirror of ContextMenuRequest; C++ uses the C struct directly.
        NativeStruct s; s.name = "ContextMenuRequestNative"; s.cs_only = true;
        get_fields<ContextMenuRequest>(s.fields, record_structs);
        structs.push_back(std::move(s));
    }

    // ---- Write OrionBackend/generated/native_types.h ----
    {
        std::string dir  = sol_dir + "OrionBackend/generated";
        std::string path = dir + "/native_types.h";
        _mkdir(dir.c_str());
        std::ofstream out(path);
        if (!out) { std::cerr << "OrionCodeGen: cannot write " << path << "\n"; return 1; }
        write_cpp_header(out, structs);
    }

    // ---- Write Orion/Interop/Generated/NativeTypes.cs ----
    {
        std::string dir  = sol_dir + "Orion/Interop/Generated";
        std::string path = dir + "/NativeTypes.cs";
        _mkdir(dir.c_str());
        std::ofstream out(path);
        if (!out) { std::cerr << "OrionCodeGen: cannot write " << path << "\n"; return 1; }
        write_cs_file(out, structs, record_structs, enums);
    }

    std::cout << "OrionCodeGen: generated native_types.h and NativeTypes.cs\n";
    return 0;
}
