package uk.community.orion;
import android.app.*;
import android.graphics.*;
import android.graphics.drawable.*;
import android.content.res.ColorStateList;
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
 static final int BOOKMARK_IMPORT_REQUEST=807, PASSWORD_IMPORT_REQUEST=808, PASSWORD_EXPORT_REQUEST=809, COOKIE_IMPORT_REQUEST=810, DNS_VPN_REQUEST=811;
 LinearLayout root, bar, navigation, tabletTabs; FrameLayout content, homeOverlay; HomeWeather homeWeather; EditText address; Button count; ProgressBar progress; Button back,forward; TextView aiBanner; int selected=-1;
 final Set<WebView> homeTabs=Collections.newSetFromMap(new IdentityHashMap<WebView,Boolean>());
 @Override public void onCreate(Bundle b){super.onCreate(b);AdBlocker.load(this,getPreferences(0));
 getWindow().setStatusBarColor(0xff171d28);getWindow().setNavigationBarColor(0xff171d28);
 root=new LinearLayout(this);root.setOrientation(LinearLayout.VERTICAL);root.setBackgroundColor(0xff171d28);
 root.setOnApplyWindowInsetsListener((v,insets)->{if(Build.VERSION.SDK_INT>=30){android.graphics.Insets i=insets.getInsets(WindowInsets.Type.systemBars()|WindowInsets.Type.displayCutout()|WindowInsets.Type.ime());v.setPadding(i.left,i.top,i.right,i.bottom);}else{v.setPadding(insets.getSystemWindowInsetLeft(),insets.getSystemWindowInsetTop(),insets.getSystemWindowInsetRight(),insets.getSystemWindowInsetBottom());}return insets;});
 getWindow().getDecorView().setSystemUiVisibility(View.SYSTEM_UI_FLAG_LAYOUT_STABLE|View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN|View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION);
 getWindow().setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE);
 content=new FrameLayout(this);root.addView(content,new LinearLayout.LayoutParams(-1,0,1));
 progress=new ProgressBar(this,null,android.R.attr.progressBarStyleHorizontal);progress.setMax(100);progress.setProgressTintList(ColorStateList.valueOf(0xff8ab4ff));root.addView(progress,new LinearLayout.LayoutParams(-1,dp(3)));progress.setVisibility(View.INVISIBLE);
 bar=new LinearLayout(this);bar.setGravity(Gravity.CENTER_VERTICAL);bar.setPadding(dp(8),dp(4),dp(8),dp(4));root.addView(bar);
 address=new EditText(this);address.setSingleLine(true);address.setTextSize(15);address.setTextColor(0xfff1f5f9);address.setHintTextColor(0xffb9c6da);address.setHint("Search or enter address");address.setContentDescription("Address and search");address.setInputType(17);address.setImeOptions(EditorInfo.IME_ACTION_GO);address.setPadding(dp(16),0,dp(12),0);address.setBackground(round(0xff293344,24));bar.addView(address,new LinearLayout.LayoutParams(0,dp(48),1));
 address.setOnEditorActionListener((v,a,e)->{if(a==EditorInfo.IME_ACTION_GO){submit();return true;}return false;});
 button("go","Open address",()->submit(),false);
 navigation=new LinearLayout(this);navigation.setGravity(Gravity.CENTER_VERTICAL);root.addView(navigation,new LinearLayout.LayoutParams(-1,dp(56)));bar=navigation;
 aiBanner=new TextView(this);
 aiBanner.setText("AI SHARING ACTIVE · TAP TO DISCONNECT");
 aiBanner.setTextSize(14);aiBanner.setTypeface(Typeface.create("sans-serif-medium",Typeface.NORMAL));
 aiBanner.setTextColor(0xffffffff);aiBanner.setGravity(Gravity.CENTER);
 aiBanner.setBackground(round(0xff22537c,8));aiBanner.setMinHeight(dp(48));
 aiBanner.setContentDescription("AI sharing is active. Tap to disconnect or manage permissions");
 aiBanner.setOnClickListener(v->aiConnectSettings());
 aiBanner.setVisibility(View.GONE);root.addView(aiBanner,new LinearLayout.LayoutParams(-1,dp(48)));
 back=button("back","Back",()->{if(geckoActive)geckoGoBack();else if(current().canGoBack())current().goBack();},true);
 forward=button("forward","Forward",()->{if(geckoActive)geckoGoForward();else if(current().canGoForward())current().goForward();},true);
 Button homeButton=button("home","Home. Hold to save current page; triple tap to reset",()->homeTap(),true);homeButton.setOnLongClickListener(v->{saveCurrentHomepage();return true;});count=button("tabs","Switch tabs",()->chooseTab(),true);button("menu","Browser menu",()->menu(),true);Button orionSecurity=button("orion","Orion security and privacy",()->securityPrivacy(),true);Drawable officialOrion=getDrawable(R.drawable.orion_icon);if(officialOrion!=null){officialOrion.setBounds(0,0,dp(24),dp(24));orionSecurity.setCompoundDrawables(null,officialOrion,null,null);}updateToolbar();tabletTabs=new LinearLayout(this);root.addView(tabletTabs,0,new LinearLayout.LayoutParams(-1,dp(48)));tabletTabs.setVisibility(View.GONE);
 address.setOnTouchListener(new View.OnTouchListener(){float x,y;public boolean onTouch(View v,android.view.MotionEvent e){if(!getPreferences(0).getBoolean("tab_swipe",true))return false;if(e.getActionMasked()==MotionEvent.ACTION_DOWN){x=e.getX();y=e.getY();}if(e.getActionMasked()==MotionEvent.ACTION_UP&&Math.abs(e.getX()-x)>dp(80)&&Math.abs(e.getY()-y)<dp(40)&&tabs.size()>1){show((selected+(e.getX()>x?-1:1)+tabs.size())%tabs.size());return true;}return false;}});setContentView(root);root.requestApplyInsets();newTab();openExternalUrl(getIntent());if(getPreferences(0).getBoolean("zero_cache",false))clearResourceCaches(false);
 }

 // Android owns default-browser selection; Orion never changes it silently.
 @Override protected void onNewIntent(Intent intent){
  super.onNewIntent(intent);setIntent(intent);openExternalUrl(intent);
 }
 void openExternalUrl(Intent intent){
  if(intent==null||!Intent.ACTION_VIEW.equals(intent.getAction()))return;
  Uri uri=intent.getData();
  if(uri==null)return;
  String url=uri.toString();
  if(!allowed(url)){Toast.makeText(this,"Unsupported link",Toast.LENGTH_SHORT).show();return;}
  if(geckoIncognito)closeGecko();
  if(selected>=0)navigate(url);
 }
 boolean isDefaultBrowser(){
  if(Build.VERSION.SDK_INT>=29){
   android.app.role.RoleManager roles=getSystemService(android.app.role.RoleManager.class);
   return roles!=null&&roles.isRoleAvailable(android.app.role.RoleManager.ROLE_BROWSER)
    &&roles.isRoleHeld(android.app.role.RoleManager.ROLE_BROWSER);
  }
  Intent test=new Intent(Intent.ACTION_VIEW,Uri.parse("https://example.com/"));
  android.content.pm.ResolveInfo resolved=getPackageManager().resolveActivity(test,android.content.pm.PackageManager.MATCH_DEFAULT_ONLY);
  return resolved!=null&&resolved.activityInfo!=null&&getPackageName().equals(resolved.activityInfo.packageName);
 }
 void requestDefaultBrowser(){
  if(isDefaultBrowser()){Toast.makeText(this,"Orion is already the default browser",Toast.LENGTH_SHORT).show();return;}
  if(Build.VERSION.SDK_INT>=29){
   android.app.role.RoleManager roles=getSystemService(android.app.role.RoleManager.class);
   if(roles!=null&&roles.isRoleAvailable(android.app.role.RoleManager.ROLE_BROWSER)){
    startActivityForResult(roles.createRequestRoleIntent(android.app.role.RoleManager.ROLE_BROWSER),812);
    return;
   }
  }
  Intent chooser=new Intent(android.provider.Settings.ACTION_MANAGE_DEFAULT_APPS_SETTINGS);
  try{startActivity(chooser);}catch(android.content.ActivityNotFoundException e){
   Toast.makeText(this,"Open Android Settings to choose your default browser",Toast.LENGTH_LONG).show();
  }
 }
 OrionAiBridge aiBridge;
 void updateAiBanner(){
  if(aiBanner==null)return;
  boolean active=aiBridge!=null&&aiBridge.running();
  aiBanner.setVisibility(active?View.VISIBLE:View.GONE);
  if(active)aiBanner.setText("AI SHARING ACTIVE · TAP TO DISCONNECT");
 }
 boolean aiInForeground=false;
 @Override protected void onStart(){super.onStart();aiInForeground=true;}
 @Override protected void onStop(){aiInForeground=false;super.onStop();}
 boolean aiCanControl(){return aiInForeground&&!geckoIncognito&&!isFinishing()&&!isDestroyed();}
 String aiPageInfo(){
  if(geckoIncognito)return "Private tabs cannot be shared";
  if(homeOverlay!=null)return "Orion start page";
  String url=geckoActive?geckoAddress:selected>=0?current().getUrl():"";
  if(!allowed(url))return "No shareable webpage";
  String title=geckoActive?"GeckoView page":current().getTitle();
  return "Title: "+(title==null?"":title.substring(0,Math.min(120,title.length())))+"\nURL: "+url;
 }
 boolean aiWebPageReady(){
  return aiCanControl()&&!geckoActive&&selected>=0&&homeOverlay==null&&allowed(current().getUrl());
 }
 void aiDecodeJs(String encoded,OrionAiBridge.Result reply){
  if(!aiCanControl()||geckoIncognito){reply.complete("Page no longer shareable");return;}
  try{
   org.json.JSONArray result=new org.json.JSONArray("["+encoded+"]");
   String content=result.optString(0,"");
   reply.complete(content.length()>12000?content.substring(0,12000):content);
  }catch(Exception e){reply.complete("Unable to read webpage result");}
 }
 void aiConnectSettings(){
  if(aiBridge==null)aiBridge=new OrionAiBridge(this,new OrionAiBridge.Host(){
   @Override public boolean canControl(){return aiCanControl();}
   @Override public void onStatusChanged(){updateAiBanner();}
   @Override public String describePage(){return aiPageInfo();}
   @Override public String currentUrl(){return geckoActive?geckoAddress:selected>=0?current().getUrl():"";}
   @Override public void openUrl(String url){if(!geckoIncognito&&aiCanControl())navigate(url);}
   @Override public void perform(String tool,org.json.JSONObject args,OrionAiBridge.Result reply){
    if(!aiCanControl()){reply.complete("Orion is not in a shareable state");return;}
    switch(tool){
     case "browser_info":reply.complete(aiPageInfo());return;
     case "navigate":navigate(args.optString("url",""));reply.complete("HTTPS navigation requested");return;
     case "back":
      if(geckoActive)geckoGoBack();else if(selected>=0&&current().canGoBack())current().goBack();
      reply.complete("Back requested");return;
     case "forward":
      if(geckoActive)geckoGoForward();else if(selected>=0&&current().canGoForward())current().goForward();
      reply.complete("Forward requested");return;
     case "reload":reloadActive();reply.complete("Reload requested");return;
     case "tabs":{
      StringBuilder list=new StringBuilder("Normal browser tabs:\n");
      for(int i=0;i<tabs.size();i++){
       WebView tab=tabs.get(i);
       String url=tab.getUrl(),title=tab.getTitle();
       list.append(i==selected?"* ":"  ").append(i+1).append(". ")
        .append(title==null?"New tab":title.substring(0,Math.min(80,title.length())))
        .append(" — ").append(allowed(url)?url:"New tab").append("\n");
      }
      if(geckoActive)list.append("Active GeckoView page: ").append(allowed(geckoAddress)?geckoAddress:"Start page");
      reply.complete(list.toString());return;
     }
     case "page_text":{
      if(!aiWebPageReady()){reply.complete("Available only for a loaded normal WebView webpage");return;}
      String js="(function(){try{return document.body?(document.body.innerText||'').slice(0,12000):'';}catch(e){return '';}})()";
      current().evaluateJavascript(js,encoded->aiDecodeJs(encoded,reply));return;
     }
     case "scroll":{
      if(!aiWebPageReady()){reply.complete("Scrolling is available only in WebView");return;}
      current().scrollBy(0,args.optInt("pixels",0));reply.complete("Scroll requested");return;
     }
     case "click":{
      if(!aiWebPageReady()){reply.complete("Clicking is available only in WebView");return;}
      String selector=org.json.JSONObject.quote(args.optString("selector",""));
      String js="(function(){try{var e=document.querySelector("+selector+");if(!e)return 'Element not found';"
       +"if(e.closest('form')||e.matches('input,textarea,button,select,[contenteditable],[role=button]'))return 'Blocked: potentially sensitive or submitting control';"
       +"e.click();return 'Clicked non-form element';}catch(x){return 'Selector error';}})()";
      current().evaluateJavascript(js,encoded->aiDecodeJs(encoded,reply));return;
     }
     case "type":{
      if(!aiWebPageReady()){reply.complete("Typing is available only in WebView");return;}
      String selector=org.json.JSONObject.quote(args.optString("selector",""));
      String value=org.json.JSONObject.quote(args.optString("text",""));
      String js="(function(){try{var e=document.querySelector("+selector+");if(!e)return 'Element not found';"
       +"if(!e.matches('input,textarea'))return 'Not a text field';"
       +"var t=(e.type||'text').toLowerCase(),a=(e.autocomplete||'').toLowerCase();"
       +"if(['password','hidden','file','email','tel','number','date','month','week','time'].includes(t)||/password|cc-|one-time-code|username/.test(a))return 'Sensitive field blocked';"
       +"e.focus();e.value="+value+";e.dispatchEvent(new Event('input',{bubbles:true}));"
       +"return 'Text entered; form not submitted';}catch(x){return 'Field error';}})()";
      current().evaluateJavascript(js,encoded->aiDecodeJs(encoded,reply));return;
     }
     default:reply.complete("Tool unavailable");
    }
   }
   @Override public void requestApproval(String tool,String detail,OrionAiBridge.Approval cb){
    if(!aiCanControl()){cb.complete(false);return;}
    new AlertDialog.Builder(MainActivity.this)
     .setTitle("AI request: "+tool)
     .setMessage(detail+"\n\nAllow this one request? AI sharing is restricted to normal browser tabs.")
     .setNegativeButton("Deny",(d,w)->cb.complete(false))
     .setPositiveButton("Allow once",(d,w)->cb.complete(true))
     .setOnCancelListener(d->cb.complete(false)).show();
   }
  });
  Dialog dialog=new Dialog(this,android.R.style.Theme_Material_NoActionBar);
  LinearLayout page=new LinearLayout(this);page.setOrientation(LinearLayout.VERTICAL);page.setPadding(dp(16),dp(20),dp(16),dp(20));page.setBackgroundColor(0xff171d28);
  Button close=new Button(this);close.setAllCaps(false);close.setText("‹  Orion AI Connect");close.setContentDescription("Close Orion AI Connect");close.setOnClickListener(v->dialog.dismiss());page.addView(close,new LinearLayout.LayoutParams(-1,dp(56)));
  ScrollView scroll=new ScrollView(this);LinearLayout items=new LinearLayout(this);items.setOrientation(LinearLayout.VERTICAL);scroll.addView(items);page.addView(scroll,new LinearLayout.LayoutParams(-1,0,1));
  section(items,"LOCAL CONNECTION");
  settingRow(items,"Status",aiBridge.status(),()->{dialog.dismiss();aiConnectSettings();});
  if(!aiBridge.running()){
   settingRow(items,"Start AI sharing","Local device only · expires after 15 minutes · confirm every action",()->{
    if(geckoIncognito){Toast.makeText(this,"Close Incognito before sharing",Toast.LENGTH_LONG).show();return;}
    aiBridge.start();updateAiBanner();dialog.dismiss();aiConnectSettings();
   });
  }else{
   settingRow(items,"Stop and disconnect","Revoke the active token immediately",()->{aiBridge.stop();updateAiBanner();dialog.dismiss();aiConnectSettings();});
   section(items,"PAIRING");
   settingRow(items,"Connection address","http://127.0.0.1:"+aiBridge.port()+"/mcp",()->{});
   settingRow(items,"Copy session token","Secret: share only with a trusted local MCP client",()->{
    android.content.ClipboardManager clipboard=(android.content.ClipboardManager)getSystemService(CLIPBOARD_SERVICE);
    if(clipboard!=null&&aiBridge.token()!=null){clipboard.setPrimaryClip(ClipData.newPlainText("Orion AI session token",aiBridge.token()));Toast.makeText(this,"Temporary token copied; clipboard access is sensitive",Toast.LENGTH_LONG).show();}
   });
  }
  section(items,"PERMISSIONS");
  settingRow(items,"Read title, URL and normal tabs","Ask on every read request",()->{});
  settingRow(items,"Read webpage text (WebView)","Ask once per request · may expose personal information",()->{});
  settingRow(items,"Navigate, back, forward and reload","Ask on each request · HTTPS links only",()->{});
  settingRow(items,"Scroll, click and type (WebView)","Ask on each request · blocks form submission and sensitive fields",()->{});
  disabledSettingRow(items,"Screenshots and downloads","Not enabled while capture and file policies are being verified");
  section(items,"BOUNDARIES");
  TextView guidance=new TextView(this);guidance.setText("The listener accepts only this device's loopback connections. A desktop AI cannot reach it directly; use a trusted local bridge or connector. ChatGPT, Claude and Gemini require their own MCP/client connection. Incognito, passwords and cookies are excluded. Close this panel using the top control; sharing continues until disconnect or expiry.");
  guidance.setTextSize(15);guidance.setTextColor(0xfff1f5f9);guidance.setPadding(dp(16),dp(12),dp(16),dp(16));items.addView(guidance);
  dialog.setContentView(page);dialog.show();dialog.getWindow().setLayout(-1,-1);
 }
 String installedVersion(){try{android.content.pm.PackageInfo p=getPackageManager().getPackageInfo(getPackageName(),0);return "Version "+p.versionName+" · Build "+(Build.VERSION.SDK_INT>=28?p.getLongVersionCode():p.versionCode);}catch(Exception e){return "Version unavailable";}}
 int dp(float n){return Math.round(n*getResources().getDisplayMetrics().density);}
 GradientDrawable round(int color,int radius){GradientDrawable d=new GradientDrawable();d.setColor(color);d.setCornerRadius(dp(radius));return d;}

 // Dispatch deliberate gestures at the native browser shell, across both engines.
 float touchStartX,touchStartY;
 int gestureZone=0,gestureAction=0,geckoScrollY=0;
 boolean gestureCaptured=false;
 boolean touchWithin(View view,float x,float y){
  if(view==null||view.getVisibility()!=View.VISIBLE)return false;
  Rect bounds=new Rect();return view.getGlobalVisibleRect(bounds)&&bounds.contains((int)x,(int)y);
 }
 boolean pageAtTop(){
  if(homeOverlay!=null){View child=homeOverlay.getChildAt(0);return child==null||!child.canScrollVertically(-1);}
  if(geckoActive)return geckoScrollY<=0;
  return selected>=0&&!current().canScrollVertically(-1);
 }
 void finishGesture(int action){
  if(action==1){if(geckoActive)geckoGoBack();else if(selected>=0&&current().canGoBack())current().goBack();}
  else if(action==2){if(geckoActive)geckoGoForward();else if(selected>=0&&current().canGoForward())current().goForward();}
  else if(action==3){if(homeOverlay!=null)originalHome();else reloadActive();}
  else if(action==4)showDownloadPanel();
 }
 @Override public boolean dispatchTouchEvent(MotionEvent event){
  int phase=event.getActionMasked();
  if(phase==MotionEvent.ACTION_DOWN){
   gestureCaptured=false;gestureAction=0;
   touchStartX=event.getRawX();touchStartY=event.getRawY();
   gestureZone=touchWithin(navigation,touchStartX,touchStartY)?1:touchWithin(content,touchStartX,touchStartY)?2:0;
   return super.dispatchTouchEvent(event);
  }
  if(event.getPointerCount()>1){
   if(gestureCaptured){gestureCaptured=false;gestureZone=0;return true;}
   gestureZone=0;return super.dispatchTouchEvent(event);
  }
  if(phase==MotionEvent.ACTION_MOVE&&gestureZone!=0){
   if(gestureCaptured)return true;
   float dx=event.getRawX()-touchStartX,dy=event.getRawY()-touchStartY;
   if(gestureZone==1){
    if(dy< -dp(76)&&-dy>Math.abs(dx)*1.5f)gestureAction=4;
   }else if(gestureZone==2){
    if(dy>dp(100)&&dy>Math.abs(dx)*1.6f&&pageAtTop())gestureAction=3;
    else if(dx< -dp(110)&&-dx>Math.abs(dy)*1.8f)gestureAction=1;
    else if(dx>dp(110)&&dx>Math.abs(dy)*1.8f)gestureAction=2;
   }
   if(gestureAction!=0){
    MotionEvent cancel=MotionEvent.obtain(event);cancel.setAction(MotionEvent.ACTION_CANCEL);
    super.dispatchTouchEvent(cancel);cancel.recycle();gestureCaptured=true;return true;
   }
  }
  if(phase==MotionEvent.ACTION_UP||phase==MotionEvent.ACTION_CANCEL){
   boolean captured=gestureCaptured;int action=gestureAction;
   gestureCaptured=false;gestureZone=0;gestureAction=0;
   if(captured){if(phase==MotionEvent.ACTION_UP)finishGesture(action);return true;}
  }
  return super.dispatchTouchEvent(event);
 }
 void submit(){navigate(address.getText().toString());address.clearFocus();((InputMethodManager)getSystemService(INPUT_METHOD_SERVICE)).hideSoftInputFromWindow(address.getWindowToken(),0);}
 Button button(String glyph,String label,Runnable r,boolean weight){Button b=new Button(this);b.setText("");b.setContentDescription(label);b.setTooltipText(label);b.setMinWidth(0);b.setMinimumWidth(0);b.setPadding(0,0,0,0);b.setBackground(new RippleDrawable(ColorStateList.valueOf(0x338ab4ff),null,round(0xffffffff,24)));b.setCompoundDrawables(null,new Glyph(glyph),null,null);b.setGravity(Gravity.CENTER);b.setPadding(0,dp(12),0,0);bar.addView(b,new LinearLayout.LayoutParams(weight?0:dp(48),dp(48),weight?1:0));b.setOnClickListener(v->r.run());return b;}
 class Glyph extends Drawable {final String kind;final Paint p=new Paint(3);Glyph(String k){kind=k;setBounds(0,0,dp(24),dp(24));}public void draw(Canvas c){c.save();c.translate(getBounds().left,getBounds().top);c.scale(getBounds().width()/24f,getBounds().height()/24f);p.setColor(0xffdce5f4);p.setStrokeWidth(1.8f);p.setStrokeCap(Paint.Cap.ROUND);p.setStrokeJoin(Paint.Join.ROUND);p.setStyle(Paint.Style.STROKE);Path a=new Path();switch(kind){case "back":a.moveTo(14,5);a.lineTo(7,12);a.lineTo(14,19);a.moveTo(7,12);a.lineTo(21,12);break;case "forward":case "go":a.moveTo(10,5);a.lineTo(17,12);a.lineTo(10,19);a.moveTo(3,12);a.lineTo(17,12);break;case "bookmark":a.moveTo(6,3);a.lineTo(18,3);a.lineTo(18,21);a.lineTo(12,16);a.lineTo(6,21);a.close();break;case "reload":c.drawArc(4,4,20,20,-45,300,false,p);a.moveTo(20,4);a.lineTo(20,10);a.lineTo(14,10);break;case "home":a.moveTo(3,11);a.lineTo(12,3);a.lineTo(21,11);a.lineTo(21,21);a.lineTo(3,21);a.close();break;case "orion":p.setColor(0xff8ab4ff);p.setStyle(Paint.Style.FILL);c.drawCircle(12,12,11,p);p.setColor(0xff26354e);c.drawCircle(12,12,8,p);p.setColor(0xfff5faff);p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(2.5f);c.drawCircle(12,12,5,p);break;case "tabs":c.drawRoundRect(3,3,21,21,4,4,p);p.setStyle(Paint.Style.FILL);p.setTextSize(11);p.setTextAlign(Paint.Align.CENTER);c.drawText(""+tabs.size(),12,16,p);break;default:p.setStyle(Paint.Style.FILL);for(int i=5;i<=19;i+=7)c.drawCircle(12,i,1.6f,p);}c.drawPath(a,p);c.restore();}public void setAlpha(int a){p.setAlpha(a);}public void setColorFilter(ColorFilter f){p.setColorFilter(f);}public int getOpacity(){return PixelFormat.TRANSLUCENT;}}
 void updateToolbar(){if(navigation==null||navigation.getChildCount()<3)return;Button shortcut=(Button)navigation.getChildAt(2);int mode=getPreferences(0).getInt("shortcut",0);String glyph=mode==1?"bookmark":mode==2?"reload":"home";String label=mode==1?"Save bookmark":mode==2?"Reload page":"Home";shortcut.setCompoundDrawables(null,new Glyph(glyph),null,null);shortcut.setContentDescription(label);shortcut.setTooltipText(label);shortcut.setOnClickListener(v->{if(mode==1)save();else if(mode==2)reloadActive();else homeTap();});shortcut.setOnLongClickListener(mode==0?v->{saveCurrentHomepage();return true;}:null);}
 void refreshTabletTabs(){if(tabletTabs==null)return;tabletTabs.removeAllViews();boolean wide=getResources().getConfiguration().screenWidthDp>=600;tabletTabs.setVisibility(wide?View.VISIBLE:View.GONE);if(!wide)return;HorizontalScrollView scroll=new HorizontalScrollView(this);LinearLayout strip=new LinearLayout(this);for(int i=0;i<tabs.size();i++){final int index=i;Button b=new Button(this);b.setAllCaps(false);b.setText(tabs.get(i).getTitle()==null?"Tab "+(i+1):tabs.get(i).getTitle());b.setMaxWidth(dp(180));b.setSingleLine(true);b.setTextColor(i==selected?0xff8ab4ff:0xfff1f5f9);b.setOnClickListener(v->show(index));strip.addView(b);}Button add=new Button(this);add.setText("+");add.setContentDescription("New tab");add.setOnClickListener(v->newTab());strip.addView(add);scroll.addView(strip);tabletTabs.addView(scroll,new LinearLayout.LayoutParams(-1,-1));}
 static org.mozilla.geckoview.GeckoRuntime geckoRuntime;
 org.mozilla.geckoview.GeckoSession geckoSession;
 org.mozilla.geckoview.GeckoView geckoView;
 boolean geckoActive=false, geckoIncognito=false, geckoCanGoBack=false, geckoCanGoForward=false;
 String geckoAddress="";
 org.mozilla.geckoview.GeckoSession.PermissionDelegate.Callback geckoAndroidCallback;
 boolean geckoMediaPending=false;
 boolean geckoSecureOrigin(String uri){
  if(!geckoActive||geckoSession==null||!allowed(uri)||!allowed(geckoAddress))return false;
  Uri request=Uri.parse(uri),currentUrl=Uri.parse(geckoAddress);
  return "https".equalsIgnoreCase(request.getScheme())&&"https".equalsIgnoreCase(currentUrl.getScheme())&&Objects.equals(request.getHost(),currentUrl.getHost())&&(request.getPort()<0?443:request.getPort())==(currentUrl.getPort()<0?443:currentUrl.getPort());
 }

 void openGecko(){openGecko(false);}
 void openIncognito(){
  if(aiBridge!=null)aiBridge.stop();updateAiBanner();
  if(geckoActive){closeGecko();}
  openGecko(true);
 }
 void openGecko(boolean incognito){
  if(geckoActive)return;
  boolean openAtHome=incognito||homeTabs.contains(current())||homeOverlay!=null;
  String initial=openAtHome?null:current().getUrl();
  if(!allowed(initial))initial=null;
  try{
   if(geckoRuntime==null)geckoRuntime=org.mozilla.geckoview.GeckoRuntime.create(getApplicationContext());
   DnsSettings.applyToGecko(geckoRuntime,getPreferences(0));
   geckoView=new org.mozilla.geckoview.GeckoView(this);
   geckoSession=new org.mozilla.geckoview.GeckoSession(new org.mozilla.geckoview.GeckoSessionSettings.Builder().usePrivateMode(incognito).build());
   final org.mozilla.geckoview.GeckoSession session=geckoSession;
   geckoAddress=initial==null?"":initial;
   session.setContentDelegate(new org.mozilla.geckoview.GeckoSession.ContentDelegate(){});
   session.setPermissionDelegate(new org.mozilla.geckoview.GeckoSession.PermissionDelegate(){
    @Override public void onAndroidPermissionsRequest(org.mozilla.geckoview.GeckoSession sess,String[] permissions,org.mozilla.geckoview.GeckoSession.PermissionDelegate.Callback cb){
     if(!geckoActive||sess!=geckoSession||permissions==null||permissions.length==0||geckoAndroidCallback!=null){cb.reject();return;}
     for(String permission:permissions){
      if(!android.Manifest.permission.CAMERA.equals(permission)&&!android.Manifest.permission.RECORD_AUDIO.equals(permission)&&!android.Manifest.permission.ACCESS_COARSE_LOCATION.equals(permission)){cb.reject();return;}
      String key=android.Manifest.permission.CAMERA.equals(permission)?"site_camera":android.Manifest.permission.RECORD_AUDIO.equals(permission)?"site_microphone":"site_location";
      if(getPreferences(0).getInt(key,0)==1){cb.reject();return;}
     }
     ArrayList<String> missing=new ArrayList<>();
     for(String permission:permissions)if(checkSelfPermission(permission)!=android.content.pm.PackageManager.PERMISSION_GRANTED)missing.add(permission);
     if(missing.isEmpty()){cb.grant();return;}
     geckoAndroidCallback=cb;
     requestPermissions(missing.toArray(new String[0]),43);
    }
    @Override public org.mozilla.geckoview.GeckoResult<Integer> onContentPermissionRequest(org.mozilla.geckoview.GeckoSession sess,org.mozilla.geckoview.GeckoSession.PermissionDelegate.ContentPermission perm){
     final org.mozilla.geckoview.GeckoResult<Integer> response=new org.mozilla.geckoview.GeckoResult<>();
     if(!geckoActive||sess!=geckoSession||perm.permission!=org.mozilla.geckoview.GeckoSession.PermissionDelegate.PERMISSION_GEOLOCATION||!geckoSecureOrigin(perm.uri)){
      response.complete(org.mozilla.geckoview.GeckoSession.PermissionDelegate.ContentPermission.VALUE_DENY);return response;
     }
     int choice=getPreferences(0).getInt("site_location",0);
     if(choice==1){response.complete(org.mozilla.geckoview.GeckoSession.PermissionDelegate.ContentPermission.VALUE_DENY);return response;}
     if(choice==2){response.complete(org.mozilla.geckoview.GeckoSession.PermissionDelegate.ContentPermission.VALUE_ALLOW);return response;}
     new AlertDialog.Builder(MainActivity.this).setTitle("Location permission").setMessage(Uri.parse(perm.uri).getHost()+" requests location access. Allow for this request?").setNegativeButton("Block",(d,i)->response.complete(org.mozilla.geckoview.GeckoSession.PermissionDelegate.ContentPermission.VALUE_DENY)).setOnCancelListener(d->response.complete(org.mozilla.geckoview.GeckoSession.PermissionDelegate.ContentPermission.VALUE_DENY)).setPositiveButton("Allow",(d,i)->response.complete(org.mozilla.geckoview.GeckoSession.PermissionDelegate.ContentPermission.VALUE_ALLOW)).show();
     return response;
    }
    @Override public void onMediaPermissionRequest(org.mozilla.geckoview.GeckoSession sess,String uri,org.mozilla.geckoview.GeckoSession.PermissionDelegate.MediaSource[] video,org.mozilla.geckoview.GeckoSession.PermissionDelegate.MediaSource[] audio,org.mozilla.geckoview.GeckoSession.PermissionDelegate.MediaCallback cb){
     if(!geckoActive||sess!=geckoSession||!geckoSecureOrigin(uri)||geckoMediaPending){cb.reject();return;}
     boolean camera=video!=null&&video.length>0,microphone=audio!=null&&audio.length>0;
     if(!camera&&!microphone){cb.reject();return;}
     int cameraChoice=getPreferences(0).getInt("site_camera",0),micChoice=getPreferences(0).getInt("site_microphone",0);
     if((camera&&cameraChoice==1)||(microphone&&micChoice==1)){cb.reject();return;}
     if((camera&&checkSelfPermission(android.Manifest.permission.CAMERA)!=android.content.pm.PackageManager.PERMISSION_GRANTED)||(microphone&&checkSelfPermission(android.Manifest.permission.RECORD_AUDIO)!=android.content.pm.PackageManager.PERMISSION_GRANTED)){cb.reject();return;}
     Runnable accept=()->{geckoMediaPending=false;if(geckoActive&&sess==geckoSession&&geckoSecureOrigin(uri))cb.grant(camera?video[0]:null,microphone?audio[0]:null);else cb.reject();};
     if((camera&&cameraChoice==0)||(microphone&&micChoice==0)){
      geckoMediaPending=true;
      new AlertDialog.Builder(MainActivity.this).setTitle("Website permission").setMessage(Uri.parse(uri).getHost()+" requests "+(camera?"camera ":"")+(microphone?"microphone":"")+". Allow for this request?").setPositiveButton("Allow",(d,i)->accept.run()).setNegativeButton("Block",(d,i)->{geckoMediaPending=false;cb.reject();}).setOnCancelListener(d->{geckoMediaPending=false;cb.reject();}).show();
     }else accept.run();
    }
   });

   session.setNavigationDelegate(new org.mozilla.geckoview.GeckoSession.NavigationDelegate(){
    @Override public void onLocationChange(org.mozilla.geckoview.GeckoSession sess,String url,java.util.List<org.mozilla.geckoview.GeckoSession.PermissionDelegate.ContentPermission> permissions,Boolean hasUserGesture){
     if(geckoActive&&geckoSession==sess){geckoAddress=url;if(!geckoIncognito)recordVisit(url);if(!address.hasFocus())address.setText(allowed(url)?url:"");}
    }
    @Override public void onCanGoBack(org.mozilla.geckoview.GeckoSession sess,boolean canGoBack){
     if(geckoActive&&geckoSession==sess){geckoCanGoBack=canGoBack;back.setEnabled(canGoBack);}
    }
    @Override public void onCanGoForward(org.mozilla.geckoview.GeckoSession sess,boolean canGoForward){
     if(geckoActive&&geckoSession==sess){geckoCanGoForward=canGoForward;forward.setEnabled(canGoForward);}
    }
   });
   geckoScrollY=0;
    session.setScrollDelegate(new org.mozilla.geckoview.GeckoSession.ScrollDelegate(){
     @Override public void onScrollChanged(org.mozilla.geckoview.GeckoSession sess,int scrollX,int scrollY){
      if(geckoSession==sess)geckoScrollY=scrollY;
     }
    });
    session.setProgressDelegate(new org.mozilla.geckoview.GeckoSession.ProgressDelegate(){
    @Override public void onPageStart(org.mozilla.geckoview.GeckoSession sess,String url){
     if(geckoActive&&geckoSession==sess){geckoAddress=url;progress.setProgress(0);progress.setVisibility(View.VISIBLE);if(!address.hasFocus())address.setText(url);}
    }
    @Override public void onPageStop(org.mozilla.geckoview.GeckoSession sess,boolean success){
     if(geckoActive&&geckoSession==sess){progress.setProgress(100);progress.setVisibility(View.INVISIBLE);if(getPreferences(0).getBoolean("zero_cache",false))clearResourceCaches(false);}
    }
   });
   geckoIncognito=incognito;
   session.open(geckoRuntime);
   geckoView.setSession(session);
   current().onPause();
   hideHome();content.removeAllViews();
   geckoActive=true;geckoCanGoBack=false;geckoCanGoForward=false;
   if(incognito)getWindow().addFlags(WindowManager.LayoutParams.FLAG_SECURE);
   address.setHint(incognito?"Incognito search or address":"Search or enter address");
   content.addView(geckoView,new FrameLayout.LayoutParams(-1,-1));
   address.setText(geckoAddress);
   back.setEnabled(false);forward.setEnabled(false);
   count.setContentDescription(incognito?"Switch normal WebView tabs; closes private session":"Switch WebView tabs; GeckoView active");
   updateToolbar();
   if(initial==null)showHome(true);else session.loadUri(initial);
  }catch(Exception e){
   if(geckoSession!=null){try{geckoSession.close();}catch(Exception ignored){}}
   geckoSession=null;geckoView=null;geckoActive=false;geckoIncognito=false;getWindow().clearFlags(WindowManager.LayoutParams.FLAG_SECURE);
   content.removeAllViews();current().onResume();content.addView(current(),new FrameLayout.LayoutParams(-1,-1));
   Toast.makeText(this,"Unable to initialise GeckoView",Toast.LENGTH_LONG).show();
  }
 }
 void closeGecko(){
  if(!geckoActive)return;
  geckoActive=false;
  hideHome();content.removeAllViews();
  if(geckoView!=null){try{geckoView.releaseSession();}catch(Exception ignored){}}
  if(geckoSession!=null){try{geckoSession.close();}catch(Exception ignored){}}
  if(geckoAndroidCallback!=null){geckoAndroidCallback.reject();geckoAndroidCallback=null;}
  geckoMediaPending=false;
  if(geckoIncognito)getWindow().clearFlags(WindowManager.LayoutParams.FLAG_SECURE);
  geckoIncognito=false;
  address.setHint("Search or enter address");
  geckoView=null;geckoSession=null;geckoAddress="";geckoScrollY=0;geckoCanGoBack=false;geckoCanGoForward=false;
  current().onResume();content.addView(current(),new FrameLayout.LayoutParams(-1,-1));
  address.setText(allowed(current().getUrl())?current().getUrl():"");
  back.setEnabled(current().canGoBack());forward.setEnabled(current().canGoForward());
  progress.setVisibility(View.INVISIBLE);count.setContentDescription("Switch tabs, "+tabs.size()+" open");
  updateToolbar();refreshTabletTabs();if(homeTabs.contains(current()))showHome(false);
 }
 void geckoGoBack(){if(geckoSession!=null&&geckoCanGoBack)geckoSession.goBack();}
 void geckoGoForward(){if(geckoSession!=null&&geckoCanGoForward)geckoSession.goForward();}
 void reloadActive(){if(geckoActive&&geckoSession!=null)geckoSession.reload();else current().reload();}
 private static final String PREF_HOMEPAGE="custom_homepage";
 private final Handler homeHandler=new Handler(Looper.getMainLooper());
 private int homeTapCount=0;
 private final Runnable openHomeAfterTap=()->{homeTapCount=0;if(!isFinishing()&&!isDestroyed())homeActive();};
 void homeTap(){
  if(geckoIncognito){originalHome();return;}
  homeHandler.removeCallbacks(openHomeAfterTap);
  if(++homeTapCount>=3){
   homeTapCount=0;getPreferences(0).edit().remove(PREF_HOMEPAGE).apply();
   originalHome();Toast.makeText(this,"Orion homepage restored",Toast.LENGTH_SHORT).show();
  }else homeHandler.postDelayed(openHomeAfterTap,350);
 }
 void saveCurrentHomepage(){
  if(geckoIncognito){Toast.makeText(this,"Cannot save a homepage in Incognito",Toast.LENGTH_SHORT).show();return;}
  homeHandler.removeCallbacks(openHomeAfterTap);homeTapCount=0;
  String url=homeOverlay!=null?"":(geckoActive?geckoAddress:(selected>=0?current().getUrl():""));
  if(!allowed(url)){Toast.makeText(this,"Open a website before saving a homepage",Toast.LENGTH_SHORT).show();return;}
  getPreferences(0).edit().putString(PREF_HOMEPAGE,url).apply();
  Toast.makeText(this,"Homepage saved",Toast.LENGTH_SHORT).show();
 }
 void homeActive(){if(geckoIncognito){originalHome();return;}String url=getPreferences(0).getString(PREF_HOMEPAGE,"");if(allowed(url))navigate(url);else originalHome();}
 void originalHome(){if(geckoActive&&geckoSession!=null){geckoAddress="";showHome(true);}else home(current());}
 void hideHome(){
  if(homeWeather!=null){homeWeather.dispose();homeWeather=null;}
  if(homeOverlay!=null){
   if(homeOverlay.getParent() instanceof ViewGroup)((ViewGroup)homeOverlay.getParent()).removeView(homeOverlay);
   homeOverlay=null;
  }
 }
 void showHome(boolean gecko){
  hideHome();
  FrameLayout page=new FrameLayout(this);page.setBackgroundColor(0xff171d28);
  page.setContentDescription(gecko?"Orion Private Browser. Powered by GeckoView. Search or enter an address below.":"Orion Private Browser. Search or enter an address below.");
  ScrollView scroll=new ScrollView(this);scroll.setFillViewport(true);scroll.setVerticalScrollBarEnabled(false);
  LinearLayout column=new LinearLayout(this);column.setOrientation(LinearLayout.VERTICAL);column.setGravity(Gravity.CENTER_HORIZONTAL);
  int top=getResources().getConfiguration().orientation==android.content.res.Configuration.ORIENTATION_LANDSCAPE?dp(16):dp(30);
  column.setPadding(dp(16),top,dp(16),dp(24));
  android.widget.ImageView emblem=new android.widget.ImageView(this);emblem.setImageResource(R.drawable.orion_icon);emblem.setScaleType(android.widget.ImageView.ScaleType.FIT_CENTER);emblem.setContentDescription("Orion logo");
  LinearLayout.LayoutParams emblemParams=new LinearLayout.LayoutParams(dp(88),dp(88));column.addView(emblem,emblemParams);
  TextView title=new TextView(this);title.setText("Orion");title.setTextSize(32);title.setTextColor(0xfff1f5f9);
  title.setTypeface(Typeface.create("sans-serif-medium",Typeface.NORMAL));title.setGravity(Gravity.CENTER);
  LinearLayout.LayoutParams titleParams=new LinearLayout.LayoutParams(-1,-2);titleParams.topMargin=dp(16);column.addView(title,titleParams);
  TextView subtitle=new TextView(this);subtitle.setText("PRIVATE BROWSER");subtitle.setTextSize(12);subtitle.setLetterSpacing(.24f);
  subtitle.setTextColor(0xffb9c6da);subtitle.setGravity(Gravity.CENTER);
  LinearLayout.LayoutParams subParams=new LinearLayout.LayoutParams(-1,-2);subParams.topMargin=dp(8);column.addView(subtitle,subParams);
  TextView instruction=new TextView(this);instruction.setText("Search or enter an address below.");
  instruction.setTextSize(16);instruction.setTextColor(0xffb9c6da);instruction.setGravity(Gravity.CENTER);
  LinearLayout.LayoutParams instructionParams=new LinearLayout.LayoutParams(-1,-2);instructionParams.topMargin=dp(22);column.addView(instruction,instructionParams);
  if(geckoIncognito){
   TextView banner=new TextView(this);banner.setText("INCOGNITO · PRIVATE SESSION");
   banner.setTextColor(0xfff5e9ff);banner.setTextSize(15);banner.setGravity(Gravity.CENTER);
   banner.setBackground(round(0xff463758,14));banner.setPadding(dp(16),dp(12),dp(16),dp(12));
   LinearLayout.LayoutParams bp=new LinearLayout.LayoutParams(-1,-2);bp.topMargin=dp(24);column.addView(banner,bp);
   TextView info=new TextView(this);info.setText("Private GeckoView storage stays separate from normal tabs. Closing Incognito ends the session. Websites, networks and downloaded files may still retain records.");
   info.setTextSize(14);info.setTextColor(0xffb9c6da);info.setGravity(Gravity.CENTER);info.setLineSpacing(dp(3),1f);
   LinearLayout.LayoutParams ip=new LinearLayout.LayoutParams(-1,-2);ip.topMargin=dp(14);column.addView(info,ip);
   Button end=new Button(this);end.setAllCaps(false);end.setText("End Incognito session");end.setTextColor(0xfff5e9ff);
   end.setBackground(round(0xff463758,12));end.setMinHeight(dp(48));end.setOnClickListener(v->closeGecko());
   LinearLayout.LayoutParams ep=new LinearLayout.LayoutParams(-1,dp(48));ep.topMargin=dp(18);column.addView(end,ep);
  }
  if(gecko){
   LinearLayout attribution=new LinearLayout(this);attribution.setGravity(Gravity.CENTER);attribution.setOrientation(LinearLayout.HORIZONTAL);
   GeckoMark mark=new GeckoMark();attribution.addView(mark,new LinearLayout.LayoutParams(dp(32),dp(32)));
   TextView powered=new TextView(this);powered.setText("Powered by GeckoView");powered.setTextSize(13);
   powered.setTextColor(0xffa8bce0);powered.setPadding(dp(8),0,0,0);
   attribution.addView(powered);
   LinearLayout.LayoutParams attrParams=new LinearLayout.LayoutParams(-1,dp(40));attrParams.topMargin=dp(24);column.addView(attribution,attrParams);
  }
  if(!geckoIncognito){
   LinearLayout weatherHost=new LinearLayout(this);weatherHost.setOrientation(LinearLayout.VERTICAL);weatherHost.setGravity(Gravity.CENTER_HORIZONTAL);
   column.addView(weatherHost,new LinearLayout.LayoutParams(-1,-2));
   homeWeather=HomeWeather.attach(this,weatherHost);
  }
  scroll.addView(column,new ScrollView.LayoutParams(-1,-2));
  page.addView(scroll,new FrameLayout.LayoutParams(-1,-1));
  homeOverlay=page;
  content.addView(page,new FrameLayout.LayoutParams(-1,-1));
  address.setText("");
  back.setEnabled(false);forward.setEnabled(false);
 }
 class GeckoMark extends View {
  final Paint p=new Paint(Paint.ANTI_ALIAS_FLAG);
  GeckoMark(){super(MainActivity.this);setContentDescription("GeckoView");}
  @Override protected void onDraw(Canvas c){
   super.onDraw(c);
   float scale=Math.min(getWidth(),getHeight())/32f;c.save();c.translate((getWidth()-32*scale)/2,(getHeight()-32*scale)/2);c.scale(scale,scale);
   p.setColor(0xff428cff);p.setStyle(Paint.Style.STROKE);p.setStrokeCap(Paint.Cap.ROUND);p.setStrokeJoin(Paint.Join.ROUND);p.setStrokeWidth(3.2f);
   Path tail=new Path();tail.moveTo(12,20);tail.cubicTo(3,27,2,14,7,11);tail.cubicTo(10,9,11,13,8,15);c.drawPath(tail,p);
   p.setStyle(Paint.Style.FILL);p.setColor(0xff3ca9ff);c.save();c.rotate(-24,18,16);c.drawOval(11,11,23,21,p);c.restore();
   c.drawCircle(25,10,5,p);p.setColor(0xff171d28);c.drawCircle(27,9,1,p);
   p.setColor(0xff3ca9ff);p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(2.4f);
   c.drawLine(14,18,9,24,p);c.drawLine(9,24,6,23,p);c.drawLine(9,24,8,27,p);
   c.drawLine(20,18,25,23,p);c.drawLine(25,23,28,21,p);c.drawLine(25,23,27,26,p);
   c.drawLine(20,13,17,8,p);c.drawLine(17,8,14,8,p);
   c.restore();
  }
 }
 void loadGeckoUrl(String url){hideHome();geckoAddress=url;geckoSession.loadUri(url);}
 void loadWebUrl(WebView w,String url){homeTabs.remove(w);if(!geckoActive&&w==current())hideHome();w.loadUrl(url);}
 
 Button geckoButton(String label,String description,Runnable action){Button b=new Button(this);b.setText(label);b.setAllCaps(false);b.setTextSize(18);b.setContentDescription(description);b.setTextColor(0xfff1f5f9);b.setMinHeight(dp(48));b.setMinWidth(dp(48));b.setPadding(0,0,0,0);b.setBackground(new RippleDrawable(ColorStateList.valueOf(0x338ab4ff),null,round(0xffffffff,10)));b.setOnClickListener(v->action.run());return b;}
  // Persist menu visibility by stable action ID, never by position.
 static final String[] MENU_IDS={"new_tab","engine","reload","find","bookmark","bookmarks","reading","history","import","share","reader","print","downloads","install","settings"};
 static final String[] MENU_TITLES={"New tab","Open in GeckoView","Reload page","Find in page","Save bookmark","Bookmarks","Reading list","History","Import browser data","Share link","Reader mode","Print / Save PDF","Downloads","Install and create shortcut","Settings"};
 boolean menuShown(String id){return getPreferences(0).getBoolean("menu_item_"+id,true);}
 void customiseMenu(){
  final String[] names=MENU_TITLES.clone();
  final boolean[] enabled=new boolean[MENU_IDS.length];
  for(int i=0;i<enabled.length;i++)enabled[i]=menuShown(MENU_IDS[i]);
  new AlertDialog.Builder(this).setTitle("Customise Orion menu")
    .setMultiChoiceItems(names,enabled,(d,i,checked)->enabled[i]=checked)
    .setPositiveButton("Save",(d,w)->{
      android.content.SharedPreferences.Editor edits=getPreferences(0).edit();
      for(int i=0;i<MENU_IDS.length;i++)edits.putBoolean("menu_item_"+MENU_IDS[i],enabled[i]);
      edits.apply();Toast.makeText(this,"Menu choices saved",Toast.LENGTH_SHORT).show();
    })
    .setNeutralButton("Reset",(d,w)->{
      android.content.SharedPreferences.Editor edits=getPreferences(0).edit();
      for(String id:MENU_IDS)edits.remove("menu_item_"+id);
      edits.apply();Toast.makeText(this,"Default menu restored",Toast.LENGTH_SHORT).show();
    }).setNegativeButton("Cancel",null).show();
 }
 void installOrShortcut(){
  if(geckoIncognito){Toast.makeText(this,"Shortcuts are unavailable in Incognito",Toast.LENGTH_LONG).show();return;}
  String url=geckoActive?geckoAddress:homeOverlay!=null?null:current().getUrl();
  if(!allowed(url)){Toast.makeText(this,"Open a website to add a shortcut",Toast.LENGTH_LONG).show();return;}
  String title=geckoActive?Uri.parse(url).getHost():current().getTitle();
  if(title==null||title.trim().isEmpty())title=Uri.parse(url).getHost();
  final String label=title.substring(0,Math.min(40,title.length()));
  final String target=url;
  new AlertDialog.Builder(this).setTitle("Add website to Home screen")
    .setMessage("Create a launcher shortcut for "+label+"?\n\nThis opens the website in Orion. Installing websites as standalone apps is not supported yet.")
    .setNegativeButton("Cancel",null)
    .setPositiveButton("Create shortcut",(d,w)->{
      if(android.os.Build.VERSION.SDK_INT<26){Toast.makeText(this,"Requires Android 8 or newer",Toast.LENGTH_LONG).show();return;}
      android.content.pm.ShortcutManager manager=getSystemService(android.content.pm.ShortcutManager.class);
      if(manager==null||!manager.isRequestPinShortcutSupported()){
        Toast.makeText(this,"Your Home screen launcher does not support pinned shortcuts",Toast.LENGTH_LONG).show();return;
      }
      Intent open=new Intent(Intent.ACTION_VIEW,Uri.parse(target),this,MainActivity.class);
      open.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
      String id="orion_site_"+Integer.toHexString(target.hashCode());
      android.content.pm.ShortcutInfo shortcut=new android.content.pm.ShortcutInfo.Builder(this,id)
       .setShortLabel(label)
       .setLongLabel(label)
       .setIcon(android.graphics.drawable.Icon.createWithResource(this,R.mipmap.ic_launcher))
       .setIntent(open).build();
      try{
       if(!manager.requestPinShortcut(shortcut,null))
        Toast.makeText(this,"Shortcut request was not accepted by your launcher",Toast.LENGTH_LONG).show();
      }catch(Exception e){Toast.makeText(this,"Could not request Home screen shortcut",Toast.LENGTH_LONG).show();}
    }).show();
 }
 void menu(){
  Dialog dialog=new Dialog(this);
  LinearLayout panel=new LinearLayout(this);panel.setOrientation(LinearLayout.VERTICAL);
  panel.setPadding(dp(16),dp(12),dp(16),dp(16));panel.setBackground(round(0xff293344,20));
  TextView title=new TextView(this);title.setText(geckoIncognito?"Orion · Incognito":geckoActive?"Orion · GeckoView":"Orion · WebView");
  title.setTextSize(22);title.setTextColor(0xfff1f5f9);title.setPadding(dp(8),dp(8),0,dp(12));panel.addView(title);
  ScrollView scroll=new ScrollView(this);LinearLayout actions=new LinearLayout(this);actions.setOrientation(LinearLayout.VERTICAL);
  scroll.addView(actions);panel.addView(scroll,new LinearLayout.LayoutParams(-1,0,1));
  // Both engines expose one stable set of actions. Unsupported GeckoView-only
  // operations stay visible but disabled; never operate a hidden WebView instead.
  String[] labels=MENU_TITLES.clone();
  labels[1]=geckoActive?"Return to WebView":"Open in GeckoView";
  String[] ids=MENU_IDS;
  Runnable[] tasks=new Runnable[]{
   ()->{if(geckoActive){closeGecko();}newTab();},
   ()->{if(geckoActive)closeGecko();else openGecko();},
   ()->refreshOrReload(),
   ()->findPage(),
   ()->saveOrAddBookmark(),
   ()->bookmarks(),()->readingList(),()->historyLibrary(),()->importBrowserData(),
   ()->shareOrionIfHome(),()->readerMode(),()->printPage(),()->showDownloadPanel(),
   ()->installOrShortcut(),()->settings()
  };
  if(homeOverlay!=null){
   labels[2]="Refresh start page";labels[4]=geckoIncognito?"Save bookmark · Unavailable":"Add bookmark";
   labels[9]="Share Orion website";
  }
  boolean canUsePage=homeOverlay==null&&allowed(geckoActive?geckoAddress:current().getUrl());
  for(int i=0;i<labels.length;i++){
    if(!menuShown(ids[i]))continue;
    final Runnable task=tasks[i];
    boolean requiresPage=ids[i].equals("find")||ids[i].equals("reader")||ids[i].equals("print")||ids[i].equals("install");
    boolean engineUnsupported=geckoActive&&(ids[i].equals("find")||ids[i].equals("reader")||ids[i].equals("print"));
    boolean unavailable=(requiresPage&&!canUsePage)||engineUnsupported||(geckoIncognito&&(ids[i].equals("bookmark")||ids[i].equals("import")||ids[i].equals("install")));
    if(engineUnsupported)labels[i]+=" · WebView only";
    Button button=new Button(this);button.setAllCaps(false);button.setText(labels[i]);
    button.setTextColor(unavailable?0xff919bab:0xfff1f5f9);button.setTextSize(16);
    button.setGravity(Gravity.START|Gravity.CENTER_VERTICAL);button.setPadding(dp(16),0,dp(16),0);
    button.setEnabled(!unavailable);button.setContentDescription(labels[i]+(unavailable?", unavailable":""));
    button.setBackground(unavailable?round(0xff293344,12):new RippleDrawable(ColorStateList.valueOf(0x338ab4ff),null,round(0xffffffff,12)));
    actions.addView(button,new LinearLayout.LayoutParams(-1,dp(48)));
    if(!unavailable)button.setOnClickListener(v->{dialog.dismiss();task.run();});
  }
  Button customise=new Button(this);customise.setText("Customise menu");customise.setAllCaps(false);
  customise.setTextSize(16);customise.setTextColor(0xfff1f5f9);customise.setMinHeight(dp(56));
  customise.setContentDescription("Customise which Orion menu items are shown");
  customise.setOnClickListener(v->{dialog.dismiss();customiseMenu();});panel.addView(customise,new LinearLayout.LayoutParams(-1,dp(56)));
  dialog.setContentView(panel);dialog.show();Window win=dialog.getWindow();
  win.setBackgroundDrawableResource(android.R.color.transparent);win.setGravity(Gravity.BOTTOM);
  win.setLayout(-1,Math.min(dp(610),(int)(getResources().getDisplayMetrics().heightPixels*.85f)));
 }
 // Give page-independent menu actions explicit, useful home-screen behaviour.
 void refreshOrReload(){
  if(homeOverlay!=null){showHome(geckoActive);if(homeWeather!=null&&HomeWeather.enabled(this))homeWeather.refreshSaved();return;}
  reloadActive();
 }
 void saveOrAddBookmark(){
  if(geckoIncognito)return;
  if(homeOverlay!=null){editBookmark(null,()->Toast.makeText(this,"Bookmark saved",Toast.LENGTH_SHORT).show());return;}
  save();
 }
 void shareOrionIfHome(){
  if(homeOverlay==null){sharePage();return;}
  Intent send=new Intent(Intent.ACTION_SEND);send.setType("text/plain");
  send.putExtra(Intent.EXTRA_TEXT,"https://2e0lxy.uk/orion.html");
  startActivity(Intent.createChooser(send,"Share Orion Browser"));
 }
 void incognitoExplained(){
  new AlertDialog.Builder(this).setTitle("How Incognito works")
   .setMessage("Orion opens a separate GeckoView private-browsing session, even when normal tabs use WebView. Its cookies, site storage, form entries and in-session history are kept apart from your ordinary tabs. Orion does not add these visits to your saved browsing history or bookmarks. Closing the private session ends that tab and its temporary browser state; the app does not claim forensic erasure of Android or network logs.\n\nYour internet provider, employer, school, mobile network and local network may still observe connections or DNS traffic. Sites still see your IP address and can track activity while the session is active. Signed-in accounts and Android services retain their own records. HTTPS encrypts website traffic but does not hide all connection metadata.\n\nIncognito does not prevent fingerprinting through browser and device characteristics. Orion does not enable extensions in private mode. Screenshots and recent-app snapshots are blocked during the session. Files you explicitly download or share outside Orion can remain on the device. Only one private GeckoView tab is supported in this experimental version; opening a normal tab ends it. Closing this session does not erase your normal browser tabs, cookies or history.")
   .setPositiveButton("Close",null).show();
 }
 void securityPrivacy(){
  Dialog dialog=new Dialog(this,android.R.style.Theme_Material_NoActionBar);
  LinearLayout page=new LinearLayout(this);page.setOrientation(LinearLayout.VERTICAL);page.setBackgroundColor(0xff171d28);
  LinearLayout heading=new LinearLayout(this);heading.setGravity(Gravity.CENTER_VERTICAL);
  heading.setPadding(dp(10),0,dp(16),0);heading.setBackgroundColor(0xff293344);
  Button backButton=new Button(this);backButton.setText("‹");backButton.setTextSize(28);
  backButton.setContentDescription("Close security and privacy");backButton.setTextColor(0xfff1f5f9);
  backButton.setBackground(round(0xff293344,12));backButton.setOnClickListener(v->dialog.dismiss());
  heading.addView(backButton,new LinearLayout.LayoutParams(dp(48),dp(56)));
  TextView title=new TextView(this);title.setText("Orion · Security and privacy");title.setTextSize(18);
  title.setTextColor(0xfff1f5f9);title.setTypeface(Typeface.create("sans-serif-medium",Typeface.NORMAL));
  title.setCompoundDrawables(new Glyph("orion"),null,null,null);title.setCompoundDrawablePadding(dp(10));
  heading.addView(title,new LinearLayout.LayoutParams(0,dp(56),1));page.addView(heading);
  ScrollView scroll=new ScrollView(this);scroll.setFillViewport(false);
  LinearLayout panel=new LinearLayout(this);panel.setOrientation(LinearLayout.VERTICAL);panel.setPadding(dp(8),dp(8),dp(8),dp(28));
  scroll.addView(panel);page.addView(scroll,new LinearLayout.LayoutParams(-1,0,1));
  section(panel,"PRIVATE BROWSING");
  settingRow(panel,geckoIncognito?"End Incognito session":"Start Incognito mode",
   geckoIncognito?"Close this private tab and return to normal browsing":"Separate GeckoView private session · no saved browsing history",()->{
    dialog.dismiss();
    if(geckoIncognito)closeGecko();else openIncognito();
  });
  TextView note=new TextView(this);note.setText("Private browsing protects local session data, not your identity or network connections.");
  note.setTextSize(14);note.setTextColor(0xffb9c6da);note.setPadding(dp(16),0,dp(16),dp(12));panel.addView(note);
  settingRow(panel,"What Incognito does and doesn't hide","Cookies, history, screenshots, downloads and network visibility",()->incognitoExplained());
  section(panel,"TRACKERS AND CONTENT");
  toggle(panel,"Ad blocker (WebView)","adblock",true);
  if(new java.io.File(getFilesDir(),"easylist.domains").length()>0)toggle(panel,"EasyList domain rules","easylist",false);
  else settingRow(panel,"Install and enable EasyList domain rules","Download supported domain rules for WebView",()->{dialog.dismiss();installFilterList("easylist");});
  if(new java.io.File(getFilesDir(),"easyprivacy.domains").length()>0)toggle(panel,"EasyPrivacy domain rules","easyprivacy",false);
  else settingRow(panel,"Install and enable EasyPrivacy domain rules","Download supported tracking-domain rules for WebView",()->{dialog.dismiss();installFilterList("easyprivacy");});
  toggle(panel,"YouTube ad request filtering","youtube_ads",true);
  settingRow(panel,"Update filter lists","Download refreshed EasyList and EasyPrivacy rules",()->updateFilters());
  settingRow(panel,"About ad blocking","Basic domain filtering; GeckoView parity is incomplete",()->new AlertDialog.Builder(this)
   .setTitle("Content blocking").setMessage("Orion implements basic domain filtering for WebView requests. It cannot guarantee every tracker is blocked and the GeckoView engine has different controls. Incognito does not prevent website tracking or fingerprinting.").setPositiveButton("OK",null).show());
  section(panel,"WEBSITE SECURITY");
  settingRow(panel,"HTTPS and connection warnings","HTTP requires confirmation in address navigation",()->privacy());
  toggle(panel,"JavaScript (WebView)","javascript",true);
  toggle(panel,"Load images (WebView)","images",true);
  toggle(panel,"No HTTP cache (WebView)","zero_cache",false);
   settingRow(panel,"Clear cached resources now","WebView and GeckoView HTTP caches; keeps cookies and bookmarks",()->clearResourceCaches(true));
  section(panel,"SITE PERMISSIONS");
  settingRow(panel,"Camera, microphone and location","Ask each time, Block or Allow on request",()->{dialog.dismiss();sitePermissions();});
  section(panel,"ENCRYPTED DNS AND NETWORK");
  settingRow(panel,"DNS and Secure DNS","Provider, IPv4/IPv6 diagnostics and local VPN",()->{dialog.dismiss();dnsSettings();});
  section(panel,"AI SHARING");
  settingRow(panel,"Orion AI Connect",aiBridge!=null&&aiBridge.running()?"Active · tap to disconnect":"Off · local MCP with per-request approval",()->{dialog.dismiss();aiConnectSettings();});
  settingRow(panel,"Share current page with an AI app","Android share sheet · title and URL only · never Incognito",()->{dialog.dismiss();shareCurrentPageToAi();});
  section(panel,"BROWSING DATA");
  settingRow(panel,"Clear normal browsing data","WebView cookies, site data and history; Orion recent URL history",()->{dialog.dismiss();clearBrowsingData();});
  dialog.setContentView(page);dialog.show();
  Window win=dialog.getWindow();if(win!=null){
   win.setLayout(-1,-1);
   win.getDecorView().setOnApplyWindowInsetsListener((v,insets)->{
    if(Build.VERSION.SDK_INT>=30){android.graphics.Insets i=insets.getInsets(WindowInsets.Type.systemBars()|WindowInsets.Type.displayCutout());
     page.setPadding(i.left,i.top,i.right,i.bottom);}
    return insets;
   });win.getDecorView().requestApplyInsets();
  }
 }
 void downloadPage(String url,String disposition,String mime){if(!allowed(url)){Toast.makeText(this,"Unsupported download address",Toast.LENGTH_SHORT).show();return;}String name=URLUtil.guessFileName(url,disposition,mime).replaceAll("[^a-zA-Z0-9._ -]","_");if(name.isEmpty()||name.equals(".")||name.equals(".."))name="download";final String filename=name;new AlertDialog.Builder(this).setTitle("Download file?").setMessage(filename+"\n"+Uri.parse(url).getHost()).setNegativeButton("Cancel",null).setPositiveButton("Parallel (4)",(d,i)->ParallelDownload.start(this,url,filename,()->standardDownload(url,filename))).setNeutralButton("Standard",(d,i)->standardDownload(url,filename)).show();}
 void standardDownload(String url,String filename){try{android.app.DownloadManager.Request req=new android.app.DownloadManager.Request(Uri.parse(url));req.setTitle(filename);req.setNotificationVisibility(android.app.DownloadManager.Request.VISIBILITY_VISIBLE_NOTIFY_COMPLETED);req.setDestinationInExternalPublicDir(Environment.DIRECTORY_DOWNLOADS,filename);long downloadId=((android.app.DownloadManager)getSystemService(DOWNLOAD_SERVICE)).enqueue(req);rememberDownload(downloadId);Toast.makeText(this,"Download queued · swipe up from the bottom toolbar for status",Toast.LENGTH_LONG).show();}catch(Exception e){Toast.makeText(this,"Unable to queue download",Toast.LENGTH_SHORT).show();}}

 // Standard download IDs survive app restarts; the system owns their transfer state.
 void rememberDownload(long id){
  ArrayList<String> ids=new ArrayList<>();ids.add(Long.toString(id));
  for(String value:getPreferences(0).getString("download_ids","").split(",")){
   if(!value.isEmpty()&&!value.equals(Long.toString(id))&&ids.size()<20)ids.add(value);
  }
  getPreferences(0).edit().putString("download_ids",android.text.TextUtils.join(",",ids)).apply();
 }
 TextView downloadLabel(String text,int size){
  TextView label=new TextView(this);label.setText(text);label.setTextSize(size);
  label.setTextColor(size>=17?0xfff1f5f9:0xffb9c6da);
  label.setPadding(dp(4),dp(6),dp(4),dp(4));return label;
 }
 int downloadColour(int percentage){
  int[] colours={0xff60a5fa,0xff22d3ee,0xfffacc15,0xff4ade80};
  float position=Math.max(0,Math.min(100,percentage))*3f/100f;
  int index=Math.min(2,(int)position);
  return (Integer)new android.animation.ArgbEvaluator().evaluate(position-index,colours[index],colours[index+1]);
 }
 void downloadProgress(LinearLayout parent,int percentage){
  ProgressBar progressBar=new ProgressBar(this,null,android.R.attr.progressBarStyleHorizontal);
  progressBar.setMax(100);progressBar.setProgress(Math.max(0,Math.min(100,percentage)));
  progressBar.setContentDescription("Download "+percentage+" per cent");
  progressBar.setProgressTintList(ColorStateList.valueOf(downloadColour(percentage)));
  parent.addView(progressBar,new LinearLayout.LayoutParams(-1,dp(9)));
 }
 void downloadRow(LinearLayout parent,String name,String detail,int progress,Runnable cancel){
  LinearLayout card=new LinearLayout(this);card.setOrientation(LinearLayout.VERTICAL);
  card.setPadding(dp(14),dp(10),dp(14),dp(12));card.setBackground(round(0xff24344a,14));
  LinearLayout.LayoutParams lp=new LinearLayout.LayoutParams(-1,-2);lp.bottomMargin=dp(12);parent.addView(card,lp);
  card.addView(downloadLabel(name,17));
  card.addView(downloadLabel(detail,13));
  if(progress>=0)downloadProgress(card,progress);
  if(cancel!=null){
   Button stop=new Button(this);stop.setText("Cancel download");stop.setAllCaps(false);
   stop.setTextColor(0xffffb4b4);stop.setMinHeight(dp(48));
   GradientDrawable stopBg=round(0xff24344a,12);stopBg.setStroke(dp(1),0xffef7777);stop.setBackground(new RippleDrawable(ColorStateList.valueOf(0x33ef7777),stopBg,null));
   stop.setOnClickListener(v->cancel.run());
   card.addView(stop,new LinearLayout.LayoutParams(-1,dp(48)));
  }
 }
 void updateDownloadRows(LinearLayout list){
  list.removeAllViews();boolean any=false;
  ParallelDownload.State transfer=ParallelDownload.snapshot();
  if(transfer!=null){
   any=true;long total=transfer.total,done=transfer.done.get();
   int percentage=total>0?(int)Math.min(100,100.0*done/total):-1;
   String details=transfer.status+(total>0?" · "+percentage+"%":"");
   if(!transfer.finished&&total>0)details+=String.format(Locale.UK," · %.2f MB/s",transfer.bytesPerSecond/1048576.0);
   downloadRow(list,transfer.name,details,percentage,transfer.finished?null:()->ParallelDownload.cancel());
   if(!transfer.finished&&total>0){
    long segmentSize=total/4;
    for(int i=0;i<4;i++){
     long segmentTotal=(i==3?total-total*3/4:total*(i+1)/4-total*i/4);
     int part=(int)Math.min(100,100.0*transfer.segmentBytes.get(i)/Math.max(1,segmentTotal));
     list.addView(downloadLabel("Connection "+(i+1)+" · "+part+"%",13));
     downloadProgress(list,part);
    }
   }
  }
  android.app.DownloadManager manager=(android.app.DownloadManager)getSystemService(DOWNLOAD_SERVICE);
  for(String value:getPreferences(0).getString("download_ids","").split(",")){
   if(value.isEmpty()||manager==null)continue;
   long id;try{id=Long.parseLong(value);}catch(NumberFormatException e){continue;}
   android.app.DownloadManager.Query query=new android.app.DownloadManager.Query();query.setFilterById(id);
   try(android.database.Cursor cursor=manager.query(query)){
    if(cursor==null||!cursor.moveToFirst())continue;
    any=true;
    int nameColumn=cursor.getColumnIndex(android.app.DownloadManager.COLUMN_TITLE);
    int statusColumn=cursor.getColumnIndex(android.app.DownloadManager.COLUMN_STATUS);
    int bytesColumn=cursor.getColumnIndex(android.app.DownloadManager.COLUMN_BYTES_DOWNLOADED_SO_FAR);
    int totalColumn=cursor.getColumnIndex(android.app.DownloadManager.COLUMN_TOTAL_SIZE_BYTES);
    String name=nameColumn>=0?cursor.getString(nameColumn):"Download";
    int status=statusColumn>=0?cursor.getInt(statusColumn):0;
    long bytes=bytesColumn>=0?cursor.getLong(bytesColumn):0,total=totalColumn>=0?cursor.getLong(totalColumn):-1;
    int percentage=total>0?(int)Math.min(100,100.0*bytes/total):-1;
    String state=status==android.app.DownloadManager.STATUS_SUCCESSFUL?"Complete":status==android.app.DownloadManager.STATUS_FAILED?"Failed":status==android.app.DownloadManager.STATUS_PAUSED?"Paused":status==android.app.DownloadManager.STATUS_RUNNING?"Downloading":"Queued";
    String description=state+(total>0?" · "+percentage+"%":"")+(status==android.app.DownloadManager.STATUS_RUNNING?" · Android managed":"");
    boolean active=status==android.app.DownloadManager.STATUS_RUNNING||status==android.app.DownloadManager.STATUS_PAUSED||status==android.app.DownloadManager.STATUS_PENDING;
    downloadRow(list,name,description,percentage,active?()->manager.remove(id):null);
   }catch(Exception ignored){list.addView(downloadLabel("Unable to inspect a system download",13));}
  }
  if(!any)list.addView(downloadLabel("No downloads yet. Start a download to see progress here.",15));
 }
 void showDownloadPanel(){
  final Dialog dialog=new Dialog(this);
  LinearLayout panel=new LinearLayout(this);panel.setOrientation(LinearLayout.VERTICAL);
  panel.setPadding(dp(18),dp(12),dp(18),dp(16));panel.setBackground(round(0xff17263d,20));
  LinearLayout header=new LinearLayout(this);header.setGravity(Gravity.CENTER_VERTICAL);
  TextView title=downloadLabel("Downloads",22);header.addView(title,new LinearLayout.LayoutParams(0,dp(52),1));
  Button close=geckoButton("×","Close downloads",()->dialog.dismiss());
  header.addView(close,new LinearLayout.LayoutParams(dp(52),dp(52)));panel.addView(header);
  TextView hint=downloadLabel("Swipe up from the bottom toolbar to reopen this panel.",13);panel.addView(hint);
  ScrollView scroll=new ScrollView(this);scroll.setFillViewport(true);
  LinearLayout list=new LinearLayout(this);list.setOrientation(LinearLayout.VERTICAL);
  list.setPadding(0,dp(12),0,dp(12));scroll.addView(list);
  panel.addView(scroll,new LinearLayout.LayoutParams(-1,0,1));
  Button system=geckoButton("Open system Downloads","Open Android downloads",()->{
   try{startActivity(new Intent(android.app.DownloadManager.ACTION_VIEW_DOWNLOADS));}
   catch(Exception e){Toast.makeText(this,"No system Downloads app available",Toast.LENGTH_SHORT).show();}
  });
  panel.addView(system,new LinearLayout.LayoutParams(-1,dp(50)));
  final Handler refreshHandler=new Handler(Looper.getMainLooper());
  final Runnable[] refresh=new Runnable[1];
  refresh[0]=()->{
   if(!dialog.isShowing()||isFinishing()||isDestroyed())return;
   updateDownloadRows(list);
   refreshHandler.postDelayed(refresh[0],900);
  };
  dialog.setOnDismissListener(d->refreshHandler.removeCallbacks(refresh[0]));
  dialog.setContentView(panel);
  dialog.show();
  Window win=dialog.getWindow();win.setBackgroundDrawableResource(android.R.color.transparent);
  win.setGravity(Gravity.BOTTOM);
  win.setLayout(-1,Math.min(dp(560),(int)(getResources().getDisplayMetrics().heightPixels*.80f)));
  refreshHandler.post(refresh[0]);
 }
 void readerMode(){WebView target=current();if(!allowed(target.getUrl()))return;target.evaluateJavascript("(function(){var e=document.querySelector('article,main,[role=main]')||document.body;return e?e.innerText.slice(0,150000):'';})()",value->{if(isDestroyed()||target!=current())return;String body;try{Object parsed=new org.json.JSONTokener(value).nextValue();body=parsed instanceof String?(String)parsed:"";}catch(Exception e){body="";}if(body.trim().isEmpty()){Toast.makeText(this,"No readable text found",Toast.LENGTH_SHORT).show();return;}Dialog dialog=new Dialog(this);LinearLayout panel=new LinearLayout(this);panel.setOrientation(LinearLayout.VERTICAL);panel.setBackgroundColor(0xff171d28);Button close=new Button(this);close.setText("Close reader");close.setOnClickListener(v->dialog.dismiss());panel.addView(close);ScrollView scroll=new ScrollView(this);TextView article=new TextView(this);article.setText(body);article.setTextColor(0xfff1f5f9);article.setTextSize(18);article.setLineSpacing(dp(5),1.2f);article.setPadding(dp(24),dp(16),dp(24),dp(24));article.setTextIsSelectable(true);scroll.addView(article);panel.addView(scroll,new LinearLayout.LayoutParams(-1,0,1));dialog.setContentView(panel);dialog.show();dialog.getWindow().setLayout(-1,-1);});}
 void printPage(){if(!allowed(current().getUrl()))return;android.print.PrintManager manager=(android.print.PrintManager)getSystemService(PRINT_SERVICE);if(manager!=null)manager.print("Orion webpage",current().createPrintDocumentAdapter("Orion webpage"),new android.print.PrintAttributes.Builder().build());}
 void sharePage(){String u=geckoActive?geckoAddress:current().getUrl();if(allowed(u)){Intent intent=new Intent(Intent.ACTION_SEND);intent.setType("text/plain");intent.putExtra(Intent.EXTRA_TEXT,u);startActivity(Intent.createChooser(intent,"Share link"));}}
 void shareCurrentPageToAi(){
  if(geckoIncognito){Toast.makeText(this,"Private pages cannot be shared with AI apps",Toast.LENGTH_LONG).show();return;}
  String url=geckoActive?geckoAddress:selected>=0?current().getUrl():null;
  if(!allowed(url)){Toast.makeText(this,"Open a normal webpage before sharing",Toast.LENGTH_SHORT).show();return;}
  String title=geckoActive?"Webpage":current().getTitle();
  Intent send=new Intent(Intent.ACTION_SEND);
  send.setType("text/plain");
  send.putExtra(Intent.EXTRA_SUBJECT,"Page shared from Orion");
  send.putExtra(Intent.EXTRA_TEXT,(title==null?"":title.substring(0,Math.min(title.length(),120))+"\n")+url);
  startActivity(Intent.createChooser(send,"Share page with an AI app"));
 }

 void findPage(){WebView target=current();EditText field=new EditText(this);field.setSingleLine(true);field.setHint("Find text");field.setContentDescription("Find text on page");AlertDialog dialog=new AlertDialog.Builder(this).setTitle("Find in page").setView(field).setPositiveButton("Next",null).setNeutralButton("Previous",null).setNegativeButton("Done",null).create();field.addTextChangedListener(new android.text.TextWatcher(){public void beforeTextChanged(CharSequence s,int a,int c,int f){}public void onTextChanged(CharSequence s,int a,int b,int c){if(s.length()==0)target.clearMatches();else target.findAllAsync(s.toString());}public void afterTextChanged(android.text.Editable e){}});dialog.setOnDismissListener(d->target.clearMatches());dialog.show();dialog.getButton(-1).setOnClickListener(v->target.findNext(true));dialog.getButton(-3).setOnClickListener(v->target.findNext(false));}
 void home(WebView w){homeTabs.add(w);if(w==current()&&!geckoActive)showHome(false);address.setText("");}
 WebView current(){return tabs.get(selected);}
 boolean allowed(String s){try{Uri u=Uri.parse(s);return ("https".equalsIgnoreCase(u.getScheme())||"http".equalsIgnoreCase(u.getScheme()))&&u.getHost()!=null&&u.getUserInfo()==null;}catch(Exception e){return false;}}
 void navigate(String raw){
  String s=raw.trim();if(s.isEmpty())return;
  if(!s.contains(" ")&&!s.contains("://")&&s.contains("."))s="https://"+s;
  if(!allowed(s)){if(s.contains("://")){Toast.makeText(this,"Only HTTP and HTTPS pages are supported",Toast.LENGTH_SHORT).show();return;}s=searchPrefix()+Uri.encode(s);}
  final String url=s;
  if(geckoActive&&geckoSession!=null){
   if("http".equalsIgnoreCase(Uri.parse(url).getScheme())){new AlertDialog.Builder(this).setTitle("Unencrypted connection").setMessage("HTTP is not encrypted. Continue to "+Uri.parse(url).getHost()+"?").setNegativeButton("Cancel",null).setPositiveButton("Continue",(d,i)->{if(geckoActive&&geckoSession!=null)loadGeckoUrl(url);}).show();}
   else loadGeckoUrl(url);
  }else if("http".equalsIgnoreCase(Uri.parse(url).getScheme())){
   final WebView target=current();
   new AlertDialog.Builder(this).setTitle("Unencrypted connection").setMessage("HTTP is not encrypted. Avoid entering passwords or personal information. Continue to "+Uri.parse(url).getHost()+"?").setNegativeButton("Cancel",null).setPositiveButton("Continue",(d,i)->{if(tabs.contains(target)&&target==current())loadWebUrl(target,url);}).show();
  }else loadWebUrl(current(),url);
 }
 void newTab(){if(tabs.size()>=20){Toast.makeText(this,"20 tabs open. Close a tab first.",Toast.LENGTH_SHORT).show();return;}WebView w=new WebView(this);WebSettings s=w.getSettings();s.setJavaScriptEnabled(true);s.setDomStorageEnabled(true);s.setAllowFileAccess(false);s.setAllowContentAccess(false);s.setMixedContentMode(WebSettings.MIXED_CONTENT_NEVER_ALLOW);s.setJavaScriptCanOpenWindowsAutomatically(false);s.setSafeBrowsingEnabled(true);CookieManager.getInstance().setAcceptThirdPartyCookies(w,false);
 w.setWebViewClient(new WebViewClient(){@Override public WebResourceResponse shouldInterceptRequest(WebView v,WebResourceRequest request){if(!request.isForMainFrame()&&((getPreferences(0).getBoolean("adblock",true)&&AdBlocker.blocks(request.getUrl().getHost()))||(getPreferences(0).getBoolean("youtube_ads",true)&&youtubeAd(request.getUrl()))))return new WebResourceResponse("text/plain","UTF-8",new java.io.ByteArrayInputStream(new byte[0]));return super.shouldInterceptRequest(v,request);}@Override public boolean shouldOverrideUrlLoading(WebView v,WebResourceRequest r){if(!allowed(r.getUrl().toString()))return true;if(r.isForMainFrame()&&"http".equalsIgnoreCase(r.getUrl().getScheme())){if(!"http".equalsIgnoreCase(Uri.parse(v.getUrl()==null?"":v.getUrl()).getScheme())||!Objects.equals(Uri.parse(v.getUrl()==null?"":v.getUrl()).getHost(),r.getUrl().getHost())){if(!geckoActive&&v==current())navigate(r.getUrl().toString());return true;}}return false;}@Override public void onPageFinished(WebView v,String u){if(!geckoActive&&selected>=0&&v==current()){if(!address.hasFocus())address.setText(allowed(u)?u:"");recordVisit(u);address.setTextColor(u!=null&&u.startsWith("http://")?0xffffcf80:0xfff1f5f9);back.setEnabled(v.canGoBack());forward.setEnabled(v.canGoForward());}if(getPreferences(0).getBoolean("zero_cache",false))v.clearCache(true);}});
 w.setWebChromeClient(new WebChromeClient(){@Override public void onProgressChanged(WebView v,int p){if(selected>=0&&v==current()){progress.setProgress(p);progress.setVisibility(p<100?View.VISIBLE:View.INVISIBLE);}}@Override public void onPermissionRequest(PermissionRequest r){mediaPermission(w,r);}@Override public void onPermissionRequestCanceled(PermissionRequest r){if(pendingMedia==r)pendingMedia=null;}@Override public void onGeolocationPermissionsShowPrompt(String origin,GeolocationPermissions.Callback cb){locationPermission(w,origin,cb); }});
 w.setDownloadListener((url,ua,disposition,mime,length)->downloadPage(url,disposition,mime));applySettings(w);tabs.add(w);show(tabs.size()-1);home(w);}
 PermissionRequest pendingMedia; WebView permissionTab; String[] mediaResources; GeolocationPermissions.Callback pendingLocation; String locationOrigin;
 boolean secureOrigin(WebView w,String origin){if(selected<0||w!=current()||origin==null)return false;Uri a=Uri.parse(origin),b=Uri.parse(w.getUrl()==null?"":w.getUrl());return "https".equalsIgnoreCase(a.getScheme())&&"https".equalsIgnoreCase(b.getScheme())&&a.getHost()!=null&&a.getHost().equalsIgnoreCase(b.getHost())&&(a.getPort()==-1?443:a.getPort())==(b.getPort()==-1?443:b.getPort());}
 void mediaPermission(WebView w,PermissionRequest req){if(pendingMedia!=null||pendingLocation!=null||!secureOrigin(w,req.getOrigin().toString())){req.deny();return;}ArrayList<String> resources=new ArrayList<>(),permissions=new ArrayList<>();for(String item:req.getResources()){if(item.equals(PermissionRequest.RESOURCE_VIDEO_CAPTURE)){resources.add(item);permissions.add(android.Manifest.permission.CAMERA);}else if(item.equals(PermissionRequest.RESOURCE_AUDIO_CAPTURE)){resources.add(item);permissions.add(android.Manifest.permission.RECORD_AUDIO);}}if(resources.isEmpty()){req.deny();return;}for(String p:permissions){String key=p.equals(android.Manifest.permission.CAMERA)?"site_camera":"site_microphone";if(getPreferences(0).getInt(key,0)==1){req.deny();return;}}pendingMedia=req;permissionTab=w;mediaResources=resources.toArray(new String[0]);Runnable grantMedia=()->{if(pendingMedia!=req)return;ArrayList<String> missing=new ArrayList<>();for(String p:permissions)if(checkSelfPermission(p)!=android.content.pm.PackageManager.PERMISSION_GRANTED)missing.add(p);if(missing.isEmpty())finishMedia(true);else requestPermissions(missing.toArray(new String[0]),41);};boolean prompt=false;for(String p:permissions){String key=p.equals(android.Manifest.permission.CAMERA)?"site_camera":"site_microphone";if(getPreferences(0).getInt(key,0)==0)prompt=true;}if(!prompt){grantMedia.run();return;}new AlertDialog.Builder(this).setTitle("Website permission").setMessage(req.getOrigin().getHost()+" requests "+(resources.contains(PermissionRequest.RESOURCE_VIDEO_CAPTURE)?"camera ":"")+(resources.contains(PermissionRequest.RESOURCE_AUDIO_CAPTURE)?"microphone":"")+". Allow for this request?").setNegativeButton("Deny",(d,i)->finishMedia(false)).setOnCancelListener(d->finishMedia(false)).setPositiveButton("Allow",(d,i)->grantMedia.run()).show();}
 void finishMedia(boolean allow){PermissionRequest r=pendingMedia;pendingMedia=null;if(r==null)return;if(allow&&secureOrigin(permissionTab,r.getOrigin().toString()))r.grant(mediaResources);else r.deny();}
 void locationPermission(WebView w,String origin,GeolocationPermissions.Callback cb){if(pendingMedia!=null||pendingLocation!=null||!secureOrigin(w,origin)||getPreferences(0).getInt("site_location",0)==1){cb.invoke(origin,false,false);return;}pendingLocation=cb;locationOrigin=origin;permissionTab=w;Runnable grantLocation=()->{if(checkSelfPermission(android.Manifest.permission.ACCESS_COARSE_LOCATION)==android.content.pm.PackageManager.PERMISSION_GRANTED)finishLocation(true);else requestPermissions(new String[]{android.Manifest.permission.ACCESS_COARSE_LOCATION},42);};if(getPreferences(0).getInt("site_location",0)==2){grantLocation.run();return;}new AlertDialog.Builder(this).setTitle("Location permission").setMessage(Uri.parse(origin).getHost()+" requests approximate location. Allow for this request?").setNegativeButton("Deny",(d,i)->finishLocation(false)).setOnCancelListener(d->finishLocation(false)).setPositiveButton("Allow",(d,i)->grantLocation.run()).show();}
 void finishLocation(boolean allow){GeolocationPermissions.Callback cb=pendingLocation;pendingLocation=null;if(cb!=null)cb.invoke(locationOrigin,allow&&secureOrigin(permissionTab,locationOrigin),false);}
 @Override public void onRequestPermissionsResult(int code,String[] permissions,int[] results){super.onRequestPermissionsResult(code,permissions,results);boolean ok=results.length>0;for(int result:results)ok&=result==android.content.pm.PackageManager.PERMISSION_GRANTED;if(code==HomeWeather.LOCATION_REQUEST){if(homeWeather!=null)homeWeather.onPermissionResult(ok);return;}if(code==41)finishMedia(ok);if(code==42)finishLocation(ok);if(code==43){org.mozilla.geckoview.GeckoSession.PermissionDelegate.Callback cb=geckoAndroidCallback;geckoAndroidCallback=null;if(cb!=null){if(ok&&geckoActive)cb.grant();else cb.reject();}}}
 void show(int i){if(geckoActive)closeGecko();if(selected>=0&&selected<tabs.size())tabs.get(selected).onPause();hideHome();content.removeAllViews();selected=i;current().onResume();content.addView(current(),new FrameLayout.LayoutParams(-1,-1));address.setText(allowed(current().getUrl())?current().getUrl():"");count.setContentDescription("Switch tabs, "+tabs.size()+" open");count.invalidate();back.setEnabled(current().canGoBack());forward.setEnabled(current().canGoForward());refreshTabletTabs();if(homeTabs.contains(current()))showHome(false);}
 void chooseTab(){String[] names=new String[tabs.size()];for(int i=0;i<names.length;i++)names[i]=tabs.get(i).getTitle()==null?"New tab":tabs.get(i).getTitle();new AlertDialog.Builder(this).setTitle("Tabs").setItems(names,(d,i)->show(i)).setPositiveButton("Archived tabs",(d,i)->archivedTabs()).setNeutralButton("Close current",(d,i)->{WebView w=current();content.removeAllViews();tabs.remove(selected);w.destroy();if(tabs.isEmpty())newTab();else show(Math.min(selected,tabs.size()-1));}).show();}
 void archivedTabs(){String[] urls=new TreeSet<>(getPreferences(0).getStringSet("archived_tabs",Collections.emptySet())).toArray(new String[0]);new AlertDialog.Builder(this).setTitle("Archived tabs").setItems(urls,(d,i)->{if(tabs.size()>=20){Toast.makeText(this,"Close a tab before restoring",Toast.LENGTH_SHORT).show();return;}newTab();navigate(urls[i]);Set<String> set=new HashSet<>(Arrays.asList(urls));set.remove(urls[i]);getPreferences(0).edit().putStringSet("archived_tabs",set).apply();}).setPositiveButton("Archive current",(d,i)->{String url=current().getUrl();if(!allowed(url))return;Set<String> set=new HashSet<>(getPreferences(0).getStringSet("archived_tabs",Collections.emptySet()));set.add(url);getPreferences(0).edit().putStringSet("archived_tabs",set).apply();WebView w=current();content.removeAllViews();int index=selected;tabs.remove(index);selected=-1;w.destroy();if(tabs.isEmpty())newTab();else show(Math.min(index,tabs.size()-1));}).setNegativeButton("Close",null).show();}
 void save(){if(geckoIncognito){Toast.makeText(this,"Bookmarks are not saved in Incognito",Toast.LENGTH_SHORT).show();return;}String u=geckoActive?geckoAddress:current().getUrl();if(!allowed(u))return;Set<String> set=new HashSet<>(getPreferences(0).getStringSet("bookmarks",Collections.emptySet()));set.add(u);getPreferences(0).edit().putStringSet("bookmarks",set).apply();Toast.makeText(this,"Bookmark saved",Toast.LENGTH_SHORT).show();}
 void bookmarks(){Dialog dialog=new Dialog(this,android.R.style.Theme_Material_NoActionBar);LinearLayout page=new LinearLayout(this);page.setOrientation(LinearLayout.VERTICAL);page.setBackgroundColor(0xff171d28);Button close=new Button(this);close.setText("‹ Bookmarks");close.setOnClickListener(v->dialog.dismiss());page.addView(close);EditText search=new EditText(this);search.setSingleLine(true);search.setHint("Search bookmarks");search.setTextColor(0xfff1f5f9);search.setHintTextColor(0xffb9c6da);page.addView(search);Button add=new Button(this);add.setText("Add bookmark");page.addView(add);ScrollView scroll=new ScrollView(this);LinearLayout list=new LinearLayout(this);list.setOrientation(LinearLayout.VERTICAL);scroll.addView(list);page.addView(scroll,new LinearLayout.LayoutParams(-1,0,1));
 Runnable refresh=()->{list.removeAllViews();String q=search.getText().toString().toLowerCase(Locale.ROOT);TreeSet<String> urls=new TreeSet<>(getPreferences(0).getStringSet("bookmarks",Collections.emptySet()));for(String u:urls){if(!u.toLowerCase(Locale.ROOT).contains(q))continue;LinearLayout row=new LinearLayout(this);Button open=new Button(this);open.setAllCaps(false);open.setText(u);open.setOnClickListener(v->{dialog.dismiss();navigate(u);});row.addView(open,new LinearLayout.LayoutParams(0,-2,1));Button edit=new Button(this);edit.setText("Edit");edit.setOnClickListener(v->editBookmark(u,()->{dialog.dismiss();bookmarks();}));row.addView(edit);list.addView(row);}if(list.getChildCount()==0){TextView empty=new TextView(this);empty.setText("No matching bookmarks");empty.setTextColor(0xffb9c6da);empty.setPadding(dp(16),dp(16),dp(16),dp(16));list.addView(empty);}};
 search.addTextChangedListener(new android.text.TextWatcher(){public void beforeTextChanged(CharSequence s,int a,int c,int f){}public void onTextChanged(CharSequence s,int a,int b,int c){refresh.run();}public void afterTextChanged(android.text.Editable e){}});add.setOnClickListener(v->editBookmark(null,refresh));refresh.run();dialog.setContentView(page);dialog.show();dialog.getWindow().setLayout(-1,-1);page.setOnApplyWindowInsetsListener((v,insets)->{if(Build.VERSION.SDK_INT>=30){android.graphics.Insets i=insets.getInsets(WindowInsets.Type.systemBars()|WindowInsets.Type.displayCutout()|WindowInsets.Type.ime());v.setPadding(i.left,i.top,i.right,i.bottom);}else{v.setPadding(insets.getSystemWindowInsetLeft(),insets.getSystemWindowInsetTop(),insets.getSystemWindowInsetRight(),insets.getSystemWindowInsetBottom());}return insets;});page.requestApplyInsets();}
 void importBrowserData(){
 boolean hasImportedPasswords=new java.io.File(getFilesDir(),"imported_passwords.enc").length()>0;
 String[] choices={"Import bookmarks · HTML / JSON","Import passwords · CSV",
  hasImportedPasswords?"Export passwords · CSV":"Export passwords · Unavailable (vault empty)",
  hasImportedPasswords?"Delete stored passwords":"Delete stored passwords · Unavailable (vault empty)",
  "Import cookies · txt / JSON","Android Autofill · Unavailable"};
  android.widget.ArrayAdapter<String> options=new android.widget.ArrayAdapter<String>(this,android.R.layout.simple_list_item_1,choices){
   @Override public boolean isEnabled(int position){return position!=5&&(hasImportedPasswords||(position!=2&&position!=3));}
   @Override public View getView(int position,View convertView,android.view.ViewGroup parent){
    View view=super.getView(position,convertView,parent);
    boolean enabled=isEnabled(position);
    if(view instanceof TextView)((TextView)view).setTextColor(enabled?0xfff1f5f9:0xff919bab);
    view.setEnabled(enabled);
    return view;
   }
  };
 new AlertDialog.Builder(this).setTitle("Import and export").setAdapter(options,(d,i)->{
  switch(i){
   case 0:pickImport(BOOKMARK_IMPORT_REQUEST);break;
   case 1:new AlertDialog.Builder(this).setTitle("Import passwords").setMessage("Select a CSV exported by your own browser or password manager. Orion will encrypt imported credentials using Android Keystore. The original CSV remains wherever you saved it and should be deleted securely after migration. Imported passwords are not yet used for automatic form filling.")
    .setNegativeButton("Cancel",null).setPositiveButton("Choose CSV",(x,y)->pickImport(PASSWORD_IMPORT_REQUEST)).show();break;
   case 2:passwordExport();break;
   case 3:new AlertDialog.Builder(this).setTitle("Delete imported passwords?").setMessage("Permanently delete Orion's local imported-password vault. This cannot be undone.")
    .setNegativeButton("Cancel",null).setPositiveButton("Delete",(x,y)->{PasswordVault.clear(getApplicationContext());Toast.makeText(this,"Orion password vault deleted",Toast.LENGTH_LONG).show();}).show();break;
   case 4:new AlertDialog.Builder(this).setTitle("Import cookies").setMessage("Choose an export file containing cookies from a browser you own. Cookies can represent active login sessions. Orion will show the included website domains for confirmation before importing into WebView. GeckoView has a separate cookie store. Only import files you trust.")
    .setNegativeButton("Cancel",null).setPositiveButton("Choose export",(x,y)->pickImport(COOKIE_IMPORT_REQUEST)).show();break;
   case 5:break; // Disabled until a supported autofill integration exists.
  }
 }).setNegativeButton("Close",null).show();
}
 void pickImport(int code){Intent intent=new Intent(Intent.ACTION_OPEN_DOCUMENT);intent.addCategory(Intent.CATEGORY_OPENABLE);intent.setType("*/*");intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);startActivityForResult(intent,code);}
 void passwordExport(){
  new Thread(()->{int total;try{total=PasswordVault.count(getApplicationContext());}catch(Exception e){runOnUiThread(()->Toast.makeText(this,"Could not unlock password vault",Toast.LENGTH_LONG).show());return;}
   final int n=total;runOnUiThread(()->{if(isFinishing()||isDestroyed())return;if(n==0){new AlertDialog.Builder(this).setTitle("No passwords stored").setMessage("Import a password CSV first.").setPositiveButton("OK",null).show();return;}
    new AlertDialog.Builder(this).setTitle("Export "+n+" passwords?")
     .setMessage("This exports a PLAIN-TEXT CSV containing all "+n+" passwords. Anyone with the file can read them. Save it only to a secure location, move it to your password manager promptly and delete the CSV when finished. Orion's encrypted local copy is retained.")
     .setNegativeButton("Cancel",null).setPositiveButton("Export CSV",(d,i)->{Intent intent=new Intent(Intent.ACTION_CREATE_DOCUMENT);intent.addCategory(Intent.CATEGORY_OPENABLE);intent.setType("text/csv");intent.putExtra(Intent.EXTRA_TITLE,"orion-passwords.csv");startActivityForResult(intent,PASSWORD_EXPORT_REQUEST);}).show();
   });},"orion-password-export-check").start();
 }
 @Override protected void onActivityResult(int request,int result,Intent data){
  super.onActivityResult(request,result,data);
  if(request==DNS_VPN_REQUEST){
   if(result==RESULT_OK)startDnsVpn();
   else Toast.makeText(this,"Secure DNS VPN not enabled",Toast.LENGTH_LONG).show();
   return;
  }
  if(result!=RESULT_OK||data==null||data.getData()==null)return;
  final Uri source=data.getData();
  if(request==BOOKMARK_IMPORT_REQUEST){
   new Thread(()->{try{Set<String> imported=BookmarkImporter.read(getApplicationContext(),source);runOnUiThread(()->{if(isFinishing()||isDestroyed())return;Set<String> existing=new HashSet<>(getPreferences(0).getStringSet("bookmarks",Collections.emptySet()));int added=0;for(String item:imported)if(existing.add(item))added++;final int count=added;new AlertDialog.Builder(this).setTitle("Import bookmarks?").setMessage(imported.size()+" valid bookmarks found. "+count+" are new. Existing bookmarks will be kept.").setNegativeButton("Cancel",null).setPositiveButton("Import",(d,i)->{getPreferences(0).edit().putStringSet("bookmarks",existing).apply();Toast.makeText(this,count+" bookmarks imported",Toast.LENGTH_LONG).show();}).show();});}catch(Exception e){importError("Bookmark import failed",e);}},"orion-bookmark-import").start();
  }else if(request==PASSWORD_IMPORT_REQUEST){
   new Thread(()->{try{
    ArrayList<PasswordVault.Entry> entries=PasswordVault.parseCsv(getApplicationContext(),source);
    runOnUiThread(()->{if(isFinishing()||isDestroyed())return;new AlertDialog.Builder(this).setTitle("Import "+entries.size()+" passwords?").setMessage("Passwords will be encrypted on this device. Entries with the same website and username will be updated. The selected CSV will not be copied into Orion's files. This does not enable autofill.")
     .setNegativeButton("Cancel",null).setPositiveButton("Import",(d,i)->new Thread(()->{try{int added=PasswordVault.merge(getApplicationContext(),entries);runOnUiThread(()->Toast.makeText(this,"Stored "+entries.size()+" passwords ("+added+" new)",Toast.LENGTH_LONG).show());}catch(Exception e){importError("Password storage failed",e);}},"orion-password-save").start()).show();});
   }catch(Exception e){importError("Password import failed",e);}},"orion-password-read").start();
  }else if(request==PASSWORD_EXPORT_REQUEST){
   new Thread(()->{try{int count=PasswordVault.exportCsv(getApplicationContext(),source);runOnUiThread(()->Toast.makeText(this,count+" passwords exported. Protect or delete the CSV.",Toast.LENGTH_LONG).show());}catch(Exception e){importError("Password export failed",e);}},"orion-password-write").start();
  }else if(request==COOKIE_IMPORT_REQUEST){
   new Thread(()->{try{CookieImporter.Preview preview=CookieImporter.read(getApplicationContext(),source);runOnUiThread(()->{if(isFinishing()||isDestroyed())return;StringBuilder hosts=new StringBuilder();int shown=0;for(String host:preview.hosts){if(shown++>=18){hosts.append("\n…and ").append(preview.hosts.size()-18).append(" more sites");break;}hosts.append("\n").append(host);}
    new AlertDialog.Builder(this).setTitle("Import "+preview.entries.size()+" cookies?").setMessage("Into WebView only, from "+preview.hosts.size()+" sites:"+hosts+"\n\nCookies may enable account sessions. Only continue if you trust every domain.")
     .setNegativeButton("Cancel",null).setPositiveButton("Import cookies",(d,i)->CookieImporter.apply(preview,accepted->Toast.makeText(this,"Imported "+accepted+" of "+preview.entries.size()+" cookies into WebView",Toast.LENGTH_LONG).show())).show();
    });}catch(Exception e){importError("Cookie import failed",e);}},"orion-cookie-import").start();
  }
 }
 void importError(String title,Exception e){String msg=e.getMessage()==null?"Could not complete operation":e.getMessage();runOnUiThread(()->{if(isFinishing()||isDestroyed())return;new AlertDialog.Builder(this).setTitle(title).setMessage(msg).setPositiveButton("OK",null).show();});}
 void editBookmark(String old,Runnable refresh){EditText field=new EditText(this);field.setSingleLine(true);field.setInputType(17);field.setHint("https://example.com");if(old!=null)field.setText(old);AlertDialog.Builder builder=new AlertDialog.Builder(this).setTitle(old==null?"Add bookmark":"Edit bookmark").setView(field).setNegativeButton("Cancel",null).setPositiveButton("Save",null);if(old!=null)builder.setNeutralButton("Delete",(d,i)->new AlertDialog.Builder(this).setTitle("Delete bookmark?").setMessage(old).setNegativeButton("Cancel",null).setPositiveButton("Delete",(x,y)->{Set<String> set=new HashSet<>(getPreferences(0).getStringSet("bookmarks",Collections.emptySet()));set.remove(old);getPreferences(0).edit().putStringSet("bookmarks",set).apply();refresh.run();}).show());AlertDialog dialog=builder.create();dialog.show();dialog.getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener(v->{String u=field.getText().toString().trim();if(!allowed(u)){field.setError("Enter a valid HTTP or HTTPS address");return;}Set<String> set=new HashSet<>(getPreferences(0).getStringSet("bookmarks",Collections.emptySet()));if(old!=null)set.remove(old);set.add(u);getPreferences(0).edit().putStringSet("bookmarks",set).apply();dialog.dismiss();refresh.run();});}
 void recordVisit(String uri){
  if(geckoIncognito||!allowed(uri))return;
  try{org.json.JSONArray old=new org.json.JSONArray(getPreferences(0).getString("recent_history","[]"));org.json.JSONArray fresh=new org.json.JSONArray();fresh.put(uri);
   for(int i=0;i<old.length()&&fresh.length()<150;i++){String u=old.optString(i,"");if(allowed(u)&&!uri.equals(u))fresh.put(u);}
   getPreferences(0).edit().putString("recent_history",fresh.toString()).apply();
  }catch(Exception ignored){}
 }
 void historyLibrary(){
  org.json.JSONArray entries;try{entries=new org.json.JSONArray(getPreferences(0).getString("recent_history","[]"));}catch(Exception e){entries=new org.json.JSONArray();}
  final org.json.JSONArray saved=entries;
  Dialog dialog=new Dialog(this,android.R.style.Theme_Material_NoActionBar);LinearLayout page=new LinearLayout(this);page.setOrientation(LinearLayout.VERTICAL);page.setBackgroundColor(0xff171d28);
  Button close=geckoButton("‹","Close history",()->dialog.dismiss());LinearLayout header=new LinearLayout(this);header.setGravity(Gravity.CENTER_VERTICAL);header.setBackgroundColor(0xff293344);header.addView(close,new LinearLayout.LayoutParams(dp(56),dp(56)));
  TextView title=new TextView(this);title.setText("Browsing history");title.setTextColor(0xfff1f5f9);title.setTextSize(20);header.addView(title,new LinearLayout.LayoutParams(0,dp(56),1));page.addView(header);
  ScrollView scroll=new ScrollView(this);LinearLayout list=new LinearLayout(this);list.setOrientation(LinearLayout.VERTICAL);scroll.addView(list);
  for(int i=0;i<saved.length();i++){final String u=saved.optString(i,"");if(allowed(u))settingRow(list,u,"Open page",()->{dialog.dismiss();navigate(u);});}
  page.addView(scroll,new LinearLayout.LayoutParams(-1,0,1));
  Button clear=geckoButton("Clear history","Clear history",()->new AlertDialog.Builder(this).setTitle("Clear visited-page history?").setNegativeButton("Cancel",null).setPositiveButton("Clear",(d,i)->{getPreferences(0).edit().remove("recent_history").apply();for(WebView w:tabs)w.clearHistory();dialog.dismiss();}).show());page.addView(clear,new LinearLayout.LayoutParams(-1,dp(56)));
  dialog.setContentView(page);dialog.show();dialog.getWindow().setLayout(-1,-1);safeDialogInsets(page,dialog);
 }
 void readingList(){
  Dialog dialog=new Dialog(this,android.R.style.Theme_Material_NoActionBar);LinearLayout page=new LinearLayout(this);page.setOrientation(LinearLayout.VERTICAL);page.setBackgroundColor(0xff171d28);
  LinearLayout header=new LinearLayout(this);header.setGravity(Gravity.CENTER_VERTICAL);header.setBackgroundColor(0xff293344);
  header.addView(geckoButton("‹","Close reading list",()->dialog.dismiss()),new LinearLayout.LayoutParams(dp(56),dp(56)));TextView title=new TextView(this);title.setText("Reading list");title.setTextColor(0xfff1f5f9);title.setTextSize(20);header.addView(title);page.addView(header);
  ScrollView scroll=new ScrollView(this);LinearLayout list=new LinearLayout(this);list.setOrientation(LinearLayout.VERTICAL);scroll.addView(list);page.addView(scroll,new LinearLayout.LayoutParams(-1,0,1));
  Runnable[] refresh={null};refresh[0]=()->{list.removeAllViews();TreeSet<String> items=new TreeSet<>(getPreferences(0).getStringSet("reading_list",Collections.emptySet()));Set<String> read=getPreferences(0).getStringSet("reading_read",Collections.emptySet());
   for(String u:items){LinearLayout row=new LinearLayout(this);row.setGravity(Gravity.CENTER_VERTICAL);
    Button open=geckoButton(read.contains(u)?"✓":"•","Open "+u,()->{dialog.dismiss();navigate(u);});row.addView(open,new LinearLayout.LayoutParams(dp(48),dp(56)));
    TextView link=new TextView(this);link.setText(u);link.setTextColor(0xfff1f5f9);link.setSingleLine(true);link.setEllipsize(android.text.TextUtils.TruncateAt.END);link.setTextSize(14);link.setOnClickListener(v->{dialog.dismiss();navigate(u);});row.addView(link,new LinearLayout.LayoutParams(0,dp(56),1));
    Button mark=geckoButton(read.contains(u)?"Unread":"Read", "Toggle read status for "+u,()->{Set<String> states=new HashSet<>(getPreferences(0).getStringSet("reading_read",Collections.emptySet()));if(!states.add(u))states.remove(u);getPreferences(0).edit().putStringSet("reading_read",states).apply();refresh[0].run();});row.addView(mark,new LinearLayout.LayoutParams(dp(78),dp(56)));
    Button remove=geckoButton("×","Remove "+u,()->{Set<String> listItems=new HashSet<>(getPreferences(0).getStringSet("reading_list",Collections.emptySet()));listItems.remove(u);getPreferences(0).edit().putStringSet("reading_list",listItems).apply();refresh[0].run();});row.addView(remove,new LinearLayout.LayoutParams(dp(48),dp(56)));list.addView(row);
   }
   if(items.isEmpty()){TextView empty=new TextView(this);empty.setText("No saved articles");empty.setTextColor(0xffb9c6da);empty.setPadding(dp(16),dp(24),dp(16),dp(24));list.addView(empty);}
  };refresh[0].run();
  Button add=geckoButton("Add current page","Add current page to reading list",()->{String u=geckoActive?geckoAddress:current().getUrl();if(!allowed(u)){Toast.makeText(this,"Open a website first",Toast.LENGTH_SHORT).show();return;}Set<String> items=new HashSet<>(getPreferences(0).getStringSet("reading_list",Collections.emptySet()));items.add(u);getPreferences(0).edit().putStringSet("reading_list",items).apply();refresh[0].run();});page.addView(add,new LinearLayout.LayoutParams(-1,dp(56)));
  dialog.setContentView(page);dialog.show();dialog.getWindow().setLayout(-1,-1);safeDialogInsets(page,dialog);
 }
 void safeDialogInsets(View page,Dialog dialog){page.setOnApplyWindowInsetsListener((v,insets)->{if(Build.VERSION.SDK_INT>=30){android.graphics.Insets i=insets.getInsets(WindowInsets.Type.systemBars()|WindowInsets.Type.displayCutout());v.setPadding(i.left,i.top,i.right,i.bottom);}else v.setPadding(insets.getSystemWindowInsetLeft(),insets.getSystemWindowInsetTop(),insets.getSystemWindowInsetRight(),insets.getSystemWindowInsetBottom());return insets;});dialog.getWindow().getDecorView().requestApplyInsets();}
 String searchPrefix(){String[] urls={"https://www.google.com/search?q=","https://duckduckgo.com/?q=","https://www.bing.com/search?q=","https://search.yahoo.com/search?p=","https://kagi.com/search?q=","https://search.brave.com/search?q=","https://www.startpage.com/sp/search?query=","https://www.ecosia.org/search?q=","https://www.qwant.com/?q=","https://www.mojeek.com/search?q=","https://swisscows.com/en/web?query=","https://en.wikipedia.org/w/index.php?search="};return urls[Math.max(0,Math.min(urls.length-1,getPreferences(0).getInt("engine",0)))];}
 void applySettings(WebView w){WebSettings s=w.getSettings();s.setCacheMode(getPreferences(0).getBoolean("zero_cache",false)?WebSettings.LOAD_NO_CACHE:WebSettings.LOAD_DEFAULT);if(Build.VERSION.SDK_INT>=33)s.setAlgorithmicDarkeningAllowed(getPreferences(0).getBoolean("darkpages",false));s.setJavaScriptEnabled(getPreferences(0).getBoolean("javascript",true));s.setLoadsImagesAutomatically(getPreferences(0).getBoolean("images",true));s.setTextZoom(getPreferences(0).getInt("zoom",100));s.setSupportZoom(true);s.setBuiltInZoomControls(true);s.setDisplayZoomControls(false);s.setUserAgentString(getPreferences(0).getBoolean("desktop",false)?"Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36":null);s.setUseWideViewPort(getPreferences(0).getBoolean("desktop",false));s.setLoadWithOverviewMode(getPreferences(0).getBoolean("desktop",false));}
 // Clear engine-managed HTTP resources without wiping cookies, saved credentials or site storage.
  void clearResourceCaches(boolean notify){
   for(WebView w:tabs)w.clearCache(true);
   if(geckoRuntime!=null){
    try{geckoRuntime.getStorageController().clearData(org.mozilla.geckoview.StorageController.ClearFlags.ALL_CACHES);}
    catch(Exception ignored){}
   }
   if(notify)Toast.makeText(this,"HTTP cache clearing requested",Toast.LENGTH_SHORT).show();
  }
  void applyAll(){for(WebView w:tabs)applySettings(w);}
  void applyNoCachePreference(boolean enabled){
   applyAll();
   if(enabled)clearResourceCaches(false);
  }
 void section(LinearLayout panel,String name){TextView t=new TextView(this);t.setText(name);t.setTextColor(0xff8ab4ff);t.setTextSize(13);t.setPadding(dp(16),dp(24),dp(16),dp(8));panel.addView(t);}
 void disabledSettingRow(LinearLayout panel,String title,String detail){
  LinearLayout row=new LinearLayout(this);row.setOrientation(LinearLayout.VERTICAL);
  row.setPadding(dp(16),dp(12),dp(16),dp(12));row.setMinimumHeight(dp(56));
  TextView heading=new TextView(this);heading.setText(title+" · Unavailable");heading.setTextSize(16);
  heading.setTextColor(0xff919bab);row.addView(heading);
  TextView message=new TextView(this);message.setText(detail);message.setTextSize(13);
  message.setTextColor(0xff919bab);row.addView(message);
  row.setContentDescription(title+", unavailable. "+detail);
  row.setEnabled(false);row.setClickable(false);row.setFocusable(false);
  panel.addView(row);
 }
 void settingRow(LinearLayout panel,String title,String detail,Runnable action){LinearLayout row=new LinearLayout(this);row.setOrientation(LinearLayout.VERTICAL);row.setPadding(dp(16),dp(12),dp(16),dp(12));row.setMinimumHeight(dp(56));row.setBackground(new RippleDrawable(ColorStateList.valueOf(0x338ab4ff),null,round(0xffffffff,8)));TextView a=new TextView(this);a.setText(title);a.setTextSize(16);a.setTextColor(0xfff1f5f9);row.addView(a);TextView b=new TextView(this);b.setText(detail);b.setTextSize(13);b.setTextColor(0xffb9c6da);row.addView(b);row.setFocusable(true);row.setOnClickListener(v->action.run());panel.addView(row);}
 void toggle(LinearLayout panel,String title,String key,boolean def){Switch sw=new Switch(this);sw.setText(title);sw.setTextColor(0xfff1f5f9);sw.setTextSize(16);sw.setPadding(dp(16),dp(8),dp(16),dp(8));sw.setMinHeight(dp(56));sw.setChecked(getPreferences(0).getBoolean(key,def));sw.setOnCheckedChangeListener((v,on)->{getPreferences(0).edit().putBoolean(key,on).apply();if(key.equals("zero_cache"))applyNoCachePreference(on);else applyAll();if(key.equals("easylist")||key.equals("easyprivacy"))AdBlocker.load(this,getPreferences(0));});panel.addView(sw);}
 void settings(){Dialog dialog=new Dialog(this,android.R.style.Theme_Material_NoActionBar);LinearLayout page=new LinearLayout(this);page.setOrientation(LinearLayout.VERTICAL);page.setBackgroundColor(0xff171d28);Button close=new Button(this);close.setText("‹  Settings");close.setAllCaps(false);close.setTextColor(0xfff1f5f9);close.setBackgroundColor(0xff293344);close.setGravity(Gravity.START|Gravity.CENTER_VERTICAL);close.setPadding(dp(16),0,0,0);close.setMinHeight(dp(56));close.setOnClickListener(v->dialog.dismiss());page.addView(close);ScrollView scroll=new ScrollView(this);LinearLayout panel=new LinearLayout(this);panel.setOrientation(LinearLayout.VERTICAL);panel.setPadding(0,0,0,dp(24));scroll.addView(panel);page.addView(scroll,new LinearLayout.LayoutParams(-1,0,1));String[] engines={"Google","DuckDuckGo","Bing","Yahoo","Kagi","Brave Search","Startpage","Ecosia","Qwant","Mojeek","Swisscows","Wikipedia"};
 section(panel,"ACCOUNT");disabledSettingRow(panel,"Sync","Google sign-in and cross-device syncing are not connected");
 section(panel,"GENERAL");settingRow(panel,"Search engine",engines[Math.max(0,Math.min(engines.length-1,getPreferences(0).getInt("engine",0)))],()->new AlertDialog.Builder(this).setTitle("Search engine").setSingleChoiceItems(engines,getPreferences(0).getInt("engine",0),(d,i)->{getPreferences(0).edit().putInt("engine",i).apply();d.dismiss();dialog.dismiss();settings();}).setNegativeButton("Cancel",null).show());
 settingRow(panel,"Default browser",isDefaultBrowser()?"Orion is the default":"Choose Orion in Android's browser selector",()->requestDefaultBrowser());
 section(panel,"AI CONTROL");settingRow(panel,"Orion AI Connect",aiBridge!=null&&aiBridge.running()?"Active local session · disconnect or inspect permissions":"Off · enable local MCP and approve each action",()->{dialog.dismiss();aiConnectSettings();});
 section(panel,"DISPLAY");settingRow(panel,"Homepage weather","ECMWF IFS forecast · location, visibility and saved area",()->HomeWeather.settings(this));settingRow(panel,"Toolbar shortcut","Choose Home, Bookmark or Reload",()->new AlertDialog.Builder(this).setTitle("Toolbar shortcut").setSingleChoiceItems(new String[]{"Home","Bookmark","Reload"},getPreferences(0).getInt("shortcut",0),(d,i)->{getPreferences(0).edit().putInt("shortcut",i).apply();updateToolbar();d.dismiss();}).setNegativeButton("Cancel",null).show());toggle(panel,"Swipe across address bar to switch tabs","tab_swipe",true);toggle(panel,"Dark web pages (WebView · experimental)","darkpages",false);settingRow(panel,"Text size",getPreferences(0).getInt("zoom",100)+"%",()->{String[] labels={"75%","100%","125%","150%","200%"};int[] sizes={75,100,125,150,200};new AlertDialog.Builder(this).setTitle("Text size").setItems(labels,(d,i)->{getPreferences(0).edit().putInt("zoom",sizes[i]).apply();applyAll();dialog.dismiss();settings();}).show();});toggle(panel,"Desktop websites (WebView)","desktop",false);
 settingRow(panel,"Security and privacy","Use the Orion icon at the right of the bottom toolbar",()->{dialog.dismiss();securityPrivacy();});
 section(panel,"BROWSER DATA");settingRow(panel,"Bookmarks","Saved websites",()->bookmarks());settingRow(panel,"Reading list","Saved pages and read status",()->readingList());settingRow(panel,"Browsing history","Recent visited pages",()->historyLibrary());
  if(geckoIncognito)disabledSettingRow(panel,"Import browser data","Leave Incognito before importing into the normal browser");else settingRow(panel,"Import browser data","Bookmarks, password options and cookie limitations",()->importBrowserData());
 section(panel,"PLANNED FEATURES");disabledSettingRow(panel,"Password autofill","Imported passwords are stored, but are not used to fill websites");disabledSettingRow(panel,"Extensions","Installing and managing GeckoView extensions is not implemented");disabledSettingRow(panel,"Orion Connect","Cross-device push links and encrypted sync are not connected");section(panel,"ABOUT");settingRow(panel,"Orion Private Browser",installedVersion(),()->new AlertDialog.Builder(this).setTitle("About Orion").setMessage("Experimental private browser. Google sign-in, Orion Connect, extensions and password autofill are not implemented. Incognito uses a separate GeckoView private session, but device verification remains outstanding.").setPositiveButton("OK",null).show());
 dialog.setContentView(page);dialog.show();dialog.getWindow().setLayout(-1,-1);dialog.getWindow().getDecorView().setOnApplyWindowInsetsListener((v,insets)->{if(Build.VERSION.SDK_INT>=30){android.graphics.Insets i=insets.getInsets(WindowInsets.Type.systemBars()|WindowInsets.Type.displayCutout());page.setPadding(i.left,i.top,i.right,i.bottom);}return insets;});dialog.getWindow().getDecorView().requestApplyInsets();}
 void startDnsVpn(){
  if(android.net.VpnService.prepare(this)!=null){Toast.makeText(this,"VPN permission required",Toast.LENGTH_LONG).show();return;}
  Intent start=new Intent(this,OrionDnsVpnService.class).setAction(OrionDnsVpnService.ACTION_START);
  try{startForegroundService(start);Toast.makeText(this,"Starting Orion Secure DNS",Toast.LENGTH_LONG).show();}
  catch(Exception e){Toast.makeText(this,"Unable to start Secure DNS: "+e.getClass().getSimpleName(),Toast.LENGTH_LONG).show();}
 }
 void dnsSettings(){DnsSettings.show(this);}
 void openDnsSettings(){DnsSettings.openPrivateDns(this);}
 void syncSettings(){new AlertDialog.Builder(this).setTitle("Sync").setMessage("Use your Google account to synchronise bookmarks, settings and shared links across devices.\n\nGoogle Sign-In is not configured in this test build. No account data is collected or uploaded.").setPositiveButton("Sign in with Google",(d,i)->new AlertDialog.Builder(this).setTitle("Google Sign-In unavailable").setMessage("The Orion OAuth client must be registered before sign-in can be enabled.").setPositiveButton("OK",null).show()).setNegativeButton("Close",null).show();}
 boolean youtubeAd(Uri u){String h=u.getHost(),p=u.getPath();return h!=null&&p!=null&&(h.equalsIgnoreCase("www.youtube.com")||h.equalsIgnoreCase("m.youtube.com")||h.equalsIgnoreCase("youtube.com"))&&(p.equals("/pagead/adview")||p.equals("/pagead/interaction")||p.equals("/api/stats/ads"));}
 void installFilterList(String key){
  if(!"easylist".equals(key)&&!"easyprivacy".equals(key))return;
  final String title="easylist".equals(key)?"EasyList":"EasyPrivacy";
  Toast.makeText(this,"Installing "+title+" domain rules…",Toast.LENGTH_SHORT).show();
  new Thread(()->{
   boolean installed=false;
   try{
    AdBlocker.update(this,key);
    getPreferences(0).edit().putBoolean(key,true).apply();
    AdBlocker.load(this,getPreferences(0));
    installed=true;
   }catch(Exception ignored){}
   final boolean ok=installed;
   runOnUiThread(()->{
    if(isFinishing()||isDestroyed())return;
    Toast.makeText(this,ok?title+" enabled. Reload open webpages to apply.":title+" download failed; filtering unchanged.",Toast.LENGTH_LONG).show();
   });
  },"orion-install-"+key).start();
 }
 void updateFilters(){Toast.makeText(this,"Updating filter lists…",Toast.LENGTH_SHORT).show();new Thread(()->{String result;try{AdBlocker.update(this,"easylist");AdBlocker.update(this,"easyprivacy");AdBlocker.load(this,getPreferences(0));result="Filter lists updated. Reopen privacy settings to enable them; reload pages to apply.";}catch(Exception e){result="Filter update incomplete. Previous lists retained where possible.";}final String msg=result;runOnUiThread(()->{if(!isDestroyed())Toast.makeText(this,msg,Toast.LENGTH_LONG).show();});}).start();}
 void sitePermissions(){Dialog dialog=new Dialog(this,android.R.style.Theme_Material_NoActionBar);LinearLayout page=new LinearLayout(this);page.setOrientation(LinearLayout.VERTICAL);page.setBackgroundColor(0xff171d28);Button close=geckoButton("‹","Back to browser",()->dialog.dismiss());LinearLayout heading=new LinearLayout(this);heading.setGravity(Gravity.CENTER_VERTICAL);heading.setBackgroundColor(0xff293344);heading.addView(close,new LinearLayout.LayoutParams(dp(56),dp(56)));TextView name=new TextView(this);name.setText("Site permissions");name.setTextSize(20);name.setTextColor(0xfff1f5f9);heading.addView(name);page.addView(heading);ScrollView scroll=new ScrollView(this);LinearLayout options=new LinearLayout(this);options.setOrientation(LinearLayout.VERTICAL);options.setPadding(dp(8),dp(16),dp(8),dp(24));scroll.addView(options);page.addView(scroll,new LinearLayout.LayoutParams(-1,0,1));TextView explain=new TextView(this);explain.setText("Camera, microphone and location settings apply to both engines on secure HTTPS sites. Android system permission is also required. GeckoView notification requests are blocked in this test build.");explain.setTextSize(14);explain.setTextColor(0xffb9c6da);explain.setPadding(dp(12),0,dp(12),dp(16));options.addView(explain);permissionChoice(options,"Camera","site_camera");permissionChoice(options,"Microphone","site_microphone");permissionChoice(options,"Approximate location","site_location");settingRow(options,"Android app permissions","Manage camera, microphone and location access in Android settings",()->startActivity(new Intent(android.provider.Settings.ACTION_APPLICATION_DETAILS_SETTINGS,Uri.parse("package:"+getPackageName()))));dialog.setContentView(page);dialog.show();Window win=dialog.getWindow();win.setLayout(-1,-1);win.setStatusBarColor(0xff171d28);win.setNavigationBarColor(0xff171d28);win.getDecorView().setSystemUiVisibility(0);page.setOnApplyWindowInsetsListener((v,insets)->{if(Build.VERSION.SDK_INT>=30){android.graphics.Insets b=insets.getInsets(WindowInsets.Type.systemBars()|WindowInsets.Type.displayCutout());page.setPadding(b.left,b.top,b.right,b.bottom);}else page.setPadding(insets.getSystemWindowInsetLeft(),insets.getSystemWindowInsetTop(),insets.getSystemWindowInsetRight(),insets.getSystemWindowInsetBottom());return insets;});page.requestApplyInsets();}
 void permissionChoice(LinearLayout parent,String title,String key){String[] values={"Ask every time","Block","Allow on request"};int choice=Math.max(0,Math.min(2,getPreferences(0).getInt(key,0)));settingRow(parent,title,values[choice],()->new AlertDialog.Builder(this).setTitle(title).setSingleChoiceItems(values,getPreferences(0).getInt(key,0),(d,i)->{getPreferences(0).edit().putInt(key,i).apply();d.dismiss();Toast.makeText(this,title+": "+values[i],Toast.LENGTH_SHORT).show();}).setNegativeButton("Cancel",null).show());}
 void privacy(){new AlertDialog.Builder(this).setTitle("Privacy protections").setMessage("HTTPS is preferred, but HTTP remains available after a warning. HTTP is unencrypted. WebView blocks third-party cookies; GeckoView uses its own cookie controls. Camera, microphone and location choices apply to secure HTTPS sites in both engines and require Android system permissions. GeckoView notification requests are blocked in this test build. Incognito uses GeckoView private mode with separate temporary site data; normal WebView tabs are unaffected. It does not conceal traffic from websites or networks, and downloads can remain on the device.").setNeutralButton("Site permissions",(d,i)->sitePermissions()).setPositiveButton("OK",null).show();}
 void clearBrowsingData(){new AlertDialog.Builder(this).setTitle("Clear browsing data?").setMessage("Clear normal WebView cookies, site storage, cache and tab history, plus Orion recent URL history. GeckoView data may require separate clearing. This may sign you out. Bookmarks are retained.").setNegativeButton("Cancel",null).setPositiveButton("Clear",(d,i)->{WebStorage.getInstance().deleteAllData();getPreferences(0).edit().remove("recent_history").apply();for(WebView w:tabs){w.clearCache(true);w.clearHistory();w.clearFormData();}CookieManager cookies=CookieManager.getInstance();cookies.removeAllCookies(removed->{cookies.flush();if(!isFinishing()&&!isDestroyed())Toast.makeText(this,"Normal WebView cookies, site data and Orion recent history cleared",Toast.LENGTH_LONG).show();});}).show();}
 @Override public void onBackPressed(){if(address.hasFocus()){address.clearFocus();return;}if(geckoActive){if(geckoCanGoBack)geckoGoBack();else closeGecko();return;}if(selected>=0&&current().canGoBack()){current().goBack();return;}super.onBackPressed();}
 @Override protected void onDestroy(){if(aiBridge!=null)aiBridge.stop();homeHandler.removeCallbacks(openHomeAfterTap);if(geckoActive)closeGecko();hideHome();content.removeAllViews();homeTabs.clear();if(getPreferences(0).getBoolean("zero_cache",false))clearResourceCaches(false);for(WebView w:tabs)w.destroy();super.onDestroy();}
}
