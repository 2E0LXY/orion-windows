/*
 * WKBase.h - IntelliSense-friendly wrapper for WebKit C API types
 *
 * This header provides real type definitions for IntelliSense navigation,
 * while falling back to the original opaque types for actual compilation.
 */

#ifndef WKBase_Wrapper_h
#define WKBase_Wrapper_h

#ifndef __INTELLISENSE__
// Normal compilation: use the real WKBase.h with opaque types
#include "WKBase_impl.h"
#else
// IntelliSense: define types using real implementation classes

#include <WebKit/WKDeclarationSpecifiers.h>
#include <stdint.h>

#if defined(BUILDING_GTK__)
#include <WebKit/WKBaseGtk.h>
#elif defined(BUILDING_WPE__)
#include <WebKit/WKBaseWPE.h>
#elif defined(__APPLE__)
#include <WebKit/WKBaseMac.h>
#elif defined(_WIN32)
#include <WebKit/WKBaseWin.h>
#elif defined(__SCE__)
#include <WebKit/WKBasePlayStation.h>
#endif

// Forward declarations - API namespace
namespace API {
    class Array;
    class Boolean;
    class CaptionUserPreferencesTestingModeToken;
    class CompletionListener;
    class ContentRuleList;
    class ContentRuleListStore;
    class Data;
    class Dictionary;
    class Double;
    class Error;
    class Feature;
    class FrameHandle;
    class FrameInfo;
    class HitTestResult;
    class HTTPCookieStore;
    class JSHandle;
    class MessageListener;
    class Navigation;
    class NavigationAction;
    class NavigationData;
    class NavigationResponse;
    class Object;
    class OpenPanelParameters;
    class PageConfiguration;
    class Point;
    class ProcessPoolConfiguration;
    class Rect;
    class ScriptMessage;
    class SecurityOrigin;
    class SessionState;
    class Size;
    class String;
    class UInt64;
    class URL;
    class URLRequest;
    class URLResponse;
    class UserContentURLPattern;
    class UserScript;
    class WebArchive;
    class WebArchiveResource;
    class WebsitePolicies;
    class WindowFeatures;
}

// Forward declarations - WebKit namespace
namespace WebKit {
    class AuthenticationChallengeProxy;
    class AuthenticationDecisionListener;
    class DownloadProxy;
    class GeolocationPermissionRequest;
    class InjectedBundle;
    class InjectedBundleBackForwardList;
    class InjectedBundleBackForwardListItem;
    class InjectedBundleCSSStyleDeclarationHandle;
    class InjectedBundleDOMWindowExtension;
    class InjectedBundleHitTestResult;
    class InjectedBundleNodeHandle;
    class InjectedBundleRangeHandle;
    class InjectedBundleScriptWorld;
    class MediaKeySystemPermissionCallback;
    class NotificationPermissionRequest;
    class PageBanner;
    class QueryPermissionResultCallback;
    class RunBeforeUnloadConfirmPanelResultListener;
    class RunJavaScriptAlertResultListener;
    class RunJavaScriptConfirmResultListener;
    class RunJavaScriptPromptResultListener;
    class RequestStorageAccessConfirmResultListener;
    class SpeechRecognitionPermissionCallback;
    class UserMediaPermissionCheckProxy;
    class UserMediaPermissionRequestProxy;
    class WebBackForwardList;
    class WebBackForwardListItem;
    class WebColorPickerResultListenerProxy;
    class WebContextMenuListenerProxy;
    class WebContextMenuItem;
    class WebCredential;
    class WebFormSubmissionListenerProxy;
    class WebFrame;
    class WebFramePolicyListenerProxy;
    class WebFrameProxy;
    class WebGeolocationManagerProxy;
    class WebGeolocationPosition;
    class WebIconDatabase;
    class WebImage;
    class WebInspectorUIProxy;
    class WebNotification;
    class WebNotificationManagerProxy;
    class WebNotificationProvider;
    class WebOpenPanelResultListenerProxy;
    class WebPage;
    class WebPageGroup;
    class WebPageOverlay;
    class WebPageProxy;
    class WebPreferences;
    class WebProcessPool;
    class WebProtectionSpace;
    class WebResourceLoadStatisticsManager;
    class WebsiteDataStore;
    class WebsiteDataStoreConfiguration;
    class WebTextChecker;
    class WebUserContentControllerProxy;
    class WebView;
    class WebViewportAttributes;
}

/* WebKit2 shared types */

typedef uint32_t WKTypeID;
typedef const API::Object* WKTypeRef;

