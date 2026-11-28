package uk.community.orion;
import android.app.PendingIntent;
import android.content.*;
import android.content.pm.PackageInstaller;
import android.content.pm.PackageInfo;
import android.os.Build;
import java.io.*;
import java.net.*;
import java.security.MessageDigest;
import java.util.*;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.regex.*;

/** In-app updater: looks up the latest published test build, verifies its SHA-256, and hands it to Android's PackageInstaller. */
final class UpdateChecker {
 static final String RELEASE_API="https://api.github.com/repos/2E0LXY/orion-windows/releases/tags/android-latest-test";
 interface Listener{void onStatus(String text,boolean finished);}
 interface Result{void done(Info info,String error);}
 static final class Info{
  final int versionCode;final String apkUrl,shaUrl;
  Info(int v,String a,String s){versionCode=v;apkUrl=a;shaUrl=s;}
 }
 static final AtomicBoolean busy=new AtomicBoolean();
 static volatile Info available;
 static volatile String statusText;
 static volatile Listener listener;

 static void status(String text,boolean finished){statusText=text;Listener l=listener;if(l!=null)l.onStatus(text,finished);}

 // Only HTTPS to GitHub hosts; every redirect hop is re-checked.
 static boolean trusted(String url){
  try{
   URL u=new URL(url);String h=u.getHost().toLowerCase(Locale.ROOT);
   return "https".equals(u.getProtocol())&&(h.equals("github.com")||h.endsWith(".github.com")||h.equals("githubusercontent.com")||h.endsWith(".githubusercontent.com"));
  }catch(MalformedURLException e){return false;}
 }
 static HttpURLConnection open(String start)throws IOException{
  String url=start;
  for(int hop=0;hop<5;hop++){
   if(!trusted(url))throw new IOException("Untrusted download address");
   HttpURLConnection c=(HttpURLConnection)new URL(url).openConnection();
   c.setInstanceFollowRedirects(false);c.setConnectTimeout(15000);c.setReadTimeout(30000);
   c.setRequestProperty("User-Agent","Orion-Android-updater");c.setRequestProperty("Accept","*/*");
   int code=c.getResponseCode();
   if(code>=300&&code<400){String next=c.getHeaderField("Location");c.disconnect();if(next==null)throw new IOException("Bad redirect");url=new URL(new URL(url),next).toString();continue;}
   if(code!=200){c.disconnect();throw new IOException("HTTP "+code);}
   return c;
  }
  throw new IOException("Too many redirects");
 }
 static String readText(String url,int limit)throws IOException{
  HttpURLConnection c=open(url);
  try(InputStream in=c.getInputStream()){
   ByteArrayOutputStream out=new ByteArrayOutputStream();byte[] buf=new byte[8192];int n;
   while((n=in.read(buf))!=-1){out.write(buf,0,n);if(out.size()>limit)throw new IOException("Response too large");}
   return out.toString("UTF-8");
  }finally{c.disconnect();}
 }
 @SuppressWarnings("deprecation")
 static int installedCode(Context c)throws Exception{
  PackageInfo p=c.getPackageManager().getPackageInfo(c.getPackageName(),0);
  return (int)(Build.VERSION.SDK_INT>=28?p.getLongVersionCode():p.versionCode);
 }
 static void check(Context ctx,Result result){
  final Context app=ctx.getApplicationContext();
  new Thread(()->{
   try{
    org.json.JSONObject release=new org.json.JSONObject(readText(RELEASE_API,262144));
    Matcher m=Pattern.compile("versionCode (\\d+)").matcher(release.optString("body",""));
    if(!m.find()){result.done(null,"Release has no version marker");return;}
    int latest=Integer.parseInt(m.group(1));
    String apk=null,sha=null;
    org.json.JSONArray assets=release.optJSONArray("assets");
    for(int i=0;assets!=null&&i<assets.length();i++){
     org.json.JSONObject asset=assets.getJSONObject(i);String name=asset.optString("name"),url=asset.optString("browser_download_url");
     if(name.equals("Orion-Browser-Android-test.apk"))apk=url;else if(name.equals("Orion-Browser-Android-test.apk.sha256"))sha=url;
    }
    if(apk==null||sha==null||!trusted(apk)||!trusted(sha)){result.done(null,"Release assets missing");return;}
    if(latest<=installedCode(app)){available=null;result.done(null,null);return;}
    Info info=new Info(latest,apk,sha);available=info;result.done(info,null);
   }catch(Exception e){result.done(null,e.getClass().getSimpleName()+(e.getMessage()==null?"":": "+e.getMessage()));}
  },"orion-update-check").start();
 }
 static String hex(byte[] bytes){StringBuilder b=new StringBuilder();for(byte x:bytes)b.append(String.format(Locale.ROOT,"%02x",x));return b.toString();}
 static void install(Context ctx,Info info){
  if(!busy.compareAndSet(false,true))return;
  final Context app=ctx.getApplicationContext();
  new Thread(()->{
   PackageInstaller installer=app.getPackageManager().getPackageInstaller();PackageInstaller.Session session=null;
   try{
    String expected=readText(info.shaUrl,4096).trim().split("\\s+")[0].toLowerCase(Locale.ROOT);
    if(!expected.matches("[0-9a-f]{64}"))throw new IOException("Checksum file invalid");
    PackageInstaller.SessionParams params=new PackageInstaller.SessionParams(PackageInstaller.SessionParams.MODE_FULL_INSTALL);
    params.setAppPackageName(app.getPackageName());
    int id=installer.createSession(params);session=installer.openSession(id);
    MessageDigest digest=MessageDigest.getInstance("SHA-256");
    HttpURLConnection c=open(info.apkUrl);long total=c.getContentLengthLong(),done=0;int lastPercent=-1;
    try(InputStream in=c.getInputStream();OutputStream out=session.openWrite("orion-update.apk",0,-1)){
     byte[] buf=new byte[65536];int n;
     while((n=in.read(buf))!=-1){
      out.write(buf,0,n);digest.update(buf,0,n);done+=n;
      int percent=total>0?(int)Math.min(100,done*100/total):-1;
      if(percent!=lastPercent){lastPercent=percent;status("Downloading update"+(percent>=0?" · "+percent+"%":""),false);}
     }
     session.fsync(out);
    }finally{c.disconnect();}
    if(!hex(digest.digest()).equals(expected))throw new IOException("Checksum mismatch, download discarded");
    Intent callback=new Intent(app,UpdateReceiver.class);callback.setPackage(app.getPackageName());
    int flags=PendingIntent.FLAG_UPDATE_CURRENT|(Build.VERSION.SDK_INT>=31?PendingIntent.FLAG_MUTABLE:0);
    PendingIntent pending=PendingIntent.getBroadcast(app,id,callback,flags);
    status("Verified · confirm the install when Android asks",false);
    session.commit(pending.getIntentSender());session.close();session=null;
    busy.set(false);
   }catch(Exception e){
    if(session!=null){try{session.abandon();}catch(Exception ignored){}}
    busy.set(false);
    status("Update failed: "+e.getMessage()+" · tap to retry",true);
   }
  },"orion-update-install").start();
 }
}
