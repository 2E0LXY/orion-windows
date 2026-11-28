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
 LinearLayout root, bar, navigation, tabletTabs; FrameLayout content, homeOverlay; EditText address; Button count; ProgressBar progress; Button back,forward; int selected=-1;
 final Set<WebView> homeTabs=Collections.newSetFromMap(new IdentityHashMap<WebView,Boolean>());
 @Override public void onCreate(Bundle b){super.onCreate(b);AdBlocker.load(this,getPreferences(0));
 getWindow().setStatusBarColor(0xff171d28);getWindow().setNavigationBarColor(0xff171d28);
 root=new LinearLayout(this);root.setOrientation(1);root.setBackgroundColor(0xff171d28);
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
 back=button("back","Back",()->{if(geckoActive)geckoGoBack();else if(current().canGoBack())current().goBack();},true);
 forward=button("forward","Forward",()->{if(geckoActive)geckoGoForward();else if(current().canGoForward())current().goForward();},true);
 Button homeButton=button("home","Home. Hold to save current page; triple tap to reset",()->homeTap(),true);homeButton.setOnLongClickListener(v->{saveCurrentHomepage();return true;});count=button("tabs","Switch tabs",()->chooseTab(),true);button("menu","Browser menu",()->menu(),true);updateToolbar();tabletTabs=new LinearLayout(this);root.addView(tabletTabs,0,new LinearLayout.LayoutParams(-1,dp(48)));tabletTabs.setVisibility(View.GONE);
 address.setOnTouchListener(new View.OnTouchListener(){float x,y;public boolean onTouch(View v,android.view.MotionEvent e){if(!getPreferences(0).getBoolean("tab_swipe",true))return false;if(e.getActionMasked()==MotionEvent.ACTION_DOWN){x=e.getX();y=e.getY();}if(e.getActionMasked()==MotionEvent.ACTION_UP&&Math.abs(e.getX()-x)>dp(80)&&Math.abs(e.getY()-y)<dp(40)&&tabs.size()>1){show((selected+(e.getX()>x?-1:1)+tabs.size())%tabs.size());return true;}return false;}});setContentView(root);root.requestApplyInsets();newTab();showStartup();
 }
 String installedVersion(){try{android.content.pm.PackageInfo p=getPackageManager().getPackageInfo(getPackageName(),0);return "Version "+p.versionName+" · Build "+(Build.VERSION.SDK_INT>=28?p.getLongVersionCode():p.versionCode);}catch(Exception e){return "Version unavailable";}}
 void showStartup(){
  if(root==null||isFinishing()||isDestroyed())return;
  if(root.getParent() instanceof ViewGroup)((ViewGroup)root.getParent()).removeView(root);
  FrameLayout host=new FrameLayout(this);
  host.addView(root,new FrameLayout.LayoutParams(-1,-1));
  StartupView splash=new StartupView();
  host.addView(splash,new FrameLayout.LayoutParams(-1,-1));
  setContentView(host);
  splash.setClickable(true);
  splash.setContentDescription("Orion Community Browser, "+installedVersion());
  Runnable finish=()->{
   if(!isFinishing()&&!isDestroyed()&&splash.getParent()==host){
    host.removeView(splash);
    root.requestApplyInsets();
   }
  };
  final boolean motion=android.animation.ValueAnimator.areAnimatorsEnabled();
  // Start the clock only after the splash has actually drawn its first frame.
  splash.firstFrame=()->{
   final long started=android.os.SystemClock.uptimeMillis();
   final long duration=motion?5000L:1800L;
   Runnable frame=new Runnable(){
    @Override public void run(){
     if(isFinishing()||isDestroyed()||!splash.isAttachedToWindow()||splash.getParent()!=host)return;
     long elapsed=android.os.SystemClock.uptimeMillis()-started;
     splash.phase=motion?Math.min(1f,elapsed/4500f):1f;
     if(motion)splash.setAlpha(elapsed>4500L?Math.max(0f,(5000L-elapsed)/500f):1f);
     splash.invalidate();
     if(elapsed>=duration)finish.run();else splash.postOnAnimation(this);
    }
   };
   splash.postOnAnimation(frame);
  };

 }
 class StartupView extends View {
  float phase=0f;
  Runnable firstFrame;
  final Paint p=new Paint(Paint.ANTI_ALIAS_FLAG);
  final String version=installedVersion();
  StartupView(){super(MainActivity.this);}
  @Override protected void onDraw(Canvas c){
   super.onDraw(c);
   if(firstFrame!=null){Runnable start=firstFrame;firstFrame=null;post(start);}
   c.drawColor(0xff171d28);
   float x=getWidth()/2f,y=getHeight()*.41f,r=dp(43),angle=(float)(-Math.PI/2+phase*Math.PI*6);
   p.setShader(null);p.setAlpha(255);p.setStyle(Paint.Style.FILL);
   p.setColor(0xff26354e);c.drawCircle(x,y,r,p);
   p.setColor(0xff8ab4ff);c.drawCircle(x,y,r*.75f,p);
   p.setColor(0xffe4efff);p.setTextAlign(Paint.Align.CENTER);p.setTypeface(Typeface.create("sans-serif-medium",Typeface.NORMAL));p.setTextSize(dp(35));c.drawText("O",x,y-(p.ascent()+p.descent())/2f,p);
   p.setStyle(Paint.Style.STROKE);p.setStrokeCap(Paint.Cap.ROUND);
   p.setStrokeWidth(dp(4));p.setColor(0xffdcebff);
   c.drawArc(x-r,y-r,x+r,y+r,(float)Math.toDegrees(angle),74,false,p);
   float sx=x+(float)Math.cos(angle)*r,sy=y+(float)Math.sin(angle)*r;
   p.setStyle(Paint.Style.FILL);p.setColor(0xfff5faff);c.drawCircle(sx,sy,dp(4),p);
   float fade=Math.min(1f,Math.max(0f,phase*4f));
   p.setTextAlign(Paint.Align.CENTER);p.setTypeface(Typeface.create("sans-serif-medium",Typeface.NORMAL));
   p.setTextSize(dp(32));p.setColor(0xfff1f5f9);p.setAlpha((int)(255*fade));
   c.drawText("Orion",x,y+dp(109),p);
   p.setTypeface(Typeface.create("sans-serif",Typeface.NORMAL));p.setTextSize(dp(12));
   p.setColor(0xffb9c6da);p.setAlpha((int)(255*fade));
   c.drawText("COMMUNITY BROWSER",x,y+dp(140),p);
   p.setTextSize(dp(12));c.drawText(version,x,getHeight()-dp(76),p);
   p.setAlpha(255);
  }
 }
 int dp(float n){return Math.round(n*getResources().getDisplayMetrics().density);}
 GradientDrawable round(int color,int radius){GradientDrawable d=new GradientDrawable();d.setColor(color);d.setCornerRadius(dp(radius));return d;}
 void submit(){navigate(address.getText().toString());address.clearFocus();((InputMethodManager)getSystemService(INPUT_METHOD_SERVICE)).hideSoftInputFromWindow(address.getWindowToken(),0);}
 Button button(String glyph,String label,Runnable r,boolean weight){Button b=new Button(this);b.setText("");b.setContentDescription(label);b.setTooltipText(label);b.setMinWidth(0);b.setMinimumWidth(0);b.setPadding(0,0,0,0);b.setBackground(new RippleDrawable(ColorStateList.valueOf(0x338ab4ff),null,round(0xffffffff,24)));b.setCompoundDrawables(null,new Glyph(glyph),null,null);b.setGravity(Gravity.CENTER);b.setPadding(0,dp(12),0,0);bar.addView(b,new LinearLayout.LayoutParams(weight?0:dp(48),dp(48),weight?1:0));b.setOnClickListener(v->r.run());return b;}
 class Glyph extends Drawable {final String kind;final Paint p=new Paint(3);Glyph(String k){kind=k;setBounds(0,0,dp(24),dp(24));}public void draw(Canvas c){c.save();c.translate(getBounds().left,getBounds().top);c.scale(getBounds().width()/24f,getBounds().height()/24f);p.setColor(0xffdce5f4);p.setStrokeWidth(1.8f);p.setStrokeCap(Paint.Cap.ROUND);p.setStrokeJoin(Paint.Join.ROUND);p.setStyle(Paint.Style.STROKE);Path a=new Path();switch(kind){case "back":a.moveTo(14,5);a.lineTo(7,12);a.lineTo(14,19);a.moveTo(7,12);a.lineTo(21,12);break;case "forward":case "go":a.moveTo(10,5);a.lineTo(17,12);a.lineTo(10,19);a.moveTo(3,12);a.lineTo(17,12);break;case "bookmark":a.moveTo(6,3);a.lineTo(18,3);a.lineTo(18,21);a.lineTo(12,16);a.lineTo(6,21);a.close();break;case "reload":c.drawArc(4,4,20,20,-45,300,false,p);a.moveTo(20,4);a.lineTo(20,10);a.lineTo(14,10);break;case "home":a.moveTo(3,11);a.lineTo(12,3);a.lineTo(21,11);a.lineTo(21,21);a.lineTo(3,21);a.close();break;case "tabs":c.drawRoundRect(3,3,21,21,4,4,p);p.setStyle(Paint.Style.FILL);p.setTextSize(11);p.setTextAlign(Paint.Align.CENTER);c.drawText(""+tabs.size(),12,16,p);break;default:p.setStyle(Paint.Style.FILL);for(int i=5;i<=19;i+=7)c.drawCircle(12,i,1.6f,p);}c.drawPath(a,p);c.restore();}public void setAlpha(int a){p.setAlpha(a);}public void setColorFilter(ColorFilter f){p.setColorFilter(f);}public int getOpacity(){return PixelFormat.TRANSLUCENT;}}
 void updateToolbar(){if(navigation==null||navigation.getChildCount()<3)return;Button shortcut=(Button)navigation.getChildAt(2);int mode=getPreferences(0).getInt("shortcut",0);String glyph=mode==1?"bookmark":mode==2?"reload":"home";String label=mode==1?"Save bookmark":mode==2?"Reload page":"Home";shortcut.setCompoundDrawables(null,new Glyph(glyph),null,null);shortcut.setContentDescription(label);shortcut.setTooltipText(label);shortcut.setOnClickListener(v->{if(mode==1)save();else if(mode==2)reloadActive();else homeActive();});}
 void refreshTabletTabs(){if(tabletTabs==null)return;tabletTabs.removeAllViews();boolean wide=getResources().getConfiguration().screenWidthDp>=600;tabletTabs.setVisibility(wide?View.VISIBLE:View.GONE);if(!wide)return;HorizontalScrollView scroll=new HorizontalScrollView(this);LinearLayout strip=new LinearLayout(this);for(int i=0;i<tabs.size();i++){final int index=i;Button b=new Button(this);b.setAllCaps(false);b.setText(tabs.get(i).getTitle()==null?"Tab "+(i+1):tabs.get(i).getTitle());b.setMaxWidth(dp(180));b.setSingleLine(true);b.setTextColor(i==selected?0xff8ab4ff:0xfff1f5f9);b.setOnClickListener(v->show(index));strip.addView(b);}Button add=new Button(this);add.setText("+");add.setContentDescription("New tab");add.setOnClickListener(v->newTab());strip.addView(add);scroll.addView(strip);tabletTabs.addView(scroll,new LinearLayout.LayoutParams(-1,-1));}
 static org.mozilla.geckoview.GeckoRuntime geckoRuntime;
 org.mozilla.geckoview.GeckoSession geckoSession;
 org.mozilla.geckoview.GeckoView geckoView;
 boolean geckoActive=false, geckoCanGoBack=false, geckoCanGoForward=false;
 String geckoAddress="";
 org.mozilla.geckoview.GeckoSession.PermissionDelegate.Callback geckoAndroidCallback;
 boolean geckoMediaPending=false;
 boolean geckoSecureOrigin(String uri){
  if(!geckoActive||geckoSession==null||!allowed(uri)||!allowed(geckoAddress))return false;
  Uri request=Uri.parse(uri),currentUrl=Uri.parse(geckoAddress);
  return "https".equalsIgnoreCase(request.getScheme())&&"https".equalsIgnoreCase(currentUrl.getScheme())&&Objects.equals(request.getHost(),currentUrl.getHost())&&(request.getPort()<0?443:request.getPort())==(currentUrl.getPort()<0?443:currentUrl.getPort());
 }

 void openGecko(){
  if(geckoActive)return;
  boolean openAtHome=homeTabs.contains(current())||homeOverlay!=null;
  String initial=openAtHome?null:current().getUrl();
  if(!allowed(initial))initial=null;
  try{
   if(geckoRuntime==null)geckoRuntime=org.mozilla.geckoview.GeckoRuntime.create(getApplicationContext());
   DnsSettings.applyToGecko(geckoRuntime,getPreferences(0));
   geckoView=new org.mozilla.geckoview.GeckoView(this);
   geckoSession=new org.mozilla.geckoview.GeckoSession();
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
     if(geckoActive&&geckoSession==sess){geckoAddress=url;recordVisit(url);if(!address.hasFocus())address.setText(allowed(url)?url:"");}
    }
    @Override public void onCanGoBack(org.mozilla.geckoview.GeckoSession sess,boolean canGoBack){
     if(geckoActive&&geckoSession==sess){geckoCanGoBack=canGoBack;back.setEnabled(canGoBack);}
    }
    @Override public void onCanGoForward(org.mozilla.geckoview.GeckoSession sess,boolean canGoForward){
     if(geckoActive&&geckoSession==sess){geckoCanGoForward=canGoForward;forward.setEnabled(canGoForward);}
    }
   });
   session.setProgressDelegate(new org.mozilla.geckoview.GeckoSession.ProgressDelegate(){
    @Override public void onPageStart(org.mozilla.geckoview.GeckoSession sess,String url){
     if(geckoActive&&geckoSession==sess){geckoAddress=url;progress.setProgress(0);progress.setVisibility(View.VISIBLE);if(!address.hasFocus())address.setText(url);}
    }
    @Override public void onPageStop(org.mozilla.geckoview.GeckoSession sess,boolean success){
     if(geckoActive&&geckoSession==sess){progress.setProgress(100);progress.setVisibility(View.INVISIBLE);}
    }
   });
   session.open(geckoRuntime);
   geckoView.setSession(session);
   current().onPause();
   hideHome();content.removeAllViews();
   geckoActive=true;geckoCanGoBack=false;geckoCanGoForward=false;
   content.addView(geckoView,new FrameLayout.LayoutParams(-1,-1));
   address.setText(geckoAddress);
   back.setEnabled(false);forward.setEnabled(false);
   count.setContentDescription("Switch WebView tabs; GeckoView active");
   updateToolbar();
   if(initial==null)showHome(true);else session.loadUri(initial);
  }catch(Exception e){
   if(geckoSession!=null){try{geckoSession.close();}catch(Exception ignored){}}
   geckoSession=null;geckoView=null;geckoActive=false;
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
  geckoView=null;geckoSession=null;geckoAddress="";geckoCanGoBack=false;geckoCanGoForward=false;
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
  homeHandler.removeCallbacks(openHomeAfterTap);
  if(++homeTapCount>=3){
   homeTapCount=0;getPreferences(0).edit().remove(PREF_HOMEPAGE).apply();
   originalHome();Toast.makeText(this,"Orion homepage restored",Toast.LENGTH_SHORT).show();
  }else homeHandler.postDelayed(openHomeAfterTap,350);
 }
 void saveCurrentHomepage(){
  homeHandler.removeCallbacks(openHomeAfterTap);homeTapCount=0;
  String url=homeOverlay!=null?"":(geckoActive?geckoAddress:(selected>=0?current().getUrl():""));
  if(!allowed(url)){Toast.makeText(this,"Open a website before saving a homepage",Toast.LENGTH_SHORT).show();return;}
  getPreferences(0).edit().putString(PREF_HOMEPAGE,url).apply();
  Toast.makeText(this,"Homepage saved",Toast.LENGTH_SHORT).show();
 }
 void homeActive(){String url=getPreferences(0).getString(PREF_HOMEPAGE,"");if(allowed(url))navigate(url);else originalHome();}
 void originalHome(){if(geckoActive&&geckoSession!=null){geckoAddress="";showHome(true);}else home(current());}
 void hideHome(){
  if(homeOverlay!=null){
   if(homeOverlay.getParent() instanceof ViewGroup)((ViewGroup)homeOverlay.getParent()).removeView(homeOverlay);
   homeOverlay=null;
  }
 }
 void showHome(boolean gecko){
  hideHome();
  FrameLayout page=new FrameLayout(this);page.setBackgroundColor(0xff171d28);
  page.setContentDescription(gecko?"Orion Community Browser. Powered by GeckoView. Search or enter an address below.":"Orion Community Browser. Search or enter an address below.");
  ScrollView scroll=new ScrollView(this);scroll.setFillViewport(true);scroll.setVerticalScrollBarEnabled(false);
  LinearLayout column=new LinearLayout(this);column.setOrientation(LinearLayout.VERTICAL);column.setGravity(Gravity.CENTER_HORIZONTAL);
  int top=getResources().getConfiguration().orientation==android.content.res.Configuration.ORIENTATION_LANDSCAPE?dp(24):dp(88);
  column.setPadding(dp(16),top,dp(16),dp(24));
  FrameLayout emblem=new FrameLayout(this);emblem.setBackground(round(0xff26354e,50));
  LinearLayout.LayoutParams emblemParams=new LinearLayout.LayoutParams(dp(88),dp(88));column.addView(emblem,emblemParams);
  View orb=new View(this);orb.setBackground(round(0xff8ab4ff,50));
  FrameLayout.LayoutParams orbParams=new FrameLayout.LayoutParams(dp(64),dp(64),Gravity.CENTER);emblem.addView(orb,orbParams);
  TextView title=new TextView(this);title.setText("Orion");title.setTextSize(32);title.setTextColor(0xfff1f5f9);
  title.setTypeface(Typeface.create("sans-serif-medium",Typeface.NORMAL));title.setGravity(Gravity.CENTER);
  LinearLayout.LayoutParams titleParams=new LinearLayout.LayoutParams(-1,-2);titleParams.topMargin=dp(16);column.addView(title,titleParams);
  TextView subtitle=new TextView(this);subtitle.setText("COMMUNITY BROWSER");subtitle.setTextSize(12);subtitle.setLetterSpacing(.24f);
  subtitle.setTextColor(0xffb9c6da);subtitle.setGravity(Gravity.CENTER);
  LinearLayout.LayoutParams subParams=new LinearLayout.LayoutParams(-1,-2);subParams.topMargin=dp(8);column.addView(subtitle,subParams);
  TextView instruction=new TextView(this);instruction.setText("Search or enter an address below.");
  instruction.setTextSize(16);instruction.setTextColor(0xffb9c6da);instruction.setGravity(Gravity.CENTER);
  LinearLayout.LayoutParams instructionParams=new LinearLayout.LayoutParams(-1,-2);instructionParams.topMargin=dp(56);column.addView(instruction,instructionParams);
  if(gecko){
   LinearLayout attribution=new LinearLayout(this);attribution.setGravity(Gravity.CENTER);attribution.setOrientation(LinearLayout.HORIZONTAL);
   GeckoMark mark=new GeckoMark();attribution.addView(mark,new LinearLayout.LayoutParams(dp(32),dp(32)));
   TextView powered=new TextView(this);powered.setText("Powered by GeckoView");powered.setTextSize(13);
   powered.setTextColor(0xffa8bce0);powered.setPadding(dp(8),0,0,0);
   attribution.addView(powered);
   LinearLayout.LayoutParams attrParams=new LinearLayout.LayoutParams(-1,dp(40));attrParams.topMargin=dp(24);column.addView(attribution,attrParams);
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
 void menu(){
 Dialog dialog=new Dialog(this);LinearLayout panel=new LinearLayout(this);panel.setOrientation(1);panel.setPadding(dp(16),dp(12),dp(16),dp(16));panel.setBackground(round(0xff293344,20));
 TextView title=new TextView(this);title.setText(geckoActive?"Orion · GeckoView":"Orion · WebView");title.setTextSize(22);title.setTextColor(0xfff1f5f9);title.setPadding(dp(8),dp(8),0,dp(12));panel.addView(title);
 ScrollView scroll=new ScrollView(this);LinearLayout actions=new LinearLayout(this);actions.setOrientation(1);scroll.addView(actions);panel.addView(scroll,new LinearLayout.LayoutParams(-1,0,1));
 String[] labels=geckoActive?new String[]{"New WebView tab","Return to WebView","Reload page","Save bookmark","Bookmarks","Reading list","History","Share link","Downloads","Import browser data","Privacy and permissions","DNS and network","Settings","Replay boot animation"}:new String[]{"New tab","Open in GeckoView","Reload page","Find in page","Save bookmark","Bookmarks","Reading list","History","Import browser data","Share link","Reader mode","Print / Save PDF","Downloads","Privacy and permissions","DNS and network","Settings","Replay boot animation"};
 Runnable[] tasks=geckoActive?new Runnable[]{()->{closeGecko();newTab();},()->closeGecko(),()->reloadActive(),()->save(),()->bookmarks(),()->readingList(),()->historyLibrary(),()->sharePage(),()->startActivity(new Intent(android.app.DownloadManager.ACTION_VIEW_DOWNLOADS)),()->importBrowserData(),()->sitePermissions(),()->dnsSettings(),()->settings(),()->showStartup()}:new Runnable[]{()->newTab(),()->openGecko(),()->reloadActive(),()->findPage(),()->save(),()->bookmarks(),()->readingList(),()->historyLibrary(),()->importBrowserData(),()->sharePage(),()->readerMode(),()->printPage(),()->startActivity(new Intent(android.app.DownloadManager.ACTION_VIEW_DOWNLOADS)),()->sitePermissions(),()->dnsSettings(),()->settings(),()->showStartup()};
 for(int i=0;i<labels.length;i++){final Runnable task=tasks[i];Button button=new Button(this);button.setAllCaps(false);button.setText(labels[i]);button.setTextColor(0xfff1f5f9);button.setTextSize(16);button.setGravity(Gravity.START|Gravity.CENTER_VERTICAL);button.setPadding(dp(16),0,dp(16),0);button.setBackground(new RippleDrawable(ColorStateList.valueOf(0x338ab4ff),null,round(0xffffffff,12)));actions.addView(button,new LinearLayout.LayoutParams(-1,dp(48)));button.setOnClickListener(v->{dialog.dismiss();task.run();});}
 dialog.setContentView(panel);dialog.show();Window win=dialog.getWindow();win.setBackgroundDrawableResource(android.R.color.transparent);win.setGravity(Gravity.BOTTOM);win.setLayout(-1,Math.min(dp(560),(int)(getResources().getDisplayMetrics().heightPixels*.80f)));
 }
 void downloadPage(String url,String disposition,String mime){if(!allowed(url)){Toast.makeText(this,"Unsupported download address",0).show();return;}String name=URLUtil.guessFileName(url,disposition,mime).replaceAll("[^a-zA-Z0-9._ -]","_");if(name.isEmpty()||name.equals(".")||name.equals(".."))name="download";final String filename=name;new AlertDialog.Builder(this).setTitle("Download file?").setMessage(filename+"\n"+Uri.parse(url).getHost()).setNegativeButton("Cancel",null).setPositiveButton("Parallel (4)",(d,i)->ParallelDownload.start(this,url,filename,()->standardDownload(url,filename))).setNeutralButton("Standard",(d,i)->standardDownload(url,filename)).show();}
 void standardDownload(String url,String filename){try{android.app.DownloadManager.Request req=new android.app.DownloadManager.Request(Uri.parse(url));req.setTitle(filename);req.setNotificationVisibility(android.app.DownloadManager.Request.VISIBILITY_VISIBLE_NOTIFY_COMPLETED);req.setDestinationInExternalPublicDir(Environment.DIRECTORY_DOWNLOADS,filename);((android.app.DownloadManager)getSystemService(DOWNLOAD_SERVICE)).enqueue(req);Toast.makeText(this,"Download queued",0).show();}catch(Exception e){Toast.makeText(this,"Unable to queue download",0).show();}}
 void readerMode(){WebView target=current();if(!allowed(target.getUrl()))return;target.evaluateJavascript("(function(){var e=document.querySelector('article,main,[role=main]')||document.body;return e?e.innerText.slice(0,150000):'';})()",value->{if(isDestroyed()||target!=current())return;String body;try{Object parsed=new org.json.JSONTokener(value).nextValue();body=parsed instanceof String?(String)parsed:"";}catch(Exception e){body="";}if(body.trim().isEmpty()){Toast.makeText(this,"No readable text found",0).show();return;}Dialog dialog=new Dialog(this);LinearLayout panel=new LinearLayout(this);panel.setOrientation(1);panel.setBackgroundColor(0xff171d28);Button close=new Button(this);close.setText("Close reader");close.setOnClickListener(v->dialog.dismiss());panel.addView(close);ScrollView scroll=new ScrollView(this);TextView article=new TextView(this);article.setText(body);article.setTextColor(0xfff1f5f9);article.setTextSize(18);article.setLineSpacing(dp(5),1.2f);article.setPadding(dp(24),dp(16),dp(24),dp(24));article.setTextIsSelectable(true);scroll.addView(article);panel.addView(scroll,new LinearLayout.LayoutParams(-1,0,1));dialog.setContentView(panel);dialog.show();dialog.getWindow().setLayout(-1,-1);});}
 void printPage(){if(!allowed(current().getUrl()))return;android.print.PrintManager manager=(android.print.PrintManager)getSystemService(PRINT_SERVICE);if(manager!=null)manager.print("Orion webpage",current().createPrintDocumentAdapter("Orion webpage"),new android.print.PrintAttributes.Builder().build());}
 void sharePage(){String u=geckoActive?geckoAddress:current().getUrl();if(allowed(u)){Intent intent=new Intent(Intent.ACTION_SEND);intent.setType("text/plain");intent.putExtra(Intent.EXTRA_TEXT,u);startActivity(Intent.createChooser(intent,"Share link"));}}
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
 void newTab(){if(tabs.size()>=20){Toast.makeText(this,"20 tabs open. Close a tab first.",0).show();return;}WebView w=new WebView(this);WebSettings s=w.getSettings();s.setJavaScriptEnabled(true);s.setDomStorageEnabled(true);s.setAllowFileAccess(false);s.setAllowContentAccess(false);s.setMixedContentMode(WebSettings.MIXED_CONTENT_NEVER_ALLOW);s.setJavaScriptCanOpenWindowsAutomatically(false);s.setSafeBrowsingEnabled(true);CookieManager.getInstance().setAcceptThirdPartyCookies(w,false);
 w.setWebViewClient(new WebViewClient(){@Override public WebResourceResponse shouldInterceptRequest(WebView v,WebResourceRequest request){if(!request.isForMainFrame()&&getPreferences(0).getBoolean("adblock",true)&&(AdBlocker.blocks(request.getUrl().getHost())||(getPreferences(0).getBoolean("youtube_ads",true)&&youtubeAd(request.getUrl()))))return new WebResourceResponse("text/plain","UTF-8",new java.io.ByteArrayInputStream(new byte[0]));return super.shouldInterceptRequest(v,request);}@Override public boolean shouldOverrideUrlLoading(WebView v,WebResourceRequest r){if(!allowed(r.getUrl().toString()))return true;if(r.isForMainFrame()&&"http".equalsIgnoreCase(r.getUrl().getScheme())){if(!"http".equalsIgnoreCase(Uri.parse(v.getUrl()==null?"":v.getUrl()).getScheme())||!Objects.equals(Uri.parse(v.getUrl()==null?"":v.getUrl()).getHost(),r.getUrl().getHost())){if(v==current())navigate(r.getUrl().toString());return true;}}return false;}@Override public void onPageFinished(WebView v,String u){if(selected>=0&&v==current()){if(!address.hasFocus())address.setText(allowed(u)?u:"");recordVisit(u);address.setTextColor(u!=null&&u.startsWith("http://")?0xffffcf80:0xfff1f5f9);back.setEnabled(v.canGoBack());forward.setEnabled(v.canGoForward());}}});
 w.setWebChromeClient(new WebChromeClient(){@Override public void onProgressChanged(WebView v,int p){if(selected>=0&&v==current()){progress.setProgress(p);progress.setVisibility(p<100?View.VISIBLE:View.INVISIBLE);}}@Override public void onPermissionRequest(PermissionRequest r){mediaPermission(w,r);}@Override public void onPermissionRequestCanceled(PermissionRequest r){if(pendingMedia==r)pendingMedia=null;}@Override public void onGeolocationPermissionsShowPrompt(String origin,GeolocationPermissions.Callback cb){locationPermission(w,origin,cb); }});
 w.setDownloadListener((url,ua,disposition,mime,length)->downloadPage(url,disposition,mime));applySettings(w);tabs.add(w);show(tabs.size()-1);home(w);}
 PermissionRequest pendingMedia; WebView permissionTab; String[] mediaResources; GeolocationPermissions.Callback pendingLocation; String locationOrigin;
 boolean secureOrigin(WebView w,String origin){if(selected<0||w!=current()||origin==null)return false;Uri a=Uri.parse(origin),b=Uri.parse(w.getUrl()==null?"":w.getUrl());return "https".equalsIgnoreCase(a.getScheme())&&"https".equalsIgnoreCase(b.getScheme())&&a.getHost()!=null&&a.getHost().equalsIgnoreCase(b.getHost())&&(a.getPort()==-1?443:a.getPort())==(b.getPort()==-1?443:b.getPort());}
 void mediaPermission(WebView w,PermissionRequest req){if(pendingMedia!=null||pendingLocation!=null||!secureOrigin(w,req.getOrigin().toString())){req.deny();return;}ArrayList<String> resources=new ArrayList<>(),permissions=new ArrayList<>();for(String item:req.getResources()){if(item.equals(PermissionRequest.RESOURCE_VIDEO_CAPTURE)){resources.add(item);permissions.add(android.Manifest.permission.CAMERA);}else if(item.equals(PermissionRequest.RESOURCE_AUDIO_CAPTURE)){resources.add(item);permissions.add(android.Manifest.permission.RECORD_AUDIO);}}if(resources.isEmpty()){req.deny();return;}for(String p:permissions){String key=p.equals(android.Manifest.permission.CAMERA)?"site_camera":"site_microphone";if(getPreferences(0).getInt(key,0)==1){req.deny();return;}}pendingMedia=req;permissionTab=w;mediaResources=resources.toArray(new String[0]);Runnable grantMedia=()->{if(pendingMedia!=req)return;ArrayList<String> missing=new ArrayList<>();for(String p:permissions)if(checkSelfPermission(p)!=android.content.pm.PackageManager.PERMISSION_GRANTED)missing.add(p);if(missing.isEmpty())finishMedia(true);else requestPermissions(missing.toArray(new String[0]),41);};boolean prompt=false;for(String p:permissions){String key=p.equals(android.Manifest.permission.CAMERA)?"site_camera":"site_microphone";if(getPreferences(0).getInt(key,0)==0)prompt=true;}if(!prompt){grantMedia.run();return;}new AlertDialog.Builder(this).setTitle("Website permission").setMessage(req.getOrigin().getHost()+" requests "+(resources.contains(PermissionRequest.RESOURCE_VIDEO_CAPTURE)?"camera ":"")+(resources.contains(PermissionRequest.RESOURCE_AUDIO_CAPTURE)?"microphone":"")+". Allow for this request?").setNegativeButton("Deny",(d,i)->finishMedia(false)).setOnCancelListener(d->finishMedia(false)).setPositiveButton("Allow",(d,i)->grantMedia.run()).show();}
 void finishMedia(boolean allow){PermissionRequest r=pendingMedia;pendingMedia=null;if(r==null)return;if(allow&&secureOrigin(permissionTab,r.getOrigin().toString()))r.grant(mediaResources);else r.deny();}
 void locationPermission(WebView w,String origin,GeolocationPermissions.Callback cb){if(pendingMedia!=null||pendingLocation!=null||!secureOrigin(w,origin)||getPreferences(0).getInt("site_location",0)==1){cb.invoke(origin,false,false);return;}pendingLocation=cb;locationOrigin=origin;permissionTab=w;Runnable grantLocation=()->{if(checkSelfPermission(android.Manifest.permission.ACCESS_COARSE_LOCATION)==android.content.pm.PackageManager.PERMISSION_GRANTED)finishLocation(true);else requestPermissions(new String[]{android.Manifest.permission.ACCESS_COARSE_LOCATION},42);};if(getPreferences(0).getInt("site_location",0)==2){grantLocation.run();return;}new AlertDialog.Builder(this).setTitle("Location permission").setMessage(Uri.parse(origin).getHost()+" requests approximate location. Allow for this request?").setNegativeButton("Deny",(d,i)->finishLocation(false)).setOnCancelListener(d->finishLocation(false)).setPositiveButton("Allow",(d,i)->grantLocation.run()).show();}
 void finishLocation(boolean allow){GeolocationPermissions.Callback cb=pendingLocation;pendingLocation=null;if(cb!=null)cb.invoke(locationOrigin,allow&&secureOrigin(permissionTab,locationOrigin),false);}
 @Override public void onRequestPermissionsResult(int code,String[] permissions,int[] results){super.onRequestPermissionsResult(code,permissions,results);boolean ok=results.length>0;for(int result:results)ok&=result==android.content.pm.PackageManager.PERMISSION_GRANTED;if(code==41)finishMedia(ok);if(code==42)finishLocation(ok);if(code==43){org.mozilla.geckoview.GeckoSession.PermissionDelegate.Callback cb=geckoAndroidCallback;geckoAndroidCallback=null;if(cb!=null){if(ok&&geckoActive)cb.grant();else cb.reject();}}}
 void show(int i){if(geckoActive)closeGecko();if(selected>=0&&selected<tabs.size())tabs.get(selected).onPause();hideHome();content.removeAllViews();selected=i;current().onResume();content.addView(current(),new FrameLayout.LayoutParams(-1,-1));address.setText(allowed(current().getUrl())?current().getUrl():"");count.setContentDescription("Switch tabs, "+tabs.size()+" open");count.invalidate();back.setEnabled(current().canGoBack());forward.setEnabled(current().canGoForward());refreshTabletTabs();if(homeTabs.contains(current()))showHome(false);}
 void chooseTab(){String[] names=new String[tabs.size()];for(int i=0;i<names.length;i++)names[i]=tabs.get(i).getTitle()==null?"New tab":tabs.get(i).getTitle();new AlertDialog.Builder(this).setTitle("Tabs").setItems(names,(d,i)->show(i)).setPositiveButton("Archived tabs",(d,i)->archivedTabs()).setNeutralButton("Close current",(d,i)->{WebView w=current();content.removeAllViews();tabs.remove(selected);w.destroy();if(tabs.isEmpty())newTab();else show(Math.min(selected,tabs.size()-1));}).show();}
 void archivedTabs(){String[] urls=new TreeSet<>(getPreferences(0).getStringSet("archived_tabs",Collections.emptySet())).toArray(new String[0]);new AlertDialog.Builder(this).setTitle("Archived tabs").setItems(urls,(d,i)->{if(tabs.size()>=20){Toast.makeText(this,"Close a tab before restoring",0).show();return;}newTab();navigate(urls[i]);Set<String> set=new HashSet<>(Arrays.asList(urls));set.remove(urls[i]);getPreferences(0).edit().putStringSet("archived_tabs",set).apply();}).setPositiveButton("Archive current",(d,i)->{String url=current().getUrl();if(!allowed(url))return;Set<String> set=new HashSet<>(getPreferences(0).getStringSet("archived_tabs",Collections.emptySet()));set.add(url);getPreferences(0).edit().putStringSet("archived_tabs",set).apply();WebView w=current();content.removeAllViews();int index=selected;tabs.remove(index);selected=-1;w.destroy();if(tabs.isEmpty())newTab();else show(Math.min(index,tabs.size()-1));}).setNegativeButton("Close",null).show();}
 void save(){String u=geckoActive?geckoAddress:current().getUrl();if(!allowed(u))return;Set<String> set=new HashSet<>(getPreferences(0).getStringSet("bookmarks",Collections.emptySet()));set.add(u);getPreferences(0).edit().putStringSet("bookmarks",set).apply();Toast.makeText(this,"Bookmark saved",0).show();}
 void bookmarks(){Dialog dialog=new Dialog(this,android.R.style.Theme_Material_NoActionBar);LinearLayout page=new LinearLayout(this);page.setOrientation(1);page.setBackgroundColor(0xff171d28);Button close=new Button(this);close.setText("‹ Bookmarks");close.setOnClickListener(v->dialog.dismiss());page.addView(close);EditText search=new EditText(this);search.setSingleLine(true);search.setHint("Search bookmarks");search.setTextColor(0xfff1f5f9);search.setHintTextColor(0xffb9c6da);page.addView(search);Button add=new Button(this);add.setText("Add bookmark");page.addView(add);ScrollView scroll=new ScrollView(this);LinearLayout list=new LinearLayout(this);list.setOrientation(1);scroll.addView(list);page.addView(scroll,new LinearLayout.LayoutParams(-1,0,1));
 Runnable refresh=()->{list.removeAllViews();String q=search.getText().toString().toLowerCase(Locale.ROOT);TreeSet<String> urls=new TreeSet<>(getPreferences(0).getStringSet("bookmarks",Collections.emptySet()));for(String u:urls){if(!u.toLowerCase(Locale.ROOT).contains(q))continue;LinearLayout row=new LinearLayout(this);Button open=new Button(this);open.setAllCaps(false);open.setText(u);open.setOnClickListener(v->{dialog.dismiss();navigate(u);});row.addView(open,new LinearLayout.LayoutParams(0,-2,1));Button edit=new Button(this);edit.setText("Edit");edit.setOnClickListener(v->editBookmark(u,()->{dialog.dismiss();bookmarks();}));row.addView(edit);list.addView(row);}if(list.getChildCount()==0){TextView empty=new TextView(this);empty.setText("No matching bookmarks");empty.setTextColor(0xffb9c6da);empty.setPadding(dp(16),dp(16),dp(16),dp(16));list.addView(empty);}};
 search.addTextChangedListener(new android.text.TextWatcher(){public void beforeTextChanged(CharSequence s,int a,int c,int f){}public void onTextChanged(CharSequence s,int a,int b,int c){refresh.run();}public void afterTextChanged(android.text.Editable e){}});add.setOnClickListener(v->editBookmark(null,refresh));refresh.run();dialog.setContentView(page);dialog.show();dialog.getWindow().setLayout(-1,-1);page.setOnApplyWindowInsetsListener((v,insets)->{if(Build.VERSION.SDK_INT>=30){android.graphics.Insets i=insets.getInsets(WindowInsets.Type.systemBars()|WindowInsets.Type.displayCutout()|WindowInsets.Type.ime());v.setPadding(i.left,i.top,i.right,i.bottom);}else{v.setPadding(insets.getSystemWindowInsetLeft(),insets.getSystemWindowInsetTop(),insets.getSystemWindowInsetRight(),insets.getSystemWindowInsetBottom());}return insets;});page.requestApplyInsets();}
 void importBrowserData(){
 String[] choices={"Import bookmarks · HTML / JSON","Import passwords · CSV","Export passwords · CSV","Delete stored passwords","Import cookies · txt / JSON","Android Autofill settings"};
 new AlertDialog.Builder(this).setTitle("Import and export").setItems(choices,(d,i)->{
  switch(i){
   case 0:pickImport(BOOKMARK_IMPORT_REQUEST);break;
   case 1:new AlertDialog.Builder(this).setTitle("Import passwords").setMessage("Select a CSV exported by your own browser or password manager. Orion will encrypt imported credentials using Android Keystore. The original CSV remains wherever you saved it and should be deleted securely after migration. Imported passwords are not yet used for automatic form filling.")
    .setNegativeButton("Cancel",null).setPositiveButton("Choose CSV",(x,y)->pickImport(PASSWORD_IMPORT_REQUEST)).show();break;
   case 2:passwordExport();break;
   case 3:new AlertDialog.Builder(this).setTitle("Delete imported passwords?").setMessage("Permanently delete Orion's local imported-password vault. This cannot be undone.")
    .setNegativeButton("Cancel",null).setPositiveButton("Delete",(x,y)->{PasswordVault.clear(getApplicationContext());Toast.makeText(this,"Orion password vault deleted",Toast.LENGTH_LONG).show();}).show();break;
   case 4:new AlertDialog.Builder(this).setTitle("Import cookies").setMessage("Choose an export file containing cookies from a browser you own. Cookies can represent active login sessions. Orion will show the included website domains for confirmation before importing into WebView. GeckoView has a separate cookie store. Only import files you trust.")
    .setNegativeButton("Cancel",null).setPositiveButton("Choose export",(x,y)->pickImport(COOKIE_IMPORT_REQUEST)).show();break;
   case 5:try{startActivity(new Intent(android.provider.Settings.ACTION_SETTINGS));}catch(Exception e){Toast.makeText(this,"Android settings unavailable",Toast.LENGTH_SHORT).show();}break;
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
     .setNegativeButton("Cancel",null).setPositiveButton("Import cookies",(d,i)->CookieImporter.apply(preview,()->Toast.makeText(this,"Cookies imported into WebView",Toast.LENGTH_LONG).show())).show();
    });}catch(Exception e){importError("Cookie import failed",e);}},"orion-cookie-import").start();
  }
 }
 void importError(String title,Exception e){String msg=e.getMessage()==null?"Could not complete operation":e.getMessage();runOnUiThread(()->{if(isFinishing()||isDestroyed())return;new AlertDialog.Builder(this).setTitle(title).setMessage(msg).setPositiveButton("OK",null).show();});}
 void editBookmark(String old,Runnable refresh){EditText field=new EditText(this);field.setSingleLine(true);field.setInputType(17);field.setHint("https://example.com");if(old!=null)field.setText(old);AlertDialog.Builder builder=new AlertDialog.Builder(this).setTitle(old==null?"Add bookmark":"Edit bookmark").setView(field).setNegativeButton("Cancel",null).setPositiveButton("Save",null);if(old!=null)builder.setNeutralButton("Delete",(d,i)->new AlertDialog.Builder(this).setTitle("Delete bookmark?").setMessage(old).setNegativeButton("Cancel",null).setPositiveButton("Delete",(x,y)->{Set<String> set=new HashSet<>(getPreferences(0).getStringSet("bookmarks",Collections.emptySet()));set.remove(old);getPreferences(0).edit().putStringSet("bookmarks",set).apply();refresh.run();}).show());AlertDialog dialog=builder.create();dialog.show();dialog.getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener(v->{String u=field.getText().toString().trim();if(!allowed(u)){field.setError("Enter a valid HTTP or HTTPS address");return;}Set<String> set=new HashSet<>(getPreferences(0).getStringSet("bookmarks",Collections.emptySet()));if(old!=null)set.remove(old);set.add(u);getPreferences(0).edit().putStringSet("bookmarks",set).apply();dialog.dismiss();refresh.run();});}
 void recordVisit(String uri){
  if(!allowed(uri))return;
  try{org.json.JSONArray old=new org.json.JSONArray(getPreferences(0).getString("recent_history","[]"));org.json.JSONArray fresh=new org.json.JSONArray();fresh.put(uri);
   for(int i=0;i<old.length()&&fresh.length()<150;i++){String u=old.optString(i,"");if(allowed(u)&&!uri.equals(u))fresh.put(u);}
   getPreferences(0).edit().putString("recent_history",fresh.toString()).apply();
  }catch(Exception ignored){}
 }
 void historyLibrary(){
  org.json.JSONArray entries;try{entries=new org.json.JSONArray(getPreferences(0).getString("recent_history","[]"));}catch(Exception e){entries=new org.json.JSONArray();}
  final org.json.JSONArray saved=entries;
  Dialog dialog=new Dialog(this,android.R.style.Theme_Material_NoActionBar);LinearLayout page=new LinearLayout(this);page.setOrientation(1);page.setBackgroundColor(0xff171d28);
  Button close=geckoButton("‹","Close history",()->dialog.dismiss());LinearLayout header=new LinearLayout(this);header.setGravity(Gravity.CENTER_VERTICAL);header.setBackgroundColor(0xff293344);header.addView(close,new LinearLayout.LayoutParams(dp(56),dp(56)));
  TextView title=new TextView(this);title.setText("Browsing history");title.setTextColor(0xfff1f5f9);title.setTextSize(20);header.addView(title,new LinearLayout.LayoutParams(0,dp(56),1));page.addView(header);
  ScrollView scroll=new ScrollView(this);LinearLayout list=new LinearLayout(this);list.setOrientation(1);scroll.addView(list);
  for(int i=0;i<saved.length();i++){final String u=saved.optString(i,"");if(allowed(u))settingRow(list,u,"Open page",()->{dialog.dismiss();navigate(u);});}
  page.addView(scroll,new LinearLayout.LayoutParams(-1,0,1));
  Button clear=geckoButton("Clear history","Clear history",()->new AlertDialog.Builder(this).setTitle("Clear visited-page history?").setNegativeButton("Cancel",null).setPositiveButton("Clear",(d,i)->{getPreferences(0).edit().remove("recent_history").apply();dialog.dismiss();}).show());page.addView(clear,new LinearLayout.LayoutParams(-1,dp(56)));
  dialog.setContentView(page);dialog.show();dialog.getWindow().setLayout(-1,-1);safeDialogInsets(page,dialog);
 }
 void readingList(){
  Dialog dialog=new Dialog(this,android.R.style.Theme_Material_NoActionBar);LinearLayout page=new LinearLayout(this);page.setOrientation(1);page.setBackgroundColor(0xff171d28);
  LinearLayout header=new LinearLayout(this);header.setGravity(Gravity.CENTER_VERTICAL);header.setBackgroundColor(0xff293344);
  header.addView(geckoButton("‹","Close reading list",()->dialog.dismiss()),new LinearLayout.LayoutParams(dp(56),dp(56)));TextView title=new TextView(this);title.setText("Reading list");title.setTextColor(0xfff1f5f9);title.setTextSize(20);header.addView(title);page.addView(header);
  ScrollView scroll=new ScrollView(this);LinearLayout list=new LinearLayout(this);list.setOrientation(1);scroll.addView(list);page.addView(scroll,new LinearLayout.LayoutParams(-1,0,1));
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
 void applyAll(){for(WebView w:tabs)applySettings(w);}
 void section(LinearLayout panel,String name){TextView t=new TextView(this);t.setText(name);t.setTextColor(0xff8ab4ff);t.setTextSize(13);t.setPadding(dp(16),dp(24),dp(16),dp(8));panel.addView(t);}
 void settingRow(LinearLayout panel,String title,String detail,Runnable action){LinearLayout row=new LinearLayout(this);row.setOrientation(1);row.setPadding(dp(16),dp(12),dp(16),dp(12));row.setMinimumHeight(dp(56));row.setBackground(new RippleDrawable(ColorStateList.valueOf(0x338ab4ff),null,round(0xffffffff,8)));TextView a=new TextView(this);a.setText(title);a.setTextSize(16);a.setTextColor(0xfff1f5f9);row.addView(a);TextView b=new TextView(this);b.setText(detail);b.setTextSize(13);b.setTextColor(0xffb9c6da);row.addView(b);row.setFocusable(true);row.setOnClickListener(v->action.run());panel.addView(row);}
 void toggle(LinearLayout panel,String title,String key,boolean def){Switch sw=new Switch(this);sw.setText(title);sw.setTextColor(0xfff1f5f9);sw.setTextSize(16);sw.setPadding(dp(16),dp(8),dp(16),dp(8));sw.setMinHeight(dp(56));sw.setChecked(getPreferences(0).getBoolean(key,def));sw.setOnCheckedChangeListener((v,on)->{getPreferences(0).edit().putBoolean(key,on).apply();applyAll();if(key.equals("easylist")||key.equals("easyprivacy"))AdBlocker.load(this,getPreferences(0));});panel.addView(sw);}
 void settings(){Dialog dialog=new Dialog(this,android.R.style.Theme_Material_NoActionBar);LinearLayout page=new LinearLayout(this);page.setOrientation(1);page.setBackgroundColor(0xff171d28);Button close=new Button(this);close.setText("‹  Settings");close.setAllCaps(false);close.setTextColor(0xfff1f5f9);close.setBackgroundColor(0xff293344);close.setGravity(Gravity.START|Gravity.CENTER_VERTICAL);close.setPadding(dp(16),0,0,0);close.setMinHeight(dp(56));close.setOnClickListener(v->dialog.dismiss());page.addView(close);ScrollView scroll=new ScrollView(this);LinearLayout panel=new LinearLayout(this);panel.setOrientation(1);panel.setPadding(0,0,0,dp(24));scroll.addView(panel);page.addView(scroll,new LinearLayout.LayoutParams(-1,0,1));String[] engines={"Google","DuckDuckGo","Bing","Yahoo","Kagi","Brave Search","Startpage","Ecosia","Qwant","Mojeek","Swisscows","Wikipedia"};
 section(panel,"ACCOUNT");settingRow(panel,"Sync","Google account · Not connected",()->syncSettings());
 section(panel,"GENERAL");settingRow(panel,"Search engine",engines[Math.max(0,Math.min(engines.length-1,getPreferences(0).getInt("engine",0)))],()->new AlertDialog.Builder(this).setTitle("Search engine").setSingleChoiceItems(engines,getPreferences(0).getInt("engine",0),(d,i)->{getPreferences(0).edit().putInt("engine",i).apply();d.dismiss();dialog.dismiss();settings();}).setNegativeButton("Cancel",null).show());
 section(panel,"DISPLAY");settingRow(panel,"Toolbar shortcut","Choose Home, Bookmark or Reload",()->new AlertDialog.Builder(this).setTitle("Toolbar shortcut").setSingleChoiceItems(new String[]{"Home","Bookmark","Reload"},getPreferences(0).getInt("shortcut",0),(d,i)->{getPreferences(0).edit().putInt("shortcut",i).apply();updateToolbar();d.dismiss();}).setNegativeButton("Cancel",null).show());toggle(panel,"Swipe across address bar to switch tabs","tab_swipe",true);toggle(panel,"Dark web pages (experimental)","darkpages",false);settingRow(panel,"Text size",getPreferences(0).getInt("zoom",100)+"%",()->{String[] labels={"75%","100%","125%","150%","200%"};int[] sizes={75,100,125,150,200};new AlertDialog.Builder(this).setTitle("Text size").setItems(labels,(d,i)->{getPreferences(0).edit().putInt("zoom",sizes[i]).apply();applyAll();dialog.dismiss();settings();}).show();});toggle(panel,"Desktop websites","desktop",false);
 section(panel,"CONTENT BLOCKING");toggle(panel,"Ad blocker","adblock",true);toggle(panel,"EasyList (domain rules)","easylist",false);toggle(panel,"EasyPrivacy (domain rules)","easyprivacy",false);settingRow(panel,"Update filter lists","Download latest domain rules",()->updateFilters());toggle(panel,"YouTube ad request filtering","youtube_ads",true);settingRow(panel,"About ad blocking","Blocks requests to known advertising domains",()->new AlertDialog.Builder(this).setTitle("Ad blocker").setMessage("Basic domain-based blocking. Some adverts will remain, and some sites may need blocking disabled. Reload the page after changing this setting.").setPositiveButton("OK",null).show());
 section(panel,"SITE SETTINGS");settingRow(panel,"Camera, microphone and location","Choose Ask every time, Block or Allow on request",()->sitePermissions());toggle(panel,"JavaScript","javascript",true);toggle(panel,"Load images","images",true);
 section(panel,"PRIVACY AND SECURITY");settingRow(panel,"DNS and Secure DNS","Cloudflare and other providers · IPv4 / IPv6 diagnostics",()->dnsSettings());settingRow(panel,"Privacy protections","HTTPS preferred · WebView cookies blocked · camera/mic/location controls",()->privacy());settingRow(panel,"Clear browsing data","Cookies, site storage, cache and tab history",()->clearBrowsingData());toggle(panel,"Zero cache (reload resources from network)","zero_cache",false);
 section(panel,"BROWSER DATA");settingRow(panel,"Bookmarks","Saved websites",()->bookmarks());settingRow(panel,"Reading list","Saved pages and read status",()->readingList());settingRow(panel,"Browsing history","Recent visited pages",()->historyLibrary());settingRow(panel,"Import browser data","Bookmarks, password options and cookie limitations",()->importBrowserData());
 section(panel,"ABOUT");settingRow(panel,"Replay launch animation","Inspect the orbit-and-star animation",()->{dialog.dismiss();showStartup();});settingRow(panel,"Orion Browser",installedVersion(),()->new AlertDialog.Builder(this).setTitle("About Orion").setMessage("Experimental community browser. Orion Connect, isolated private browsing, extensions and password sync are not implemented.").setPositiveButton("OK",null).show());
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
 void updateFilters(){Toast.makeText(this,"Updating filter lists…",0).show();new Thread(()->{String result;try{AdBlocker.update(this,"easylist");AdBlocker.update(this,"easyprivacy");AdBlocker.load(this,getPreferences(0));result="Filter lists updated. Reload pages to apply.";}catch(Exception e){result="Filter update incomplete. Previous lists retained where possible.";}final String msg=result;runOnUiThread(()->{if(!isDestroyed())Toast.makeText(this,msg,Toast.LENGTH_LONG).show();});}).start();}
 void sitePermissions(){Dialog dialog=new Dialog(this,android.R.style.Theme_Material_NoActionBar);LinearLayout page=new LinearLayout(this);page.setOrientation(1);page.setBackgroundColor(0xff171d28);Button close=geckoButton("‹","Back to browser",()->dialog.dismiss());LinearLayout heading=new LinearLayout(this);heading.setGravity(Gravity.CENTER_VERTICAL);heading.setBackgroundColor(0xff293344);heading.addView(close,new LinearLayout.LayoutParams(dp(56),dp(56)));TextView name=new TextView(this);name.setText("Site permissions");name.setTextSize(20);name.setTextColor(0xfff1f5f9);heading.addView(name);page.addView(heading);ScrollView scroll=new ScrollView(this);LinearLayout options=new LinearLayout(this);options.setOrientation(1);options.setPadding(dp(8),dp(16),dp(8),dp(24));scroll.addView(options);page.addView(scroll,new LinearLayout.LayoutParams(-1,0,1));TextView explain=new TextView(this);explain.setText("Camera, microphone and location settings apply to both engines on secure HTTPS sites. Android system permission is also required. GeckoView notification requests are blocked in this test build.");explain.setTextSize(14);explain.setTextColor(0xffb9c6da);explain.setPadding(dp(12),0,dp(12),dp(16));options.addView(explain);permissionChoice(options,"Camera","site_camera");permissionChoice(options,"Microphone","site_microphone");permissionChoice(options,"Approximate location","site_location");settingRow(options,"Android app permissions","Manage camera, microphone and location access in Android settings",()->startActivity(new Intent(android.provider.Settings.ACTION_APPLICATION_DETAILS_SETTINGS,Uri.parse("package:"+getPackageName()))));dialog.setContentView(page);dialog.show();Window win=dialog.getWindow();win.setLayout(-1,-1);win.setStatusBarColor(0xff171d28);win.setNavigationBarColor(0xff171d28);win.getDecorView().setSystemUiVisibility(0);page.setOnApplyWindowInsetsListener((v,insets)->{if(Build.VERSION.SDK_INT>=30){android.graphics.Insets b=insets.getInsets(WindowInsets.Type.systemBars()|WindowInsets.Type.displayCutout());page.setPadding(b.left,b.top,b.right,b.bottom);}else page.setPadding(insets.getSystemWindowInsetLeft(),insets.getSystemWindowInsetTop(),insets.getSystemWindowInsetRight(),insets.getSystemWindowInsetBottom());return insets;});page.requestApplyInsets();}
 void permissionChoice(LinearLayout parent,String title,String key){String[] values={"Ask every time","Block","Allow on request"};int choice=Math.max(0,Math.min(2,getPreferences(0).getInt(key,0)));settingRow(parent,title,values[choice],()->new AlertDialog.Builder(this).setTitle(title).setSingleChoiceItems(values,getPreferences(0).getInt(key,0),(d,i)->{getPreferences(0).edit().putInt(key,i).apply();d.dismiss();Toast.makeText(this,title+": "+values[i],Toast.LENGTH_SHORT).show();}).setNegativeButton("Cancel",null).show());}
 void privacy(){new AlertDialog.Builder(this).setTitle("Privacy protections").setMessage("HTTPS is preferred, but HTTP remains available after a warning. HTTP is unencrypted. WebView blocks third-party cookies; GeckoView uses its own cookie controls. Camera, microphone and location choices apply to secure HTTPS sites in both engines and require Android system permissions. GeckoView notification requests are blocked in this test build. No app telemetry. Isolated private browsing is not implemented.").setNeutralButton("Site permissions",(d,i)->sitePermissions()).setPositiveButton("OK",null).show();}
 void clearBrowsingData(){new AlertDialog.Builder(this).setTitle("Clear browsing data?").setMessage("Remove cookies, website storage, cache and tab history. This may sign you out of websites. Bookmarks are retained.").setNegativeButton("Cancel",null).setPositiveButton("Clear",(d,i)->{CookieManager.getInstance().removeAllCookies(v->CookieManager.getInstance().flush());WebStorage.getInstance().deleteAllData();for(WebView w:tabs){w.clearCache(true);w.clearHistory();w.clearFormData();}Toast.makeText(this,"Browsing data cleared",0).show();}).show();}
 @Override public void onBackPressed(){if(address.hasFocus()){address.clearFocus();return;}if(geckoActive){if(geckoCanGoBack)geckoGoBack();else closeGecko();return;}if(selected>=0&&current().canGoBack()){current().goBack();return;}super.onBackPressed();}
 @Override protected void onDestroy(){homeHandler.removeCallbacks(openHomeAfterTap);if(geckoActive)closeGecko();hideHome();content.removeAllViews();homeTabs.clear();for(WebView w:tabs)w.destroy();super.onDestroy();}
}