typedef const API::Array* WKArrayRef;
typedef API::Array* WKMutableArrayRef;

typedef const API::Dictionary* WKDictionaryRef;
typedef API::Dictionary* WKMutableDictionaryRef;

typedef const API::Boolean* WKBooleanRef;
typedef const void* WKCertificateInfoRef; // Platform-specific, no common mapping
typedef const WebKit::WebContextMenuItem* WKContextMenuItemRef;
typedef const API::Data* WKDataRef;
typedef const API::Double* WKDoubleRef;
typedef const API::Error* WKErrorRef;
typedef const void* WKGraphicsContextRef; // Platform-specific
typedef const WebKit::WebImage* WKImageRef;
typedef const API::Point* WKPointRef;
typedef const API::Rect* WKRectRef;
typedef const API::SecurityOrigin* WKSecurityOriginRef;
typedef const void* WKSerializedScriptValueRef; // Internal type
typedef const API::Size* WKSizeRef;
typedef const API::String* WKStringRef;
typedef const API::UInt64* WKUInt64Ref;
typedef const API::URL* WKURLRef;
typedef const API::URLRequest* WKURLRequestRef;
typedef const API::URLResponse* WKURLResponseRef;
typedef const API::UserContentURLPattern* WKUserContentURLPatternRef;
typedef const API::WebArchive* WKWebArchiveRef;
typedef const API::WebArchiveResource* WKWebArchiveResourceRef;

/* WebKit2 main API types */

typedef const WebKit::AuthenticationChallengeProxy* WKAuthenticationChallengeRef;
typedef const WebKit::AuthenticationDecisionListener* WKAuthenticationDecisionListenerRef;
typedef const WebKit::WebBackForwardList* WKBackForwardListRef;
typedef const WebKit::WebBackForwardListItem* WKBackForwardListItemRef;
typedef const void* WKResourceCacheManagerRef; // Deprecated
typedef const API::CaptionUserPreferencesTestingModeToken* WKCaptionUserPreferencesTestingModeTokenRef;
typedef const WebKit::WebColorPickerResultListenerProxy* WKColorPickerResultListenerRef;
typedef const WebKit::WebProcessPool* WKContextRef;
typedef const API::ProcessPoolConfiguration* WKContextConfigurationRef;
typedef const WebKit::WebContextMenuListenerProxy* WKContextMenuListenerRef;
typedef const void* WKCookieManagerRef; // Internal
typedef const WebKit::WebCredential* WKCredentialRef;
typedef const WebKit::DownloadProxy* WKDownloadRef;
typedef const API::Feature* WKFeatureRef;
typedef const WebKit::WebFormSubmissionListenerProxy* WKFormSubmissionListenerRef;
typedef const API::FrameHandle* WKFrameHandleRef;
typedef const API::FrameInfo* WKFrameInfoRef;
typedef const WebKit::WebFrameProxy* WKFrameRef;
typedef const WebKit::WebFramePolicyListenerProxy* WKFramePolicyListenerRef;
typedef const WebKit::WebGeolocationManagerProxy* WKGeolocationManagerRef;
typedef const WebKit::GeolocationPermissionRequest* WKGeolocationPermissionRequestRef;
typedef const WebKit::WebGeolocationPosition* WKGeolocationPositionRef;
typedef const void* WKGrammarDetailRef; // Platform-specific
typedef const API::HTTPCookieStore* WKHTTPCookieStoreRef;
typedef const API::HitTestResult* WKHitTestResultRef;
typedef const WebKit::WebIconDatabase* WKIconDatabaseRef;
typedef const WebKit::WebInspectorUIProxy* WKInspectorRef;
typedef const void* WKInternalDebugFeatureRef; // Internal
typedef const API::JSHandle* WKJSHandleRef;
typedef const void* WKKeyValueStorageManagerRef; // Internal
typedef const API::MessageListener* WKMessageListenerRef;
typedef const API::NavigationAction* WKNavigationActionRef;
typedef const API::NavigationData* WKNavigationDataRef;
typedef const API::Navigation* WKNavigationRef;
typedef const API::NavigationResponse* WKNavigationResponseRef;
typedef const WebKit::WebNotification* WKNotificationRef;
typedef const WebKit::WebNotificationManagerProxy* WKNotificationManagerRef;
typedef const WebKit::NotificationPermissionRequest* WKNotificationPermissionRequestRef;
typedef const WebKit::WebNotificationProvider* WKNotificationProviderRef;
typedef const API::OpenPanelParameters* WKOpenPanelParametersRef;
typedef const WebKit::WebOpenPanelResultListenerProxy* WKOpenPanelResultListenerRef;
typedef const WebKit::WebPageProxy* WKPageRef;
typedef const API::PageConfiguration* WKPageConfigurationRef;
typedef const WebKit::WebPageGroup* WKPageGroupRef;
typedef const WebKit::WebPreferences* WKPreferencesRef;
typedef const WebKit::WebProtectionSpace* WKProtectionSpaceRef;
typedef const WebKit::RunBeforeUnloadConfirmPanelResultListener* WKPageRunBeforeUnloadConfirmPanelResultListenerRef;
typedef const WebKit::RunJavaScriptAlertResultListener* WKPageRunJavaScriptAlertResultListenerRef;
typedef const WebKit::RunJavaScriptConfirmResultListener* WKPageRunJavaScriptConfirmResultListenerRef;
typedef const WebKit::RunJavaScriptPromptResultListener* WKPageRunJavaScriptPromptResultListenerRef;
typedef const WebKit::RequestStorageAccessConfirmResultListener* WKPageRequestStorageAccessConfirmResultListenerRef;
typedef const WebKit::WebResourceLoadStatisticsManager* WKResourceLoadStatisticsManagerRef;
typedef const WebKit::WebTextChecker* WKTextCheckerRef;
typedef const API::ScriptMessage* WKScriptMessageRef;
typedef const void* WKSessionRef; // Deprecated
typedef const API::SessionState* WKSessionStateRef;
typedef const WebKit::WebUserContentControllerProxy* WKUserContentControllerRef;
typedef const API::ContentRuleListStore* WKUserContentExtensionStoreRef;
typedef const API::ContentRuleList* WKUserContentFilterRef;
typedef const WebKit::UserMediaPermissionCheckProxy* WKUserMediaPermissionCheckRef;
typedef const WebKit::UserMediaPermissionRequestProxy* WKUserMediaPermissionRequestRef;
typedef const API::UserScript* WKUserScriptRef;
typedef const WebKit::WebViewportAttributes* WKViewportAttributesRef;
typedef const WebKit::WebsiteDataStoreConfiguration* WKWebsiteDataStoreConfigurationRef;
typedef const WebKit::WebsiteDataStore* WKWebsiteDataStoreRef;
typedef const API::WebsitePolicies* WKWebsitePoliciesRef;
typedef const API::WindowFeatures* WKWindowFeaturesRef;
typedef const WebKit::SpeechRecognitionPermissionCallback* WKSpeechRecognitionPermissionCallbackRef;
typedef const WebKit::MediaKeySystemPermissionCallback* WKMediaKeySystemPermissionRequestRef;
typedef const WebKit::MediaKeySystemPermissionCallback* WKMediaKeySystemPermissionCallbackRef;
typedef const WebKit::QueryPermissionResultCallback* WKQueryPermissionResultCallbackRef;
typedef const API::CompletionListener* WKCompletionListenerRef;

