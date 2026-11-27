package uk.community.orion;
import android.app.*;
import android.os.*;
import android.view.*;
import android.view.inputmethod.*;
import android.content.*;
import android.net.Uri;
import android.webkit.*;
import android.widget.*;
import java.util.*;

public class MainActivity extends Activity {
 final ArrayList<WebView> tabs=new ArrayList<>();
 LinearLayout root, bar; FrameLayout content; EditText address; Button count; int selected=-1;
 @Override public void onCreate(Bundle b){super.onCreate(b);
 root=new LinearLayout(this);root.setOrientation(1);
 root.setPadding(0, getResources().getIdentifier("status_bar_height","dimen","android")!=0?getResources().getDimensionPixelSize(getResources().getIdentifier("status_bar_height","dimen","android")):0,0,0);
 bar=new LinearLayout(this);root.addView(bar);
 button("‹",()->{if(current().canGoBack())current().goBack();});
 button("›",()->{if(current().canGoForward())current().goForward();});
 address=new EditText(this);address.setSingleLine(true);address.setHint("Search or HTTPS address");address.setInputType(17);address.setImeOptions(EditorInfo.IME_ACTION_GO);bar.addView(address,new LinearLayout.LayoutParams(0,-2,1));
 address.setOnEditorActionListener((v,a,e)->{if(a==EditorInfo.IME_ACTION_GO){navigate(address.getText().toString());((InputMethodManager)getSystemService(INPUT_METHOD_SERVICE)).hideSoftInputFromWindow(address.getWindowToken(),0);return true;}return false;});
 button("Go",()->navigate(address.getText().toString()));
 LinearLayout tools=new LinearLayout(this);root.addView(tools);bar=tools;
 button("+",()->newTab());count=button("Tabs",()->chooseTab());button("Reload",()->current().reload());button("Save",()->save());button("Bookmarks",()->bookmarks());button("Privacy",()->privacy());
 content=new FrameLayout(this);root.addView(content,new LinearLayout.LayoutParams(-1,0,1));setContentView(root);newTab();
 }
 Button button(String s,Runnable r){Button b=new Button(this);b.setText(s);b.setAllCaps(false);b.setMinWidth(0);b.setMinimumWidth(0);b.setPadding(8,0,8,0);bar.addView(b);b.setOnClickListener(v->r.run());return b;}
 WebView current(){return tabs.get(selected);}
 boolean allowed(String s){try{Uri u=Uri.parse(s);return "https".equalsIgnoreCase(u.getScheme())&&u.getHost()!=null&&u.getUserInfo()==null;}catch(Exception e){return false;}}
 void navigate(String raw){String s=raw.trim();if(s.isEmpty())return;if(!s.contains(" ")&&!s.contains("://")&&s.contains("."))s="https://"+s;if(!allowed(s)){if(s.contains("://")){Toast.makeText(this,"Only HTTPS pages are supported",0).show();return;}s="https://www.google.com/search?q="+Uri.encode(s);}current().loadUrl(s);}
 void newTab(){WebView w=new WebView(this);WebSettings s=w.getSettings();s.setJavaScriptEnabled(true);s.setDomStorageEnabled(true);s.setAllowFileAccess(false);s.setAllowContentAccess(false);s.setMixedContentMode(WebSettings.MIXED_CONTENT_NEVER_ALLOW);s.setJavaScriptCanOpenWindowsAutomatically(false);s.setSafeBrowsingEnabled(true);CookieManager.getInstance().setAcceptThirdPartyCookies(w,false);
 w.setWebViewClient(new WebViewClient(){@Override public boolean shouldOverrideUrlLoading(WebView v,WebResourceRequest r){return !allowed(r.getUrl().toString());}@Override public void onPageFinished(WebView v,String u){if(selected>=0&&v==current())address.setText(u);}});
 w.setWebChromeClient(new WebChromeClient(){@Override public void onPermissionRequest(PermissionRequest r){r.deny();}});
 tabs.add(w);show(tabs.size()-1);w.loadDataWithBaseURL(null,"<html><body style='background:#182235;color:white;font-family:sans-serif;padding:24px'><h1>Orion Community</h1><p>Enter a search or HTTPS address above.</p></body></html>","text/html","UTF-8",null);}
 void show(int i){content.removeAllViews();selected=i;content.addView(current(),new FrameLayout.LayoutParams(-1,-1));address.setText(current().getUrl()==null?"":current().getUrl());count.setText("Tabs "+tabs.size());}
 void chooseTab(){String[] names=new String[tabs.size()];for(int i=0;i<names.length;i++)names[i]=tabs.get(i).getTitle()==null?"New tab":tabs.get(i).getTitle();new AlertDialog.Builder(this).setTitle("Tabs").setItems(names,(d,i)->show(i)).setNeutralButton("Close current",(d,i)->{WebView w=current();content.removeAllViews();tabs.remove(selected);w.destroy();if(tabs.isEmpty())newTab();else show(Math.min(selected,tabs.size()-1));}).show();}
 void save(){String u=current().getUrl();if(!allowed(u))return;Set<String> set=new HashSet<>(getPreferences(0).getStringSet("bookmarks",Collections.emptySet()));set.add(u);getPreferences(0).edit().putStringSet("bookmarks",set).apply();Toast.makeText(this,"Bookmark saved",0).show();}
 void bookmarks(){String[] urls=getPreferences(0).getStringSet("bookmarks",Collections.emptySet()).toArray(new String[0]);Arrays.sort(urls);new AlertDialog.Builder(this).setTitle("Bookmarks").setItems(urls,(d,i)->navigate(urls[i])).setNegativeButton("Close",null).show();}
 void privacy(){new AlertDialog.Builder(this).setTitle("Privacy").setMessage("HTTPS only. Third-party cookies blocked. Camera and microphone permissions denied. No app telemetry. This prototype uses Android System WebView; it does not provide isolated private browsing.").setPositiveButton("Clear browsing data",(d,i)->{CookieManager.getInstance().removeAllCookies(v->CookieManager.getInstance().flush());WebStorage.getInstance().deleteAllData();for(WebView w:tabs){w.clearCache(true);w.clearHistory();w.clearFormData();}Toast.makeText(this,"Browsing data cleared",0).show();}).setNegativeButton("Close",null).show();}
 @Override protected void onDestroy(){content.removeAllViews();for(WebView w:tabs)w.destroy();super.onDestroy();}
}
