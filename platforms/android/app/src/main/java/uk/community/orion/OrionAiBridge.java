package uk.community.orion;

import android.app.Activity;
import android.os.Handler;
import android.os.Looper;
import java.io.BufferedReader;
import java.io.BufferedWriter;
import java.io.InputStreamReader;
import java.io.OutputStreamWriter;
import java.net.InetAddress;
import java.net.ServerSocket;
import java.net.Socket;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.SecureRandom;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicReference;
import org.json.JSONArray;
import org.json.JSONObject;

/**
 * Local-only experimental MCP/JSON-RPC endpoint.
 * Manual on-device approval gates every request. It never binds to LAN or public interfaces.
 */
final class OrionAiBridge {
 interface Host {
  boolean canControl();
  String describePage();
  String currentUrl();
  void openUrl(String url);
  void requestApproval(String tool, String detail, Approval response);
 }
 interface Approval { void complete(boolean accepted); }
 private static final int MAX_BODY=16384, TIMEOUT_MS=20000, PORT=18796;
 private final Activity activity;
 private final Host host;
 private final Handler ui=new Handler(Looper.getMainLooper());
 private final SecureRandom random=new SecureRandom();
 private final Map<String,Boolean> scopes=Collections.synchronizedMap(new LinkedHashMap<>());
 private volatile boolean running;
 private volatile String token;
 private volatile String pairingCode;
 private volatile long deadline;
 private ServerSocket server;
 private Thread worker;
 private volatile String latestEvent="Disconnected";