/* WebKit2 Bundle types */

typedef const WebKit::InjectedBundle* WKBundleRef;
typedef const WebKit::InjectedBundleBackForwardList* WKBundleBackForwardListRef;
typedef const WebKit::InjectedBundleBackForwardListItem* WKBundleBackForwardListItemRef;
typedef const WebKit::InjectedBundleCSSStyleDeclarationHandle* WKBundleCSSStyleDeclarationRef;
typedef const WebKit::InjectedBundleDOMWindowExtension* WKBundleDOMWindowExtensionRef;
typedef const void* WKBundleFileHandleRef; // Internal
typedef const WebKit::WebFrame* WKBundleFrameRef;
typedef const WebKit::InjectedBundleHitTestResult* WKBundleHitTestResultRef;
typedef const void* WKBundleNavigationActionRef; // Internal
typedef const WebKit::InjectedBundleNodeHandle* WKBundleNodeHandleRef;
typedef const WebKit::WebPage* WKBundlePageRef;
typedef const WebKit::PageBanner* WKBundlePageBannerRef;
typedef const void* WKBundlePageGroupRef; // Internal
typedef const WebKit::WebPageOverlay* WKBundlePageOverlayRef;
typedef const WebKit::InjectedBundleRangeHandle* WKBundleRangeHandleRef;
typedef const WebKit::InjectedBundleScriptWorld* WKBundleScriptWorldRef;

#endif /* __INTELLISENSE__ */

#endif /* WKBase_Wrapper_h */
