#include "pch.h"
#include "engine_impl.h"

#include <commctrl.h>
#pragma comment(lib, "comctl32.lib")

#include "webkit/BrowserWindow.h"
#include "webkit/WebKitBrowserWindow.h"
#include "utils.h"
#include <src/log.h>

int checkBrowserSubprocess() { return -1; }
HANDLE getCefWakeupEventHandle() { return nullptr; }
void pumpCefMessageLoop() {}

class BrowserTabContainer : public BrowserWindowClient
{
private:
	HWND m_hwnd = 0;
	BrowserCallbacks m_callbacks = { 0 };
	int64_t m_profileId = 0;
public:
    BrowserTabContainer() {};
    ~BrowserTabContainer() {};

    void progressChanged(double) override;
    void loadingStateChanged(bool isLoading) override;
    void activeURLChanged(std::wstring url) override;
    void titleChanged(std::wstring title) override;
    bool unhandledKeyEvent(uint32_t vkCode, uint32_t modifiers) override;
    bool reservedKeyEvent(uint32_t vkCode, uint32_t modifiers) override;
    void faviconUrlChanged(std::wstring pageUrl, std::wstring iconUrls) override;
    std::wstring navigationError(int32_t category, int32_t rawCode,
        std::wstring failingUrl, std::wstring description) override;
    void webProcessTerminated(const char* processName, uint32_t reason, const char* reasonString) override;
    void inputFocus(std::string json) override;
    void formSubmit(std::string json) override;
    void elementFullscreenChanged(bool isFullscreen) override;
    int32_t contextMenuRequested(const ContextMenuRequest& request) override;
    void findResult(int32_t matchCount, int32_t activeMatchOrdinal) override;
    void snapshotTaken(const uint8_t* data, int32_t size) override;
    void closeRequested() override;
    bool authCredentialsRequested(std::wstring host, int32_t port,
        bool isProxy, std::wstring realm, int64_t requestId) override;
    void navigationStateChanged(bool canGoBack, bool canGoForward) override;
    void devToolsViewChanged(HWND devToolsHwnd) override;

	void setHwnd(HWND hwnd) { m_hwnd = hwnd; }
	void setCallbacks(BrowserCallbacks callbacks) { m_callbacks = callbacks; }
	void setProfileId(int64_t id) { m_profileId = id; }
	int64_t profileId() const { return m_profileId; }
};

void BrowserTabContainer::progressChanged(double progress)
{
    if (m_callbacks.progressChanged)
		m_callbacks.progressChanged(progress);
}
void BrowserTabContainer::loadingStateChanged(bool isLoading)
{
    if (m_callbacks.loadingStateChanged)
        m_callbacks.loadingStateChanged(isLoading ? 1 : 0);
}
void BrowserTabContainer::activeURLChanged(std::wstring url)
{
    if (m_callbacks.activeURLChanged)
        m_callbacks.activeURLChanged(url.c_str());
}

void BrowserTabContainer::titleChanged(std::wstring title)
{
    if (m_callbacks.titleChanged)
        m_callbacks.titleChanged(title.c_str());
}

bool BrowserTabContainer::unhandledKeyEvent(uint32_t vkCode, uint32_t modifiers)
{
    if (m_callbacks.unhandledKeyEvent)
        return m_callbacks.unhandledKeyEvent(vkCode, modifiers) != 0;
    return false;
}

bool BrowserTabContainer::reservedKeyEvent(uint32_t vkCode, uint32_t modifiers)
{
    if (m_callbacks.reservedKeyEvent)
        return m_callbacks.reservedKeyEvent(vkCode, modifiers) != 0;
    return false;
}

void BrowserTabContainer::faviconUrlChanged(std::wstring pageUrl, std::wstring iconUrls)
{
    if (m_callbacks.faviconUrlChanged)
        m_callbacks.faviconUrlChanged(pageUrl.c_str(), iconUrls.c_str());
}

