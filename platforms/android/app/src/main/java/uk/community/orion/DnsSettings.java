package uk.community.orion;

import android.app.AlertDialog;
import android.app.Dialog;
import android.content.ClipData;
import android.content.ClipboardManager;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.res.ColorStateList;
import android.graphics.Typeface;
import android.graphics.drawable.RippleDrawable;
import android.net.ConnectivityManager;
import android.net.LinkAddress;
import android.net.LinkProperties;
import android.net.Network;
import android.net.Uri;
import android.os.Build;
import android.view.Gravity;
import android.view.View;
import android.view.Window;
import android.view.WindowInsets;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;
import java.io.ByteArrayOutputStream;
import java.io.DataOutputStream;
import java.io.InputStream;
import java.net.HttpURLConnection;
import java.net.Inet6Address;
import java.net.InetAddress;
import java.net.InetSocketAddress;
import java.net.Socket;
import java.net.URL;
import java.util.Locale;
import javax.net.ssl.HttpsURLConnection;
import org.mozilla.geckoview.GeckoRuntime;
import org.mozilla.geckoview.GeckoRuntimeSettings;

/** Browser DNS controls. WebView uses Android DNS; GeckoView can use independent DoH. */
final class DnsSettings {
 static final String PREF_PROVIDER="dns_provider";
 static final String PREF_CUSTOM="dns_custom_doh";
 static final String PREF_FALLBACK="dns_allow_fallback";
 static final String CLOUDFLARE="https://cloudflare-dns.com/dns-query";
 static final String[] NAMES={"Android system DNS","Cloudflare","Control D (ads + malware)","Quad9","Google Public DNS","OpenDNS","NextDNS (custom account)","AdGuard DNS","CleanBrowsing Security","DNS.Watch (system only)","Mullvad Ad-block","Custom DNS-over-HTTPS"};
 static final String[] ENDPOINTS={"",CLOUDFLARE,"https://freedns.controld.com/p2","https://dns.quad9.net/dns-query","https://dns.google/dns-query","https://doh.opendns.com/dns-query","","https://dns.adguard-dns.com/dns-query","https://doh.cleanbrowsing.org/doh/security-filter/","","https://adblock.dns.mullvad.net/dns-query",""};
 static final String[] DOT_HOSTS={"","one.one.one.one","p2.freedns.controld.com","dns.quad9.net","dns.google","dns.opendns.com","","dns.adguard-dns.com","security-filter-dns.cleanbrowsing.org","","adblock.dns.mullvad.net",""};
 static final String[] IPV4={"","1.1.1.1 / 1.0.0.1","76.76.2.2 / 76.76.10.2","9.9.9.9 / 149.112.112.112","8.8.8.8 / 8.8.4.4","208.67.222.222 / 208.67.220.220","45.90.28.0 / 45.90.30.0","94.140.14.14 / 94.140.15.15","185.228.168.9 / 185.228.169.9","84.200.69.80 / 84.200.70.40","194.242.2.3",""};
 static final String[] IPV6={"","2606:4700:4700::1111 / 2606:4700:4700::1001","2606:1a40::2 / 2606:1a40:1::2","2620:fe::fe / 2620:fe::9","2001:4860:4860::8888 / 2001:4860:4860::8844","2620:119:35::35 / 2620:119:53::53","2a07:a8c0:: / 2a07:a8c1::","2a10:50c0::ad1:ff / 2a10:50c0::ad2:ff","2a0d:2a00:1::2 / 2a0d:2a00:2::2","2001:1608:10:25::1c04:b12f / 2001:1608:10:25::9249:d69b","2a07:e340::3",""};
 private DnsSettings(){}

