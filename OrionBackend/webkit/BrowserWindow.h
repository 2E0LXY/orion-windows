/*
 * Copyright (C) 2018 Sony Interactive Entertainment Inc.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions
 * are met:
 * 1. Redistributions of source code must retain the above copyright
 *    notice, this list of conditions and the following disclaimer.
 * 2. Redistributions in binary form must reproduce the above copyright
 *    notice, this list of conditions and the following disclaimer in the
 *    documentation and/or other materials provided with the distribution.
 *
 * THIS SOFTWARE IS PROVIDED BY APPLE INC. ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
 * PURPOSE ARE DISCLAIMED.  IN NO EVENT SHALL APPLE INC. OR
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
 * EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
 * PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
 * PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY
 * OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

#pragma once

#include "WebkitCommon.h"
#include "../context_menu_types.h"

#include <memory>
#include <windows.h>

class BrowserWindowClient {
public:
    virtual void progressChanged(double) = 0;
    virtual void loadingStateChanged(bool isLoading) = 0;
    virtual void activeURLChanged(std::wstring) = 0;
    virtual void titleChanged(std::wstring) = 0;
    virtual bool unhandledKeyEvent(uint32_t vkCode, uint32_t modifiers) { return false; }
    virtual bool reservedKeyEvent(uint32_t vkCode, uint32_t modifiers) { return false; }
    virtual void faviconUrlChanged(std::wstring pageUrl, std::wstring iconUrls) {}
    virtual std::wstring navigationError(int32_t category, int32_t rawCode,
        std::wstring failingUrl, std::wstring description) { return {}; }
    virtual void webProcessTerminated(const char* processName, uint32_t reason, const char* reasonString) {}
    virtual void inputFocus(std::string json) {}
    virtual void formSubmit(std::string json) {}
    virtual void elementFullscreenChanged(bool isFullscreen) {}
    // Returns nonzero if the client shows its own menu (the engine's native
    // menu is suppressed), 0 for the engine default.
    virtual int32_t contextMenuRequested(const ContextMenuRequest&) { return 0; }
    // matchCount: 0 = no matches; negative = more than |matchCount| matches.
    // activeMatchOrdinal: 1-based index of the current match, 0 = unknown.
    virtual void findResult(int32_t matchCount, int32_t activeMatchOrdinal) {}
    // Result of a takeSnapshot call: encoded JPEG bytes of the visible
    // viewport, or nullptr/0 on failure. data is only valid for the call.
    virtual void snapshotTaken(const uint8_t* data, int32_t size) {}
    // Page-initiated close (window.close()) the page was allowed to make;
    // the embedder closes the hosting tab or popup window.
    virtual void closeRequested() {}
    // HTTP auth challenge (401, or 407 when isProxy). Returns true when the
    // client took ownership (it must resolve requestId exactly once via
    // respondToWebKitAuthChallenge); false = unhandled, the caller continues
    // the challenge credential-less.
    virtual bool authCredentialsRequested(std::wstring host, int32_t port,
        bool isProxy, std::wstring realm, int64_t requestId) { return false; }
    virtual void navigationStateChanged(bool canGoBack, bool canGoForward) {}
    // The Web Inspector frontend view appeared (a hidden child HWND for the
    // client to host; repeated when it asks to be brought to the front) or
    // closed (nullptr).
    virtual void devToolsViewChanged(HWND devToolsHwnd) {}
    virtual int64_t profileId() const { return 0; }
};

class BrowserWindow : public std::enable_shared_from_this<BrowserWindow> {
public:
    enum class FeatureType { Experimental, InternalDebug };

    virtual ~BrowserWindow() { };

    virtual HRESULT init() = 0;
    virtual HWND hwnd() = 0;

    virtual HRESULT loadURL(const std::wstring& url) = 0;
    virtual void reload() = 0;
    virtual void reloadBypassingCache() = 0;
    virtual void navigateForwardOrBackward(bool forward) = 0;
    virtual void navigateToHistory(UINT menuID) = 0;
    virtual void setPreference(UINT menuID, bool enable) = 0;
    virtual bool usesLayeredWebView() const { return false; }

    virtual void resetFeatureMenu(FeatureType, HMENU, bool resetsSettingsToDefaults = false) = 0;

    virtual void print() = 0;
    virtual void launchInspector() = 0;
    virtual void openProxySettings() = 0;

    virtual std::wstring userAgent() = 0;
    virtual void setUserAgent(const std::wstring& customUAString) = 0;

    virtual void showLayerTree() = 0;
    virtual void updateStatistics(HWND dialog) = 0;

    virtual void resetZoom() = 0;
    virtual void zoomIn() = 0;
    virtual void zoomOut() = 0;

    virtual void clearCookies() = 0;
    virtual void clearWebsiteData() = 0;

    virtual void adjustScaleFactors() = 0;
};
