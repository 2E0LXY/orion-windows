"""Source contracts for Orion default browser behaviour and AI control safety."""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = (ROOT / "app/src/main/AndroidManifest.xml").read_text(encoding="utf-8")
SOURCE = (ROOT / "app/src/main/java/uk/community/orion/MainActivity.java").read_text(encoding="utf-8")

class DefaultBrowserContracts(unittest.TestCase):
    def test_view_intent_registered_for_both_schemes(self):
        self.assertIn('android:launchMode="singleTop"', MANIFEST)
        self.assertIn('android.intent.action.VIEW', MANIFEST)
        self.assertIn('android.intent.category.BROWSABLE', MANIFEST)
        self.assertIn('android:scheme="http"', MANIFEST)
        self.assertIn('android:scheme="https"', MANIFEST)

    def test_links_are_validated_and_private_session_is_closed(self):
        self.assertIn('void openExternalUrl(Intent intent)', SOURCE)
        self.assertIn('if(!allowed(url))', SOURCE)
        self.assertIn('if(geckoIncognito)closeGecko()', SOURCE)
        self.assertIn('void onNewIntent(Intent intent)', SOURCE)

    def test_default_browser_requires_android_consent(self):
        self.assertIn('RoleManager.ROLE_BROWSER', SOURCE)
        self.assertIn('createRequestRoleIntent', SOURCE)
        self.assertIn('ACTION_MANAGE_DEFAULT_APPS_SETTINGS', SOURCE)
        self.assertIn('settingRow(panel,"Default browser"', SOURCE)

    def test_ai_control_is_opt_in_with_explicit_capabilities(self):
        self.assertIn('settingRow(panel,"Orion AI Connect"',SOURCE)
        self.assertIn('void aiConnectSettings()',SOURCE)
        self.assertIn('if(geckoIncognito){Toast.makeText(this,"Close Incognito before sharing"',SOURCE)
        self.assertIn('settingRow(items,"Read webpage text (WebView)"',SOURCE)
        self.assertIn('settingRow(items,"Scroll, click and type (WebView)"',SOURCE)
        self.assertIn('disabledSettingRow(items,"Screenshots and downloads"',SOURCE)

if __name__ == "__main__":
    unittest.main()
