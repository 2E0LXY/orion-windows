# WebKit C API Reference Counting (WKPtr / adoptWK)

Rules for handling `WK*Ref` objects in `OrionBackend`. Getting these wrong
corrupts the heap: one extra `WKRelease` destroys an object someone else still
references, and libpas later panics with
`"Alloc bit not set in pas_segregated_page_deallocate_with_page"` (a double
free) — usually far from the buggy call site, because libpas defers free
validation to a per-thread deallocation-log flush. See the 2026-07
`scrapeCredentialsAndAllow` incident below.

## The two operations

Every `WK*Ref` is a pointer to a reference-counted object (`API::Object`
subclasses inside WebKit). Ownership is a simple ledger: **every `WKRelease`
you cause must be matched by one reference you own.**

- **Retain** — `WKPtr<T>(ptr)` (retaining constructor) calls `WKRetain`,
  incrementing the refcount. You created your own reference; the `WKPtr`
  destructor's `WKRelease` gives back exactly what you took. Net effect on
  other owners: zero. Always safe, even on a pointer you merely borrowed.

- **Adopt** — `adoptWK(ptr)` stores the pointer **without** calling
  `WKRetain`, but the destructor still calls `WKRelease`. It *claims an
  existing +1 reference* that the callee created on your behalf. Correct only
  when the function you called actually returned a +1 reference. Adopting a
  borrowed pointer creates an unmatched release → premature destruction →
  use-after-free / double free.

Mistakes are asymmetric:

| Mistake | Effect |
|---|---|
| Retain a +1 return (should have adopted) | Leak — object never freed. Annoying, not fatal. |
| Adopt a borrowed ref (should have retained) | Over-release — heap corruption, delayed libpas panic. |

When unsure, retaining is the safe error.

## The Create/Copy rule (Core Foundation "Create Rule")

