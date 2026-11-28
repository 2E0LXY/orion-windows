package uk.community.orion.sync;
import static org.junit.Assert.*;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.security.GeneralSecurityException;
import java.util.*;
import org.junit.Test;

public class SyncCoreTest {
 static final class MemStore implements DriveStore {
  final Map<String,byte[]> data=new LinkedHashMap<>();final Map<String,FileInfo> info=new LinkedHashMap<>();int n=0;long clock=1000;
  public List<FileInfo> list(){return new ArrayList<>(info.values());}
  public byte[] read(String id)throws IOException{byte[] b=data.get(id);if(b==null)throw new IOException("404");return b;}
  public String write(String name,byte[] d,String existing){String id=existing!=null?existing:"f"+(++n);data.put(id,d);if(existing==null)info.put(id,new FileInfo(id,name,clock++));return id;}
  public void delete(String id){data.remove(id);info.remove(id);}
 }
 static SyncEngine.Rec rec(String id,long l,boolean del,String dev,String p){return new SyncEngine.Rec(id,l,del,dev,p);}

 @Test public void sealOpenRoundTripAndTamper()throws Exception{
  byte[] k=SyncCrypto.newKey();String s=SyncCrypto.seal(k,"bookmarks|a","hello".getBytes(StandardCharsets.UTF_8));
  assertEquals("hello",new String(SyncCrypto.open(k,"bookmarks|a",s),StandardCharsets.UTF_8));
  try{SyncCrypto.open(k,"bookmarks|b",s);fail("aad swap accepted");}catch(GeneralSecurityException expected){}
  try{SyncCrypto.open(SyncCrypto.newKey(),"bookmarks|a",s);fail("wrong key accepted");}catch(GeneralSecurityException expected){}
  char[] c=s.toCharArray();c[c.length-3]=c[c.length-3]=='A'?'B':'A';
  try{SyncCrypto.open(k,"bookmarks|a",new String(c));fail("tampered ciphertext accepted");}catch(GeneralSecurityException expected){}
  assertNotEquals(s,SyncCrypto.seal(k,"bookmarks|a","hello".getBytes(StandardCharsets.UTF_8)));
 }

 @Test public void twoDevicesMergeLastWriterWins()throws Exception{
  MemStore st=new MemStore();SyncEngine a=new SyncEngine(st,"devA"),b=new SyncEngine(st,"devB");
  a.push("bookmarks",Arrays.asList(rec("x",1,false,"devA","pa1"),rec("only-a",1,false,"devA","pa")));
  b.push("bookmarks",Arrays.asList(rec("x",2,false,"devB","pb2")));
  Map<String,SyncEngine.Rec> view=a.pull("bookmarks");
  assertEquals("pb2",view.get("x").payload);assertEquals("pa",view.get("only-a").payload);
  // Equal lamport: higher device id wins deterministically on every device.
  a.push("bookmarks",Arrays.asList(rec("y",5,false,"devA","ya")));
  b.push("bookmarks",Arrays.asList(rec("x",2,false,"devB","pb2"),rec("y",5,false,"devB","yb")));
  assertEquals("yb",a.pull("bookmarks").get("y").payload);assertEquals("yb",b.pull("bookmarks").get("y").payload);
 }

 @Test public void tombstoneBeatsOlderRecordAndNewerRecordBeatsTombstone()throws Exception{
  MemStore st=new MemStore();SyncEngine a=new SyncEngine(st,"devA"),b=new SyncEngine(st,"devB");
  a.push("tabs",Arrays.asList(rec("t",1,false,"devA","p")));
  b.push("tabs",Arrays.asList(rec("t",2,true,"devB","")));
  assertTrue(a.pull("tabs").get("t").deleted);
  a.push("tabs",Arrays.asList(rec("t",3,false,"devA","p3")));
  assertFalse(b.pull("tabs").get("t").deleted);
 }

