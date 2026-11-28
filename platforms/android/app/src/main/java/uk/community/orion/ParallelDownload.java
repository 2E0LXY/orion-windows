package uk.community.orion;
import android.app.*;
import android.content.*;
import android.os.*;
import android.provider.MediaStore;
import android.widget.*;
import java.io.*;
import java.net.*;
import java.util.*;
import java.util.concurrent.*;
import java.util.concurrent.atomic.*;

final class ParallelDownload {
 interface Fallback { void run(); }
 static final AtomicBoolean busy=new AtomicBoolean();
 // One active transfer. Progress is retained until the next transfer starts.
 static volatile State state;
 static final class State {
  final String name;
  final String url;
  volatile String error="";
  final AtomicBoolean cancelled=new AtomicBoolean();
  final AtomicLong done=new AtomicLong();
  final AtomicLongArray segmentBytes=new AtomicLongArray(4);
  volatile long total=0;
  volatile double bytesPerSecond=0;
  volatile String status="Checking server support";
  volatile boolean finished=false,success=false;
  State(String n,String u){name=n;url=u;}
 }
 // Activity was recreated or closed (for example rotation) before the transfer failed: queue the system download directly.
 static void queueStandard(Context c,String url,String name,State transfer){
  try{
   DownloadManager.Request req=new DownloadManager.Request(android.net.Uri.parse(url));
   req.setTitle(name);req.setNotificationVisibility(DownloadManager.Request.VISIBILITY_VISIBLE_NOTIFY_COMPLETED);
   req.setDestinationInExternalPublicDir(Environment.DIRECTORY_DOWNLOADS,name);
   long id=((DownloadManager)c.getSystemService(Context.DOWNLOAD_SERVICE)).enqueue(req);
   SharedPreferences prefs=c.getSharedPreferences(MainActivity.class.getName(),Context.MODE_PRIVATE);
   String ids=id+(prefs.getString("download_ids","").isEmpty()?"":","+prefs.getString("download_ids",""));
   prefs.edit().putString("download_ids",ids).putString("download_url_"+id,url).putString("download_name_"+id,name).apply();
   transfer.status="Parallel failed · standard download queued";
  }catch(Exception e){transfer.status="Automatic fallback failed · retry manually";transfer.error=e.getClass().getSimpleName();}
 }
 static State snapshot(){return state;}
 static void cancel(){State s=state;if(s!=null&&!s.finished)s.cancelled.set(true);}
 static HttpURLConnection connect(String url,String range,String tag)throws IOException{
  HttpURLConnection c=(HttpURLConnection)new URL(url).openConnection();
  c.setInstanceFollowRedirects(false);c.setConnectTimeout(15000);c.setReadTimeout(20000);
  c.setRequestProperty("Accept-Encoding","identity");c.setRequestProperty("Range",range);
  if(tag!=null)c.setRequestProperty("If-Match",tag);
  return c;
 }
 static void start(Activity a,String url,String name,Fallback fallback){
  if(Build.VERSION.SDK_INT<29||!"https".equalsIgnoreCase(android.net.Uri.parse(url).getScheme())){fallback.run();return;}
  if(!busy.compareAndSet(false,true)){Toast.makeText(a,"A parallel download is already running",Toast.LENGTH_SHORT).show();return;}
  final State transfer=new State(name,url);
  state=transfer;
  Toast.makeText(a,"Download started · swipe up on the bottom toolbar for progress",Toast.LENGTH_LONG).show();
  final Context app=a.getApplicationContext();
  new Thread(()->{
   File temp=null;ExecutorService pool=null;android.net.Uri output=null;boolean eligible=false,success=false;
   try{
    HttpURLConnection probe=connect(url,"bytes=0-0",null);long total;String tag;
    try{
     if(probe.getResponseCode()!=206)throw new IOException("Range unsupported");
     String cr=probe.getHeaderField("Content-Range");tag=probe.getHeaderField("ETag");
     if(cr==null||!cr.matches("bytes 0-0/[0-9]+")||tag==null||!tag.startsWith("\"")||!tag.endsWith("\""))throw new IOException("No stable range validator");
     total=Long.parseLong(cr.substring(10));if(total<1048576||total>2147483648L)throw new IOException("Size outside experimental range");
    }finally{probe.disconnect();}
    eligible=true;transfer.total=total;transfer.status="Downloading (4 connections)";
    temp=File.createTempFile("orion-range-",".part",app.getCacheDir());final File file=temp;
    try(RandomAccessFile f=new RandomAccessFile(file,"rw")){f.setLength(total);}
    pool=Executors.newFixedThreadPool(4);ArrayList<Future<?>> jobs=new ArrayList<>();
    for(int i=0;i<4;i++){final int segment=i;final long start=total*i/4,end=total*(i+1)/4-1;
     jobs.add(pool.submit(()->{HttpURLConnection c=connect(url,"bytes="+start+"-"+end,tag);
      try{
       if(c.getResponseCode()!=206||!("bytes "+start+"-"+end+"/"+total).equals(c.getHeaderField("Content-Range"))||!tag.equals(c.getHeaderField("ETag")))throw new IOException("Range changed");
       String encoding=c.getHeaderField("Content-Encoding");if(encoding!=null&&!"identity".equalsIgnoreCase(encoding))throw new IOException("Encoded range");
       try(InputStream in=c.getInputStream();RandomAccessFile f=new RandomAccessFile(file,"rw")){
        f.seek(start);long left=end-start+1;byte[] buf=new byte[65536];
        while(left>0){if(transfer.cancelled.get()||Thread.currentThread().isInterrupted())throw new IOException("Cancelled");int n=in.read(buf,0,(int)Math.min(left,buf.length));if(n<0)throw new EOFException();f.write(buf,0,n);left-=n;transfer.done.addAndGet(n);transfer.segmentBytes.addAndGet(segment,n);}
        if(in.read()!=-1)throw new IOException("Oversized range");
       }
      }finally{c.disconnect();}return null;
     }));
    }
    long began=SystemClock.elapsedRealtime();
    while(true){
     if(transfer.cancelled.get())throw new IOException("Cancelled");
     boolean complete=true;
     for(Future<?> j:jobs){if(j.isDone())j.get();else complete=false;}
     long bytes=transfer.done.get();
     transfer.bytesPerSecond=bytes*1000.0/Math.max(1,SystemClock.elapsedRealtime()-began);
     if(complete)break;
     Thread.sleep(250);
    }
    if(transfer.done.get()!=total||transfer.cancelled.get())throw new IOException("Incomplete");
    transfer.status="Saving to Downloads";
    ContentValues values=new ContentValues();values.put(MediaStore.Downloads.DISPLAY_NAME,name);values.put(MediaStore.Downloads.MIME_TYPE,"application/octet-stream");values.put(MediaStore.Downloads.IS_PENDING,1);
    output=app.getContentResolver().insert(MediaStore.Downloads.EXTERNAL_CONTENT_URI,values);if(output==null)throw new IOException("Cannot create output");
    try(InputStream in=new FileInputStream(file);OutputStream out=app.getContentResolver().openOutputStream(output)){
     if(out==null)throw new IOException("Cannot open output");byte[] buf=new byte[65536];int n;
     while((n=in.read(buf))!=-1){if(transfer.cancelled.get())throw new IOException("Cancelled");out.write(buf,0,n);}
    }
    values.clear();values.put(MediaStore.Downloads.IS_PENDING,0);app.getContentResolver().update(output,values,null,null);success=true;
   }catch(Exception e){
    transfer.error=e.getClass().getSimpleName()+": "+(e.getMessage()==null?"transfer interrupted":e.getMessage());
    if(output!=null)app.getContentResolver().delete(output,null,null);
   }finally{
    if(pool!=null){pool.shutdownNow();try{pool.awaitTermination(25,TimeUnit.SECONDS);}catch(InterruptedException ignored){Thread.currentThread().interrupt();}}
    if(temp!=null)temp.delete();
    boolean fallbackNeeded=!success&&!transfer.cancelled.get();
    transfer.success=success;
    transfer.finished=true;
    transfer.status=success?"Complete":transfer.cancelled.get()?"Cancelled":fallbackNeeded?"Parallel failed · switching to standard download":"Failed";
    busy.set(false);
    final boolean ok=success;
    a.runOnUiThread(()->{
     boolean gone=a.isFinishing()||a.isDestroyed();
     if(fallbackNeeded){
      if(gone){queueStandard(app,url,name,transfer);return;}
      try{fallback.run();}catch(Exception e){transfer.status="Automatic fallback failed · retry manually";transfer.error=e.getClass().getSimpleName();}
     }
     else if(!gone)Toast.makeText(a,ok?"Download complete":transfer.cancelled.get()?"Download cancelled":"Parallel download failed; retry with standard download",Toast.LENGTH_LONG).show();
    });
   }
  },"orion-download").start();
 }
}