 static int provider(SharedPreferences prefs){int n=prefs.getInt(PREF_PROVIDER,1);return n>=0&&n<NAMES.length?n:1;}
 static String endpoint(SharedPreferences prefs){int p=provider(prefs);return p==11?prefs.getString(PREF_CUSTOM,""):ENDPOINTS[p];}
 static boolean validDoh(String input){
  if(input==null||input.length()>2048||input.contains(" ")||input.contains("\n")||input.contains("\r"))return false;
  try{Uri u=Uri.parse(input);return "https".equalsIgnoreCase(u.getScheme())&&u.getHost()!=null&&u.getHost().contains(".")&&u.getUserInfo()==null&&u.getFragment()==null&&!u.getPath().isEmpty();}catch(Exception e){return false;}
 }
 static void applyToGecko(GeckoRuntime runtime,SharedPreferences prefs){
  if(runtime==null)return;
  GeckoRuntimeSettings settings=runtime.getSettings();
  String url=endpoint(prefs);
  if(!validDoh(url)){settings.setTrustedRecursiveResolverMode(GeckoRuntimeSettings.TRR_MODE_DISABLED);return;}
  settings.setTrustedRecursiveResolverUri(url);
  settings.setTrustedRecursiveResolverMode(prefs.getBoolean(PREF_FALLBACK,false)?GeckoRuntimeSettings.TRR_MODE_FIRST:GeckoRuntimeSettings.TRR_MODE_ONLY);
 }
 static void show(MainActivity a){
  SharedPreferences prefs=a.getPreferences(0);
  Dialog dialog=new Dialog(a,android.R.style.Theme_Material_NoActionBar);
  LinearLayout root=new LinearLayout(a);root.setOrientation(1);root.setBackgroundColor(0xff171d28);
  Button close=new Button(a);close.setText("‹  DNS and network");close.setAllCaps(false);close.setTextSize(20);close.setGravity(Gravity.START|Gravity.CENTER_VERTICAL);
  close.setTextColor(0xfff1f5f9);close.setBackgroundColor(0xff293344);close.setPadding(a.dp(16),0,0,0);close.setMinHeight(a.dp(56));
  close.setContentDescription("Back to Orion settings");close.setOnClickListener(v->dialog.dismiss());root.addView(close,new LinearLayout.LayoutParams(-1,a.dp(56)));
  ScrollView scroll=new ScrollView(a);scroll.setFillViewport(true);root.addView(scroll,new LinearLayout.LayoutParams(-1,0,1));
  LinearLayout body=new LinearLayout(a);body.setPadding(a.dp(12),a.dp(8),a.dp(12),a.dp(32));body.setOrientation(1);scroll.addView(body);
  a.section(body,"ACTIVE RESOLVER");
  int p=provider(prefs);
  String active=endpoint(prefs);
  boolean gecko=validDoh(active);
  a.settingRow(body,"Selected: "+NAMES[p],gecko?"GeckoView uses encrypted DNS-over-HTTPS":"GeckoView uses Android system DNS",()->{dialog.dismiss();chooseProvider(a);});
  a.settingRow(body,"GeckoView DNS fallback",prefs.getBoolean(PREF_FALLBACK,false)?"System fallback on lookup failure":"Strict encrypted DNS (no system fallback)",()->{
   boolean current=prefs.getBoolean(PREF_FALLBACK,false);
   new AlertDialog.Builder(a).setTitle("GeckoView fallback").setMessage("Strict: GeckoView name lookups fail if the selected DoH server is unavailable. Fallback: GeckoView may use Android system DNS if DoH cannot resolve a name. Local and managed networks may need system DNS.").setNegativeButton("Cancel",null).setNeutralButton("Strict",(d,i)->saveFallback(a,false)).setPositiveButton("Allow fallback",(d,i)->saveFallback(a,true)).show();
  });
  a.section(body,"WEBVIEW SECURE DNS — LOCAL VPN");
  boolean vpnActive=OrionDnsVpnService.active;
  String vpnStatus=vpnActive?OrionDnsVpnService.state:"Off · Android system DNS";
  a.settingRow(body,"Local DNS VPN",vpnStatus,()->{dialog.dismiss();vpnSettings(a);});
  TextView vpnNote=new TextView(a);vpnNote.setText("Optional for WebView. Android will request VPN permission. Only Orion is included; normal web traffic is not routed through the VPN. It may replace your current VPN, and device Private DNS or WebView encrypted DNS can affect results. Requires real-device verification.");vpnNote.setTextSize(14);vpnNote.setTextColor(0xffb9c6da);vpnNote.setLineSpacing(a.dp(4),1f);vpnNote.setPadding(a.dp(16),a.dp(8),a.dp(16),a.dp(12));body.addView(vpnNote);
  a.section(body,"ANDROID WEBVIEW");
  a.settingRow(body,"System Private DNS","WebView follows Android settings. Orion cannot override WebView DNS.",()->openPrivateDns(a));
  TextView expl=new TextView(a);expl.setText("Cloudflare is Orion's default GeckoView resolver. Android Private DNS is device-wide and must be configured in system settings. Network and device policies still apply.");expl.setTextSize(14);expl.setTextColor(0xffb9c6da);expl.setLineSpacing(a.dp(4),1f);expl.setPadding(a.dp(16),a.dp(8),a.dp(16),a.dp(8));body.addView(expl);
  a.section(body,"IPv4 / IPv6 — AUTOMATIC DUAL STACK");
  a.settingRow(body,"Network IP support","View Android's actual IPv4/IPv6 addresses and DNS servers",()->{dialog.dismiss();networkDetails(a);});
  a.settingRow(body,"Test IPv4 and IPv6","Measure selected resolver DNS response on each IP family",()->{dialog.dismiss();testNetwork(a);});
  a.section(body,"DNS TEST");
  a.settingRow(body,"Test selected DoH server",gecko?active:"Select a DoH provider first",()->{dialog.dismiss();testDns(a);});
  a.settingRow(body,"Resolver details","View IPv4, IPv6, DoH and Android Private DNS hostname",()->{dialog.dismiss();providerDetails(a,provider(prefs));});
  TextView note=new TextView(a);note.setText("IPv4-only / IPv6-only forcing and per-WebView DNS are not available through the supported browser APIs. DNS tests check connectivity, not which resolver processed every website request.");note.setTextSize(13);note.setTextColor(0xffb9c6da);note.setPadding(a.dp(16),a.dp(24),a.dp(16),0);body.addView(note);
  dialog.setContentView(root);dialog.show();
  Window win=dialog.getWindow();if(win!=null){win.setLayout(-1,-1);win.setStatusBarColor(0xff171d28);win.setNavigationBarColor(0xff171d28);}
  root.setOnApplyWindowInsetsListener((v,insets)->{if(Build.VERSION.SDK_INT>=30){android.graphics.Insets b=insets.getInsets(WindowInsets.Type.systemBars()|WindowInsets.Type.displayCutout()|WindowInsets.Type.ime());v.setPadding(b.left,b.top,b.right,b.bottom);}else v.setPadding(insets.getSystemWindowInsetLeft(),insets.getSystemWindowInsetTop(),insets.getSystemWindowInsetRight(),insets.getSystemWindowInsetBottom());return insets;});
  root.requestApplyInsets();
 }
 static void vpnSettings(MainActivity a){
  boolean running=OrionDnsVpnService.active;
  String endpoint=endpoint(a.getPreferences(0));
  String selected=NAMES[provider(a.getPreferences(0))];
  String message="Selected: "+selected+"\\n\\n"
   +(running?"Current state: "+OrionDnsVpnService.state:"Current state: Off")+
   "\\n\\nThis opt-in Android VPN captures DNS requests for Orion, forwards supported UDP DNS requests to the selected HTTPS resolver, and leaves normal internet traffic outside the VPN. The Android system shows a VPN icon. It can replace another VPN. It does not bypass device management or guarantee every WebView request uses this resolver. Unsupported DNS transports may not work.";
  new AlertDialog.Builder(a).setTitle("WebView Secure DNS").setMessage(message)
   .setNegativeButton("Back",(d,i)->show(a))
   .setNeutralButton("Stop", (d,i)->{
    Intent stop=new Intent(a,OrionDnsVpnService.class).setAction(OrionDnsVpnService.ACTION_STOP);
    a.startService(stop);show(a);
   })
   .setPositiveButton(running?"Restart":"Enable",(d,i)->{
    if(!validDoh(endpoint)){new AlertDialog.Builder(a).setTitle("Choose a compatible provider").setMessage("This DNS VPN requires a valid DNS-over-HTTPS endpoint. Select a supported preset or custom HTTPS endpoint first.").setPositiveButton("DNS settings",(x,y)->show(a)).show();return;}
    Intent request=android.net.VpnService.prepare(a);
    if(request!=null)a.startActivityForResult(request,MainActivity.DNS_VPN_REQUEST);
    else a.startDnsVpn();
   }).show();
 }
 static void saveFallback(MainActivity a,boolean enabled){
  a.getPreferences(0).edit().putBoolean(PREF_FALLBACK,enabled).apply();
  applyToGecko(MainActivity.geckoRuntime,a.getPreferences(0));
  show(a);
 }
 static void chooseProvider(MainActivity a){
  int selected=provider(a.getPreferences(0));
  new AlertDialog.Builder(a).setTitle("DNS provider · GeckoView").setSingleChoiceItems(NAMES,selected,(d,i)->{
   d.dismiss();
   if(i==11){customDoh(a);return;}
   if(i==6||i==9){providerDetails(a,i);return;}
   a.getPreferences(0).edit().putInt(PREF_PROVIDER,i).apply();
   applyToGecko(MainActivity.geckoRuntime,a.getPreferences(0));
   if(OrionDnsVpnService.active){a.stopService(new Intent(a,OrionDnsVpnService.class));OrionDnsVpnService.active=false;Toast.makeText(a,"DNS provider changed. Restart local DNS VPN to apply.",Toast.LENGTH_LONG).show();}
   Toast.makeText(a,"DNS applied to GeckoView. Reload open sites.",Toast.LENGTH_LONG).show();
   show(a);
  }).setNegativeButton("Cancel",(d,i)->show(a)).show();
 }
 static void customDoh(MainActivity a){
  android.widget.EditText edit=new android.widget.EditText(a);
  edit.setSingleLine(true);edit.setInputType(android.text.InputType.TYPE_CLASS_TEXT|android.text.InputType.TYPE_TEXT_VARIATION_URI);
  edit.setHint("https://resolver.example/dns-query");
  edit.setText(a.getPreferences(0).getString(PREF_CUSTOM,""));edit.setPadding(a.dp(20),a.dp(16),a.dp(20),a.dp(16));
  new AlertDialog.Builder(a).setTitle("Custom DNS-over-HTTPS").setMessage("Enter your provider's complete HTTPS DoH URI. Plain IPv4/IPv6 addresses cannot be used as a DoH endpoint without an HTTPS server and valid certificate.").setView(edit).setNegativeButton("Cancel",(d,i)->show(a)).setPositiveButton("Save",(d,i)->{
   String url=edit.getText().toString().trim();
   if(!validDoh(url)){new AlertDialog.Builder(a).setTitle("Invalid DoH address").setMessage("Enter a valid HTTPS URL with a hostname and path.").setPositiveButton("Edit",(x,y)->customDoh(a)).show();return;}
   a.getPreferences(0).edit().putString(PREF_CUSTOM,url).putInt(PREF_PROVIDER,11).apply();
   applyToGecko(MainActivity.geckoRuntime,a.getPreferences(0));
   if(OrionDnsVpnService.active){a.stopService(new Intent(a,OrionDnsVpnService.class));OrionDnsVpnService.active=false;Toast.makeText(a,"Restart local DNS VPN to apply new provider",Toast.LENGTH_LONG).show();}
   show(a);
  }).show();
 }
 static void providerDetails(MainActivity a,int p){
  String doh=p==11?a.getPreferences(0).getString(PREF_CUSTOM,""):ENDPOINTS[p];
  String message="IPv4: "+(IPV4[p].isEmpty()?"Not set":IPV4[p])+"\n\nIPv6: "+(IPV6[p].isEmpty()?"Not set":IPV6[p])+"\n\nDoH: "+(doh.isEmpty()?"Provider-specific or unavailable":doh)+"\n\nAndroid Private DNS host (DoT): "+(DOT_HOSTS[p].isEmpty()?"Provider-specific or unavailable":DOT_HOSTS[p]);
  if(p==6)message+="\n\nNextDNS needs your own account-linked DoH URL or Private DNS hostname. The plain IPs are not a substitute for your account configuration.";
  if(p==9)message+="\n\nDNS.Watch is listed for reference only; an independent DoH endpoint has not been verified.";
  new AlertDialog.Builder(a).setTitle(NAMES[p]).setMessage(message).setNegativeButton("Back",(d,i)->show(a)).setPositiveButton("Android Private DNS",(d,i)->openPrivateDns(a)).setNeutralButton("Copy DoT hostname",(d,i)->{
   if(DOT_HOSTS[p].isEmpty()){Toast.makeText(a,"No confirmed hostname",Toast.LENGTH_SHORT).show();return;}
   ClipboardManager cb=(ClipboardManager)a.getSystemService(Context.CLIPBOARD_SERVICE);
   cb.setPrimaryClip(ClipData.newPlainText("Private DNS hostname",DOT_HOSTS[p]));
   Toast.makeText(a,"Hostname copied",Toast.LENGTH_SHORT).show();
  }).show();
 }
 static void openPrivateDns(MainActivity a){
  try{a.startActivity(new Intent("android.settings.PRIVATE_DNS_SETTINGS"));}
  catch(Exception e){try{a.startActivity(new Intent(android.provider.Settings.ACTION_WIRELESS_SETTINGS));}catch(Exception ignored){Toast.makeText(a,"Open Android Settings → Private DNS",Toast.LENGTH_LONG).show();}}
 }
 static void networkDetails(MainActivity a){
  ConnectivityManager cm=(ConnectivityManager)a.getSystemService(Context.CONNECTIVITY_SERVICE);
  Network net=cm==null?null:cm.getActiveNetwork();LinkProperties link=cm==null||net==null?null:cm.getLinkProperties(net);
  StringBuilder out=new StringBuilder();boolean v4=false,v6=false;
  if(link==null)out.append("No active network information available.");
  else{
   out.append("Interface: ").append(link.getInterfaceName()).append("\n\nAssigned addresses:\n");
   for(LinkAddress address:link.getLinkAddresses()){InetAddress ip=address.getAddress();if(ip instanceof Inet6Address)v6=true;else v4=true;out.append(ip.getHostAddress()).append("/").append(address.getPrefixLength()).append("\n");}
   out.append("\nAndroid DNS servers:\n");for(InetAddress dns:link.getDnsServers())out.append(dns.getHostAddress()).append("\n");
   out.append("\nIPv4 address: ").append(v4?"Present":"Not reported").append("\nIPv6 address: ").append(v6?"Present":"Not reported");
  }
  out.append("\n\nIP routing is controlled by Android and the active network. An assigned address does not prove Internet reachability.");
  new AlertDialog.Builder(a).setTitle("Live network details").setMessage(out.toString()).setNegativeButton("Close",null).setPositiveButton("DNS settings",(d,i)->show(a)).show();
 }
 static void testNetwork(MainActivity a){
  final int p=provider(a.getPreferences(0));
  final java.util.ArrayList<String> ips=new java.util.ArrayList<>();
  if(p==0){
   ConnectivityManager cm=(ConnectivityManager)a.getSystemService(Context.CONNECTIVITY_SERVICE);
   Network net=cm==null?null:cm.getActiveNetwork();
   LinkProperties link=net==null?null:cm.getLinkProperties(net);
   if(link!=null)for(InetAddress ip:link.getDnsServers())ips.add(ip.getHostAddress());
  }else for(String group:new String[]{IPV4[p],IPV6[p]})if(!group.isEmpty())for(String ip:group.split(" / "))ips.add(ip.trim());
  TextView result=new TextView(a);result.setText("Testing "+NAMES[p]+"…");result.setTextSize(15);result.setPadding(a.dp(24),a.dp(16),a.dp(24),a.dp(16));
  ScrollView scroll=new ScrollView(a);scroll.addView(result);
  AlertDialog dialog=new AlertDialog.Builder(a).setTitle("IPv4 / IPv6 DNS diagnostics").setView(scroll).setNegativeButton("Close",null).create();dialog.show();
  new Thread(()->{
   StringBuilder out=new StringBuilder(NAMES[p]).append("\n\n");
   if(ips.isEmpty())out.append("No static resolver addresses available. Use the DoH endpoint test.\n");
   for(String ip:ips){
    if(!dialog.isShowing())break;
    out.append(ip.contains(":")?"IPv6: ":"IPv4: ").append(ip).append("\n").append(probe(ip)).append("\n\n");
    final String snapshot=out.toString();a.runOnUiThread(()->{if(!a.isDestroyed()&&dialog.isShowing())result.setText(snapshot);});
   }
   out.append("Direct UDP DNS tests are unencrypted and may be blocked or intercepted by the network. They do not verify browser DoH traffic. AAAA resolution does not prove IPv6 connectivity.");
   a.runOnUiThread(()->{if(!a.isDestroyed()&&dialog.isShowing())result.setText(out.toString());});
  },"orion-network-probe").start();
 }
 static String probe(String ip){
  try(java.net.DatagramSocket socket=new java.net.DatagramSocket()){
   socket.setSoTimeout(2500);socket.connect(InetAddress.getByName(ip),53);
   byte[] query=dnsQuery(1);byte[] answer=new byte[4096];
   long start=android.os.SystemClock.elapsedRealtimeNanos();
   socket.send(new java.net.DatagramPacket(query,query.length));
   java.net.DatagramPacket packet=new java.net.DatagramPacket(answer,answer.length);socket.receive(packet);
   if(packet.getLength()<12||answer[0]!=query[0]||answer[1]!=query[1]||(answer[2]&128)==0)return "Invalid DNS reply";
   if((answer[3]&15)!=0)return "DNS error "+(answer[3]&15);
   return String.format(Locale.UK,"DNS reply · %.1f ms",(android.os.SystemClock.elapsedRealtimeNanos()-start)/1e6);
  }catch(Exception e){return "No DNS reply: "+e.getClass().getSimpleName();}
 }
 static byte[] dnsQuery(int type)throws java.io.IOException{
   ByteArrayOutputStream bytes=new ByteArrayOutputStream();
   DataOutputStream query=new DataOutputStream(bytes);
   query.writeShort(new java.security.SecureRandom().nextInt(65536));query.writeShort(0x0100);query.writeShort(1);query.writeShort(0);query.writeShort(0);query.writeShort(0);
   for(String label:new String[]{"example","com"}){query.writeByte(label.length());query.writeBytes(label);}query.writeByte(0);query.writeShort(type);query.writeShort(1);query.flush();return bytes.toByteArray();
 }
 static void testDns(MainActivity a){
  final int selected=provider(a.getPreferences(0));
  String endpoint=endpoint(a.getPreferences(0));
  if(!validDoh(endpoint)){new AlertDialog.Builder(a).setTitle("No DoH endpoint").setMessage("Select an encrypted DNS provider for GeckoView first. WebView uses Android system DNS.").setPositiveButton("OK",null).show();return;}
  TextView result=new TextView(a);result.setText("Resolving example.com over HTTPS…");result.setTextSize(15);result.setPadding(a.dp(24),a.dp(16),a.dp(24),a.dp(16));
  AlertDialog dialog=new AlertDialog.Builder(a).setTitle("DoH resolver test").setView(result).setNegativeButton("Close",null).create();dialog.show();
  new Thread(()->{
   String aResult=queryDns(endpoint,1),aaaaResult=queryDns(endpoint,28);
   a.runOnUiThread(()->{if(!a.isDestroyed()&&dialog.isShowing())result.setText("Provider: "+NAMES[selected]+"\n\nA record (IPv4): "+aResult+"\n\nAAAA record (IPv6): "+aaaaResult+"\n\nMeasured over HTTPS from this Android device. This tests the selected endpoint, not every WebView or GeckoView request.");});
  },"orion-doh-test").start();
 }
 static String queryDns(String endpoint,int type){
  HttpsURLConnection conn=null;
  try{
   ByteArrayOutputStream bytes=new ByteArrayOutputStream();
   DataOutputStream query=new DataOutputStream(bytes);
   query.writeShort(0x7269);query.writeShort(0x0100);query.writeShort(1);query.writeShort(0);query.writeShort(0);query.writeShort(0);
   for(String label:new String[]{"example","com"}){query.writeByte(label.length());query.writeBytes(label);}query.writeByte(0);query.writeShort(type);query.writeShort(1);query.flush();
   String encoded=android.util.Base64.encodeToString(bytes.toByteArray(),android.util.Base64.URL_SAFE|android.util.Base64.NO_PADDING|android.util.Base64.NO_WRAP);
   String sep=endpoint.contains("?")?"&":"?";
   URL target=new URL(endpoint+sep+"dns="+encoded);
   conn=(HttpsURLConnection)target.openConnection();conn.setConnectTimeout(5000);conn.setReadTimeout(5000);conn.setRequestProperty("Accept","application/dns-message");conn.setInstanceFollowRedirects(false);
   long start=android.os.SystemClock.elapsedRealtimeNanos();
   int code=conn.getResponseCode();if(code!=200)return "HTTP "+code;
   byte[] answer=new byte[12];try(InputStream stream=conn.getInputStream()){int offset=0;while(offset<12){int n=stream.read(answer,offset,12-offset);if(n<0)break;offset+=n;}if(offset<12)return "Short DNS reply";}
   if((answer[0]&255)!=0x72||(answer[1]&255)!=0x69||((answer[2]&0x80)==0))return "Invalid DNS reply";
   int rcode=answer[3]&15;if(rcode!=0)return "DNS response error "+rcode;
   return String.format(Locale.UK,"OK · %.1f ms",(android.os.SystemClock.elapsedRealtimeNanos()-start)/1e6);
  }catch(Exception e){return "Failed: "+e.getClass().getSimpleName();}finally{if(conn!=null)conn.disconnect();}
 }
}