std::wstring BrowserTabContainer::navigationError(int32_t category, int32_t rawCode,
    std::wstring failingUrl, std::wstring description)
{
    if (!m_callbacks.navigationError)
        return {};
    wchar_t* html = m_callbacks.navigationError(category, rawCode,
        failingUrl.c_str(), description.c_str());
    if (!html)
        return {};
    std::wstring result(html);
    CoTaskMemFree(html);
    return result;
}

void BrowserTabContainer::webProcessTerminated(const char* processName, uint32_t reason, const char* reasonString)
{
    if (m_callbacks.webProcessTerminated)
        m_callbacks.webProcessTerminated(processName, reason, reasonString);
}

void BrowserTabContainer::inputFocus(std::string json)
{
    if (m_callbacks.inputFocus)
        m_callbacks.inputFocus(json.c_str());
}

void BrowserTabContainer::formSubmit(std::string json)
{
    if (m_callbacks.formSubmit)
        m_callbacks.formSubmit(json.c_str());
}

void BrowserTabContainer::elementFullscreenChanged(bool isFullscreen)
{
    if (m_callbacks.elementFullscreenChanged)
        m_callbacks.elementFullscreenChanged(isFullscreen ? 1 : 0);
}

int32_t BrowserTabContainer::contextMenuRequested(const ContextMenuRequest& request)
{
    if (m_callbacks.contextMenuRequested)
        return m_callbacks.contextMenuRequested(&request);
    return 0;
}

void BrowserTabContainer::findResult(int32_t matchCount, int32_t activeMatchOrdinal)
{
    if (m_callbacks.findResult)
        m_callbacks.findResult(matchCount, activeMatchOrdinal);
}

void BrowserTabContainer::snapshotTaken(const uint8_t* data, int32_t size)
{
    if (m_callbacks.snapshotTaken)
        m_callbacks.snapshotTaken(data, size);
}

void BrowserTabContainer::navigationStateChanged(bool canGoBack, bool canGoForward)
{
    if (m_callbacks.navigationStateChanged)
        m_callbacks.navigationStateChanged(canGoBack ? 1 : 0, canGoForward ? 1 : 0);
}

void BrowserTabContainer::devToolsViewChanged(HWND devToolsHwnd)
{
    if (m_callbacks.devToolsViewChanged)
        m_callbacks.devToolsViewChanged(devToolsHwnd);
}

void BrowserTabContainer::closeRequested()
{
    if (m_callbacks.closeRequested)
        m_callbacks.closeRequested();
    else
        orion::log_warning("closeRequested: no callback wired; window.close() ignored");
}

bool BrowserTabContainer::authCredentialsRequested(std::wstring host, int32_t port,
    bool isProxy, std::wstring realm, int64_t requestId)
{
    if (!m_callbacks.authCredentialsRequested)
        return false;
    m_callbacks.authCredentialsRequested(
        host.c_str(), port, isProxy ? 1 : 0, realm.c_str(), requestId);
    return true;
}


// Subclass proc that intercepts reserved browser shortcuts before WebKit processes them.
// Reserved keys (e.g. Ctrl+T, Ctrl+W) are handled directly by the browser and never
// forwarded to the web page, matching Chromium's PreHandleKeyboardEvent pattern.
static LRESULT CALLBACK ReservedKeySubclassProc(
    HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam,
    UINT_PTR uIdSubclass, DWORD_PTR dwRefData)
{
    if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
    {
        auto* container = reinterpret_cast<BrowserTabContainer*>(dwRefData);
        uint32_t vkCode = static_cast<uint32_t>(wParam);
        uint32_t modifiers = toVirtualKeyModifiers(
            GetKeyState(VK_CONTROL) & 0x8000,
            GetKeyState(VK_MENU)    & 0x8000,
            GetKeyState(VK_SHIFT)   & 0x8000);
        if (container->reservedKeyEvent(vkCode, modifiers))
            return 0;
    }
    if (msg == WM_NCDESTROY)
        RemoveWindowSubclass(hwnd, ReservedKeySubclassProc, uIdSubclass);
    return DefSubclassProc(hwnd, msg, wParam, lParam);
}


