I think I finally figured out what the mix-up over the "new web extensions API" is. Ever since the first meetings in Tokyo, I've been very confused, because all this time I've had the impression that Martin was talking about the WebKit WebExtensions API. The one that's enabled by the (ENABLE_)WK_WEB_EXTENSIONS build flag. But that's a C/C++ API, so when Martin said "the new API is what the extensions call", I didn't understand what it meant. The core extension system doesn't reference the WebKit API at all and even compiles without the build flag set. It just directly injects the JavaScript, so what "the extensions call" is just the JavaScript injected from the core module.

I looked a bit deeper into the history of the things, and I think the actual "new API" is related to the split between the JavaScript extension APIs (Firefox browser.* vs. Chrome chrome.*) and the current attempts to combine them.

So until now, my vague understanding has been that "Chrome has the extension JavaScript API in chrome.* namespace and uses callbacks, and Firefox has everything in the browser.* namespace and uses Promise objects instead, but otherwise the API is very portable." Which is why the [Mozilla WebExtension polyfill](https://github.com/mozilla/webextension-polyfill) mostly works for lots of extensions by just translating between callbacks and promises. I never had any idea why they made the calling conventions different, while keeping almost everything else the same.

So, as an example of what I'm talking about, for most API calls, Chrome extensions have something like:

```js
const newPerms = { permissions: ['topSites'] };

chrome.permissions.request(newPerms, (granted) => {
  if (granted) {
    console.log('granted');
  }
});
```

While Firefox might have:

```js
const newPerms = { permissions: ['topSites'] };

browser.permissions.request(newPerms).then((granted) => {
  if (granted) {
    console.log('granted');
  }
});
```

### History

To check if my understanding is correct and to bring everyone to the same level, here is very abridged history of browser extensions:
1. 2009 Chrome started implementing a JS API for extensions, using chrome.* namespace. At that time, Promise didn't exist in JavaScript/ECMAScript.
2. Firefox had had its own XUL/XPCOM based add-on system since 2004. (Mozilla-specific technologies.)
3. In 2015, Promises were officially included in ECMAScript 2015, based on 2012 Promises/A+ community spec.
4. Same year, Firefox decided to abandon their own vendor-specific XUL/XPCOM based add-on project and announced their WebExtensions initiative to move to Chrome-compatible API, but under browser.* and using Promises instead, as a "suggested standard".
5. Since the very beginning, Firefox has also had the chrome.* namespace that should in theory mean that Chrome extensions could work in Firefox out of the box. (More on this later.)
6. These almost-compatible Chrome-based APIs started winning, especially when one by one most browsers started switching to Firefox-based (Zen, open-source Linux browsers), or Chrome-based technology. (Edge, Opera, etc, etc.)
  * Side note: In 2020, Safari tried to add support for both with their own implementations. I assume this is when the WebKit extension API started forming, even if it started as Safari/Apple-only.
7. Already in 2015, World Wide Web Consortium (W3C) formed Browser Extension Community Group and started looking into standardizing the extensions, but it didn't lead to much.
8. After some years of no activity, in 2021, W3C started a new group, WebExtensions Community Group (WECG), that started gathering more steam. The goal is to unify the extension APIs back together based on the browser.* style.
  * https://www.w3.org/community/webextensions/
  * https://github.com/w3c/webextensions/blob/main/charter.md

### Manifest confusion

For context, "Manifest" refers to the manifest JSON file that is included in all extensions that specifies the permissions, scripts, capabilities, name, version, etc. But the different versions also have changes to the rules/contracts and the  API itself.

Chrome moved from Manifest v1 to v2 (MV2) in 2012, but this was the early days, so it was easier. However, starting from 2018, Google has been moving to Manifest v3 (MV3). The thing that made headlines recently was that Chrome disabled MV2 altogether, but they've been going in that direction for 8 years. Infamously, this broke extensions like uBlock Origin, which caused all kinds of conspiracy theories.

Safari has been supporting MV3 since 2022. Firefox since 2023. But they both have a bit of a different flavor.

Main changes in MV3 are replacing background pages with service workers (ephemeral, no DOM), overhaul of permission systems, banning remotely hosted code, adding promise support. (More on this later.) 

Firefox's support for MV3 has differences from Chrome. Background pages still work (it took a while for them to even start supporting service workers), and they are committed to MV2 support indefinitely. MV3 also removed some specific features like hooking on blocking webRequest, which mitigates security issues, but also interferes with ad-blockers. Firefox explicitly kept blocking webRequest, even in MV3, to keep support for uBlock Origin and other ad blockers.

WECG does attempt to standardize things like permissions and background contexts and all the Manifest-related issues, but for MV3, they were late to have much influence, and are aiming more for making sure the possible future manifest version is properly cross-browser.

### Recent developments

Presumably because of the push by WECG etc, Chrome has been moving more and more towards compatibility with the browser.* API style, including the Promise convention.

In fact just a couple of months ago, on May 5th, 2026 they released Chrome 148 where they added this: https://developer.chrome.com/docs/extensions/develop/concepts/browser-namespace

Note: as mentioned earlier, MV3 already introduced limited Promise support, so a lot of chrome.* API calls have been accessible through Firefox-style Promise flavor calls for a while.

### What's the difference?

Firefox has supported chrome.* API with callbacks since the beginning. So, why wouldn't all Chrome extensions work in Firefox? Here are some reasons and possible pitfalls for us in the future.

With MV3, Chrome moved to "service workers only", and even though it's based on a [W3C spec](https://www.w3.org/TR/service-workers/), it introduces breaking differences. Chrome MV3 mandates a background service worker ("background": {"service_worker": ...}). Firefox instead switched from "always-on" background pages to event pages, that are DOM-capable, but still ephemeral. For a long time Firefox didn't accept the service_worker key at all, so a Chrome-only MV3 manifest produced an extension with no background context in Firefox. The portable idiom was declaring both background.service_worker and background.scripts and letting each browser pick. This pattern might still exist in some extensions.

Browser-specific features:
* For a long time Chrome had tab groups and Firefox didn't, so [chrome.tabGroups](https://developer.chrome.com/docs/extensions/reference/api/tabGroups) existed for years without a Firefox equivalent, and even now that Firefox has added it, the implementations have differences.
* Same for [chrome.sidePanel](https://developer.chrome.com/docs/extensions/reference/api/sidePanel)
* [chrome.pageCapture](https://developer.chrome.com/docs/extensions/reference/api/pageCapture) Firefox just doesn't have the feature of saving MHTML.

Solved by other means:
* [chrome.declarativeContent](https://developer.chrome.com/docs/extensions/reference/api/declarativeContent) was handled with a manifest property instead in Firefox and is made moot by MV3 anyway.
* [chrome.tts](https://developer.chrome.com/docs/extensions/reference/api/tts) and [chrome.ttsEngine](https://developer.chrome.com/docs/extensions/reference/api/ttsEngine) were made mostly obsolete by the Web Speech API.
* For the extensions that really need an "always running background page", there is [chrome.offscreen](https://developer.chrome.com/docs/extensions/reference/api/offscreen) but this is less of an issue since even though they're ephemeral, the Firefox MV3 event pages have a DOM. (But Firefox doesn't have a full equivalent for offscreen's keep-alive use cases, like background audio playback, in MV3.)

Vendor-specific features:
* [chrome.gcm](https://developer.chrome.com/docs/extensions/reference/api/gcm) formerly Google Cloud Messaging, now Firebase Cloud Messaging (Google-owned cloud platform)
* [chrome.enterprise](https://developer.chrome.com/docs/extensions/reference/api/enterprise/login) and other ChromeOS specific APIs.

Then there are some things that both have, but with very different opinions.
* Firefox's [browser.proxy](https://developer.mozilla.org/en-US/docs/Mozilla/Add-ons/WebExtensions/API/proxy) is very different from Chrome's [chrome.proxy](https://developer.chrome.com/docs/extensions/reference/api/proxy). In fact, it's one of the few things that Firefox doesn't mirror to the chrome.* namespace. (In Firefox, chrome.proxy is just undefined.) The reasons come down to very different handlings of such a low-level feature.
  * This is still being worked on by WECG.
* declarativeNetRequest exists in [both](https://developer.mozilla.org/en-US/docs/Mozilla/Add-ons/WebExtensions/API/declarativeNetRequest) [browsers](https://developer.chrome.com/docs/extensions/reference/api/declarativeNetRequest), but with different features/assumptions/limitations.

Some things are not covered by WECG. They specify packaging format, but browsers might have different signing methods.

Another difference is the devtools namespace, which is somewhat compatible, but understandably very difficult to implement perfectly.

There are also some more specific differences: https://developer.mozilla.org/en-US/docs/Mozilla/Add-ons/WebExtensions/Chrome_incompatibilities
https://developer.mozilla.org/en-US/docs/Mozilla/Add-ons/WebExtensions/Build_a_cross_browser_extension

### Age of convergence, and what that means to us

As mentioned [before](https://developer.chrome.com/docs/extensions/develop/concepts/browser-namespace), Chrome has actually started adopting the browser.* namespace convention. In Chrome, actually `browser === chrome`. So everything in chrome.* is available exactly the same from browser.*.

They separate the callback-style from promise-style calls based on per-method schemas, not runtime heuristics. Every Chromium extension API is generated from schema files in the source tree, which declare per method whether a trailing callback is a completion callback and what it receives. In the newest format the promise form is canonical, e.g. from [alarms.webidl](https://github.com/chromium/chromium/blob/main/extensions/common/api/alarms.webidl): `static Promise<Alarm?> get(optional DOMString name);` and "also accepts a trailing callback" is a platform-wide bindings rule. Rare methods that can't support promises (multi-argument callbacks like devtools' eval) are explicitly marked does_not_support_promises.

Mozilla's polyfill encodes the same knowledge in its curated [api-metadata.json](https://github.com/mozilla/webextension-polyfill/blob/ff0f24b2e3d816c76330ff931f7cc042c622c123/api-metadata.json). Being listed there is what marks a method as "async, trailing callback in Chrome, wrap into a promise," with minArgs/maxArgs locating the callback slot.

In Firefox, however `chrome` and `browser` are not the same. For most things, as far as I understand, they are equal, so `chrome.alarms` and `browser.alarms` could theoretically be the same object. They both support callback-style and promise-style calls based on function arguments, regardless if you call them through browser.* or chrome.* namespace. However, as mentioned before, there are some real differences like Firefox defining `chrome.proxy` as undefined.

There are also some more subtle differences:
```js
X.runtime.onMessage.addListener((msg, sender, sendResponse) => {
  return somePromise;
});
```
Firefox/browser semantics: returning a Promise from the listener is the async-response mechanism. The promise's resolution value is sent as the reply (the preferred approach). Classic Chrome semantics: only a literal return true keeps the channel open for a later sendResponse(); a returned Promise is just a truthy-but-not-true value and has been historically ignored. So it might be necessary to handle differently in chrome.* and browser.* namespaces with Firefox-style extensions.

### Implementation suggestions

Even though the JS APIs are converging, if we want to support both kinds of extensions, it is still important to handle them as separate kinds. *Especially* because we most likely want backwards compatibility with older extensions.

My suggestion for the orion-linux team is this: don't handle window.browser and window.chrome separately, like right now. For both Firefox AND Chrome extensions they should be almost the same. For Chrome extensions, to match Chrome itself, it should be safe to set `window.chrome = window.browser;`. For Firefox, they can be separate, but for most API namespaces, it should be OK to set `window.chrome.{X} = window.browser.{X};`, and then handle the callback vs. promise flavor - NOT based on whether chrome.* or browser.* was called - but based on the arguments, because this is what a lot of extensions already expect. So if the calls are not handled like that, many extensions will break already. Theoretically, some extensions might even use both browser.* and chrome.* namespaces in the same extension, implicitly assuming they're linked. (Public/shared library code for example.) Not to mention if you can get over the hurdle of differentiating the calls between callback/promise, it should be easier to implement as well with some wrappers.

The "hurdle of differentiating the calls" might not be trivial, though, because for some functions it might be quite difficult to decide if it's something like `sort(array, comparison_function)` or `sort(array, callback)`. (Non-existing toy example.) But if Google has decided that it is safe to set `browser === chrome`, while supporting both callbacks and promises on both based on arguments, it should be at least theoretically possible, but it's not as easy as `typeof lastArg === "function"`.

Some other interesting files I found, this one has lists for the permissions for each API https://github.com/chromium/chromium/blob/main/extensions/common/api/_api_features.json

There are of course still some extra special handling needed for the callback/promise difference, like my earlier PR and my [new PR](https://github.com/kagisearch/orion-linux/pull/30), because I realized that: [according](https://developer.chrome.com/docs/extensions/reference/api/runtime#property-lastError) to Chrome, even if an API call fails, it should still call the callback, but set runtime.lastError, and then check if runtime.lastError was actually checked. This is actually what dropped me in this whole rabbit hole.





