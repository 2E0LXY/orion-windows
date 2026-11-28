package uk.community.orion;
import java.util.Locale;
final class AdBlocker {
 private static final String[] DOMAINS={"doubleclick.net","googlesyndication.com","googleadservices.com","adnxs.com","advertising.com","rubiconproject.com","pubmatic.com","openx.net","criteo.com","criteo.net","adform.net","casalemedia.com","adsrvr.org","smartadserver.com","yieldmo.com","taboola.com","outbrain.com"};
 static volatile java.util.Set<String> extra=java.util.Collections.emptySet();
 static void load(android.content.Context c,android.content.SharedPreferences prefs){java.util.HashSet<String> set=new java.util.HashSet<>();for(String key:new String[]{"easylist","easyprivacy"}){if(!prefs.getBoolean(key,false))continue;try(java.io.BufferedReader reader=new java.io.BufferedReader(new java.io.InputStreamReader(c.openFileInput(key+".domains"),java.nio.charset.StandardCharsets.UTF_8))){String line;while((line=reader.readLine())!=null&&set.size()<200000)set.add(line);}catch(java.io.IOException ignored){}}extra=java.util.Collections.unmodifiableSet(set);}
 static java.util.Set<String> parse(java.io.InputStream input)throws java.io.IOException{java.util.HashSet<String> out=new java.util.HashSet<>();try(java.io.BufferedReader reader=new java.io.BufferedReader(new java.io.InputStreamReader(input,java.nio.charset.StandardCharsets.UTF_8))){String line;long total=0;while((line=reader.readLine())!=null){total+=line.length()+1;if(total>8000000)throw new java.io.IOException("Filter list too large");if(line.matches("^\\|\\|[a-zA-Z0-9.-]+\\^$")){String d=line.substring(2,line.length()-1).toLowerCase(Locale.ROOT);if(d.contains(".")&&!d.startsWith(".")&&!d.endsWith("."))out.add(d);}}}return out;}
 static void update(android.content.Context c,String key)throws java.io.IOException{if(!key.equals("easylist")&&!key.equals("easyprivacy"))throw new java.io.IOException("Unknown list");java.net.HttpURLConnection conn=(java.net.HttpURLConnection)new java.net.URL("https://easylist.to/easylist/"+key+".txt").openConnection();conn.setConnectTimeout(10000);conn.setReadTimeout(15000);conn.setInstanceFollowRedirects(false);try{if(conn.getResponseCode()!=200)throw new java.io.IOException("Download failed");java.util.Set<String> domains=parse(conn.getInputStream());if(domains.isEmpty())throw new java.io.IOException("Empty list");android.util.AtomicFile file=new android.util.AtomicFile(new java.io.File(c.getFilesDir(),key+".domains"));java.io.FileOutputStream stream=file.startWrite();try{for(String d:domains)stream.write((d+"\n").getBytes(java.nio.charset.StandardCharsets.UTF_8));file.finishWrite(stream);}catch(java.io.IOException e){file.failWrite(stream);throw e;}}finally{conn.disconnect();}}
 static boolean blocks(String host){
  if(host==null)return false;
  String h=host.toLowerCase(Locale.ROOT);
  if(h.endsWith("."))h=h.substring(0,h.length()-1);
  for(String d:DOMAINS)if(h.equals(d)||h.endsWith("."+d))return true;
  for(String d=h;;){if(extra.contains(d))return true;int i=d.indexOf('.');if(i<0)break;d=d.substring(i+1);}
  return false;
 }
}
