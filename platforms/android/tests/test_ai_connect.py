"""Source-level security regression contracts for Orion AI Connect."""
from pathlib import Path
import unittest

ROOT=Path(__file__).resolve().parents[1]
BRIDGE=(ROOT/"app/src/main/java/uk/community/orion/OrionAiBridge.java").read_text(encoding="utf-8")
MAIN=(ROOT/"app/src/main/java/uk/community/orion/MainActivity.java").read_text(encoding="utf-8")

class AiConnectContracts(unittest.TestCase):
    def test_loopback_only_and_disabled_by_default(self):
        self.assertIn('InetAddress.getByName("127.0.0.1")',BRIDGE)
        self.assertIn('new ServerSocket(PORT,8,InetAddress.getByName("127.0.0.1"))',BRIDGE)
        self.assertIn('private volatile boolean running;',BRIDGE)
        self.assertNotIn('"0.0.0.0"',BRIDGE)
    def test_authorisation_limits(self):
        self.assertIn('MessageDigest.isEqual(a,b)',BRIDGE)
        self.assertIn('System.currentTimeMillis()>deadline',BRIDGE)
        self.assertIn('if(origin!=null',BRIDGE)
        self.assertIn('length>MAX_BODY',BRIDGE)
        self.assertIn('if(!authorise(authorisation))',BRIDGE)
    def test_consent_and_private_guards(self):
        self.assertIn('host.requestApproval(name,',BRIDGE)
        self.assertIn('if(!running||!host.canControl())',BRIDGE)
        self.assertIn('void openIncognito(){\n  if(aiBridge!=null)aiBridge.stop();',MAIN)
        self.assertIn('boolean aiCanControl(){return !geckoIncognito',MAIN)
        self.assertIn('setNegativeButton("Deny"',MAIN)
        self.assertIn('setPositiveButton("Allow once"',MAIN)
        self.assertIn('void onDestroy(){if(aiBridge!=null)aiBridge.stop();',MAIN)
    def test_only_low_risk_tools(self):
        self.assertIn('"browser_info"',BRIDGE)
        self.assertIn('"navigate"',BRIDGE)
        self.assertIn('if(!"https".equalsIgnoreCase(parsed.getScheme())',BRIDGE)
        self.assertNotIn('evaluateJavascript(',BRIDGE)
        self.assertNotIn('"click"',BRIDGE)

if __name__=="__main__": unittest.main()
