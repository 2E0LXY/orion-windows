package uk.community.orion.sync;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.util.Base64;
import org.json.JSONObject;

/** Finds or creates the account data key in the user's own Drive. Concurrent first-run devices converge on the oldest key file. */
public final class KeyBootstrap {
 static final String NAME="orion-key.json";
 private KeyBootstrap(){}
 static DriveStore.FileInfo oldest(DriveStore s)throws IOException{
  DriveStore.FileInfo best=null;
  for(DriveStore.FileInfo f:s.list()){
   if(!NAME.equals(f.name))continue;
   if(best==null||f.created<best.created||(f.created==best.created&&f.id.compareTo(best.id)<0))best=f;
  }
  return best;
 }
 public static byte[] ensureKey(DriveStore s)throws IOException{
  DriveStore.FileInfo found=oldest(s);
  if(found==null){
   JSONObject o=new JSONObject();
   try{o.put("v",1);o.put("key",Base64.getEncoder().encodeToString(SyncCrypto.newKey()));}catch(Exception e){throw new IOException(e);}
   String mine=s.write(NAME,o.toString().getBytes(StandardCharsets.UTF_8),null);
   found=oldest(s);
   if(found==null)throw new IOException("key file vanished");
   if(!found.id.equals(mine))s.delete(mine); // another device won the race; adopt its key, drop ours (never used)
  }
  try{
   JSONObject o=new JSONObject(new String(s.read(found.id),StandardCharsets.UTF_8));
   byte[] key=Base64.getDecoder().decode(o.getString("key"));
   if(o.getInt("v")!=1||key.length!=32)throw new IOException("unsupported key file");
   return key;
  }catch(IOException e){throw e;}catch(Exception e){throw new IOException("invalid key file",e);}
 }
}