 OrionAiBridge(Activity activity,Host host){this.activity=activity;this.host=host;}
 boolean running(){return running;}
 int port(){return PORT;}
 String status(){return running?"Connected locally · "+latestEvent:"Disconnected";}
 String pairingCode(){return pairingCode;}
 String token(){return token;}
 void start(){
  if(running)return;
  byte[] secret=new byte[32];random.nextBytes(secret);
  StringBuilder out=new StringBuilder();for(byte b:secret)out.append(String.format(java.util.Locale.ROOT,"%02x",b&255));
  token=out.toString();
  pairingCode=String.format(java.util.Locale.ROOT,"%06d",random.nextInt(1000000));
  deadline=System.currentTimeMillis()+15*60*1000L;
  scopes.clear();scopes.put("browser_info",true);scopes.put("navigate",true);
  try{
   server=new ServerSocket(PORT,8,InetAddress.getByName("127.0.0.1"));
   running=true;latestEvent="Awaiting approval";
   worker=new Thread(this::serve,"orion-ai-local");worker.start();
  }catch(Exception e){
   running=false;token=null;pairingCode=null;latestEvent="Unable to bind local port";
   try{if(server!=null)server.close();}catch(Exception ignored){}
   server=null;
  }
 }
 void stop(){
  running=false;deadline=0;token=null;pairingCode=null;latestEvent="Disconnected";
  try{if(server!=null)server.close();}catch(Exception ignored){}
  server=null;
 }
 private void serve(){
  while(running){
   try(Socket client=server.accept()){
    client.setSoTimeout(TIMEOUT_MS);
    handle(client);
   }catch(Exception e){if(!running)break;}
  }
 }
 private boolean authorise(String header){
  if(!running||System.currentTimeMillis()>deadline){stop();return false;}
  if(header==null||!header.startsWith("Bearer ")||token==null)return false;
  byte[] a=header.substring(7).getBytes(StandardCharsets.UTF_8);
  byte[] b=token.getBytes(StandardCharsets.UTF_8);
  return MessageDigest.isEqual(a,b);
 }
 private void handle(Socket socket) throws Exception{
  BufferedReader in=new BufferedReader(new InputStreamReader(socket.getInputStream(),StandardCharsets.UTF_8));
  BufferedWriter out=new BufferedWriter(new OutputStreamWriter(socket.getOutputStream(),StandardCharsets.UTF_8));
  String request=in.readLine();
  if(request==null)return;
  String[] parts=request.split(" ");
  if(parts.length!=3||!"HTTP/1.1".equals(parts[2])){respond(out,400,"Bad Request","{}");return;}
  String path=parts[1],verb=parts[0];
  int length=-1;
  String authorisation=null,origin=null,hostHeader=null,encoding=null,line;
  while((line=in.readLine())!=null&&!line.isEmpty()){
   int sep=line.indexOf(':');if(sep<=0)continue;
   String key=line.substring(0,sep).trim().toLowerCase(java.util.Locale.ROOT),value=line.substring(sep+1).trim();
   if(key.equals("authorization"))authorisation=value;
   if(key.equals("content-length")){try{length=Integer.parseInt(value);}catch(Exception ignored){}}
   if(key.equals("origin"))origin=value;
   if(key.equals("host"))hostHeader=value;
   if(key.equals("transfer-encoding"))encoding=value;
  }
  if(origin!=null||encoding!=null||hostHeader==null||!(hostHeader.equals("127.0.0.1:"+PORT)||hostHeader.equals("localhost:"+PORT))){
   respond(out,403,"Forbidden","{}");return;
  }
  if(!authorise(authorisation)){respond(out,401,"Unauthorized","{}");return;}
  if(!"POST".equals(verb)||!"/mcp".equals(path)||length<0||length>MAX_BODY){respond(out,400,"Bad Request","{}");return;}
  char[] chars=new char[length];int read=0;
  while(read<length){int n=in.read(chars,read,length-read);if(n<0)throw new java.io.EOFException();read+=n;}
  JSONObject requestJson;
  try{requestJson=new JSONObject(new String(chars));}
  catch(Exception e){respond(out,400,"Bad Request","{}");return;}
  JSONObject reply=dispatch(requestJson);
  respond(out,200,"OK",reply.toString());
 }
 private void respond(BufferedWriter out,int status,String label,String body)throws Exception{
  byte[] bytes=body.getBytes(StandardCharsets.UTF_8);
  out.write("HTTP/1.1 "+status+" "+label+"\r\nContent-Type: application/json; charset=utf-8\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nConnection: close\r\nContent-Length: "+bytes.length+"\r\n\r\n");
  out.write(body);out.flush();
 }
 private JSONObject dispatch(JSONObject msg)throws Exception{
  Object id=msg.opt("id");
  String method=msg.optString("method","");
  JSONObject result=new JSONObject();
  if(method.equals("initialize")){
   result.put("protocolVersion","2025-06-18");
   result.put("capabilities",new JSONObject().put("tools",new JSONObject()));
   result.put("serverInfo",new JSONObject().put("name","orion-local").put("version","0.1"));
  }else if(method.equals("ping")){
   result.put("ok",true);
  }else if(method.equals("tools/list")){
   JSONArray tools=new JSONArray();
   tools.put(new JSONObject().put("name","browser_info").put("description","Read the current tab URL and page title after approval").put("inputSchema",new JSONObject().put("type","object").put("properties",new JSONObject()).put("additionalProperties",false)));
   tools.put(new JSONObject().put("name","navigate").put("description","Ask the user to open an HTTPS page in the current normal browser tab").put("inputSchema",new JSONObject().put("type","object").put("properties",new JSONObject().put("url",new JSONObject().put("type","string"))).put("required",new JSONArray().put("url")).put("additionalProperties",false)));
   result.put("tools",tools);
  }else if(method.equals("tools/call")){
   JSONObject params=msg.optJSONObject("params");
   String name=params==null?"":params.optString("name","");
   JSONObject args=params==null?null:params.optJSONObject("arguments");
   if(!scopes.containsKey(name)||args==null){return failure(id,-32602,"Invalid tool request");}
   String url=args.optString("url","");
   if(name.equals("navigate")){
    android.net.Uri parsed=android.net.Uri.parse(url);
    if(!"https".equalsIgnoreCase(parsed.getScheme())||parsed.getHost()==null||parsed.getUserInfo()!=null||url.length()>2048)return failure(id,-32602,"Only valid HTTPS URLs are allowed");
   }else if(args.length()!=0)return failure(id,-32602,"Unexpected arguments");
   CountDownLatch done=new CountDownLatch(1);
   AtomicReference<String> output=new AtomicReference<>("Rejected or unavailable");
   ui.post(()->{
    if(!running||!host.canControl()){done.countDown();return;}
    host.requestApproval(name,name.equals("navigate")?url:"Read current page title and URL",approved->{
     if(approved&&running&&host.canControl()){
      try{if(name.equals("navigate")){host.openUrl(url);output.set("Navigation requested");}
       else output.set(host.describePage());}
      catch(Exception e){output.set("Action failed");}
     }
     latestEvent=name+" · "+(approved?"approved":"rejected");done.countDown();
    });
   });
   if(!done.await(15,TimeUnit.SECONDS))return failure(id,-32001,"User approval timed out");
   result.put("content",new JSONArray().put(new JSONObject().put("type","text").put("text",output.get())));
  }else return failure(id,-32601,"Method not found");
  return new JSONObject().put("jsonrpc","2.0").put("id",id==null?JSONObject.NULL:id).put("result",result);
 }
 private JSONObject failure(Object id,int code,String message)throws Exception{
  return new JSONObject().put("jsonrpc","2.0").put("id",id==null?JSONObject.NULL:id).put("error",new JSONObject().put("code",code).put("message",message));
 }
}