// Minimal child window procedure
LRESULT CALLBACK ChildWndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    switch (msg)
    {
    case WM_DESTROY:
        return 0;
    default:
        return DefWindowProc(hwnd, msg, wParam, lParam);
    }
}

std::list<BrowserTabContainer> temp_containers;
std::unordered_map<HWND, std::shared_ptr<BrowserWindow>> temp_browserWindows;

// Effective UI appearance for prefers-color-scheme; swept over live views on
// change and applied to each new view at creation.
static bool g_useDarkAppearance = false;

// Creates a WKView for a popup using the supplied WKPageConfiguration so
// window.opener linkage, same-origin, and the shared process pool survive.
// Registers the view in temp_browserWindows just like createBrowserView.
// Used by WebKitBrowserWindow::createNewPage.
HWND createBrowserViewFromPageConf(HWND parentHwnd, WKPageConfigurationRef pageConf, BrowserCallbacks callbacks)
{
    BrowserTabContainer& container = temp_containers.emplace_back();
    auto browserWindow = std::shared_ptr<BrowserWindow>(
        new WebKitBrowserWindow(container, pageConf, parentHwnd));
    HWND hwnd = browserWindow->hwnd();
    container.setHwnd(hwnd);
    container.setCallbacks(callbacks);
    temp_browserWindows.emplace(hwnd, browserWindow);
    SetWindowSubclass(hwnd, ReservedKeySubclassProc, 0, reinterpret_cast<DWORD_PTR>(&container));
    static_cast<WebKitBrowserWindow*>(browserWindow.get())->setUseDarkAppearance(g_useDarkAppearance);
    return hwnd;
}

HWND createBrowserView(HWND hwndParent, BrowserCallbacks callbacks, int64_t profileId, const char* /*initialUrl*/)
{
    std::filesystem::path profilePath;
    if (profileId > 0)
        profilePath = getProfileDataPath(profileId, EngineId::WebKit);

    orion::log_info("WebKit createBrowserView: profileId=%lld, profilePath=%s",
        (long long)profileId, profilePath.empty() ? "(default)" : profilePath.string().c_str());

    BrowserTabContainer& container = temp_containers.emplace_back();
    auto browserWindow = WebKitBrowserWindow::create(container, hwndParent, false, profilePath);
    HWND hwnd = browserWindow->hwnd();
    container.setHwnd(hwnd);
    container.setCallbacks(callbacks);
    container.setProfileId(profileId);
    temp_browserWindows.emplace(hwnd, browserWindow);

    // Intercept reserved shortcuts before WebKit processes them.
    // BrowserTabContainer lives in std::list (stable address).
    SetWindowSubclass(hwnd, ReservedKeySubclassProc, 0, reinterpret_cast<DWORD_PTR>(&container));

    static_cast<WebKitBrowserWindow*>(browserWindow.get())->setUseDarkAppearance(g_useDarkAppearance);

    return hwnd;
}

// Look up a WKPageRef from a registered view HWND (used by createNewPage
// to hand the page back to WebKit).  Returns a +1-retained WKPageRef or
// nullptr if the HWND isn't known.  Caller takes ownership of the retain.
WKPageRef retainPageFromViewHwnd(HWND hwnd)
{
    auto it = temp_browserWindows.find(hwnd);
    if (it == temp_browserWindows.end()) return nullptr;
    // webkit.cpp only registers WebKitBrowserWindow instances in
    // temp_browserWindows, so the static cast is safe.
    auto* wkbw = static_cast<WebKitBrowserWindow*>(it->second.get());
    WKPageRef page = wkbw->pageRef();
    if (page) WKRetain(page);
    return page;
}

