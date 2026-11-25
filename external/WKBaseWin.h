#ifndef WKBaseWin_Wrapper_h
#define WKBaseWin_Wrapper_h

#ifndef __INTELLISENSE__
#include "WKBaseWin_impl.h"
#else

typedef int pid_t;

namespace WebKit { class WebView; class WebEditCommandProxy; }
typedef const WebKit::WebView* WKViewRef;
typedef const WebKit::WebEditCommandProxy* WKEditCommandRef;

#endif
#endif
