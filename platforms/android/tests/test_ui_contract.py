"""Source-level UI regression guards; Android compilation and device checks are separate gates."""
import pathlib
import unittest

APP = pathlib.Path(__file__).resolve().parents[1]
SOURCE = (APP / "app/src/main/java/uk/community/orion/MainActivity.java").read_text(encoding="utf-8")


def method(name, next_name):
    begin = SOURCE.index(name)
    end = SOURCE.index(next_name, begin)
    return SOURCE[begin:end]


class OrionUiContract(unittest.TestCase):
    def test_startup_not_restricted_to_first_activity_state(self):
        oncreate = method("public void onCreate(Bundle b)", "String installedVersion()")
        self.assertIn("newTab();showStartup();", oncreate)
        self.assertNotIn("if(b==null)showStartup()", oncreate)

    def test_boot_logo_has_real_animation_and_reduced_motion(self):
        start = method("void showStartup()", "class StartupView")
        self.assertIn("ValueAnimator.areAnimatorsEnabled()", start)
        self.assertIn("ValueAnimator.ofFloat(0f,1f)", start)
        self.assertIn("animator.addUpdateListener", start)
        self.assertIn("splash.postOnAnimation", start)
        self.assertIn("splash.invalidate()", start)
        splash = method("class StartupView extends View", "int dp(float n)")
        self.assertIn('c.drawText("Orion"', splash)
        self.assertIn('c.drawText("COMMUNITY BROWSER"', splash)
        self.assertIn("Math.cos(angle)", splash)

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

    def test_shared_menu_and_replay(self):
        menu = method("void menu()", "void downloadPage(")
        self.assertIn('geckoActive?new String[]{', menu)
        self.assertIn("Replay boot animation", menu)
        self.assertIn("sitePermissions()", menu)
        self.assertIn("readingList()", menu)


if __name__ == "__main__":
    unittest.main()