// Reverse lookup: the registered window that owns a WKPageRef. Returns
// nullptr when no live window matches (e.g. a script message from a window
// destroyed while the message was in flight). Used by script message
// handlers on a user content controller shared between windows (window.open
// popups share their opener's), which therefore cannot bake a window pointer
// into their registration context — see onInputFocusMessage.
WebKitBrowserWindow* browserWindowForPage(WKPageRef page)
{
    if (!page)
        return nullptr;
    for (auto& [hwnd, window] : temp_browserWindows) {
        auto* wkbw = static_cast<WebKitBrowserWindow*>(window.get());
        if (wkbw->pageRef() == page)
            return wkbw;
    }
    return nullptr;
}


void destroyBrowserView(HWND hwnd)
{
    auto it = temp_browserWindows.find(hwnd);
    if (it != temp_browserWindows.end())
    {
        // Continue (credential-less) any auth challenge still waiting on a
        // dialog for this view, so its network request resolves.
        cancelWebKitAuthChallengesForView(hwnd);
        DestroyWindow(hwnd);
        temp_browserWindows.erase(it);
    }
}


void cleanBrowserEngine()
{
    cancelAllWebKitAuthChallenges();
    for (auto& [hwnd, browserWindow] : temp_browserWindows)
    {
        DestroyWindow(hwnd);
    }
    temp_browserWindows.clear();
    clearWebKitProfileCache();
}

HRESULT loadURL(HWND hwnd, const std::string& url)
{
    if (auto* win = get_if(temp_browserWindows, hwnd))
    {
        return (*win)->loadURL(widen(url));
    }
    return E_HANDLE;
}

void loadHTMLString(HWND hwnd, const std::string& html, const std::string& unreachableUrl)
{
    auto it = temp_browserWindows.find(hwnd);
    if (it == temp_browserWindows.end()) return;
    auto* wkbw = static_cast<WebKitBrowserWindow*>(it->second.get());
    wkbw->loadHTMLString(widen(html), widen(unreachableUrl));
}

void reload(HWND hwnd)
{
    if (auto* win = get_if(temp_browserWindows, hwnd))
    {
        (*win)->reload();
    }
}

void reloadBypassingCache(HWND hwnd)
{
    if (auto* win = get_if(temp_browserWindows, hwnd))
    {
        (*win)->reloadBypassingCache();
    }
}

void forward(HWND hwnd)
{
    if (auto* win = get_if(temp_browserWindows, hwnd))
    {
        (*win)->navigateForwardOrBackward(true);
    }
}

void backward(HWND hwnd)
{
    if (auto* win = get_if(temp_browserWindows, hwnd))
    {
        (*win)->navigateForwardOrBackward(false);
    }
}

void setMinimumFontSize(int32_t size)
{
    setWebKitMinimumFontSize(size > 0 ? static_cast<uint32_t>(size) : 0);
}

void setJavaScriptEnabled(bool enabled)
{
    setWebKitJavaScriptEnabled(enabled);
}

void setNativePopupBlocking(bool /*enabled*/)
{
    // WebKit has no built-in popup blocker — every window.open already
    // reaches the CreatePopupCallback gate.
}

void setCustomUserAgent(HWND hwnd, const wchar_t* userAgent)
{
    auto it = temp_browserWindows.find(hwnd);
    if (it == temp_browserWindows.end()) {
        orion::log_info("setCustomUserAgent: hwnd=%p NOT FOUND in temp_browserWindows (size=%zu)",
            hwnd, temp_browserWindows.size());
        return;
    }
    orion::log_info("setCustomUserAgent: hwnd=%p ua='%ls'",
        hwnd, userAgent ? userAgent : L"(null)");
    it->second->setUserAgent(userAgent ? userAgent : L"");
}

void setDefaultUserAgent(const wchar_t* userAgent)
{
    setWebKitDefaultUserAgent(userAgent ? userAgent : L"");
}

