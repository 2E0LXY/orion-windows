"""Source-level UI regression guards; Android compilation and device checks are separate gates."""
import pathlib
import unittest

APP = pathlib.Path(__file__).resolve().parents[1]
SOURCE = (APP / "app/src/main/java/uk/community/orion/MainActivity.java").read_text(encoding="utf-8")
DNS = (APP / "app/src/main/java/uk/community/orion/DnsSettings.java").read_text(encoding="utf-8")
MANIFEST = (APP / "app/src/main/AndroidManifest.xml").read_text(encoding="utf-8")
WEATHER = (APP / "app/src/main/java/uk/community/orion/HomeWeather.java").read_text(encoding="utf-8")


def method(name, next_name):
    begin = SOURCE.index(name)
    end = SOURCE.index(next_name, begin)
    return SOURCE[begin:end]


class OrionUiContract(unittest.TestCase):
    def test_cloudflare_is_gecko_default_and_applied(self):
        self.assertIn('PREF_PROVIDER="dns_provider"', DNS)
        self.assertIn('prefs.getInt(PREF_PROVIDER,1)', DNS)
        self.assertIn('CLOUDFLARE="https://cloudflare-dns.com/dns-query"', DNS)
        self.assertIn('setTrustedRecursiveResolverUri(url)', DNS)
        self.assertIn('setTrustedRecursiveResolverMode(', DNS)
        self.assertIn('DnsSettings.applyToGecko(geckoRuntime,getPreferences(0));', SOURCE)

    def test_webview_dns_is_honest_and_android_managed(self):
        self.assertIn('WebView follows Android settings', DNS)
        self.assertIn('PRIVATE_DNS_SETTINGS', DNS)
        self.assertIn('per-WebView DNS are not available', DNS)
        self.assertIn('ACCESS_NETWORK_STATE', MANIFEST)

    def test_ipv4_and_ipv6_are_actually_probed(self):
        self.assertIn('LinkProperties', DNS)
        self.assertIn('Inet6Address', DNS)
        self.assertIn('new String[]{IPV4[p],IPV6[p]}', DNS)
        self.assertIn('socket.connect(InetAddress.getByName(ip),53)', DNS)
        self.assertIn('append(probe(ip))', DNS)
        self.assertNotIn('probe("1.1.1.1")', DNS)
        self.assertIn('queryDns(endpoint,1)', DNS)
        self.assertIn('queryDns(endpoint,28)', DNS)
        self.assertIn('application/dns-message', DNS)

    def test_webview_dns_vpn_requires_android_consent_and_scopes_traffic(self):
        vpn = (APP / "app/src/main/java/uk/community/orion/OrionDnsVpnService.java").read_text(encoding="utf-8")
        self.assertIn('OrionDnsVpnService', MANIFEST)
        self.assertIn('android.permission.BIND_VPN_SERVICE', MANIFEST)
        self.assertIn('android.permission.FOREGROUND_SERVICE_SPECIAL_USE', MANIFEST)
        self.assertIn('VpnService.prepare(a)', DNS)
        self.assertIn('DNS_VPN_REQUEST', SOURCE)
        self.assertIn('if(result==RESULT_OK)startDnsVpn()', SOURCE)
        self.assertIn('builder.addAllowedApplication(getPackageName())', vpn)
        self.assertIn('.addRoute("10.203.0.53",32)', vpn)
        self.assertIn('.addRoute("fd70::53",128)', vpn)
        self.assertIn('network.openConnection(new URL(doh))', vpn)
        self.assertIn('connection.setRequestMethod("POST")', vpn)
        self.assertIn('application/dns-message', vpn)
        self.assertIn('ACTION_STOP', vpn)
        self.assertIn('onRevoke()', vpn)
        self.assertIn('if(tunnel==fd)', vpn)
        self.assertIn('Local DNS VPN', DNS)

    def test_custom_secure_dns_rejects_insecure_protocols(self):
        self.assertIn('"https".equalsIgnoreCase(u.getScheme())', DNS)
        self.assertIn('u.getUserInfo()==null', DNS)
        self.assertIn('validDoh(url)', DNS)
        self.assertIn('TRR_MODE_ONLY', DNS)
        self.assertIn('TRR_MODE_FIRST', DNS)

    def test_boot_overlay_is_completely_removed(self):
        self.assertNotIn("showStartup()", SOURCE)
        self.assertNotIn("StartupView", SOURCE)
        self.assertNotIn("Replay boot animation", SOURCE)
        self.assertNotIn("Replay launch animation", SOURCE)
        oncreate = method("public void onCreate(Bundle b)", "String installedVersion()")
        self.assertIn("newTab();", oncreate)
        self.assertNotIn("newTab();showStartup();", oncreate)

    def test_shared_native_home_weather_card(self):
        home = method("void showHome(boolean gecko)", "class GeckoMark")
        self.assertIn("HomeWeather.attach(this,weatherHost)", home)
        self.assertIn("emblem.setImageResource(R.drawable.orion_icon)", home)
        self.assertIn('title.setText("Orion")', home)
        self.assertIn('powered.setText("Powered by GeckoView")', home)
        self.assertIn("homeWeather.dispose()", SOURCE)
        self.assertIn("HomeWeather.settings(this)", SOURCE)
        self.assertIn("if(code==HomeWeather.LOCATION_REQUEST)", SOURCE)
        self.assertIn("requestPermissions(new String[]{Manifest.permission.ACCESS_COARSE_LOCATION}", WEATHER)
        self.assertIn("LocationManager.NETWORK_PROVIDER", WEATHER)
        self.assertIn("Choose a town", WEATHER)
        self.assertIn("geocoding-api.open-meteo.com", WEATHER)
        self.assertIn("api.open-meteo.com/v1/ecmwf", WEATHER)
        self.assertIn("forecast_days=5", WEATHER)
        self.assertIn("timeformat=unixtime", WEATHER)
        self.assertIn("CACHE_TTL_MS=90L*60*1000", WEATHER)
        self.assertIn("ECMWF IFS", WEATHER)
        self.assertIn("static final class WeatherIcon extends View", WEATHER)
        self.assertIn("Math.round(location.getLatitude()*100.0)/100.0", WEATHER)
        self.assertIn("void dispose()", WEATHER)

    def test_gecko_permissions_are_connected_and_described(self):
        gecko = method("void openGecko()", "void closeGecko()")
        self.assertIn("session.setPermissionDelegate(", gecko)
        self.assertIn("onAndroidPermissionsRequest(", gecko)
        self.assertIn("onContentPermissionRequest(", gecko)
        self.assertIn("onMediaPermissionRequest(", gecko)
        self.assertIn("geckoSecureOrigin(uri)", gecko)
        self.assertIn("cb.reject()", gecko)
        permissions = method("void sitePermissions()", "void clearBrowsingData()")
        self.assertIn("apply to both engines", permissions)
        self.assertNotIn("permission management is not yet integrated", permissions)
        self.assertIn("GeckoView uses its own cookie controls", permissions)

    def test_start_page_shared_between_webview_and_geckoview(self):
        layout = method("void showHome(boolean gecko)", "class GeckoMark")
        self.assertIn('title.setText("Orion")', layout)
        self.assertIn('subtitle.setText("PRIVATE BROWSER")', layout)
        self.assertIn('instruction.setText("Search or enter an address below.")', layout)
        self.assertIn('powered.setText("Powered by GeckoView")', layout)
        self.assertIn("if(gecko)", layout)
        self.assertIn("homeOverlay=page", layout)
        webview = method("void home(WebView w)", "WebView current()")
        self.assertIn("homeTabs.add(w)", webview)
        self.assertIn("showHome(false)", webview)
        self.assertNotIn("loadDataWithBaseURL", webview)

    def test_gecko_opens_orion_home_not_google(self):
        gecko = method("void openGecko()", "void closeGecko()")
        self.assertIn("boolean openAtHome=", gecko)
        self.assertIn("if(initial==null)showHome(true)", gecko)
        self.assertNotIn("https://www.google.com/", gecko)
        self.assertIn("content.addView(geckoView", gecko)
        self.assertIn("geckoView.setSession(session)", gecko)
        self.assertNotIn("new Dialog(", gecko)
        home = method("void homeActive()", "void hideHome()")
        self.assertIn("showHome(true)", home)
        self.assertIn("home(current())", home)
        self.assertNotIn("loadUri", home)

    def test_home_is_removed_when_navigating(self):
        load = method("void loadGeckoUrl(String url)", "Button geckoButton(")
        self.assertIn("hideHome()", load)
        self.assertIn("homeTabs.remove(w)", load)
        self.assertIn("geckoSession.loadUri(url)", load)
        navigate = method("void navigate(String raw)", "void newTab()")
        self.assertIn("loadGeckoUrl(url)", navigate)
        self.assertIn("loadWebUrl(current(),url)", navigate)

    def test_gecko_returns_to_webview(self):
        close = method("void closeGecko()", "void geckoGoBack()")
        self.assertIn("geckoView.releaseSession()", close)
        self.assertIn("geckoSession.close()", close)
        self.assertIn("content.addView(current()", close)
        self.assertIn("if(homeTabs.contains(current()))showHome(false)", close)

    def test_navigation_routes_to_active_engine(self):
        navigate = method("void navigate(String raw)", "void newTab()")
        self.assertIn("if(geckoActive&&geckoSession!=null)", navigate)
        self.assertIn("loadGeckoUrl(url)", navigate)
        self.assertIn("loadWebUrl(current(),url)", navigate)

    def test_shared_menu_without_replay(self):
        menu = method("void menu()", "void incognitoExplained()")
        self.assertIn('geckoActive?new String[]{', menu)
        self.assertNotIn("Replay boot animation", menu)
        self.assertNotIn('"Privacy and permissions","DNS and network"', menu)
        self.assertIn("readingList()", menu)

    def test_orion_security_button_at_toolbar_right(self):
        oncreate = method("public void onCreate(Bundle b)", "String installedVersion()")
        self.assertIn('Button orionSecurity=button("orion","Orion security and privacy",()->securityPrivacy(),true)', oncreate)
        self.assertIn("getDrawable(R.drawable.orion_icon)", oncreate)
        self.assertIn("orionSecurity.setCompoundDrawables(null,officialOrion,null,null)", oncreate)
        glyph = method("class Glyph extends Drawable", "void updateToolbar()")
        self.assertIn('case "orion":', glyph)
        panel = method("void securityPrivacy()", "void downloadPage(")
        self.assertIn('section(panel,"PRIVATE BROWSING")', panel)
        self.assertIn('settingRow(panel,geckoIncognito?"End Incognito session":"Start Incognito mode"', panel)
        for item in ['section(panel,"TRACKERS AND CONTENT")', 'section(panel,"WEBSITE SECURITY")',
                     'section(panel,"SITE PERMISSIONS")', 'section(panel,"ENCRYPTED DNS AND NETWORK")',
                     'section(panel,"BROWSING DATA")', 'toggle(panel,"Ad blocker (WebView)"',
                     'toggle(panel,"EasyPrivacy domain rules"', 'toggle(panel,"JavaScript (WebView)"',
                     'toggle(panel,"Load images (WebView)"', 'settingRow(panel,"DNS and Secure DNS"',
                     'settingRow(panel,"Camera, microphone and location"',
                     'settingRow(panel,"Clear normal browsing data"']:
            self.assertIn(item, panel)
        settings = method("void settings()", "void startDnsVpn()")
        self.assertNotIn('section(panel,"CONTENT BLOCKING")', settings)
        self.assertNotIn('section(panel,"PRIVACY AND SECURITY")', settings)
        self.assertIn('securityPrivacy()', settings)

    def test_unavailable_controls_are_disabled_and_visibly_grey(self):
        menu = method("void menu()", "void incognitoExplained()")
        self.assertIn("boolean canUsePage=", menu)
        self.assertIn("boolean requiresPage=", menu)
        self.assertIn("button.setEnabled(!unavailable)", menu)
        self.assertIn("button.setTextColor(unavailable?0xff919bab", menu)
        self.assertIn('button.setContentDescription(labels[i]+(unavailable?", unavailable":""))', menu)
        settings = method("void settings()", "void startDnsVpn()")
        self.assertIn('disabledSettingRow(panel,"Sync"', settings)
        self.assertIn('disabledSettingRow(panel,"Password autofill"', settings)
        self.assertIn('disabledSettingRow(panel,"Extensions"', settings)
        self.assertIn('disabledSettingRow(panel,"Orion Connect"', settings)
        self.assertIn('if(geckoIncognito)disabledSettingRow(panel,"Import browser data"', settings)
        self.assertNotIn('settingRow(panel,"Sync"', settings)
        disabled = method("void disabledSettingRow(", "void settingRow(")
        self.assertIn("row.setEnabled(false)", disabled)
        self.assertIn("row.setClickable(false)", disabled)
        self.assertIn("0xff919bab", disabled)
        imports = method("void importBrowserData()", "void pickImport(")
        self.assertIn("position!=5", imports)
        self.assertIn("setAdapter(options,", imports)
        self.assertIn('"Android Autofill · Unavailable"', imports)
        self.assertNotIn('ACTION_SETTINGS', imports)

    def test_clear_browsing_data_clears_orion_recent_history_too(self):
        clear = method("void clearBrowsingData()", "void onBackPressed()")
        self.assertIn('getPreferences(0).edit().remove("recent_history").apply()', clear)
        self.assertIn('for(WebView w:tabs){w.clearCache(true);w.clearHistory();w.clearFormData();}', clear)
        history = method("void historyLibrary()", "void readingList()")
        self.assertIn('getPreferences(0).edit().remove("recent_history").apply()', history)
        self.assertIn("for(WebView w:tabs)w.clearHistory()", history)

    def test_bookmark_import_strips_a_real_utf8_bom(self):
        importer = (APP / "app/src/main/java/uk/community/orion/BookmarkImporter.java").read_text(encoding="utf-8")
        self.assertIn(r'replace("\uFEFF","")', importer)
        self.assertNotIn(r'replace("\\uFEFF","")', importer)

    def test_dns_controls_disabled_without_valid_provider(self):
        self.assertIn('if(!gecko)a.disabledSettingRow(body,"GeckoView DNS fallback"', DNS)
        self.assertIn('if(!gecko&&!vpnActive)a.disabledSettingRow(body,"Local DNS VPN"', DNS)
        self.assertIn('if(!gecko)a.disabledSettingRow(body,"Test selected DoH server"', DNS)

    def test_missing_filter_lists_do_not_enable_dead_switches(self):
        panel = method("void securityPrivacy()", "void downloadPage(")
        self.assertIn('new java.io.File(getFilesDir(),"easylist.domains").length()>0', panel)
        self.assertIn('new java.io.File(getFilesDir(),"easyprivacy.domains").length()>0', panel)
        self.assertIn('disabledSettingRow(panel,"EasyList domain rules"', panel)
        self.assertIn('disabledSettingRow(panel,"EasyPrivacy domain rules"', panel)
        self.assertIn('getPreferences(0).getBoolean("youtube_ads",true)&&youtubeAd', SOURCE)
        self.assertIn('getPreferences(0).getBoolean("adblock",true)&&AdBlocker.blocks', SOURCE)

    def test_canonical_orion_branding_and_direct_apk_release(self):
        from pathlib import Path
        icon=(APP/"app/src/main/res/drawable/orion_icon.xml").read_text(encoding="utf-8")
        logo=(APP.parent.parent/"assets/orion-mark.svg").read_text(encoding="utf-8")
        workflow=(APP.parent.parent/".github/workflows/android-prototype.yml").read_text(encoding="utf-8")
        self.assertIn('android:pathData="M8,91 Q21,60 51,45 Q72,35 91,30"', icon)
        self.assertNotIn('android:pathData="M0,0H108V108H0Z"', icon)
        self.assertIn('M8 91 Q21 60 51 45 Q72 35 91 30', logo)
        self.assertIn('fill="none"', logo)
        self.assertIn('R.drawable.orion_icon', SOURCE)
        self.assertIn('android:icon="@mipmap/ic_launcher"', MANIFEST)
        self.assertIn('android:roundIcon="@mipmap/ic_launcher"', MANIFEST)
        adaptive=(APP/"app/src/main/res/mipmap-anydpi-v26/ic_launcher.xml").read_text(encoding="utf-8")
        foreground=(APP/"app/src/main/res/drawable/orion_launcher_foreground.xml").read_text(encoding="utf-8")
        monochrome=(APP/"app/src/main/res/drawable/orion_launcher_monochrome.xml").read_text(encoding="utf-8")
        self.assertIn('@android:color/transparent', adaptive)
        self.assertIn('@drawable/orion_launcher_foreground', adaptive)
        self.assertIn('@drawable/orion_launcher_monochrome', adaptive)
        self.assertIn('M79,21 L83,30 L93,34', foreground)
        self.assertIn('M79,21 L83,30 L93,34', monochrome)
        self.assertIn('releases/download/android-latest-test/Orion-Browser-Android-test.apk', (APP/"README.md").read_text(encoding="utf-8"))
        self.assertIn('gh release upload "$latest_tag" Orion-Browser-Android-test.apk Orion-Browser-Android-test.apk.sha256 --clobber', workflow)
        self.assertNotIn('actions/upload-artifact@', workflow)

    def test_no_cache_mode_clears_engine_http_resources(self):
        self.assertIn('s.setCacheMode(getPreferences(0).getBoolean("zero_cache",false)?WebSettings.LOAD_NO_CACHE:WebSettings.LOAD_DEFAULT)', SOURCE)
        self.assertIn('toggle(panel,"No HTTP cache (WebView)","zero_cache",false)', SOURCE)
        self.assertIn('if(key.equals("zero_cache"))applyNoCachePreference(on)', SOURCE)
        self.assertIn('if(enabled)clearResourceCaches(false)', SOURCE)
        self.assertIn('for(WebView w:tabs)w.clearCache(true)', SOURCE)
        self.assertIn('StorageController.ClearFlags.ALL_CACHES', SOURCE)
        self.assertIn('if(getPreferences(0).getBoolean("zero_cache",false))v.clearCache(true)', SOURCE)
        self.assertIn('settingRow(panel,"Clear cached resources now"', SOURCE)
        self.assertIn('if(getPreferences(0).getBoolean("zero_cache",false))clearResourceCaches(false)', SOURCE)

    def test_native_swipe_gestures_and_download_tracking(self):
        parallel = (APP / "app/src/main/java/uk/community/orion/ParallelDownload.java").read_text(encoding="utf-8")
        gestures = method("boolean touchWithin(View view", "void submit()")
        self.assertIn("dispatchTouchEvent(MotionEvent event)", gestures)
        self.assertIn("touchWithin(navigation,touchStartX,touchStartY)", gestures)
        self.assertIn("touchWithin(content,touchStartX,touchStartY)", gestures)
        self.assertIn("pageAtTop()", gestures)
        self.assertIn("else if(action==4)showDownloadPanel()", gestures)
        self.assertIn("else if(action==3)", gestures)
        self.assertIn("if(action==1)", gestures)
        self.assertIn("else if(action==2)", gestures)
        self.assertIn("MotionEvent.ACTION_CANCEL", gestures)
        self.assertIn("session.setScrollDelegate(new org.mozilla.geckoview.GeckoSession.ScrollDelegate()", SOURCE)
        self.assertIn("void showDownloadPanel()", SOURCE)
        self.assertIn("void updateDownloadRows(LinearLayout list)", SOURCE)
        self.assertIn("rememberDownload(downloadId)", SOURCE)
        self.assertIn("manager.query(query)", SOURCE)
        self.assertIn("ParallelDownload.snapshot()", SOURCE)
        self.assertIn("ParallelDownload.cancel()", SOURCE)
        self.assertIn("static volatile State state", parallel)
        self.assertIn("static void cancel()", parallel)
        self.assertNotIn("dialog.setCancelable(false)", parallel)

    def test_incognito_is_a_real_gecko_private_session_and_not_recorded(self):
        gecko = method("void openGecko()", "void closeGecko()")
        self.assertIn('new org.mozilla.geckoview.GeckoSessionSettings.Builder().usePrivateMode(incognito).build()', gecko)
        self.assertIn('void openIncognito()', gecko)
        self.assertIn('if(geckoActive){closeGecko();}', gecko)
        self.assertIn('boolean openAtHome=incognito||', gecko)
        self.assertIn('if(!geckoIncognito)recordVisit(url);', gecko)
        self.assertIn('FLAG_SECURE', gecko)
        close = method("void closeGecko()", "void geckoGoBack()")
        self.assertIn('getWindow().clearFlags(WindowManager.LayoutParams.FLAG_SECURE)', close)
        self.assertIn('geckoIncognito=false;', close)
        self.assertIn('geckoSession.close()', close)
        history = method("void recordVisit(String uri)", "void historyLibrary()")
        self.assertIn('if(geckoIncognito||!allowed(uri))return;', history)
        bookmark = method("void save()", "void bookmarks()")
        self.assertIn('if(geckoIncognito)', bookmark)
        home = method("void showHome(boolean gecko)", "class GeckoMark")
        self.assertIn('if(!geckoIncognito){', home)
        self.assertIn('homeWeather=HomeWeather.attach(this,weatherHost);', home)
        self.assertIn('End Incognito session', home)
        self.assertIn('if(geckoIncognito){originalHome();return;}', SOURCE)
        self.assertIn('else homeTap();', SOURCE)
        self.assertIn('shortcut.setOnLongClickListener(mode==0?', SOURCE)
        self.assertIn('if(!geckoActive&&selected>=0&&v==current())', SOURCE)
        self.assertIn('if(!geckoActive&&v==current())navigate(', SOURCE)
        info = method("void incognitoExplained()", "void securityPrivacy()")
        self.assertIn('internet provider', info)
        self.assertIn('fingerprinting', info)
        self.assertIn('normal browser tabs, cookies or history', info)


if __name__ == "__main__":
    unittest.main()
