#pragma once

// Media-capture permission types shared across the P/Invoke boundary.
// Plain C: this header defines the wire ABI directly. OrionCodeGen reflects
// the enum into C# (Orion/Interop/Generated/NativeTypes.cs); the generator
// strips the enum-name prefix, so C# reads WebMediaPermission.Camera.

// What a page asked to capture (bitmask). Camera/Microphone are device
// capture (getUserMedia); ScreenShare is display capture (getDisplayMedia),
// covering both the picture and any system-audio component.
enum WebMediaPermission {
    WebMediaPermissionNone        = 0,
    WebMediaPermissionCamera      = 1 << 0,
    WebMediaPermissionMicrophone  = 1 << 1,
    WebMediaPermissionScreenShare = 1 << 2,
};
