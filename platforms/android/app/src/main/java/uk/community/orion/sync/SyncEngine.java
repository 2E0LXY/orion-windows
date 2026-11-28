package uk.community.orion.sync;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.util.*;
import org.json.JSONObject;

/**
 * Conflict-free sync over per-device files. A device only ever writes its own file
 * (orion-&lt;category&gt;-&lt;deviceId&gt;.json); readers merge every device's file, last writer wins on (lamport, deviceId).
 */
public final class SyncEngine {
 public static final int MAX_FILE=2*1024*1024;
 public static final Set<String> CATEGORIES=new HashSet<>(Arrays.asList("bookmarks","settings","readinglist","tabs","history","cookies","passwords"));
 public static final class Rec {
  public final String id,device,payload;public final long lamport;public final boolean deleted;
  public Rec(String id,long lamport,boolean deleted,String device,String payload){this.id=id;this.lamport=lamport;this.deleted=deleted;this.device=device;this.payload=payload;}
  boolean newerThan(Rec o){return lamport>o.lamport||(lamport==o.lamport&&device.compareTo(o.device)>0);}
 }
 private final DriveStore store;private final String device;
 public SyncEngine(DriveStore store,String deviceId){
  if(!deviceId.matches("[A-Za-z0-9_-]{4,64}"))throw new IllegalArgumentException("bad device id");
  this.store=store;this.device=deviceId;
 }
 static String fileName(String category,String device){return "orion-"+category+"-"+device+".json";}
 static void check(String category){if(!CATEGORIES.contains(category))throw new IllegalArgumentException("unknown category");}

 /** Merged view of one category across all devices (including this one). Malformed or oversized files are ignored. */
 public Map<String,Rec> pull(String category)throws IOException{
  check(category);
  String prefix="orion-"+category+"-";
  Map<String,Rec> merged=new HashMap<>();
  for(DriveStore.FileInfo f:store.list()){
   if(!f.name.startsWith(prefix)||!f.name.endsWith(".json"))continue;
   String owner=f.name.substring(prefix.length(),f.name.length()-5);
   if(!owner.matches("[A-Za-z0-9_-]{4,64}"))continue;
   try{
    byte[] data=store.read(f.id);if(data.length>MAX_FILE)continue;
    JSONObject root=new JSONObject(new String(data,StandardCharsets.UTF_8));
    if(root.getInt("v")!=1||!category.equals(root.getString("category"))||!owner.equals(root.getString("device")))continue;
    JSONObject recs=root.getJSONObject("records");
    for(Iterator<String> it=recs.keys();it.hasNext();){
     String id=it.next();JSONObject r=recs.getJSONObject(id);
     Rec rec=new Rec(id,r.getLong("l"),r.optBoolean("d",false),owner,r.optString("p",""));
     Rec cur=merged.get(id);if(cur==null||rec.newerThan(cur))merged.put(id,rec);
    }
   }catch(Exception ignored){/* one bad device file must not break sync */}
  }
  return merged;
 }

 /** Replaces this device's file for the category with the records it owns. */
 public void push(String category,Collection<Rec> own)throws IOException{
  check(category);
  JSONObject root=new JSONObject(),recs=new JSONObject();
  try{
   root.put("v",1);root.put("category",category);root.put("device",device);
   for(Rec r:own){
    if(!r.device.equals(device))throw new IllegalArgumentException("record not owned by this device");
    JSONObject o=new JSONObject();o.put("l",r.lamport);if(r.deleted)o.put("d",true);else o.put("p",r.payload);
    recs.put(r.id,o);
   }
   root.put("records",recs);
  }catch(org.json.JSONException e){throw new IOException(e);}
  byte[] data=root.toString().getBytes(StandardCharsets.UTF_8);
  if(data.length>MAX_FILE)throw new IOException("category too large; compact or reduce history");
  String name=fileName(category,device),existing=null;
  for(DriveStore.FileInfo f:store.list())if(f.name.equals(name)){existing=f.id;break;}
  store.write(name,data,existing);
 }

 /** Removes a revoked device's files so it can no longer contribute and is detected as signed out. */
 public void revokeDevice(String otherDevice)throws IOException{
  if(otherDevice.equals(device))throw new IllegalArgumentException("cannot revoke self");
  String suffix="-"+otherDevice+".json";
  for(DriveStore.FileInfo f:store.list())if(f.name.startsWith("orion-")&&f.name.endsWith(suffix))store.delete(f.id);
 }
}
