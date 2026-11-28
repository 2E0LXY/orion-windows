package uk.community.orion;
import android.content.Context;
import android.net.Uri;
import android.util.Xml;
import org.json.*;
import org.xmlpull.v1.XmlPullParser;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.util.*;
import java.util.regex.*;

/** Imports browser-created bookmark exports selected explicitly by the user. */
final class BookmarkImporter {
 static final int MAX_BYTES=8*1024*1024, MAX_BOOKMARKS=30000;
 static boolean valid(String address){
  try{Uri u=Uri.parse(address);String s=u.getScheme();return ("http".equalsIgnoreCase(s)||"https".equalsIgnoreCase(s))&&u.getHost()!=null&&u.getUserInfo()==null;}catch(Exception e){return false;}
 }
 static Set<String> read(Context context,Uri file)throws Exception{
  String text;
  try(InputStream in=context.getContentResolver().openInputStream(file);ByteArrayOutputStream out=new ByteArrayOutputStream()){
   if(in==null)throw new IOException("Cannot open selected file");byte[] block=new byte[8192];int n;
   while((n=in.read(block))>=0){if(out.size()+n>MAX_BYTES)throw new IOException("File is too large (8 MB limit)");out.write(block,0,n);}
   text=out.toString(StandardCharsets.UTF_8.name());
  }
  LinkedHashSet<String> result=new LinkedHashSet<>();String first=text.trim();
  if(first.startsWith("{")||first.startsWith("["))parseJson(first,result);
  else if(first.startsWith("<")&&(first.contains("NETSCAPE-Bookmark-file-1")||first.matches("(?s).*<(?i:A)\\s+HREF.*")))parseHtml(text,result);
  else throw new IOException("Choose an exported bookmarks HTML or JSON file");
  if(result.isEmpty())throw new IOException("No HTTP or HTTPS bookmarks found");
  return result;
 }
 static void add(Set<String> output,String u)throws IOException{
  if(valid(u)){if(output.size()>=MAX_BOOKMARKS)throw new IOException("Bookmark import exceeds 30,000 entries");output.add(u);}
 }
 static void parseHtml(String content,Set<String> output)throws IOException{
  Matcher m=Pattern.compile("(?i)<a\\b[^>]*\\bhref\\s*=\\s*([\"'])(.*?)\\1",Pattern.DOTALL).matcher(content);
  while(m.find()){String s=m.group(2).replace("&amp;","&").replace("&quot;","\"").replace("&#39;","'");add(output,s);}
 }
 static void parseJson(String content,Set<String> output)throws Exception{Object root=new JSONTokener(content).nextValue();walk(root,output,0);}
 static void walk(Object node,Set<String> output,int depth)throws Exception{
  if(depth>32)throw new IOException("Bookmark nesting too deep");
  if(node instanceof JSONObject){JSONObject obj=(JSONObject)node;String address=obj.optString("url","");add(output,address);Iterator<String> keys=obj.keys();while(keys.hasNext()){String k=keys.next();if(k.equals("url"))continue;Object v=obj.opt(k);if(v instanceof JSONObject||v instanceof JSONArray)walk(v,output,depth+1);}}
  else if(node instanceof JSONArray){JSONArray array=(JSONArray)node;for(int i=0;i<array.length();i++)walk(array.opt(i),output,depth+1);}
 }
}
