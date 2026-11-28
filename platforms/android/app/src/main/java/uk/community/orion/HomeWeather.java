package uk.community.orion;

import android.Manifest;
import android.app.AlertDialog;
import android.content.Context;
import android.content.pm.PackageManager;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.drawable.GradientDrawable;
import android.location.Location;
import android.location.LocationListener;
import android.location.Geocoder;
import android.location.Address;
import android.location.LocationManager;
import android.os.Handler;
import android.os.Looper;
import android.view.Gravity;
import android.view.View;
import android.widget.Button;
import android.widget.EditText;
import android.widget.LinearLayout;
import android.widget.TextView;
import android.text.InputType;
import android.widget.Toast;
import org.json.JSONArray;
import org.json.JSONObject;
import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.util.Calendar;
import java.util.Date;
import java.util.Locale;
import java.util.TimeZone;
import javax.net.ssl.HttpsURLConnection;

/** A native, consent-based ECMWF IFS forecast card on the Orion start screen. */
final class HomeWeather {
 static final int LOCATION_REQUEST=44;
 private static final String PREFIX="home_weather_";
 private static final long CACHE_TTL_MS=90L*60*1000;
 private final MainActivity activity;
 private final LinearLayout host;
 private final Handler main=new Handler(Looper.getMainLooper());
 private final LocationManager locationManager;
 private LocationListener listener;
 private boolean disposed;
 private int sequence;
 private HomeWeather(MainActivity a,LinearLayout parent){
  activity=a;host=parent;
  locationManager=(LocationManager)a.getSystemService(Context.LOCATION_SERVICE);
 }
 static HomeWeather attach(MainActivity a,LinearLayout parent){
  HomeWeather weather=new HomeWeather(a,parent);
  weather.render();
  return weather;
 }
 void dispose(){
  disposed=true;sequence++;main.removeCallbacksAndMessages(null);
  if(listener!=null&&locationManager!=null){try{locationManager.removeUpdates(listener);}catch(Exception ignored){}listener=null;}
 }
 private android.content.SharedPreferences prefs(){return activity.getPreferences(0);}
 static boolean enabled(MainActivity a){return a.getPreferences(0).getBoolean(PREFIX+"enabled",true);}
 private boolean safe(){return !disposed&&!activity.isFinishing()&&!activity.isDestroyed();}
 private int dp(float value){return activity.dp(value);}
 private static int BLUE=0xff79b7ff,WHITE=0xfff5f8ff,MUTED=0xffb9c6da;
 private GradientDrawable bg(int colour,int radius){
  GradientDrawable out=new GradientDrawable();out.setColor(colour);out.setCornerRadius(dp(radius));
  return out;
 }
 private TextView text(String value,int size,int color,boolean strong){
  TextView v=new TextView(activity);v.setText(value);v.setTextColor(color);v.setTextSize(size);
  if(strong)v.setTypeface(android.graphics.Typeface.create("sans-serif-medium",android.graphics.Typeface.NORMAL));
  return v;
 }
 private Button button(String label,Runnable action){
  Button v=new Button(activity);v.setText(label);v.setAllCaps(false);v.setTextSize(13);
  v.setTextColor(WHITE);v.setMinHeight(dp(48));v.setPadding(dp(12),0,dp(12),0);
  v.setBackground(bg(0xff354861,12));v.setOnClickListener(x->action.run());
  return v;
 }
 private void render(){
  if(!safe())return;
  host.removeAllViews();
  if(!enabled(activity))return;
  LinearLayout card=new LinearLayout(activity);card.setOrientation(LinearLayout.VERTICAL);
  card.setPadding(dp(16),dp(16),dp(16),dp(16));
  card.setBackground(bg(0xff26354e,20));card.setElevation(dp(2));
  int width=Math.min(activity.getResources().getDisplayMetrics().widthPixels-dp(32),dp(520));
  LinearLayout.LayoutParams cardParams=new LinearLayout.LayoutParams(Math.max(dp(260),width),-2);
  cardParams.topMargin=dp(22);cardParams.bottomMargin=dp(20);
  host.addView(card,cardParams);
  LinearLayout header=new LinearLayout(activity);header.setGravity(Gravity.CENTER_VERTICAL);
  TextView heading=text("LOCAL WEATHER",12,0xffbfd8ff,true);heading.setLetterSpacing(.15f);
  header.addView(heading,new LinearLayout.LayoutParams(0,-2,1));
  TextView source=text("ECMWF IFS",12,MUTED,false);header.addView(source);
  card.addView(header);

  String raw=prefs().getString(PREFIX+"cache","");
  if(!raw.isEmpty()){
   try{renderForecast(card,new JSONObject(raw));}
   catch(Exception e){card.addView(text("Saved forecast is unavailable.",14,MUTED,false));}
  }else{
   TextView title=text("Weather in your area",20,WHITE,true);
   LinearLayout.LayoutParams lp=new LinearLayout.LayoutParams(-1,-2);lp.topMargin=dp(16);card.addView(title,lp);
   TextView explanation=text("Choose a town or allow approximate location to show a colourful five-day ECMWF forecast.",14,MUTED,false);
   explanation.setLineSpacing(dp(3),1f);card.addView(explanation);
  }
  LinearLayout actions=new LinearLayout(activity);actions.setGravity(Gravity.CENTER_VERTICAL);
  LinearLayout.LayoutParams ap=new LinearLayout.LayoutParams(-1,-2);ap.topMargin=dp(12);card.addView(actions,ap);
  String mode=prefs().getString(PREFIX+"mode","");
  boolean hasSaved=!mode.isEmpty();
  Button select=button(hasSaved?"Change area":"Choose a town",this::chooseTown);
  LinearLayout.LayoutParams first=new LinearLayout.LayoutParams(0,dp(48),1);first.rightMargin=dp(8);
  actions.addView(select,first);
  Button location=button(hasSaved?"Refresh":"Use my location",()->{
   if("town".equals(prefs().getString(PREFIX+"mode","")))refreshSaved();
   else useLocation();
  });
  actions.addView(location,new LinearLayout.LayoutParams(0,dp(48),1));
  TextView note=text("ECMWF forecast via Open-Meteo · updates about every 90 min",11,MUTED,false);
  LinearLayout.LayoutParams np=new LinearLayout.LayoutParams(-1,-2);np.topMargin=dp(10);card.addView(note,np);
  card.setContentDescription("Orion local weather forecast and weather location controls");
  if(hasSaved&&System.currentTimeMillis()-prefs().getLong(PREFIX+"updated",0)>CACHE_TTL_MS){
   if("town".equals(mode))refreshSaved();
   else if(locationPermission())useLocation();
  }
 }
 private void renderForecast(LinearLayout card,JSONObject data)throws Exception{
  JSONObject hourly=data.getJSONObject("hourly");
  JSONArray times=hourly.getJSONArray("time");
  JSONArray temps=hourly.getJSONArray("temperature_2m");
  JSONArray feels=hourly.getJSONArray("apparent_temperature");
  JSONArray winds=hourly.getJSONArray("wind_speed_10m");
  JSONArray humidity=hourly.getJSONArray("relative_humidity_2m");
  JSONArray rains=hourly.getJSONArray("precipitation");
  JSONArray codes=hourly.getJSONArray("weather_code");
  JSONArray days=hourly.getJSONArray("is_day");
  int n=times.length();if(n==0)throw new IllegalArgumentException("Empty forecast");
  long now=System.currentTimeMillis()/1000L;
  int current=0;
  for(int i=0;i<n;i++){if(times.getLong(i)<=now)current=i;else break;}
  int code=codes.optInt(current,3);
  boolean daylight=days.optInt(current,1)==1;
  String place=prefs().getString(PREFIX+"label","Approximate location");
  TextView location=text(place,17,WHITE,true);
  LinearLayout.LayoutParams locp=new LinearLayout.LayoutParams(-1,-2);locp.topMargin=dp(12);card.addView(location,locp);
  LinearLayout summary=new LinearLayout(activity);summary.setGravity(Gravity.CENTER_VERTICAL);
  LinearLayout.LayoutParams summp=new LinearLayout.LayoutParams(-1,-2);summp.topMargin=dp(6);card.addView(summary,summp);
  WeatherIcon currentIcon=new WeatherIcon(activity,code,daylight);
  summary.addView(currentIcon,new LinearLayout.LayoutParams(dp(72),dp(72)));
  LinearLayout stats=new LinearLayout(activity);stats.setOrientation(LinearLayout.VERTICAL);
  stats.setPadding(dp(14),0,0,0);summary.addView(stats);
  stats.addView(text(Math.round(temps.optDouble(current,0))+"°C",30,WHITE,true));
  stats.addView(text(description(code,daylight)+" · feels "+Math.round(feels.optDouble(current,0))+"°C",13,MUTED,false));

  LinearLayout metrics=new LinearLayout(activity);metrics.setGravity(Gravity.CENTER);
  LinearLayout.LayoutParams mp=new LinearLayout.LayoutParams(-1,-2);mp.topMargin=dp(10);card.addView(metrics,mp);
  addMetric(metrics,"WIND",Math.round(winds.optDouble(current,0))+" km/h");
  addMetric(metrics,"HUMIDITY",Math.round(humidity.optDouble(current,0))+"%");
  addMetric(metrics,"RAIN",String.format(Locale.UK,"%.1f mm",rains.optDouble(current,0)));

  View divider=new View(activity);divider.setBackgroundColor(0xff49617e);
  LinearLayout.LayoutParams line=new LinearLayout.LayoutParams(-1,dp(1));line.topMargin=dp(14);line.bottomMargin=dp(12);card.addView(divider,line);
  LinearLayout forecast=new LinearLayout(activity);forecast.setGravity(Gravity.CENTER);card.addView(forecast);
  int offset=data.optInt("utc_offset_seconds",0);
  TimeZone tz=TimeZone.getTimeZone(data.optString("timezone","UTC"));
  java.text.SimpleDateFormat format=new java.text.SimpleDateFormat("EEE",Locale.getDefault());format.setTimeZone(tz);
  long startDay=Math.floorDiv(times.getLong(0)+offset,86400L);
  int count=0;
  for(long d=startDay;d<startDay+5&&count<5;d++){
   double low=Double.POSITIVE_INFINITY,high=Double.NEGATIVE_INFINITY;
   int sampleCode=3,priority=-1;boolean sampleDay=true;
   int at=-1;
   for(int i=0;i<n;i++){
    long day=Math.floorDiv(times.getLong(i)+offset,86400L);
    if(day!=d)continue;
    double temp=temps.optDouble(i,Double.NaN);
    if(!Double.isNaN(temp)){low=Math.min(low,temp);high=Math.max(high,temp);}
    int hour=(int)Math.floorMod(times.getLong(i)+offset,86400L)/3600;
    if(at==-1||Math.abs(hour-12)<priority){at=i;priority=Math.abs(hour-12);sampleCode=codes.optInt(i,3);sampleDay=days.optInt(i,1)==1;}
   }
   if(at==-1||!Double.isFinite(high))continue;
   LinearLayout dayCard=new LinearLayout(activity);dayCard.setOrientation(LinearLayout.VERTICAL);dayCard.setGravity(Gravity.CENTER);
   LinearLayout.LayoutParams dcp=new LinearLayout.LayoutParams(0,-2,1);
   forecast.addView(dayCard,dcp);
   String dayLabel=count==0?"Today":format.format(new Date((d*86400L-offset)*1000L+12L*3600000L));
   TextView dayText=text(dayLabel,12,MUTED,false);dayText.setGravity(Gravity.CENTER);dayCard.addView(dayText);
   WeatherIcon icon=new WeatherIcon(activity,sampleCode,sampleDay);
   LinearLayout.LayoutParams iconp=new LinearLayout.LayoutParams(dp(38),dp(38));iconp.topMargin=dp(4);iconp.bottomMargin=dp(4);dayCard.addView(icon,iconp);
   TextView highLow=text(Math.round(high)+"°/"+Math.round(low)+"°",12,WHITE,true);
   highLow.setGravity(Gravity.CENTER);highLow.setMaxLines(1);dayCard.addView(highLow);
   count++;
  }
  long updated=prefs().getLong(PREFIX+"updated",0);
  if(updated>0){
   long age=Math.max(0,(System.currentTimeMillis()-updated)/60000);
   TextView stamp=text(age<2?"Updated just now":"Updated "+age+" min ago"+(age>=180?" · cached":""),11,MUTED,false);
   LinearLayout.LayoutParams sp=new LinearLayout.LayoutParams(-1,-2);sp.topMargin=dp(12);card.addView(stamp,sp);
  }
 }
 private void addMetric(LinearLayout metrics,String title,String value){
  LinearLayout cell=new LinearLayout(activity);cell.setOrientation(LinearLayout.VERTICAL);cell.setGravity(Gravity.CENTER);
  metrics.addView(cell,new LinearLayout.LayoutParams(0,-2,1));
  TextView v=text(value,14,WHITE,true);v.setGravity(Gravity.CENTER);cell.addView(v);
  TextView t=text(title,10,MUTED,false);t.setGravity(Gravity.CENTER);cell.addView(t);
 }
 private boolean locationPermission(){
  return activity.checkSelfPermission(Manifest.permission.ACCESS_COARSE_LOCATION)==PackageManager.PERMISSION_GRANTED;
 }
 private void useLocation(){
  if(!safe())return;
  if(!locationPermission()){
   new AlertDialog.Builder(activity).setTitle("Use approximate location?")
    .setMessage("Orion will request Android's approximate location permission. After you agree, it sends a rounded location (about 1 km precision) to Open-Meteo to fetch an ECMWF forecast. You can choose a town instead or clear the saved location at any time.")
    .setNegativeButton("Choose a town",(d,i)->chooseTown())
    .setPositiveButton("Continue",(d,i)->activity.requestPermissions(new String[]{Manifest.permission.ACCESS_COARSE_LOCATION},LOCATION_REQUEST)).show();
   return;
  }
  locate();
 }
 void onPermissionResult(boolean granted){
  if(!safe())return;
  if(granted)locate();
  else {Toast.makeText(activity,"Location declined — choose a town instead",Toast.LENGTH_SHORT).show();chooseTown();}
 }
 private void locate(){
  if(locationManager==null){chooseTown();return;}
  try{
   Location last=null;
   for(String provider:new String[]{LocationManager.NETWORK_PROVIDER,LocationManager.PASSIVE_PROVIDER}){
    if(!locationManager.isProviderEnabled(provider))continue;
    Location loc=locationManager.getLastKnownLocation(provider);
    if(loc!=null&&(last==null||loc.getTime()>last.getTime()))last=loc;
   }
   if(last!=null&&System.currentTimeMillis()-last.getTime()<30*60*1000L){useCoordinates(last);return;}
   String provider=locationManager.isProviderEnabled(LocationManager.NETWORK_PROVIDER)?LocationManager.NETWORK_PROVIDER:null;
   if(provider==null){Toast.makeText(activity,"Turn on Android location or choose a town",Toast.LENGTH_LONG).show();return;}
   Toast.makeText(activity,"Finding approximate location…",Toast.LENGTH_SHORT).show();
   if(listener!=null)locationManager.removeUpdates(listener);
   listener=new LocationListener(){
    @Override public void onLocationChanged(Location location){
     if(locationManager!=null)locationManager.removeUpdates(this);
     listener=null;if(safe())useCoordinates(location);
    }
    @Override public void onProviderDisabled(String provider){}
    @Override public void onProviderEnabled(String provider){}
    @Override public void onStatusChanged(String provider,int status,android.os.Bundle extras){}
   };
   LocationListener pending=listener;
   locationManager.requestLocationUpdates(provider,0,0,pending,Looper.getMainLooper());
   main.postDelayed(()->{
    if(listener==pending){locationManager.removeUpdates(pending);listener=null;
     if(safe())Toast.makeText(activity,"Location unavailable — choose a town",Toast.LENGTH_LONG).show();}
   },12000);
  }catch(SecurityException e){Toast.makeText(activity,"Android location permission required",Toast.LENGTH_LONG).show();}
   catch(Exception e){Toast.makeText(activity,"Location unavailable — choose a town",Toast.LENGTH_LONG).show();}
 }
 private void useCoordinates(Location location){
  // Use Android's coarse location only; send and store rounded coordinates, not raw GPS data.
  double lat=Math.round(location.getLatitude()*100.0)/100.0;
  double lon=Math.round(location.getLongitude()*100.0)/100.0;
  if(!Double.isFinite(lat)||!Double.isFinite(lon))return;
  boolean changed=!"gps".equals(prefs().getString(PREFIX+"mode",""))||
   !Double.toString(lat).equals(prefs().getString(PREFIX+"lat",""))||
   !Double.toString(lon).equals(prefs().getString(PREFIX+"lon",""));
  android.content.SharedPreferences.Editor edit=prefs().edit().putString(PREFIX+"mode","gps")
   .putString(PREFIX+"label",changed?"Near your location":prefs().getString(PREFIX+"label","Near your location"))
   .putString(PREFIX+"lat",Double.toString(lat)).putString(PREFIX+"lon",Double.toString(lon));
  if(changed)edit.remove(PREFIX+"cache");
  edit.apply();
  // Reverse geocode off the UI thread. Weather still works when Android has no geocoder.
  final int lookup=++sequence;
  new Thread(()->{
   String label=changed?"Near your location":prefs().getString(PREFIX+"label","Near your location");
   try{
    if(Geocoder.isPresent()){
     java.util.List<Address> addresses=new Geocoder(activity,Locale.getDefault()).getFromLocation(lat,lon,1);
     if(addresses!=null&&!addresses.isEmpty()){
      Address address=addresses.get(0);
      String locality=address.getLocality();
      if(locality==null||locality.isEmpty())locality=address.getSubAdminArea();
      if(locality!=null&&!locality.isEmpty())label=locality;
     }
    }
   }catch(Exception ignored){}
   final String chosenLabel=label;
   main.post(()->{
    if(!safe()||lookup!=sequence)return;
    prefs().edit().putString(PREFIX+"label",chosenLabel).apply();
    fetch(lat,lon,chosenLabel);
   });
  },"orion-weather-area").start();
 }
 private void chooseTown(){
  if(!safe())return;
  EditText input=new EditText(activity);input.setHint("Town or city");input.setSingleLine(true);
  input.setInputType(InputType.TYPE_CLASS_TEXT|InputType.TYPE_TEXT_FLAG_CAP_WORDS);
  input.setPadding(dp(18),dp(12),dp(18),dp(12));
  new AlertDialog.Builder(activity).setTitle("Choose a weather location")
   .setMessage("Search by town or city. No device GPS permission is needed.")
   .setView(input).setNegativeButton("Cancel",null).setPositiveButton("Search",(d,i)->lookupTown(input.getText().toString().trim())).show();
 }
 private void lookupTown(String name){
  if(name.length()<2||name.length()>80){Toast.makeText(activity,"Enter a town name (2–80 characters)",Toast.LENGTH_LONG).show();return;}
  final int token=++sequence;
  Toast.makeText(activity,"Searching for "+name+"…",Toast.LENGTH_SHORT).show();
  new Thread(()->{
   try{
    JSONObject json=getJson("https://geocoding-api.open-meteo.com/v1/search?name="+
     java.net.URLEncoder.encode(name,"UTF-8")+"&count=5&language=en&format=json");
    JSONArray results=json.optJSONArray("results");
    if(results==null||results.length()==0)throw new IllegalStateException("No matching town");
    final JSONArray matches=results;
    main.post(()->{
     if(!safe()||token!=sequence)return;
     String[] choices=new String[matches.length()];
     for(int i=0;i<choices.length;i++){
      JSONObject item=matches.optJSONObject(i);
      choices[i]=item.optString("name","Unknown")+", "+item.optString("country","")
       +(item.has("admin1")?" · "+item.optString("admin1"):"");
     }
     new AlertDialog.Builder(activity).setTitle("Choose a town").setItems(choices,(d,which)->{
      JSONObject item=matches.optJSONObject(which);
      double lat=item.optDouble("latitude",Double.NaN),lon=item.optDouble("longitude",Double.NaN);
      if(!Double.isFinite(lat)||!Double.isFinite(lon))return;
      lat=Math.round(lat*100.0)/100.0;lon=Math.round(lon*100.0)/100.0;
      prefs().edit().putString(PREFIX+"mode","town").putString(PREFIX+"label",choices[which])
       .putString(PREFIX+"lat",Double.toString(lat)).putString(PREFIX+"lon",Double.toString(lon))
       .remove(PREFIX+"cache").apply();
      fetch(lat,lon,choices[which]);
     }).setNegativeButton("Cancel",null).show();
    });
   }catch(Exception e){showError(token,"Town search: "+e.getMessage());}
  },"orion-weather-town").start();
 }
 private void refreshSaved(){
  String mode=prefs().getString(PREFIX+"mode","");
  if("gps".equals(mode)){useLocation();return;}
  if(!"town".equals(mode))return;
  try{fetch(Double.parseDouble(prefs().getString(PREFIX+"lat","")),Double.parseDouble(prefs().getString(PREFIX+"lon","")),
   prefs().getString(PREFIX+"label","Selected town"));}
  catch(Exception e){Toast.makeText(activity,"Choose a town again",Toast.LENGTH_SHORT).show();}
 }
 private void fetch(double lat,double lon,String label){
  if(!safe()||!Double.isFinite(lat)||!Double.isFinite(lon)||Math.abs(lat)>90||Math.abs(lon)>180)return;
  final int token=++sequence;
  Toast.makeText(activity,"Updating ECMWF forecast…",Toast.LENGTH_SHORT).show();
  final String endpoint=String.format(Locale.US,
    "https://api.open-meteo.com/v1/ecmwf?latitude=%.2f&longitude=%.2f&hourly=temperature_2m,apparent_temperature,relative_humidity_2m,wind_speed_10m,precipitation,weather_code,is_day&timezone=auto&forecast_days=5&timeformat=unixtime",lat,lon);
  new Thread(()->{
   try{
    JSONObject json=getJson(endpoint);
    JSONObject hourly=json.getJSONObject("hourly");
    if(hourly.getJSONArray("time").length()<12)throw new IllegalStateException("Forecast missing");
    String raw=json.toString();
    main.post(()->{
     if(!safe()||token!=sequence)return;
     prefs().edit().putString(PREFIX+"cache",raw).putLong(PREFIX+"updated",System.currentTimeMillis()).apply();
     render();
    });
   }catch(Exception e){showError(token,"Weather unavailable: "+e.getMessage());}
  },"orion-weather-ecmwf").start();
 }
 private void showError(int token,String message){
  main.post(()->{if(!safe()||token!=sequence)return;
   Toast.makeText(activity,message,Toast.LENGTH_LONG).show();
   // Keep previously cached forecast visible when the connection is unavailable.
  });
 }
 private static JSONObject getJson(String url)throws Exception{
  HttpsURLConnection conn=(HttpsURLConnection)new URL(url).openConnection();
  try{
   conn.setConnectTimeout(7000);conn.setReadTimeout(9000);
   conn.setInstanceFollowRedirects(false);conn.setRequestProperty("Accept","application/json");
   if(conn.getResponseCode()!=200)throw new IllegalStateException("HTTP "+conn.getResponseCode());
   try(InputStream in=conn.getInputStream()){
    ByteArrayOutputStream out=new ByteArrayOutputStream();byte[] buffer=new byte[4096];int read;
    while((read=in.read(buffer))!=-1){
     if(out.size()+read>196608)throw new IllegalStateException("Response too large");
     out.write(buffer,0,read);
    }
    return new JSONObject(out.toString("UTF-8"));
   }
  }finally{conn.disconnect();}
 }
 static void settings(MainActivity a){
  boolean enabled=enabled(a);
  new AlertDialog.Builder(a).setTitle("Homepage weather")
   .setMessage("ECMWF IFS forecasts provided by Open-Meteo. Orion only requests approximate location when you choose to allow it. The selected town or rounded coordinates and cached forecast are stored on this device. Opening Orion will refresh saved forecasts when they are old. Forecasts are predictions, not live weather observations.\n\nWeather is currently "+(enabled?"On":"Off")+".")
   .setNegativeButton("Close",null)
   .setNeutralButton("Clear saved location",(d,i)->{
    android.content.SharedPreferences.Editor e=a.getPreferences(0).edit();
    for(String key:new String[]{"mode","label","lat","lon","cache","updated"})e.remove(PREFIX+key);
    e.apply();refreshHome(a);
   })
   .setPositiveButton(enabled?"Hide weather":"Show weather",(d,i)->{
    a.getPreferences(0).edit().putBoolean(PREFIX+"enabled",!enabled).apply();
    refreshHome(a);
   }).show();
 }
 private static void refreshHome(MainActivity a){
  if(a.homeOverlay!=null)a.showHome(a.geckoActive);
 }
 static String description(int code,boolean day){
  if(code==0)return day?"Sunny":"Clear night";
  if(code==1||code==2)return "Partly cloudy";
  if(code==3)return "Cloudy";
  if(code==45||code==48)return "Foggy";
  if(code==51||code==53||code==55||code==56||code==57)return "Drizzle";
  if(code==61||code==63||code==65||code==66||code==67||code==80||code==81||code==82)return "Rain";
  if(code==71||code==73||code==75||code==77||code==85||code==86)return "Snow";
  if(code==95||code==96||code==99)return "Thunderstorms";
  return "Mixed weather";
 }
 /** Vector-like Canvas drawing avoids emoji inconsistencies across Android fonts. */
 static final class WeatherIcon extends View {
  final Paint paint=new Paint(Paint.ANTI_ALIAS_FLAG);
  final int code;final boolean day;
  WeatherIcon(Context c,int code,boolean day){
   super(c);this.code=code;this.day=day;
   setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
  }
  private void line(Canvas c,float a,float b,float x,float y,int colour,float width){
   paint.setColor(colour);paint.setStrokeWidth(width);paint.setStrokeCap(Paint.Cap.ROUND);
   c.drawLine(a,b,x,y,paint);
  }
  @Override protected void onDraw(Canvas original){
   super.onDraw(original);
   Canvas c=original;float scale=Math.min(getWidth(),getHeight())/80f;
   c.save();c.translate((getWidth()-80*scale)/2,(getHeight()-80*scale)/2);c.scale(scale,scale);
   boolean clear=code==0,partial=code==1||code==2;
   boolean raining=code>=51&&code<=67||code>=80&&code<=82;
   boolean snowy=code>=71&&code<=77||code==85||code==86;
   boolean thunder=code>=95,cloudy=!clear,night=!day;
   if(clear||partial){
    if(night){
     paint.setColor(0xffd5ddff);c.drawCircle(38,33,18,paint);
     paint.setColor(0xff26354e);c.drawCircle(46,24,17,paint);
     paint.setColor(0xffffe49a);c.drawCircle(18,19,2.2f,paint);c.drawCircle(60,13,2.2f,paint);
    }else{
     paint.setColor(0xffffc755);c.drawCircle(37,30,17,paint);
     for(int i=0;i<8;i++){
      double a=i*Math.PI/4;line(c,37+(float)Math.cos(a)*23,30+(float)Math.sin(a)*23,
        37+(float)Math.cos(a)*30,30+(float)Math.sin(a)*30,0xffffcc64,4);
     }
    }
   }
   if(cloudy){
    paint.setColor(code==45||code==48?0xffa3aeca:0xffd6e5ff);
    c.drawCircle(28,48,14,paint);c.drawCircle(43,40,18,paint);c.drawCircle(57,48,13,paint);
    c.drawRoundRect(17,44,69,59,8,8,paint);
    paint.setColor(0xffa9c4e8);c.drawRoundRect(23,55,66,60,3,3,paint);
   }
   if(raining){for(int i=0;i<3;i++)line(c,28+i*12,63,24+i*12,71,0xff60beff,3);}
   if(snowy){for(int i=0;i<3;i++){
    float x=28+i*12;line(c,x,63,x,73,0xffbcf2ff,2);
    line(c,x-4,68,x+4,68,0xffbcf2ff,2);
   }}
   if(thunder){Path bolt=new Path();bolt.moveTo(43,57);bolt.lineTo(36,69);bolt.lineTo(44,69);
    bolt.lineTo(39,79);bolt.lineTo(55,64);bolt.lineTo(47,64);bolt.close();
    paint.setColor(0xffffd064);c.drawPath(bolt,paint);}
   c.restore();
  }
 }
}
