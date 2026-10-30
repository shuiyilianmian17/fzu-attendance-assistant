using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
namespace FzuAttendance {
static class EmulatorHost {
 static IntPtr desktop;
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct StartupInfo {public int size;public string reserved,desktop,title;public int x,y,width,height,charsX,charsY,fill,flags;public short show,reserved2;public IntPtr reservedPtr,input,output,error;}
 [StructLayout(LayoutKind.Sequential)]struct ProcessInfo {public IntPtr process,thread;public int pid,tid;}
 [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr CreateDesktop(string name,IntPtr device,IntPtr mode,int flags,uint access,IntPtr security);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool CreateProcess(string app,StringBuilder command,IntPtr processSecurity,IntPtr threadSecurity,bool inherit,uint flags,IntPtr environment,string directory,ref StartupInfo startup,out ProcessInfo info);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
 [DllImport("user32.dll")]static extern IntPtr GetThreadDesktop(uint thread);
 [DllImport("kernel32.dll")]static extern uint GetCurrentThreadId();
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern bool GetUserObjectInformation(IntPtr handle,int index,StringBuilder value,int length,out int needed);
 static string DesktopName(IntPtr handle){var name=new StringBuilder(256);int needed;return GetUserObjectInformation(handle,2,name,512,out needed)?name.ToString():null;}
 public static bool OnCurrentDesktop(IntPtr window){uint pid;uint thread=GetWindowThreadProcessId(window,out pid);string target=DesktopName(GetThreadDesktop(thread)),current=DesktopName(GetThreadDesktop(GetCurrentThreadId()));return target!=null&&target==current;}
 public static bool StartMuMu(Config config){
  if(desktop==IntPtr.Zero)desktop=CreateDesktop("FzuAttendanceAndroid",IntPtr.Zero,IntPtr.Zero,0,0x01FF,IntPtr.Zero);
  if(desktop==IntPtr.Zero)return false;
  var startup=new StartupInfo{size=Marshal.SizeOf(typeof(StartupInfo)),desktop=@"winsta0\FzuAttendanceAndroid",flags=1,show=0};ProcessInfo info;
  string args="\""+config.EmulatorPath+"\" control -v "+config.EmulatorIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)+" launch";
  if(!CreateProcess(config.EmulatorPath,new StringBuilder(args),IntPtr.Zero,IntPtr.Zero,false,0x08000000,IntPtr.Zero,Path.GetDirectoryName(config.EmulatorPath),ref startup,out info))return false;
  CloseHandle(info.thread);CloseHandle(info.process);return true;
 }
 public static bool QuitHidden(Config config){
  if(desktop==IntPtr.Zero)desktop=CreateDesktop("FzuAttendanceAndroid",IntPtr.Zero,IntPtr.Zero,0,0x01FF,IntPtr.Zero);
  if(desktop==IntPtr.Zero)return false;
  var startup=new StartupInfo{size=Marshal.SizeOf(typeof(StartupInfo)),desktop=@"winsta0\FzuAttendanceAndroid",flags=1,show=0};ProcessInfo info;
  string command="\""+config.EmulatorPath+"\" quit --index "+config.EmulatorIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
  if(!CreateProcess(config.EmulatorPath,new StringBuilder(command),IntPtr.Zero,IntPtr.Zero,false,0x08000000,IntPtr.Zero,Path.GetDirectoryName(config.EmulatorPath),ref startup,out info))return false;
  CloseHandle(info.thread);CloseHandle(info.process);return true;
 }
 public static bool Start(Config config){
  // Retain the desktop handle while the child initializes its windowing runtime.
  if(desktop==IntPtr.Zero)desktop=CreateDesktop("FzuAttendanceAndroid",IntPtr.Zero,IntPtr.Zero,0,0x01FF,IntPtr.Zero);
  if(desktop==IntPtr.Zero)return false;
  string app=Path.Combine(Path.GetDirectoryName(config.EmulatorPath),"dnplayer.exe");if(!File.Exists(app))return false;
  var startup=new StartupInfo{size=Marshal.SizeOf(typeof(StartupInfo)),desktop=@"winsta0\FzuAttendanceAndroid"};ProcessInfo info;
  string command="\""+app+"\" index="+config.EmulatorIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
  if(!CreateProcess(app,new StringBuilder(command),IntPtr.Zero,IntPtr.Zero,false,0,IntPtr.Zero,Path.GetDirectoryName(app),ref startup,out info))return false;
  CloseHandle(info.thread);CloseHandle(info.process);return true;
 }
}
}
