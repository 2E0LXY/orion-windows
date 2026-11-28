"""Source-level regression guards; Android compilation and device checks are separate gates."""
import pathlib
import re
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

    def test_splash_draws_new_frames(self):
        start = method("void showStartup()", "class StartupView")
        self.assertIn("splash.postOnAnimation(this)", start)
        self.assertIn("splash.phase=", start)
        self.assertIn("splash.invalidate()", start)

    def test_gecko_uses_main_browser_content_area(self):
        gecko = method("void openGecko()", "void closeGecko()")
        self.assertIn("content.addView(geckoView", gecko)
        self.assertIn("geckoView.setSession(session)", gecko)
        self.assertNotIn("new Dialog(", gecko)

    def test_gecko_returns_to_previous_webview(self):
        close = method("void closeGecko()", "void geckoGoBack()")
        self.assertIn("geckoView.releaseSession()", close)
        self.assertIn("geckoSession.close()", close)
        self.assertIn("content.addView(current()", close)

    def test_navigation_routes_to_active_engine(self):
        navigate = method("void navigate(String raw)", "void newTab()")
        self.assertIn("if(geckoActive&&geckoSession!=null)", navigate)
        self.assertIn("geckoSession.loadUri(url)", navigate)
        self.assertIn("current().loadUrl(url)", navigate)

    def test_shared_menu_and_replay(self):
        menu = method("void menu()", "void downloadPage(")
        self.assertIn('geckoActive?new String[]{', menu)
        self.assertIn("Replay boot animation", menu)
        self.assertIn("sitePermissions()", menu)
        self.assertIn("readingList()", menu)

if __name__ == "__main__":
    unittest.main()
