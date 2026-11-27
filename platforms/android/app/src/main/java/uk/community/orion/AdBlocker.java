package uk.community.orion;
import java.util.Locale;
final class AdBlocker {
 private static final String[] DOMAINS={"doubleclick.net","googlesyndication.com","googleadservices.com","adnxs.com","advertising.com","rubiconproject.com","pubmatic.com","openx.net","criteo.com","criteo.net","adform.net","casalemedia.com","adsrvr.org","smartadserver.com","yieldmo.com","taboola.com","outbrain.com"};
 static boolean blocks(String host){
  if(host==null)return false;
  String h=host.toLowerCase(Locale.ROOT);
  if(h.endsWith("."))h=h.substring(0,h.length()-1);
  for(String d:DOMAINS)if(h.equals(d)||h.endsWith("."+d))return true;
  return false;
 }
}
