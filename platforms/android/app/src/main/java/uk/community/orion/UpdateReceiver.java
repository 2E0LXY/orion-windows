package uk.community.orion;
import android.content.*;
import android.content.pm.PackageInstaller;
import android.os.Build;

/** Receives PackageInstaller results for the in-app updater. */
public final class UpdateReceiver extends BroadcastReceiver {
 @SuppressWarnings("deprecation")
 @Override public void onReceive(Context context,Intent intent){
  int status=intent.getIntExtra(PackageInstaller.EXTRA_STATUS,PackageInstaller.STATUS_FAILURE);
  String message=intent.getStringExtra(PackageInstaller.EXTRA_STATUS_MESSAGE);
  if(status==PackageInstaller.STATUS_PENDING_USER_ACTION){
   Intent confirm=Build.VERSION.SDK_INT>=33?intent.getParcelableExtra(Intent.EXTRA_INTENT,Intent.class):intent.getParcelableExtra(Intent.EXTRA_INTENT);
   if(confirm!=null){
    confirm.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
    try{context.startActivity(confirm);}catch(Exception e){UpdateChecker.status("Open Orion and tap to continue the install",true);}
   }
   return;
  }
  if(status==PackageInstaller.STATUS_SUCCESS){UpdateChecker.status("Update installed",true);return;}
  if(status==PackageInstaller.STATUS_FAILURE_ABORTED){UpdateChecker.status("Install cancelled · tap to retry",true);return;}
  if(status==PackageInstaller.STATUS_FAILURE_CONFLICT||status==PackageInstaller.STATUS_FAILURE_INCOMPATIBLE){
   UpdateChecker.status("Update rejected: signed with a different key. Uninstall Orion once, then install the new APK (browser data is lost). "+(message==null?"":message),true);return;
  }
  UpdateChecker.status("Install failed: "+(message==null?"status "+status:message)+" · tap to retry",true);
 }
}
