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
 static HttpURLConnection connect(String url,String range,String tag)throws IOException{
  HttpURLConnection c=(HttpURLConnection)new URL(url).openConnection();
  c.setInstanceFollowRedirects(false);c.setConnectTimeout(15000);c.setReadTimeout(20000);
  c.setRequestProperty("Accept-Encoding","identity");c.setRequestProperty("Range",range);
  if(tag!=null)c.setRequestProperty("If-Match",tag);
  return c;
 }
 static void start(Activity a,String url,String name,Fallback fallback){
  if(Build.VERSION.SDK_INT<29||!"https".equalsIgnoreCase(android.net.Uri.parse(url).getScheme())){fallback.run();return;}
  if(!busy.compareAndSet(false,true)){Toast.makeText(a,"A parallel download is already running",0).show();return;}
  AtomicBoolean cancelled=new AtomicBoolean();AtomicLong done=new AtomicLong();
  ProgressDialog dialog=new ProgressDialog(a);dialog.setTitle(name);dialog.setMessage("Checking range support…");dialog.setProgressStyle(ProgressDialog.STYLE_HORIZONTAL);dialog.setMax(100);dialog.setCancelable(false);dialog.setButton(ProgressDialog.BUTTON_NEGATIVE,"Cancel",(d,w)->cancelled.set(true));dialog.show();
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
    eligible=true;temp=File.createTempFile("orion-range-",".part",a.getCacheDir());final File file=temp;
    try(RandomAccessFile f=new RandomAccessFile(file,"rw")){f.setLength(total);}
    pool=Executors.newFixedThreadPool(4);ArrayList<Future<?>> jobs=new ArrayList<>();
    for(int i=0;i<4;i++){final long start=total*i/4,end=total*(i+1)/4-1;
     jobs.add(pool.submit(()->{HttpURLConnection c=connect(url,"bytes="+start+"-"+end,tag);
      try{
       if(c.getResponseCode()!=206||!("bytes "+start+"-"+end+"/"+total).equals(c.getHeaderField("Content-Range"))||!tag.equals(c.getHeaderField("ETag")))throw new IOException("Range changed");
       String encoding=c.getHeaderField("Content-Encoding");if(encoding!=null&&!"identity".equalsIgnoreCase(encoding))throw new IOException("Encoded range");
       try(InputStream in=c.getInputStream();RandomAccessFile f=new RandomAccessFile(file,"rw")){
        f.seek(start);long left=end-start+1;byte[] buf=new byte[65536];
        while(left>0){if(cancelled.get()||Thread.currentThread().isInterrupted())throw new IOException("Cancelled");int n=in.read(buf,0,(int)Math.min(left,buf.length));if(n<0)throw new EOFException();f.write(buf,0,n);left-=n;done.addAndGet(n);}
        if(in.read()!=-1)throw new IOException("Oversized range");
       }
      }finally{c.disconnect();}return null;
     }));
    }
    long began=SystemClock.elapsedRealtime();
    while(true){if(cancelled.get())throw new IOException("Cancelled");boolean complete=true;for(Future<?> j:jobs){if(j.isDone())j.get();else complete=false;}long bytes=done.get();double speed=bytes/1048576.0/Math.max(0.1,(SystemClock.elapsedRealtime()-began)/1000.0);a.runOnUiThread(()->{dialog.setProgress((int)(100*bytes/total));dialog.setMessage(String.format(Locale.UK,"4 connections • %.2f MB/s",speed));});if(complete)break;Thread.sleep(250);}
    if(done.get()!=total||cancelled.get())throw new IOException("Incomplete");
    ContentValues values=new ContentValues();values.put(MediaStore.Downloads.DISPLAY_NAME,name);values.put(MediaStore.Downloads.MIME_TYPE,"application/octet-stream");values.put(MediaStore.Downloads.IS_PENDING,1);
    output=a.getContentResolver().insert(MediaStore.Downloads.EXTERNAL_CONTENT_URI,values);if(output==null)throw new IOException("Cannot create output");
    try(InputStream in=new FileInputStream(file);OutputStream out=a.getContentResolver().openOutputStream(output)){if(out==null)throw new IOException("Cannot open output");byte[] buf=new byte[65536];int n;while((n=in.read(buf))!=-1){if(cancelled.get())throw new IOException("Cancelled");out.write(buf,0,n);}}
    values.clear();values.put(MediaStore.Downloads.IS_PENDING,0);a.getContentResolver().update(output,values,null,null);success=true;
   }catch(Exception e){if(output!=null)a.getContentResolver().delete(output,null,null);}
   finally{if(pool!=null){pool.shutdownNow();try{pool.awaitTermination(25,TimeUnit.SECONDS);}catch(InterruptedException ignored){Thread.currentThread().interrupt();}}if(temp!=null)temp.delete();busy.set(false);boolean ok=success,fall=!eligible&&!cancelled.get();a.runOnUiThread(()->{if(a.isFinishing()||a.isDestroyed())return;dialog.dismiss();if(fall)fallback.run();else Toast.makeText(a,ok?"Download complete":cancelled.get()?"Download cancelled":"Parallel download failed; retry with standard download",Toast.LENGTH_LONG).show();});}
  },"orion-download").start();
 }
}
