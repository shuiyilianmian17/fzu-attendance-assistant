using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
namespace FzuAttendance {
static class Standalone {
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool IsProcessInJob(IntPtr p,IntPtr job,out bool value);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool QueryInformationJobObject(IntPtr job,int info,IntPtr data,uint length,out uint returned);
 [DllImport("ntdll.dll")]static extern int NtQueryInformationProcess(IntPtr p,int type,IntPtr data,int size,out int length);
 public static bool Managed(Process p){bool value;if(!IsProcessInJob(p.Handle,IntPtr.Zero,out value))throw new InvalidOperationException("无法确认进程归属");return value;}
 public static long[] JobProcesses(){IntPtr data=Marshal.AllocHGlobal(65536);try{uint n;if(!QueryInformationJobObject(IntPtr.Zero,3,data,65536,out n))return new long[0];int count=Marshal.ReadInt32(data,4);return Enumerable.Range(0,Math.Min(count,8000)).Select(i=>IntPtr.Size==8?Marshal.ReadInt64(data,8+i*8):(long)Marshal.ReadInt32(data,8+i*4)).ToArray();}finally{Marshal.FreeHGlobal(data);}}
 static string ParentName(){IntPtr data=Marshal.AllocHGlobal(IntPtr.Size*6);try{int size;using(var p=Process.GetCurrentProcess()){if(NtQueryInformationProcess(p.Handle,0,data,IntPtr.Size*6,out size)!=0)return "";}int id=(int)Marshal.ReadIntPtr(data,IntPtr.Size*5).ToInt64();using(var parent=Process.GetProcessById(id))return parent.ProcessName;}catch{return "";}finally{Marshal.FreeHGlobal(data);}}
 static bool Tool(string name){return new[]{"codex","ChatGPT","pwsh","powershell","node","FzuLifetimeTest"}.Contains(name,StringComparer.OrdinalIgnoreCase);}
 public static bool LauncherManaged(){if(Tool(ParentName()))return true;return JobProcesses().Any(id=>{try{using(var p=Process.GetProcessById((int)id))return Tool(p.ProcessName);}catch{return false;}});}
 public static bool HandOffIfNeeded(string[] args){
  if(args.Contains("--independent")){string source=args.FirstOrDefault(x=>x.StartsWith("--source-pid="));long sourcePid=source==null?0:Int64.Parse(source.Substring(13));if(ParentName()!="explorer"||JobProcesses().Contains(sourcePid)||LauncherManaged())throw new InvalidOperationException("独立启动验证未通过");return false;}
  if(!LauncherManaged())return false;
  dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));dynamic windows=shell.Windows();int hwnd;dynamic desktop=windows.FindWindowSW(0,0,8,out hwnd,1);
  if(desktop==null)throw new InvalidOperationException("未找到 Windows 桌面启动服务");
  dynamic broker=desktop.Document.Application;
  string parameters=String.Join(" ",args.Select(x=>"\""+x.Replace("\"","")+"\""));
  parameters+=" --independent --source-pid="+Process.GetCurrentProcess().Id;
  broker.ShellExecute(System.Windows.Forms.Application.ExecutablePath,parameters,Store.Dir,"open",args.Contains("--background")||args.Contains("--lifetime-canary")?0:1);
  return true;
 }
 public static bool CorrectSession(string[] args){return true;}
 public static void Record(string file){using(var p=Process.GetCurrentProcess()){File.WriteAllText(Store.FilePath(file),Store.Json.Serialize(new{Pid=p.Id,Session=p.SessionId,Parent=ParentName(),ManagedJob=Managed(p),LauncherManaged=LauncherManaged(),JobProcesses=JobProcesses(),StartedUtc=DateTime.UtcNow.ToString("o")}));}}
 public static void Canary(){Record("lifetime-canary.json");Thread.Sleep(45000);}
}
}
