package uk.community.orion;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.Intent;
import android.net.ConnectivityManager;
import android.net.Network;
import android.net.NetworkCapabilities;
import android.net.VpnService;
import android.os.Build;
import android.os.ParcelFileDescriptor;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.URL;
import java.util.Arrays;
import javax.net.ssl.HttpsURLConnection;

/**
 * Opt-in app-scoped DNS VPN. Only the virtual DNS server is routed into the TUN;
 * ordinary traffic is not routed through the service. It is not a general VPN,
 * firewall, parental-control override or a guarantee that all applications use DoH.
 */
public final class OrionDnsVpnService extends VpnService {
 public static final String ACTION_START="uk.community.orion.DNS_VPN_START";
 public static final String ACTION_STOP="uk.community.orion.DNS_VPN_STOP";
 public static volatile boolean active=false;
 public static volatile String state="Off";
 private static final String CHANNEL="orion_dns_vpn";
 private static final byte[] V4={10,(byte)203,0,53};
 private static final byte[] V6={(byte)0xfd,0x70,0,0,0,0,0,0,0,0,0,0,0,0,0,0x53};
 private ParcelFileDescriptor tunnel;
 private Thread worker;
 private volatile boolean running;
 private String doh;
 @Override public int onStartCommand(Intent intent,int flags,int startId){
  if(intent==null)return START_NOT_STICKY;
  if(ACTION_STOP.equals(intent.getAction())){shutdown();stopSelf();return START_NOT_STICKY;}
  if(!ACTION_START.equals(intent.getAction()))return START_NOT_STICKY;
  String uri=DnsSettings.endpoint(getSharedPreferences("MainActivity",MODE_PRIVATE));
  if(!DnsSettings.validDoh(uri)){state="Unavailable: select a supported DoH endpoint";shutdown();stopSelf();return START_NOT_STICKY;}
  shutdown();doh=uri;
  createNotification();
  try{
   Builder builder=new Builder().setSession("Orion WebView Secure DNS").setMtu(1500)
    .addAddress("10.203.0.54",32).addDnsServer("10.203.0.53").addRoute("10.203.0.53",32)
    .addAddress("fd70::54",128).addDnsServer("fd70::53").addRoute("fd70::53",128);
   // Limit the VPN to Orion's UID; other installed apps retain their network routes.
   builder.addAllowedApplication(getPackageName());
   tunnel=builder.establish();
   if(tunnel==null)throw new IllegalStateException("VPN permission not available");
   running=true;active=true;state="Connected — DoH DNS proxy";
   worker=new Thread(this::serve,"orion-dns-vpn");worker.start();
  }catch(Exception e){state="VPN unavailable: "+e.getClass().getSimpleName();shutdown();stopSelf();}
  return START_NOT_STICKY;
 }
 private void createNotification(){
  NotificationManager nm=(NotificationManager)getSystemService(NOTIFICATION_SERVICE);
  if(Build.VERSION.SDK_INT>=26)nm.createNotificationChannel(new NotificationChannel(CHANNEL,"Orion Secure DNS",NotificationManager.IMPORTANCE_LOW));
  Intent open=new Intent(this,MainActivity.class);
  PendingIntent pi=PendingIntent.getActivity(this,0,open,PendingIntent.FLAG_IMMUTABLE|PendingIntent.FLAG_UPDATE_CURRENT);
  Notification n=new Notification.Builder(this,CHANNEL).setContentTitle("Orion WebView Secure DNS")
   .setContentText("Active for Orion only · tap to open settings")
   .setSmallIcon(R.drawable.orion_icon).setContentIntent(pi).setOngoing(true).build();
  if(Build.VERSION.SDK_INT>=34)startForeground(813,n,android.content.pm.ServiceInfo.FOREGROUND_SERVICE_TYPE_SPECIAL_USE);
  else startForeground(813,n);
 }
 private Network underlying(){
  ConnectivityManager cm=(ConnectivityManager)getSystemService(CONNECTIVITY_SERVICE);
  if(cm==null)return null;
  for(Network n:cm.getAllNetworks()){
   NetworkCapabilities cap=cm.getNetworkCapabilities(n);
   if(cap!=null&&cap.hasCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET)
    &&cap.hasCapability(NetworkCapabilities.NET_CAPABILITY_NOT_VPN)
    &&cap.hasCapability(NetworkCapabilities.NET_CAPABILITY_VALIDATED))return n;
  }
  return null;
 }
 private void serve(){
  ParcelFileDescriptor fd=tunnel;
  if(fd==null)return;
  try(FileInputStream input=new FileInputStream(fd.getFileDescriptor());
      FileOutputStream output=new FileOutputStream(fd.getFileDescriptor())){
   byte[] packet=new byte[65535];int n;
   while(running&&(n=input.read(packet))>0){
    byte[] p=Arrays.copyOf(packet,n);
    byte[] request=extractDns(p);
    if(request==null)continue;
    byte[] response=resolve(request);
    if(response==null||!running)continue;
    byte[] reply=encodeReply(p,response);
    if(reply!=null){synchronized(output){output.write(reply);}}
   }
  }catch(Exception e){if(running)state="DNS tunnel stopped: "+e.getClass().getSimpleName();}
  finally{shutdown();stopSelf();}
 }
 private byte[] resolve(byte[] query){
  if(query.length<12||query.length>4096||((query[2]&0x80)!=0))return null;
  Network network=underlying();
  if(network==null){state="No validated underlying network";return null;}
  HttpsURLConnection connection=null;
  try{
   connection=(HttpsURLConnection)network.openConnection(new URL(doh));
   connection.setConnectTimeout(4000);connection.setReadTimeout(4000);
   connection.setRequestMethod("POST");connection.setDoOutput(true);
   connection.setRequestProperty("Content-Type","application/dns-message");
   connection.setRequestProperty("Accept","application/dns-message");
   connection.setInstanceFollowRedirects(false);
   connection.setFixedLengthStreamingMode(query.length);
   try(OutputStream out=connection.getOutputStream()){out.write(query);}
   if(connection.getResponseCode()!=200){state="DoH HTTP "+connection.getResponseCode();return null;}
   String mime=connection.getContentType();
   if(mime==null||!mime.toLowerCase(java.util.Locale.ROOT).startsWith("application/dns-message"))return null;
   try(InputStream in=connection.getInputStream()){
    java.io.ByteArrayOutputStream out=new java.io.ByteArrayOutputStream();
    byte[] buf=new byte[1024];int count;
    while((count=in.read(buf))!=-1){if(out.size()+count>4096)return null;out.write(buf,0,count);}
    byte[] reply=out.toByteArray();
    if(reply.length<12||reply[0]!=query[0]||reply[1]!=query[1]||(reply[2]&0x80)==0)return null;
    state="Connected — DoH DNS proxy";
    return reply;
   }
  }catch(Exception e){state="DoH query failed: "+e.getClass().getSimpleName();return null;}
  finally{if(connection!=null)connection.disconnect();}
 }
 private static int u16(byte[] p,int n){return ((p[n]&255)<<8)|(p[n+1]&255);}
 private static void put16(byte[] p,int i,int n){p[i]=(byte)(n>>8);p[i+1]=(byte)n;}
 private static void put32(byte[] p,int i,long n){p[i]=(byte)(n>>24);p[i+1]=(byte)(n>>16);p[i+2]=(byte)(n>>8);p[i+3]=(byte)n;}
 private static int sum(byte[] p,int from,int len,long initial){
  long s=initial;
  for(int i=from;i<from+len;i+=2)s+=((p[i]&255)<<8)|((i+1<from+len)?(p[i+1]&255):0);
  while((s>>>16)!=0)s=(s&65535)+(s>>>16);
  return (int)s;
 }
 private static int checksum(byte[] p,int from,int len,long pseudo){
  return (~sum(p,from,len,pseudo))&65535;
 }
 private static long pseudo(byte[] packet,int family,int transportLength){
  long s=17+transportLength;
  if(family==4){for(int i=12;i<20;i+=2)s+=u16(packet,i);}
  else {for(int i=8;i<40;i+=2)s+=u16(packet,i);}
  return s;
 }
 private static byte[] extractDns(byte[] p){
  if(p.length<28)return null;
  int v=(p[0]>>>4)&15,ipLen,udp;
  if(v==4){
   ipLen=(p[0]&15)*4;if(ipLen<20||p.length<ipLen+8||u16(p,6)!=0||p[9]!=17)return null;
   if(!Arrays.equals(Arrays.copyOfRange(p,16,20),V4))return null;
  }else if(v==6){
   ipLen=40;if(p.length<48||p[6]!=17||!Arrays.equals(Arrays.copyOfRange(p,24,40),V6))return null;
  }else return null;
  udp=ipLen;int len=u16(p,udp+4);
  if(u16(p,udp+2)!=53||len<20||udp+len>p.length)return null;
  return Arrays.copyOfRange(p,udp+8,udp+len);
 }
 private static byte[] encodeReply(byte[] req,byte[] dns){
  int family=(req[0]>>>4)&15,header=family==4?(req[0]&15)*4:40;
  if(family!=4&&family!=6)return null;
  int udpLength=8+dns.length,total=header+udpLength;
  if(total>1500)return null; // No fragmentation: safely drop oversized replies.
  byte[] p=new byte[total];
  System.arraycopy(req,0,p,0,header);
  if(family==4){
   put16(p,2,total);put16(p,6,0);p[8]=64;p[10]=p[11]=0;
   System.arraycopy(req,16,p,12,4);System.arraycopy(req,12,p,16,4);
   put16(p,10,checksum(p,0,header,0));
  }else{
   put16(p,4,udpLength);p[7]=64;
   System.arraycopy(req,24,p,8,16);System.arraycopy(req,8,p,24,16);
  }
  put16(p,header,u16(req,header+2));put16(p,header+2,u16(req,header));
  put16(p,header+4,udpLength);
  System.arraycopy(dns,0,p,header+8,dns.length);
  long ps=pseudo(p,family,udpLength);
  int check=checksum(p,header,udpLength,ps);put16(p,header+6,check==0?65535:check);
  return p;
 }
 private synchronized void shutdown(){
  running=false;active=false;
  if(tunnel!=null){try{tunnel.close();}catch(Exception ignored){}tunnel=null;}
 }
 @Override public void onRevoke(){state="VPN permission revoked";shutdown();stopSelf();super.onRevoke();}
 @Override public void onDestroy(){shutdown();super.onDestroy();}
}
