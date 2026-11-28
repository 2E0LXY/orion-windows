package uk.community.orion;

import android.content.Context;
import android.net.Uri;
import android.security.keystore.KeyGenParameterSpec;
import android.security.keystore.KeyProperties;
import android.util.AtomicFile;
import org.json.JSONArray;
import org.json.JSONObject;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.security.KeyStore;
import java.security.SecureRandom;
import java.util.*;
import javax.crypto.Cipher;
import javax.crypto.KeyGenerator;
import javax.crypto.SecretKey;
import javax.crypto.spec.GCMParameterSpec;

/** Locally encrypted password migration store. Does not hook into WebView autofill. */
final class PasswordVault {
 static final int MAX_CSV_BYTES=4*1024*1024,MAX_ITEMS=5000;
 private static final String KEY_ALIAS="orion-imported-credentials-v1";
 private static final String FILE="imported_passwords.enc";
 private static final byte[] MAGIC="OPV1".getBytes(StandardCharsets.US_ASCII);
 static final class Entry {
  final String url,username,password,name;
  Entry(String url,String username,String password,String name){this.url=url;this.username=username;this.password=password;this.name=name;}
 }
 static boolean validUrl(String s){
  try{Uri u=Uri.parse(s);return ("http".equalsIgnoreCase(u.getScheme())||"https".equalsIgnoreCase(u.getScheme()))&&u.getHost()!=null&&u.getUserInfo()==null;}catch(Exception e){return false;}
 }
 static ArrayList<Entry> parseCsv(Context app,Uri source)throws Exception{
  String csv;
  try(InputStream in=app.getContentResolver().openInputStream(source);ByteArrayOutputStream bytes=new ByteArrayOutputStream()){
   if(in==null)throw new IOException("Cannot open selected file");
   byte[] block=new byte[8192];int n;
   while((n=in.read(block))!=-1){if(bytes.size()+n>MAX_CSV_BYTES)throw new IOException("Password CSV is larger than 4 MB");bytes.write(block,0,n);}
   csv=bytes.toString(StandardCharsets.UTF_8.name()).replace("\uFEFF","");
  }
  ArrayList<ArrayList<String>> records=records(csv);
  if(records.isEmpty())throw new IOException("CSV is empty");
  ArrayList<String> headers=records.get(0);
  int url=column(headers,"url","origin","login_uri","website","site","hostname","login_url","uri");
  int user=column(headers,"username","user","login_username","login","username_value","email");
  int pass=column(headers,"password","login_password","pass","password_value");
  int title=column(headers,"name","title","site_name");
  if(url<0||user<0||pass<0)throw new IOException("Expected URL, username and password CSV columns");
  ArrayList<Entry> result=new ArrayList<>();
  for(int i=1;i<records.size();i++){
   ArrayList<String> row=records.get(i);
   String address=cell(row,url).trim(),secret=cell(row,pass);
   if(!validUrl(address)||secret.isEmpty())continue;
   String username=cell(row,user),label=title<0?"":cell(row,title);
   if(secret.length()>8192||username.length()>2048||address.length()>4096)continue;
   if(result.size()>=MAX_ITEMS)throw new IOException("Too many passwords (limit 5,000)");
   result.add(new Entry(address,username,secret,label));
  }
  if(result.isEmpty())throw new IOException("No valid password entries found");
  return result;
 }
 private static String cell(ArrayList<String> row,int i){return i>=0&&i<row.size()?row.get(i):"";}
 private static int column(ArrayList<String> heads,String... names){for(int i=0;i<heads.size();i++){String h=heads.get(i).trim().toLowerCase(Locale.ROOT).replace(" ","_").replace("-","_");for(String n:names)if(h.equals(n))return i;}return -1;}
 private static ArrayList<ArrayList<String>> records(String csv)throws IOException{
  ArrayList<ArrayList<String>> rows=new ArrayList<>();ArrayList<String> row=new ArrayList<>();StringBuilder field=new StringBuilder();
  boolean quoted=false;boolean ended=false;
  for(int i=0;i<csv.length();i++){
   char c=csv.charAt(i);
   if(quoted){
    if(c=='"'){if(i+1<csv.length()&&csv.charAt(i+1)=='"'){field.append('"');i++;}else{quoted=false;ended=true;}}
    else field.append(c);
   }else{
    if(c=='"'&&field.length()==0&&!ended)quoted=true;
    else if(c==','){row.add(field.toString());field.setLength(0);ended=false;}
    else if(c=='\r'||c=='\n'){if(c=='\r'&&i+1<csv.length()&&csv.charAt(i+1)=='\n')i++;row.add(field.toString());field.setLength(0);ended=false;if(row.size()>1||!row.get(0).isBlank())rows.add(row);row=new ArrayList<>();if(rows.size()>MAX_ITEMS+100)throw new IOException("CSV contains too many rows");}
    else if(ended&&c!=' '&&c!='\t')throw new IOException("Invalid CSV quoting");
    else if(!ended)field.append(c);
   }
   if(field.length()>65536)throw new IOException("CSV field is too large");
  }
  if(quoted)throw new IOException("Unterminated quoted CSV field");
  if(field.length()>0||!row.isEmpty()){row.add(field.toString());rows.add(row);}
  return rows;
 }
 private static SecretKey key()throws Exception{
  KeyStore store=KeyStore.getInstance("AndroidKeyStore");store.load(null);
  if(store.containsAlias(KEY_ALIAS))return ((KeyStore.SecretKeyEntry)store.getEntry(KEY_ALIAS,null)).getSecretKey();
  KeyGenerator gen=KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES,"AndroidKeyStore");
  gen.init(new KeyGenParameterSpec.Builder(KEY_ALIAS,KeyProperties.PURPOSE_ENCRYPT|KeyProperties.PURPOSE_DECRYPT)
   .setBlockModes(KeyProperties.BLOCK_MODE_GCM).setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE).setRandomizedEncryptionRequired(true).build());
  return gen.generateKey();
 }
 private static AtomicFile file(Context app){return new AtomicFile(new File(app.getFilesDir(),FILE));}
 static ArrayList<Entry> load(Context app)throws Exception{
  AtomicFile f=file(app);if(!f.getBaseFile().exists())return new ArrayList<>();
  byte[] raw;
  try(InputStream in=f.openRead();ByteArrayOutputStream out=new ByteArrayOutputStream()){
   byte[] block=new byte[8192];int n;while((n=in.read(block))!=-1){if(out.size()+n>MAX_CSV_BYTES*3)throw new IOException("Vault is too large");out.write(block,0,n);}raw=out.toByteArray();
  }
  if(raw.length<32||!Arrays.equals(MAGIC,Arrays.copyOf(raw,4)))throw new IOException("Unknown password vault format");
  byte[] iv=Arrays.copyOfRange(raw,4,16),ciphertext=Arrays.copyOfRange(raw,16,raw.length);
  Cipher cipher=Cipher.getInstance("AES/GCM/NoPadding");cipher.init(Cipher.DECRYPT_MODE,key(),new GCMParameterSpec(128,iv));
  JSONArray all=new JSONArray(new String(cipher.doFinal(ciphertext),StandardCharsets.UTF_8));
  ArrayList<Entry> list=new ArrayList<>();for(int i=0;i<all.length();i++){JSONObject obj=all.getJSONObject(i);list.add(new Entry(obj.getString("url"),obj.optString("username"),obj.getString("password"),obj.optString("name")));}return list;
 }
 static int merge(Context app,List<Entry> incoming)throws Exception{
  ArrayList<Entry> existing=load(app);LinkedHashMap<String,Entry> index=new LinkedHashMap<>();
  for(Entry e:existing)index.put(e.url+"\u0000"+e.username,e);
  int inserted=0;
  for(Entry e:incoming){String id=e.url+"\u0000"+e.username;if(!index.containsKey(id))inserted++;index.put(id,e);}
  if(index.size()>MAX_ITEMS)throw new IOException("Vault exceeds 5,000 entries");
  JSONArray data=new JSONArray();for(Entry e:index.values()){JSONObject obj=new JSONObject();obj.put("url",e.url);obj.put("username",e.username);obj.put("password",e.password);obj.put("name",e.name);data.put(obj);}
  byte[] plaintext=data.toString().getBytes(StandardCharsets.UTF_8);
  if(plaintext.length>MAX_CSV_BYTES*2)throw new IOException("Vault is too large");
  byte[] iv=new byte[12];new SecureRandom().nextBytes(iv);
  Cipher cipher=Cipher.getInstance("AES/GCM/NoPadding");cipher.init(Cipher.ENCRYPT_MODE,key(),new GCMParameterSpec(128,iv));
  byte[] sealed=cipher.doFinal(plaintext);
  AtomicFile target=file(app);FileOutputStream out=target.startWrite();
  try{out.write(MAGIC);out.write(iv);out.write(sealed);target.finishWrite(out);}catch(Exception e){target.failWrite(out);throw e;}finally{Arrays.fill(plaintext,(byte)0);}
  return inserted;
 }
 static int count(Context app)throws Exception{return load(app).size();}
 private static String esc(String s){return "\""+s.replace("\"","\"\"")+"\"";}
 static int exportCsv(Context app,Uri target)throws Exception{
  ArrayList<Entry> entries=load(app);if(entries.isEmpty())throw new IOException("No imported passwords to export");
  try(OutputStream stream=app.getContentResolver().openOutputStream(target,"wt")){
   if(stream==null)throw new IOException("Cannot write selected file");
   Writer writer=new BufferedWriter(new OutputStreamWriter(stream,StandardCharsets.UTF_8));
   writer.write("name,url,username,password\r\n");
   for(Entry e:entries){writer.write(esc(e.name)+","+esc(e.url)+","+esc(e.username)+","+esc(e.password)+"\r\n");}
   writer.flush();
  }
  return entries.size();
 }
 static void clear(Context app){file(app).delete();}
}
