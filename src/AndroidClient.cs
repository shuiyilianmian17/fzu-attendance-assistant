using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace FzuAttendance {
sealed class AndroidNode {
 public string Text,Description,Package; public bool Web,Enabled; public int Left,Top,Right,Bottom; public double ScaleX=1,ScaleY=1,OffsetX,OffsetY;
 public string Label {get{return String.IsNullOrEmpty(Text)?Description:Text;}}
 public int X {get{return (int)Math.Round((Left+Right)*0.5*ScaleX+OffsetX);}}
 public int Y {get{return (int)Math.Round((Top+Bottom)*0.5*ScaleY+OffsetY);}}
}
sealed class AndroidPage {
 public List<AndroidNode> Nodes=new List<AndroidNode>(); public int Width,Height;
 public string Text {get{return String.Join("\n",Nodes.Where(n=>n.Web).Select(n=>n.Label));}}
 public string Date {get{return Regex.Match(Text,@"\b20\d{2}-\d{2}-\d{2}\b").Value;}}
 public bool Attendance {get{return Nodes.Any(n=>!n.Web&&n.Package==AndroidClient.Package&&n.Label=="晚点名签到")&&Nodes.Any(n=>n.Web);}}
 public bool Confirmed {get{return Attendance&&Nodes.Any(n=>n.Web&&(n.Label.Trim()=="已签到"||n.Label.Trim()=="签到成功"));}}
 public bool Locating {get{return Text.Contains("定位中");}}
 public bool Limited {get{return Text.Contains("定位服务流量限制");}}
 public bool LoginExpired {get{return Regex.IsMatch(Text,@"登录.*失效|请.*登录|token.*失效|身份验证失败",RegexOptions.IgnoreCase);}}
 public AndroidNode Ready {get{return Nodes.FirstOrDefault(n=>n.Web&&n.Enabled&&(n.Label.Trim()=="点击签到"||n.Label.Trim()=="立即签到"||n.Label.Trim()=="签到"));}}
 public bool CanSubmit(DateTime now){return Attendance&&Date==now.ToString("yyyy-MM-dd")&&Rules.InWindow(now)&&!Confirmed&&Ready!=null&&Reason=="可签到"&&!LoginExpired;}
 public string Reason {get{foreach(string message in new[]{"不在签到范围内","未到签到时间","定位失败","请开启定位","无法调用","定位服务流量限制"})if(Text.Contains(message))return message;return Locating?"定位中":Ready!=null?"可签到":Confirmed?"已签到":"页面尚未准备好";}}
}
static class AndroidClient {
 public const string Package="cn.edu.fzu.fdxypa";
 static readonly SemaphoreSlim DeviceGate=new SemaphoreSlim(1,1);
 public static bool Selected(Config c){return String.Equals(c.Backend,"android",StringComparison.OrdinalIgnoreCase);}
 public static bool ValidConsole(string path){return !String.IsNullOrWhiteSpace(path)&&File.Exists(path)&&String.Equals(Path.GetFileName(path),"ldconsole.exe",StringComparison.OrdinalIgnoreCase)&&File.Exists(Path.Combine(Path.GetDirectoryName(path),"adb.exe"));}
 public static string Detect(string current){var paths=new List<string>{current,@"D:\leidian\LDPlayer14\ldconsole.exe",@"C:\leidian\LDPlayer14\ldconsole.exe",@"D:\leidian\LDPlayer9\ldconsole.exe",@"C:\leidian\LDPlayer9\ldconsole.exe"};foreach(string key in new[]{@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"}){try{using(var root=Microsoft.Win32.Registry.LocalMachine.OpenSubKey(key)){if(root==null)continue;foreach(var name in root.GetSubKeyNames())using(var child=root.OpenSubKey(name)){if(child==null||!Regex.IsMatch(Convert.ToString(child.GetValue("DisplayName")),"雷电|LDPlayer",RegexOptions.IgnoreCase))continue;string icon=Convert.ToString(child.GetValue("DisplayIcon")).Split(',')[0].Trim('"');if(!String.IsNullOrEmpty(icon))paths.Add(Path.Combine(Path.GetDirectoryName(icon),"ldconsole.exe"));}}}catch{}}return paths.FirstOrDefault(ValidConsole);}
 static string AdbPath(Config c){return Path.Combine(Path.GetDirectoryName(c.EmulatorPath),"adb.exe");}
 static string Device(Config c){return "emulator-"+(5554+c.EmulatorIndex*2).ToString(CultureInfo.InvariantCulture);}
 static string Index(Config c){if(c.EmulatorIndex<0||c.EmulatorIndex>99)throw new InvalidOperationException("模拟器编号应为 0–99。");return c.EmulatorIndex.ToString(CultureInfo.InvariantCulture);}
 public static string SavedPosition(Config c){Index(c);string path=Path.Combine(Path.GetDirectoryName(c.EmulatorPath),"vms","config","leidian"+Index(c)+".config");var cfg=Store.Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(path));object location;if(!cfg.TryGetValue("statusSettings.location",out location)||!(location is Dictionary<string,object>))throw new InvalidOperationException("请先在雷电右侧“定位”中选择当前位置并确认。");var p=(Dictionary<string,object>)location;double lng=Convert.ToDouble(p["lng"],CultureInfo.InvariantCulture),lat=Convert.ToDouble(p["lat"],CultureInfo.InvariantCulture);if(Double.IsNaN(lng)||Double.IsNaN(lat)||Double.IsInfinity(lng)||Double.IsInfinity(lat)||lng<-180||lng>180||lat<-90||lat>90)throw new InvalidOperationException("雷电保存的位置无效，请重新确认定位。");return lng.ToString("R",CultureInfo.InvariantCulture)+","+lat.ToString("R",CultureInfo.InvariantCulture);}
 static async Task<byte[]> Command(string path,string arguments,int seconds){var start=new ProcessStartInfo(path,arguments){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Path.GetDirectoryName(path)};using(var p=Process.Start(start))using(var output=new MemoryStream()){var copy=p.StandardOutput.BaseStream.CopyToAsync(output);var errors=p.StandardError.ReadToEndAsync();var exited=Task.Run(()=>p.WaitForExit(seconds*1000));if(!await exited){try{p.Kill();}catch{}throw new TimeoutException("模拟器控制超时。");}await copy;await errors;if(p.ExitCode!=0)throw new InvalidOperationException("模拟器未响应，请确认该实例已启动且 ADB 调试允许本机连接。");return output.ToArray();}}
 static async Task<string> Console(Config c,string args){if(!ValidConsole(c.EmulatorPath))throw new InvalidOperationException("请在运行环境页选择雷电的 ldconsole.exe。");return Encoding.UTF8.GetString(await Command(c.EmulatorPath,args,15));}
 static async Task<byte[]> Adb(Config c,string args,int seconds){return await Command(AdbPath(c),"-s "+Device(c)+" "+args,seconds);}
 static async Task<string> Shell(Config c,string args){return Encoding.UTF8.GetString(await Adb(c,"shell "+args,15));}
 [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr h,int cmd);
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32.dll")]internal static extern IntPtr GetForegroundWindow();
 static bool InstanceRunning(string line,Config c,out IntPtr window){window=IntPtr.Zero;var fields=line.Trim().Split(',');int index,run,pid;long handle;if(fields.Length<7||!Int32.TryParse(fields[0],out index)||index!=c.EmulatorIndex||!Int32.TryParse(fields[4],out run)||run!=1)return false;if(Int64.TryParse(fields[2],out handle)&&Int32.TryParse(fields[5],out pid)){uint owner;var h=new IntPtr(handle);GetWindowThreadProcessId(h,out owner);if(owner==(uint)pid)window=h;}return true;}
 public static async Task Hide(Config c){var list=await Console(c,"list2");foreach(var line in list.Split('\n')){IntPtr window;if(InstanceRunning(line,c,out window)&&window!=IntPtr.Zero)ShowWindow(window,0);}}
 static async Task Ensure(Config c,Action<string> state){Index(c);if(!ValidConsole(c.EmulatorPath))throw new InvalidOperationException("请在运行环境页选择雷电的 ldconsole.exe。");try{if((await Shell(c,"getprop sys.boot_completed")).Trim()=="1"){await Hide(c);return;}}catch{}string list=await Console(c,"list2");bool running=list.Split('\n').Any(line=>{IntPtr w;return InstanceRunning(line,c,out w);});if(!running){state("正在隐藏桌面启动雷电模拟器…");if(!Recovery.StartAndroid(c))throw new InvalidOperationException("雷电未能在隐藏桌面启动。");}for(int i=0;i<60;i++){await Hide(c);try{string ready=(await Shell(c,"getprop sys.boot_completed")).Trim();if(ready=="1")return;}catch{}await Task.Delay(1000);}throw new InvalidOperationException("模拟器启动尚未完成，请稍后重试。");}
 static int[] Bounds(string text){var matches=Regex.Matches(text??"",@"-?\d+");if(matches.Count!=4)return new int[4];return matches.Cast<Match>().Select(x=>Int32.Parse(x.Value,CultureInfo.InvariantCulture)).ToArray();}
 public static AndroidPage ParsePage(string xml){var doc=new XmlDocument{XmlResolver=null};doc.LoadXml(xml);var result=new AndroidPage();var root=doc.SelectSingleNode("/hierarchy/node") as XmlElement;if(root==null)throw new InvalidOperationException("没有读到模拟器画面。");var screen=Bounds(root.GetAttribute("bounds"));result.Width=screen[2]-screen[0];result.Height=screen[3]-screen[1];foreach(XmlElement e in doc.SelectNodes("//node")){var b=Bounds(e.GetAttribute("bounds"));var n=new AndroidNode{Text=e.GetAttribute("text"),Description=e.GetAttribute("content-desc"),Package=e.GetAttribute("package"),Enabled=e.GetAttribute("enabled")=="true",Left=b[0],Top=b[1],Right=b[2],Bottom=b[3]};XmlElement web=null;for(XmlNode a=e;a is XmlElement;a=a.ParentNode){if(((XmlElement)a).GetAttribute("class")=="android.webkit.WebView"){web=(XmlElement)a;}}n.Web=web!=null&&n.Package==Package;if(n.Web){var wb=Bounds(web.GetAttribute("bounds"));var parent=web.ParentNode as XmlElement;if(parent!=null){var pb=Bounds(parent.GetAttribute("bounds"));if(wb[2]>wb[0]&&wb[3]>wb[1]&&pb[2]>pb[0]&&pb[3]>pb[1]){n.ScaleX=(pb[2]-pb[0])/(double)(wb[2]-wb[0]);n.ScaleY=(pb[3]-pb[1])/(double)(wb[3]-wb[1]);n.OffsetX=pb[0]-wb[0]*n.ScaleX;n.OffsetY=pb[1]-wb[1]*n.ScaleY;}}}result.Nodes.Add(n);}return result;}
 public static async Task<AndroidPage> Read(Config c){await DeviceGate.WaitAsync();try{await Shell(c,"uiautomator dump /sdcard/fzu-attendance-ui.xml");string xml=await Shell(c,"cat /sdcard/fzu-attendance-ui.xml");return ParsePage(xml);}finally{DeviceGate.Release();}}
 static async Task Tap(Config c,AndroidPage page,AndroidNode node){if(node==null||node.Package!=Package||!node.Enabled||node.X<0||node.Y<0||node.X>=page.Width||node.Y>=page.Height||node.Right<=node.Left||node.Bottom<=node.Top)throw new InvalidOperationException("页面按钮不在可操作区域，请查看隐藏页。");await Shell(c,"input tap "+node.X.ToString(CultureInfo.InvariantCulture)+" "+node.Y.ToString(CultureInfo.InvariantCulture));}
 public static async Task RefreshPosition(Config c){string coordinates=SavedPosition(c);await Console(c,"action --index "+Index(c)+" --key call.locate --value "+coordinates);}
 static async Task<AndroidPage> OpenAttendance(Config c,bool restart,Action<string> state){if(restart){state("正在重新打开模拟器内的智汇福大…");await Shell(c,"am force-stop "+Package);}await Shell(c,"am start -n "+Package+"/cn.edu.fzu.fdxy_app.MainActivity");await Task.Delay(1500);bool serviceTapped=false;for(int i=0;i<10;i++){var page=await Read(c);if(page.Attendance){if(!String.IsNullOrEmpty(page.Date)&&page.Date!=Store.Now().ToString("yyyy-MM-dd")){await Shell(c,"input keyevent 4");await Task.Delay(700);continue;}return page;}var entry=page.Nodes.FirstOrDefault(n=>!n.Web&&n.Package==Package&&n.Label=="晚点名签到");if(entry!=null){await Tap(c,page,entry);await Task.Delay(1500);continue;}var service=page.Nodes.FirstOrDefault(n=>!n.Web&&n.Package==Package&&(n.Label.StartsWith("业务\n")||n.Label.StartsWith("服务\n")||n.Label=="业务"||n.Label=="服务"));if(service!=null){if(!serviceTapped){await Tap(c,page,service);serviceTapped=true;}else{int x=page.Width/2;await Shell(c,"input swipe "+x+" "+(page.Height*8/10)+" "+x+" "+(page.Height*3/10)+" 400");}await Task.Delay(1000);continue;}if(page.Nodes.Any(n=>n.Package==Package&&Regex.IsMatch(n.Label,"验证码|手机号|密码登录|登录失效")))throw new InvalidOperationException("请先在模拟器内登录智汇福大，再重试。");await Task.Delay(1000);}throw new InvalidOperationException("未找到晚点名入口，请确认登录并将晚点名签到加入“我的服务”。");}
 public static async Task<byte[]> Capture(Config c){await DeviceGate.WaitAsync();try{byte[] data=await Adb(c,"exec-out screencap -p",15);if(data.Length<8||data[0]!=137||data[1]!=80||data[2]!=78||data[3]!=71)throw new InvalidOperationException("模拟器画面暂不可用。");return data;}finally{DeviceGate.Release();}}
 public static async Task CloseApp(Config c){await Shell(c,"am force-stop "+Package);}
 public static async Task Check(Config c,bool dry,bool force,Func<bool> restartRequested,Action clearRestart,Action<string> state){
  IntPtr foreground=GetForegroundWindow();await Ensure(c,state);Store.Log("安卓后台连接已建立，前台窗口"+(foreground==GetForegroundWindow()?"未改变。":"由用户或其他程序改变；助手未请求前台切换。"));int restarts=0;
  while(true){bool restart=restartRequested();if(restart)clearRestart();var page=await OpenAttendance(c,restart,state);bool submit=!dry||(restart&&Rules.Due(c,Store.Now(),true));
   // A saved point is not a new GPS event. Publish it AFTER the page starts requesting location.
   await Task.Delay(1200);await RefreshPosition(c);state("已在后台重新发送雷电中已选的位置，正在等待晚点名读取定位…");DateTime began=DateTime.UtcNow,nextPosition=began.AddSeconds(5);DateTime? limitSince=null;bool retryOpen=false;
   for(int attempts=0;attempts<40;attempts++){if(restartRequested()){retryOpen=true;break;}page=await Read(c);string today=Store.Now().ToString("yyyy-MM-dd");if(page.Attendance&&page.Date==today&&page.Confirmed){c.SuccessDate=today;state("今日已签到，已确认安卓页面结果。");return;}
    if(page.LoginExpired)throw new InvalidOperationException("智汇福大登录已失效，请在模拟器中重新登录。");if(!page.Attendance)throw new InvalidOperationException("模拟器当前已离开晚点名页，请重新检查连接。");
    if(submit&&!force&&!c.Enabled){state("自动签到已暂停，已停止本次操作。");return;}
    if(page.Limited){if(!limitSince.HasValue)limitSince=DateTime.UtcNow;if(Engine.LimitExpired(limitSince,DateTime.UtcNow,true)){retryOpen=true;break;}}else limitSince=null;
    if(page.Date==today&&page.Ready!=null&&page.Reason=="可签到"&&!page.LoginExpired){if(!submit){state("定位刷新成功：安卓晚点名页面已显示“点击签到”；本次检查未提交。");return;}if(!Rules.InWindow(Store.Now())){state("已超出签到时段，未提交。");return;}
     // Read the live page again, including its date and enabled label, immediately before the tap.
     var live=await Read(c);if(restartRequested()){retryOpen=true;break;}if(!live.CanSubmit(Store.Now())||(!force&&!c.Enabled)){state("页面状态已变化，本次未提交；稍后重试。");return;}await Tap(c,live,live.Ready);state("已点击安卓页面签到按钮，正在核验结果…");for(int i=0;i<10;i++){await Task.Delay(1200);var checkedPage=await Read(c);if(checkedPage.Attendance&&checkedPage.Date==today&&checkedPage.Confirmed){c.SuccessDate=today;state("签到成功，已确认安卓晚点名页面显示“已签到”。");return;}}state("已点击安卓页面，但尚未确认成功；5 分钟后重新检查。");return;}
    if(page.Date==today&&!page.Locating&&!page.Limited&&page.Reason!="页面尚未准备好"){state("定位已刷新，当前页面提示："+page.Reason+"。稍后重试。");return;}
    if(DateTime.UtcNow>=nextPosition){await RefreshPosition(c);nextPosition=DateTime.UtcNow.AddSeconds(5);}if((DateTime.UtcNow-began).TotalSeconds>=110)break;await Task.Delay(1500);
   }
   if(retryOpen&&restarts++<2){state("定位等待已达到一分钟或收到重开请求，正在重新进入晚点名…");clearRestart();await CloseApp(c);await Task.Delay(600);await Shell(c,"am start -n "+Package+"/cn.edu.fzu.fdxy_app.MainActivity");continue;}
   state("已重新发送定位，但页面仍未准备好；5 分钟后重试，可查看隐藏页。");return;
  }
 }
}
}
