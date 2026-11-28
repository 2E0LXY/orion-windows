"""Source-contract regression checks for Orion's native menu and pinned shortcuts."""
from pathlib import Path
import unittest

ROOT=Path(__file__).resolve().parents[1]
SRC=(ROOT/"app/src/main/java/uk/community/orion/MainActivity.java").read_text(encoding="utf-8")

class MenuCustomisationTests(unittest.TestCase):
    def test_menu_has_install_and_customise_actions(self):
        self.assertIn('"Install and create shortcut"',SRC)
        self.assertIn('customise.setText("Customise menu")',SRC)
        self.assertIn('void customiseMenu()',SRC)
        self.assertIn('setMultiChoiceItems(names,enabled',SRC)
    def test_visibility_is_stable_across_engines_and_sessions(self):
        self.assertIn('getBoolean("menu_item_"+id,true)',SRC)
        self.assertIn('if(!menuShown(ids[i]))continue;',SRC)
        self.assertIn('edits.putBoolean("menu_item_"+MENU_IDS[i],enabled[i]);',SRC)
        self.assertIn('for(String id:MENU_IDS)edits.remove("menu_item_"+id)',SRC)
        self.assertIn('ids[i].equals("install")',SRC)
    def test_shortcut_requires_webpage_and_launcher_consent(self):
        self.assertIn('if(geckoIncognito){Toast.makeText(this,"Shortcuts are unavailable in Incognito"',SRC)
        self.assertIn('if(!allowed(url))',SRC)
        self.assertIn('isRequestPinShortcutSupported()',SRC)
        self.assertIn('requestPinShortcut(shortcut,null)',SRC)
        self.assertIn('new Intent(Intent.ACTION_VIEW,Uri.parse(target),this,MainActivity.class)',SRC)
        self.assertIn('Installing websites as standalone apps is not supported yet.',SRC)

if __name__=="__main__":
    unittest.main()
