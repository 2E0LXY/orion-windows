package uk.community.orion.sync;
import java.nio.ByteBuffer;
import java.nio.charset.StandardCharsets;
import java.security.GeneralSecurityException;
import java.security.SecureRandom;
import java.util.Base64;
import javax.crypto.Cipher;
import javax.crypto.spec.GCMParameterSpec;
import javax.crypto.spec.SecretKeySpec;

/** AES-256-GCM sealing. The associated data binds each payload to its category and record id, so records cannot be swapped. */
public final class SyncCrypto {
 private static final SecureRandom RANDOM=new SecureRandom();
 private SyncCrypto(){}
 public static byte[] newKey(){byte[] k=new byte[32];RANDOM.nextBytes(k);return k;}
 public static String seal(byte[] key,String aad,byte[] plain)throws GeneralSecurityException{
  byte[] nonce=new byte[12];RANDOM.nextBytes(nonce);
  Cipher c=Cipher.getInstance("AES/GCM/NoPadding");
  c.init(Cipher.ENCRYPT_MODE,new SecretKeySpec(key,"AES"),new GCMParameterSpec(128,nonce));
  c.updateAAD(aad.getBytes(StandardCharsets.UTF_8));
  byte[] ct=c.doFinal(plain);
  return Base64.getEncoder().withoutPadding().encodeToString(ByteBuffer.allocate(nonce.length+ct.length).put(nonce).put(ct).array());
 }
 public static byte[] open(byte[] key,String aad,String sealed)throws GeneralSecurityException{
  byte[] all=Base64.getDecoder().decode(sealed);
  if(all.length<12+16)throw new GeneralSecurityException("sealed payload too short");
  Cipher c=Cipher.getInstance("AES/GCM/NoPadding");
  c.init(Cipher.DECRYPT_MODE,new SecretKeySpec(key,"AES"),new GCMParameterSpec(128,all,0,12));
  c.updateAAD(aad.getBytes(StandardCharsets.UTF_8));
  return c.doFinal(all,12,all.length-12);
 }
}
