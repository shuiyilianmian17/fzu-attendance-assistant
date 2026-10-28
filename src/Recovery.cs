using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FzuAttendance {
static class Recovery {
 const string DesktopName="FzuAttendanceBackground";
 static IntPtr androidDesktop;
 delegate bool EnumProc(IntPtr window,IntPtr arg);
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct StartupInfo {public int size;public string reserved,desktop,title;public int x,y,width,height,charsX,charsY,fill,flags;public short show,reserved2;public IntPtr reservedPtr,input,output,error;}
 [StructLayout(LayoutKind.Sequential)]struct ProcessInfo {public IntPtr process,thread;public int pid,tid;}
 [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr CreateDesktop(string name,IntPtr device,IntPtr mode,int flags,uint access,IntPtr security);
 [DllImport("user32.dll")]static extern bool CloseDesktop(IntPtr desktop);
 [DllImport("user32.dll")]static extern bool SetProcessDPIAware();
 [DllImport("user32.dll")]static extern bool EnumDesktopWindows(IntPtr desktop,EnumProc callback,IntPtr arg);
 [DllImport("user32.dll")]static extern bool EnumChildWindows(IntPtr window,EnumProc callback,IntPtr arg);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr window,StringBuilder text,int size);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr window,StringBuilder text,int size);
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint id);
 [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr window,int cmd);
 [DllImport("user32.dll")]static extern bool PostMessage(IntPtr window,uint msg,IntPtr w,IntPtr l);
 [DllImport("user32.dll")]static extern uint GetDpiForWindow(IntPtr window);
 [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool CreateProcess(string app,StringBuilder command,IntPtr processSecurity,IntPtr threadSecurity,bool inherit,uint flags,IntPtr environment,string directory,ref StartupInfo startup,out ProcessInfo info);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
 public class Owner {public int Pid;public long Started;public string Path;}
 public static bool StartAndroid(Config config){if(androidDesktop==IntPtr.Zero)androidDesktop=CreateDesktop("FzuAttendanceAndroid",IntPtr.Zero,IntPtr.Zero,0,0x01FF,IntPtr.Zero);if(androidDesktop==IntPtr.Zero)return false;string app=Path.Combine(Path.GetDirectoryName(config.EmulatorPath),"dnplayer.exe");if(!File.Exists(app))return false;var startup=new StartupInfo{size=Marshal.SizeOf(typeof(StartupInfo)),desktop=@"winsta0\FzuAttendanceAndroid"};ProcessInfo info;string command="\""+app+"\" index="+config.EmulatorIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);if(!CreateProcess(app,new StringBuilder(command),IntPtr.Zero,IntPtr.Zero,false,0,IntPtr.Zero,Path.GetDirectoryName(app),ref startup,out info))return false;CloseHandle(info.thread);CloseHandle(info.process);return true;}
 static IntPtr Find(IntPtr desktop,string title,int pid){IntPtr result=IntPtr.Zero;EnumDesktopWindows(desktop,delegate(IntPtr h,IntPtr arg){uint p;GetWindowThreadProcessId(h,out p);var text=new StringBuilder(256);GetWindowText(h,text,256);if(p==pid&&text.ToString()==title)result=h;return true;},IntPtr.Zero);return result;}
 static bool Navigate(IntPtr root,int x,int y){var children=new List<IntPtr>();EnumChildWindows(root,delegate(IntPtr h,IntPtr arg){var name=new StringBuilder(128);GetClassName(h,name,128);if(name.ToString()=="FLUTTERVIEW")children.Add(h);return true;},IntPtr.Zero);if(children.Count!=1)return false;var child=children[0];double scale=GetDpiForWindow(child)/96.0;if(scale<0.5||scale>5)return false;int px=(int)(x*scale),py=(int)(y*scale);IntPtr point=new IntPtr((py<<16)|(px&65535));return PostMessage(child,0x0200,IntPtr.Zero,point)&&PostMessage(child,0x0201,new IntPtr(1),point)&&PostMessage(child,0x0202,IntPtr.Zero,point);}
 static bool Owns(Owner owner,string path,Process process){try{return owner!=null&&owner.Pid==process.Id&&owner.Started==process.StartTime.ToUniversalTime().Ticks&&String.Equals(owner.Path,path,StringComparison.OrdinalIgnoreCase)&&String.Equals(process.MainModule.FileName,path,StringComparison.OrdinalIgnoreCase);}catch{return false;}}
 public static bool StopOwned(string path){try{Owner owner=Store.Json.Deserialize<Owner>(File.ReadAllText(Store.FilePath("native-owner.json")));using(var p=Process.GetProcessById(owner.Pid)){if(!Owns(owner,path,p))return false;p.Kill();return p.WaitForExit(5000);}}catch{return !Process.GetProcessesByName("fdxy_desktop").Any(p=>{try{return String.Equals(p.MainModule.FileName,path,StringComparison.OrdinalIgnoreCase);}catch{return true;}finally{p.Dispose();}});}}
 public static async Task<int> Run(Config config){
  // No SwitchDesktop, SendInput, cursor setters, foreground activation, or default-desktop navigation.
  SetProcessDPIAware();IntPtr before=GetForegroundWindow();IntPtr desktop=CreateDesktop(DesktopName,IntPtr.Zero,IntPtr.Zero,0,0x01FF,IntPtr.Zero);
  if(desktop==IntPtr.Zero){Store.Log("无法建立后台桌面："+Marshal.GetLastWin32Error());return 3;}
  try {
   try{if(await Cdp.Target()!=null)return 0;}catch{}
   int pid=0;IntPtr root=IntPtr.Zero;
   var existing=Process.GetProcessesByName("fdxy_desktop");foreach(var p in existing){try{if(!String.Equals(p.MainModule.FileName,config.ClientPath,StringComparison.OrdinalIgnoreCase))continue;var h=Find(desktop,"智汇福大",p.Id);if(h!=IntPtr.Zero){pid=p.Id;root=h;break;}}catch{}finally{p.Dispose();}}
   if(pid==0&&existing.Length>0){Store.Log("原客户端在普通桌面运行，未强制终止它。请彻底退出原客户端后重试。 ");return 2;}
   if(pid==0){
    var startup=new StartupInfo();startup.size=Marshal.SizeOf(startup);startup.desktop=@"winsta0\"+DesktopName;ProcessInfo info;
    Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS","--remote-debugging-port=19228 --remote-debugging-address=127.0.0.1");
    if(!CreateProcess(config.ClientPath,new StringBuilder("\""+config.ClientPath+"\""),IntPtr.Zero,IntPtr.Zero,false,0,IntPtr.Zero,Path.GetDirectoryName(config.ClientPath),ref startup,out info)){Store.Log("后台客户端启动失败："+Marshal.GetLastWin32Error());return 3;}
    CloseHandle(info.thread);CloseHandle(info.process);pid=info.pid;
    try{using(var p=Process.GetProcessById(pid)){File.WriteAllText(Store.FilePath("native-owner.json"),Store.Json.Serialize(new Owner{Pid=pid,Started=p.StartTime.ToUniversalTime().Ticks,Path=config.ClientPath}),Encoding.UTF8);}}catch{}
    for(int i=0;i<50;i++){root=Find(desktop,"智汇福大",pid);if(root!=IntPtr.Zero)break;await Task.Delay(200);}
   }
   if(root==IntPtr.Zero)return 3;
   // Showing a window on the separate, inactive desktop never displays it on the input desktop.
   ShowWindow(root,4);await Task.Delay(8000);
   if(!Navigate(root,30,190))return 3;await Task.Delay(4000);
   if(!Navigate(root,269,68))return 3;await Task.Delay(2500);
   if(!Navigate(root,424,135))return 3;
   for(int i=0;i<50;i++){await Task.Delay(500);try{if(await Cdp.Target()!=null){Store.Log("后台恢复成功：签到页已在独立隐藏桌面建立。前台窗口"+(before==GetForegroundWindow()?"未改变。":"由用户或其他程序改变；助手未调用前台切换。"));return 0;}}catch{}}
   Store.Log("后台恢复未完成，可能网络未就绪、登录失效或客户端界面已变化。");return 3;
  }finally{CloseDesktop(desktop);}
 }
}
}