The WK C API follows Core Foundation's ownership naming conventions — the
authoritative description is Apple's
[Ownership Policy / Naming Conventions](https://developer.apple.com/library/archive/documentation/CoreFoundation/Conceptual/CFDesignConcepts/Articles/NamingConventions.html)
("Create Rule" / "Get Rule"). Function names encode ownership of the returned
reference:

- **`Create` / `Copy` in the name** → returns **+1** (caller owns it) →
  **`adoptWK` it**, exactly once, immediately:
  `adoptWK(WKStringCreateWithUTF8CString(...))`,
  `adoptWK(WKPageCopyTitle(page))`, `adoptWK(WKURLCopyHostName(url))`.
  Failing to adopt (or release) these leaks.

- **`Get` in the name** → returns a **borrowed** reference kept alive by its
  owner (`WKViewGetPage`, `WKPageConfigurationGetUserContentController`,
  `WKAuthenticationChallengeGetDecisionListener`,
  `WKScriptMessageGetBody`). Use it during the current call without any
  retain. To keep it longer, `WKPtr<T>(ptr)` — never `adoptWK`.

- **Callback / client parameters are borrowed.** Every `WK*Ref` passed *into*
  a client callback (navigation actions, script messages, policy listeners,
  completion listeners, URL scheme tasks, ...) is owned by the caller — in
  the C API glue (`WKPage.cpp` etc.) it is typically a stack `Ref<>` that is
  dropped when your callback returns, though other internal owners (e.g.
  `WebFrameProxy::m_activeListener` for policy listeners) may also hold it.
  Never assume any of those outlive your callback.
  - Respond synchronously inside the callback → use the raw pointer, no
    retain needed (this includes while blocked in a modal dialog — the
    caller's stack `Ref` is still alive underneath you).
  - Respond asynchronously (stash it for a later completion) → **retain**:
    `WKPtr<WKFramePolicyListenerRef>(listener)`. Upstream MiniBrowser's
    equivalent is `WKRetainPtr { listener }`.

- **Returning a ref *to* WebKit from a client callback** may be +1 by
  contract — check the header comment. Example: `createNewPage` must return a
  +1-retained `WKPageRef` (the glue `adoptRef`s it); see
  `retainPageFromViewHwnd()` in `webkit.cpp`.

## Passing refs into WK functions

In-parameters are borrowed by the callee for the duration of the call. If the
function keeps the object (stores it, uses it asynchronously), the C glue
retains it internally (`toProtectedImpl` → `Ref`/`RefPtr`) — e.g.
`WKPageConfigurationSetUserContentController` stores a `RefPtr`. No WK C API
function consumes an argument's reference, so passing `myWKPtr` (or
`.get()`) is always refcount-neutral for the caller; your `WKPtr` releasing
at scope end is safe even if WebKit kept the object. Ownership only transfers
*into* WebKit via documented +1 return contracts from client callbacks
(e.g. `createNewPage` must return a retained `WKPageRef`).

## Borrowed references (everything that isn't Create/Copy)

Ownership is binary — there is no special "Get rule" beyond this: a function
with `Create`/`Copy` in the name hands you a +1 you must adopt; **everything
else is borrowed** — `Get` results, callback parameters, in-parameters,
items pulled out of containers. All borrowed refs follow the same decision
rule:

1. **Used and dropped within the current call** (read it, pass it into WK
   calls, chain another Get from it) → keep it **raw**. Do not retain:
   `WKPtr` should signal ownership, and wrapping transient borrows erases
   that signal (and adds refcount churn). Example:
   `WKFrameInfoRef frameInfo = WKScriptMessageGetFrameInfo(message)` inside a
   message callback — borrowed from `message`, dead before the callback
   returns.
2. **Escapes the current call** — stored in a member/global/context
   struct/lambda capture, or **returned from your own helper** → owning
   position → retain: `WKPtr<T>(ptr)`.
3. **Never `adoptWK` it** — that's the over-release bug.

The escape test decides raw vs `WKPtr`; the function name only decides
*retain vs adopt* once you do need to own it.

**Exception to rule 1 — protectors.** CF's docs permit retaining a borrowed
ref "to ensure the object is not disposed of while you are using it", and
within a call that is occasionally load-bearing: retain when something *you
do during the call* can invalidate the lender — mutating the container an
item was borrowed from, releasing the object you borrowed through, completing
a listener whose reply tears things down, or anything that runs a nested
message loop (modal dialogs) during which the owner can be destroyed.
WebKit's own code does this pervasively (`Ref protectedThis`, `RefPtr
protector`). The extra retain/release cost is negligible; the reason not to
do it *by default* is the reader-facing signal — a `WKPtr` implies an
ownership claim. So when you do need a pin, say so in the name:
`WKPtr<T> protectedFoo{foo}`, not `foo`.

Beyond "don't release them", borrowed refs have lifetime and aliasing traps
of their own:

- **Valid only while the owner lives.** A Get result is kept alive by its
  owner; it dangles when the owner is destroyed (or mutated away). Retaining
  extends *memory* validity, not *semantic* validity — a retained page whose
  view was destroyed is a safe-to-touch zombie. Container items
  (`WKArrayGetItemAtIndex`) are owned by the array: keep the array's `WKPtr`
  in scope while using them.

- **Don't return a Get result borrowed from a temporary.** Example in this
  codebase: `contentControllerForPage()` (`script_bridge_webkit.cpp`) gets
  the UCC from an `adoptWK`'d *temporary* configuration copy and returns the
  raw pointer after the copy dies. It works only because
  `PageConfiguration::copy()` shallow-shares the UCC with the page's real
  configuration — a WebKit-internal detail. Helpers like this should return
  a retained `WKPtr` instead of a raw ref.

- **Get returns live shared state, not your copy.** Mutating an object
  obtained via Get affects every other holder. `WKPageCopyPageConfiguration`
  copies the configuration object, but its sub-objects (user content
  controller, preferences, ...) are shared — so a UCC reached through the
  copy aliases the live one, and popups created via `window.open` share their
  opener's UCC. Anything registered on it (user scripts, script message
  handlers, and their raw context pointers) is visible to *all* pages using
  that UCC and outlives any single window. Never register a handler whose
  context is a raw window pointer without a plan for the window dying first.

- **Get may return null** (e.g. `WKPageConfigurationGetUserContentController`
  before one is set, `WKScriptMessageGetBody`). Null-check before use — and
  note that some `Copy` functions return null too (`WKPageCopyActiveURL` on a
  fresh page).

## WKPtr specifics (`OrionBackend/webkit/WKPtr.h`)

**Every owning reference must be a `WKPtr`; raw `WK*Ref` is only for
non-owning, transient, within-call use.** Wrap a ref in `WKPtr` at the point
of acquisition — `adoptWK(...)` for Create/Copy returns, `WKPtr<T>(ptr)` for
borrowed refs you keep — so ownership is decided exactly once, where the ref
enters your code, instead of at every use site. A raw `WK*Ref` must never
*own* anything: it is acceptable only where the lifetime is guaranteed by
someone else for the duration of the current call — function parameters you
use and drop before returning, and arguments passed straight into WK calls.
Anything that outlives the current call — a member, global, heap context
struct, or lambda capture — is an owning position and must be a `WKPtr`;
storing a raw ref there is how the `scrapeCredentialsAndAllow` and
`inputFocus`-handler bugs happened. A `WKPtr` in every owning position makes
the ownership question impossible to skip.

Minimal replacement for `WKRetainPtr`:

- `WKPtr<T>(ptr)` — retaining constructor (handles null). Use for borrowed refs.
- `adoptWK(ptr)` — free function, non-retaining. Use for Create/Copy returns.
- Copy ctor/assignment retain; move transfers; destructor releases.
- `leakRef()` — detach without releasing (hand a +1 to someone else).
- Beware the implicit `operator T()`: passing a `WKPtr` where a raw ref is
  expected works silently. Never wrap a `WKPtr` (or its raw value) in
  `adoptWK` — that double-claims the same reference.

## Async completion contexts (related lifetime rule)

Async C API calls (`WKPageEvaluateJavaScriptInMainFrame`, ...) take a
`void* context` + function pointer. WebKit does **not** own the context; the
convention is that the callback frees it. The callback is guaranteed to run
exactly once on every path — success, JS error, page close, web process crash
(WTF `CompletionHandler` invariant; cancelled calls deliver null results) — so
`delete ctx` in the callback does not leak. But:

- Raw back-pointers (e.g. `WebKitBrowserWindow*`) inside such contexts are
  only safe if the success path cannot outlive the pointee (cancellation
  delivers a null result on the same thread; check result/error before
  touching the pointer).
- Objects the context must keep alive across the wait (like a policy
  listener) must be held **retained** (`WKPtr` member), not adopted.

## Case study: the scrapeCredentialsAndAllow double free (2026-07)

`decidePolicyForNavigationAction` receives a borrowed
`WKFramePolicyListenerRef`. The code deferred the decision until an async JS
credential scrape finished, storing the listener with `adoptWK(listener)`.
That phantom-claimed a reference nobody had given us. The listener survived
the callback (the frame's `m_activeListener` held it), so
`WKFramePolicyListenerUse()` in the completion appeared to work — delivering
the decision cleared `m_activeListener` and destroyed the object, and the
`ScrapeContext`'s `~WKPtr` then released freed memory: double free, reported
by libpas much later inside unrelated code (`IPC::Decoder` teardown). If the
user navigated again mid-scrape, the listener was replaced/destroyed earlier
and the `Use()` call itself was a use-after-free. Fix: retain
(`WKPtr<WKFramePolicyListenerRef>(listener)`).