 @Test public void categoriesAreIsolatedAndValidated()throws Exception{
  MemStore st=new MemStore();SyncEngine a=new SyncEngine(st,"devA");
  a.push("bookmarks",Arrays.asList(rec("x",1,false,"devA","p")));
  assertTrue(a.pull("settings").isEmpty());
  try{a.pull("../etc");fail();}catch(IllegalArgumentException expected){}
  try{a.push("bookmarks",Arrays.asList(rec("x",1,false,"devB","p")));fail("foreign record accepted");}catch(IllegalArgumentException expected){}
  try{new SyncEngine(st,"bad/id");fail();}catch(IllegalArgumentException expected){}
 }

 @Test public void malformedOrForeignFilesAreIgnored()throws Exception{
  MemStore st=new MemStore();SyncEngine a=new SyncEngine(st,"devA");
  a.push("bookmarks",Arrays.asList(rec("x",1,false,"devA","p")));
  st.write("orion-bookmarks-devZ.json","not json".getBytes(StandardCharsets.UTF_8),null);
  // File claims to belong to devA but is named devQ: rejected.
  st.write("orion-bookmarks-devQ.json","{\"v\":1,\"category\":\"bookmarks\",\"device\":\"devA\",\"records\":{\"evil\":{\"l\":99,\"p\":\"x\"}}}".getBytes(StandardCharsets.UTF_8),null);
  Map<String,SyncEngine.Rec> v=a.pull("bookmarks");
  assertEquals(1,v.size());assertTrue(v.containsKey("x"));
 }

 @Test public void revokeDeletesOnlyThatDevicesFiles()throws Exception{
  MemStore st=new MemStore();SyncEngine a=new SyncEngine(st,"devA"),b=new SyncEngine(st,"devB");
  a.push("bookmarks",Arrays.asList(rec("x",1,false,"devA","pa")));b.push("bookmarks",Arrays.asList(rec("y",1,false,"devB","pb")));
  b.push("tabs",Arrays.asList(rec("z",1,false,"devB","pz")));
  a.revokeDevice("devB");
  assertEquals(1,a.pull("bookmarks").size());assertTrue(a.pull("tabs").isEmpty());
  try{a.revokeDevice("devA");fail();}catch(IllegalArgumentException expected){}
 }

 @Test public void keyBootstrapCreatesOnceAndRaceConvergesOnOldest()throws Exception{
  MemStore st=new MemStore();
  byte[] k1=KeyBootstrap.ensureKey(st);assertEquals(32,k1.length);
  assertArrayEquals(k1,KeyBootstrap.ensureKey(st));
  assertEquals(1,st.list().size());
  // Simulate two devices that both created a key before seeing each other's.
  MemStore race=new MemStore();
  String older=race.write("orion-key.json","{\"v\":1,\"key\":\"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=\"}".getBytes(StandardCharsets.UTF_8),null);
  race.write("orion-key.json","{\"v\":1,\"key\":\"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB=\"}".getBytes(StandardCharsets.UTF_8),null);
  byte[] adopted=KeyBootstrap.ensureKey(race);
  assertArrayEquals(Base64.getDecoder().decode("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA="),adopted);
  assertNotNull(race.info.get(older));
 }

 @Test public void endToEndEncryptedRecordsThroughStore()throws Exception{
  MemStore st=new MemStore();byte[] key=KeyBootstrap.ensureKey(st);
  SyncEngine a=new SyncEngine(st,"devA"),b=new SyncEngine(st,"devB");
  String sealed=SyncCrypto.seal(key,"cookies|example.org",("{\"name\":\"sid\"}").getBytes(StandardCharsets.UTF_8));
  a.push("cookies",Arrays.asList(rec("example.org",1,false,"devA",sealed)));
  SyncEngine.Rec got=b.pull("cookies").get("example.org");
  assertEquals("{\"name\":\"sid\"}",new String(SyncCrypto.open(key,"cookies|example.org",got.payload),StandardCharsets.UTF_8));
  for(byte[] raw:st.data.values())assertFalse(new String(raw,StandardCharsets.UTF_8).contains("sid\\\""));
 }
}
