using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
namespace FzuAttendance {
static class MuMuClient {
 public static bool Is(Config c){return IsPath(c.EmulatorPath);}
 public static bool IsPath(string path){return String.Equals(Path.GetFileName(path),"mumu-cli.exe",StringComparison.OrdinalIgnoreCase)||String.Equals(Path.GetFileName(path),"MuMuManager.exe",StringComparison.OrdinalIgnoreCase);}
 static string Target(Config c){if(c.EmulatorIndex<0||c.EmulatorIndex>99)throw new InvalidOperationException("模拟器编号应为 0–99。");return " -v "+c.EmulatorIndex.ToString(CultureInfo.InvariantCulture);}
 public static async Task<string> Run(Config c,string args){return System.Text.Encoding.UTF8.GetString(await AndroidClient.Command(c.EmulatorPath,args,20));}
 public static async Task<Dictionary<string,object>> Info(Config c){var d=Store.Json.Deserialize<Dictionary<string,object>>(await Run(c,"info"+Target(c)));object error;if(d.TryGetValue("error_code",out error)&&Convert.ToInt32(error)!=0||d.TryGetValue("errcode",out error)&&Convert.ToInt32(error)!=0)throw new InvalidOperationException("未找到所选 MuMu 实例，请核对编号。");return d;}
 public static async Task<string> Device(Config c){var d=await Info(c);object port;if(!d.TryGetValue("adb_port",out port))throw new InvalidOperationException("MuMu 尚未提供 ADB 连接，请等待实例启动。");int p=Convert.ToInt32(port);if(p<1||p>65535)throw new InvalidOperationException("MuMu ADB 端口无效。");string serial="127.0.0.1:"+p;await AndroidClient.Command(Path.Combine(Path.GetDirectoryName(c.EmulatorPath),"adb.exe"),"connect "+serial,10);return serial;}
 public static string Position(Config c){
  Target(c);string root=Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(c.EmulatorPath)),"vms");string suffix="-"+c.EmulatorIndex.ToString(CultureInfo.InvariantCulture);string selected=null;
  // Use this instance's selected/default point, never another instance or a built-in point.
  foreach(string dir in Directory.GetDirectories(root,"MuMuPlayer-*")){if(!dir.EndsWith(suffix,StringComparison.Ordinal))continue;string file=Path.Combine(dir,"configs","customer_config.json");if(!File.Exists(file))continue;if(selected==null||File.GetLastWriteTimeUtc(file)>File.GetLastWriteTimeUtc(selected))selected=file;}
  if(selected==null)throw new InvalidOperationException("未找到 MuMu 实例的定位配置。");
  var all=Store.Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(selected));var data=(Dictionary<string,object>)all["customer"];
  double lat=Convert.ToDouble(data["default_location_latitute"],CultureInfo.InvariantCulture),lon=Convert.ToDouble(data["default_location_longtitute"],CultureInfo.InvariantCulture);
  if(Double.IsNaN(lon)||Double.IsNaN(lat)||Double.IsInfinity(lon)||Double.IsInfinity(lat)||lon<-180||lon>180||lat<-90||lat>90||(lon==0&&lat==0))throw new InvalidOperationException("请先在 MuMu 定位工具中确认实际位置并保存为默认定位。");
  return lon.ToString("R",CultureInfo.InvariantCulture)+","+lat.ToString("R",CultureInfo.InvariantCulture);
 }
 public static async Task Refresh(Config c){var point=Position(c).Split(',');await Run(c,"control"+Target(c)+" tool location -lon "+point[0]+" -lat "+point[1]);}
 public static async Task Hide(Config c){await Run(c,"control"+Target(c)+" hide_window");}
 public static async Task Ensure(Config c,Action<string> state){var d=await Info(c);object running;if(!d.TryGetValue("is_process_started",out running)||!Convert.ToBoolean(running)){state("正在后台启动 MuMu 模拟器…");if(!EmulatorHost.StartMuMu(c))throw new InvalidOperationException("MuMu 后台启动失败。");}
  for(int i=0;i<75;i++){d=await Info(c);try{await Hide(c);}catch{}object ready;if(d.TryGetValue("is_android_started",out ready)&&Convert.ToBoolean(ready)){await Device(c);return;}await Task.Delay(1000);}throw new InvalidOperationException("MuMu 启动尚未完成，请稍后重试。");}
 public static async Task Stop(Config c,Action<string> state){state("正在关闭所选 MuMu 模拟器…");await Run(c,"control"+Target(c)+" shutdown");for(int i=0;i<45;i++){var d=await Info(c);object value;if(d.TryGetValue("is_process_started",out value)&&!Convert.ToBoolean(value))return;await Task.Delay(800);}throw new InvalidOperationException("MuMu 尚未关闭，请稍后重试。");}
 public static async Task<bool> Visible(Config c){var d=await Info(c);object value;if(!d.TryGetValue("main_wnd",out value))return false;long h;if(!Int64.TryParse(Convert.ToString(value),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out h))return false;return AndroidClient.IsWindowVisible(new IntPtr(h))&&EmulatorHost.OnCurrentDesktop(new IntPtr(h));}
 public static async Task Open(Config c,Action<string> state){state("正在打开 MuMu 模拟器窗口…");var d=await Info(c);object running;object wnd;long handle;if(d.TryGetValue("main_wnd",out wnd)&&Int64.TryParse(Convert.ToString(wnd),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out handle)&&handle!=0&&!EmulatorHost.OnCurrentDesktop(new IntPtr(handle))){await Stop(c,state);d=await Info(c);}if(!d.TryGetValue("is_process_started",out running)||!Convert.ToBoolean(running)){using(var p=Process.Start(new ProcessStartInfo(c.EmulatorPath,"control"+Target(c)+" launch"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden})){} }
  for(int i=0;i<75;i++){await Run(c,"control"+Target(c)+" show_window");if(await Visible(c))return;await Task.Delay(1000);}throw new InvalidOperationException("MuMu 窗口尚未出现。");}
}
}
