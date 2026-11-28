package uk.community.orion;

import android.content.Context;
import android.net.Uri;
import android.webkit.CookieManager;
import org.json.JSONArray;
import org.json.JSONObject;
import org.json.JSONTokener;
import java.io.*;
import java.net.IDN;
import java.nio.charset.StandardCharsets;
import java.text.SimpleDateFormat;
import java.util.*;
import java.util.regex.Pattern;

/** Import only explicitly selected cookie exports into Orion's WebView cookie jar. */
final class CookieImporter {
 static final int MAX_BYTES=2*1024*1024,MAX_COOKIES=2000;
 static final Pattern NAME=Pattern.compile("^[!#$%&'*+.^_`|~0-9A-Za-z-]{1,128}$");
 static final class Entry {
  final String url,header,host;
  Entry(String url,String header,String host){this.url=url;this.header=header;this.host=host;}
 }
 static final class Preview {
  final ArrayList<Entry> entries=new ArrayList<>();
  final TreeSet<String> hosts=new TreeSet<>();
 }
 static Preview read(Context context,Uri source)throws Exception{
  String text;
  try(InputStream in=context.getContentResolver().openInputStream(source);ByteArrayOutputStream out=new ByteArrayOutputStream()){
   if(in==null)throw new IOException("Cannot open selected export");
   byte[] buf=new byte[8192];int n;
   while((n=in.read(buf))!=-1){if(out.size()+n>MAX_BYTES)throw new IOException("Export exceeds 2 MB");out.write(buf,0,n);}
   text=out.toString(StandardCharsets.UTF_8.name()).replace("\uFEFF","");
  }
  Preview preview=new Preview();String trim=text.trim();
  if(trim.startsWith("[")){JSONArray array=new JSONArray(trim);for(int i=0;i<array.length();i++){JSONObject item=array.optJSONObject(i);if(item!=null)json(item,preview);}}
  else if(trim.startsWith("{")){Object obj=new JSONTokener(trim).nextValue();if(obj instanceof JSONObject){JSONObject root=(JSONObject)obj;JSONArray cookies=root.optJSONArray("cookies");if(cookies!=null){for(int i=0;i<cookies.length();i++){JSONObject item=cookies.optJSONObject(i);if(item!=null)json(item,preview);}}else json(root,preview);}}
  else {for(String line:text.split("\\r?\\n"))netscape(line,preview);}
  if(preview.entries.isEmpty())throw new IOException("No valid HTTPS cookies in export. Supported: Netscape cookies.txt or JSON cookie array.");
  return preview;
 }
 private static void json(JSONObject x,Preview p)throws IOException {
  String domain=x.optString("domain","");String name=x.optString("name","");String value=x.optString("value","");
  boolean hostOnly=x.optBoolean("hostOnly",false);
  boolean secure=x.optBoolean("secure",false);boolean httpOnly=x.optBoolean("httpOnly",false);
  String path=x.optString("path","/");String sameSite=x.optString("sameSite","");
  long expires=0;
  Object exp=x.opt("expirationDate");if(exp==null)exp=x.opt("expires");if(exp==null)exp=x.opt("expiry");
  if(exp instanceof Number)expires=((Number)exp).longValue();
  if(exp instanceof String)try{expires=(long)Double.parseDouble((String)exp);}catch(Exception ignored){}
  add(p,domain,name,value,path,secure,httpOnly,hostOnly,expires,sameSite);
 }
 private static void netscape(String line,Preview p)throws IOException{
  if(line.trim().isEmpty()||line.startsWith("#")&&!line.startsWith("#HttpOnly_"))return;
  boolean httpOnly=line.startsWith("#HttpOnly_");
  if(httpOnly)line=line.substring("#HttpOnly_".length());
  String[] parts=line.split("\\t",-1);
  if(parts.length!=7)return;
  long expires=0;try{expires=Long.parseLong(parts[4]);}catch(Exception ignored){}
  add(p,parts[0],parts[5],parts[6],parts[2],"TRUE".equalsIgnoreCase(parts[3]),httpOnly,!"TRUE".equalsIgnoreCase(parts[1]),expires,"");
 }
 private static void add(Preview preview,String domain,String name,String value,String path,boolean secure,boolean httpOnly,boolean hostOnly,long expires,String sameSite)throws IOException{
  if(domain==null||name==null||value==null)return;
  String host=domain.trim().toLowerCase(Locale.ROOT);
  if(host.startsWith("."))host=host.substring(1);
  if(host.isEmpty()||host.length()>253||host.contains("/")||host.contains(":")||host.endsWith("."))return;
  try{host=IDN.toASCII(host,IDN.USE_STD3_ASCII_RULES).toLowerCase(Locale.ROOT);}catch(Exception e){return;}
  if(!host.contains(".")||host.startsWith(".")||host.endsWith("."))return;
  if(!NAME.matcher(name).matches()||value.length()>4096||value.indexOf(';')>=0||hasControl(value))return;
  if(path==null||path.isEmpty()||!path.startsWith("/")||path.indexOf(';')>=0||hasControl(path))path="/";
  if(expires>0&&expires< System.currentTimeMillis()/1000L)return;
  if(preview.entries.size()>=MAX_COOKIES)throw new IOException("Cookie export exceeds 2,000 entries");
  // Bind imported cookies to HTTPS, without weakening the browser's secure-cookie rules.
  StringBuilder h=new StringBuilder(name).append("=").append(value).append("; Path=").append(path);
  if(!hostOnly)h.append("; Domain=").append(host);
  if(secure)h.append("; Secure");
  if(httpOnly)h.append("; HttpOnly");
  if("strict".equalsIgnoreCase(sameSite))h.append("; SameSite=Strict");
  else if("lax".equalsIgnoreCase(sameSite))h.append("; SameSite=Lax");
  else if("no_restriction".equalsIgnoreCase(sameSite)||"none".equalsIgnoreCase(sameSite)){h.append("; SameSite=None; Secure");}
  if(expires>0){SimpleDateFormat fmt=new SimpleDateFormat("EEE, dd MMM yyyy HH:mm:ss 'GMT'",Locale.US);fmt.setTimeZone(TimeZone.getTimeZone("GMT"));h.append("; Expires=").append(fmt.format(new Date(expires*1000L)));}
  preview.entries.add(new Entry("https://"+host+"/",h.toString(),host));preview.hosts.add(host);
 }
 private static boolean hasControl(String v){for(int i=0;i<v.length();i++){char c=v.charAt(i);if(c<32||c==127)return true;}return false;}
 // Android cookie writes are asynchronous; report only acknowledged writes.
 static void apply(Preview preview,java.util.function.IntConsumer complete){
  CookieManager cm=CookieManager.getInstance();
  int count=preview.entries.size();
  if(count==0){complete.accept(0);return;}
  java.util.concurrent.atomic.AtomicInteger pending=new java.util.concurrent.atomic.AtomicInteger(count);
  java.util.concurrent.atomic.AtomicInteger accepted=new java.util.concurrent.atomic.AtomicInteger();
  for(Entry entry:preview.entries){
   cm.setCookie(entry.url,entry.header,stored->{
    if(Boolean.TRUE.equals(stored))accepted.incrementAndGet();
    if(pending.decrementAndGet()==0){
     cm.flush();
     complete.accept(accepted.get());
    }
   });
  }
 }
}