void startDownload(const char* url)
{
    for (auto& [hwnd, window] : temp_browserWindows) {
        auto* wkbw = static_cast<WebKitBrowserWindow*>(window.get());
        auto wkUrl = adoptWK(WKURLCreateWithUTF8CString(url));
        WKPageLoadURL(wkbw->pageRef(), wkUrl.get());
        return;
    }
}

void setUseDarkAppearance(bool useDarkAppearance)
{
    g_useDarkAppearance = useDarkAppearance;
    for (auto& [hwnd, window] : temp_browserWindows)
        static_cast<WebKitBrowserWindow*>(window.get())->setUseDarkAppearance(useDarkAppearance);
}

// The Windows WebKit build has ENABLE_MEDIA_STREAM off (no capture backend),
// so no view ever raises mediaAccessPermissionRequested — nothing to resolve.
void respondToMediaAccessPermission(int64_t /*requestId*/, bool /*allow*/) {}

void respondToAuthCredentials(int64_t requestId, bool allow,
    const wchar_t* username, const wchar_t* password)
{
    respondToWebKitAuthChallenge(requestId, allow, username, password);
}

void executeJavaScript(HWND hwnd, const char* script)
{
    auto it = temp_browserWindows.find(hwnd);
    if (it == temp_browserWindows.end()) return;
    auto* wkbw = static_cast<WebKitBrowserWindow*>(it->second.get());
    auto wkScript = adoptWK(WKStringCreateWithUTF8CString(script));
    WKPageEvaluateJavaScriptInMainFrame(wkbw->pageRef(), wkScript.get(), nullptr, nullptr);
}

void requestExitElementFullscreen(HWND hwnd)
{
    auto it = temp_browserWindows.find(hwnd);
    if (it == temp_browserWindows.end()) return;
    auto* wkbw = static_cast<WebKitBrowserWindow*>(it->second.get());
    if (WKPageRef page = wkbw->pageRef())
        WKPageRequestExitFullScreen(page);
}

void executeContextMenuCommand(HWND hwnd, WebContextMenuCommand command, const char* arg, int32_t /*x*/, int32_t /*y*/)
{
    auto it = temp_browserWindows.find(hwnd);
    if (it == temp_browserWindows.end()) return;
    static_cast<WebKitBrowserWindow*>(it->second.get())->executeContextMenuCommand(command, arg);
}

// The C API opens the inspector as a whole; there is no inspect-at-point.
void showDevTools(HWND hwnd, int32_t /*x*/, int32_t /*y*/)
{
    auto it = temp_browserWindows.find(hwnd);
    if (it == temp_browserWindows.end()) return;
    it->second->launchInspector();
}

void closeDevTools(HWND hwnd)
{
    auto it = temp_browserWindows.find(hwnd);
    if (it == temp_browserWindows.end()) return;
    static_cast<WebKitBrowserWindow*>(it->second.get())->closeInspector();
}

void findInPage(HWND hwnd, const char* text, bool forward, bool findNext)
{
    auto it = temp_browserWindows.find(hwnd);
    if (it == temp_browserWindows.end()) return;
    static_cast<WebKitBrowserWindow*>(it->second.get())->findInPage(text, forward, findNext);
}

void stopFindInPage(HWND hwnd, bool /*clearSelection*/)
{
    // WKPageHideFindUI has no clear-selection knob; like macOS, the selection
    // stays at the last match.
    auto it = temp_browserWindows.find(hwnd);
    if (it == temp_browserWindows.end()) return;
    static_cast<WebKitBrowserWindow*>(it->second.get())->stopFindInPage();
}

void requestTabSnapshot(HWND hwnd, int32_t maxPixelWidth)
{
    auto it = temp_browserWindows.find(hwnd);
    if (it == temp_browserWindows.end()) return;
    static_cast<WebKitBrowserWindow*>(it->second.get())->takeSnapshot(maxPixelWidth);
}
