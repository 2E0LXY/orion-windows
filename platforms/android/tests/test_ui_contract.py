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
        self.assertIn("HomeWeather.attach(this,column)", home)
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
        self.assertIn('subtitle.setText("COMMUNITY BROWSER")', layout)
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
        menu = method("void menu()", "void downloadPage(")
        self.assertIn('geckoActive?new String[]{', menu)
        self.assertNotIn("Replay boot animation", menu)
        self.assertIn("sitePermissions()", menu)
        self.assertIn("readingList()", menu)


if __name__ == "__main__":
    unittest.main()
