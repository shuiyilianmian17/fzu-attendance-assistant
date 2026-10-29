using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace FzuAttendance {
sealed class AndroidNode {
 public string Text,Description,Package,ResourceId; public bool Web,Enabled,Clickable; public int Left,Top,Right,Bottom; public double ScaleX=1,ScaleY=1,OffsetX,OffsetY;
 public string Label {get{return String.IsNullOrWhiteSpace(Text)?Description:Text;}}
 public int X {get{return (int)Math.Round((Left+Right)*0.5*ScaleX+OffsetX);}}
 public int Y {get{return (int)Math.Round((Top+Bottom)*0.5*ScaleY+OffsetY);}}
}
sealed class AndroidPage {
 public List<AndroidNode> Nodes=new List<AndroidNode>(); public int Width,Height;
 public string Text {get{return String.Join("\n",Nodes.Where(n=>n.Web).Select(n=>n.Label));}}
 public string Date {get{return Regex.Match(Text,@"\b20\d{2}-\d{2}-\d{2}\b").Value;}}
 public bool Attendance {get{return !AndroidNavigation.HasEmulatorAd(this)&&Nodes.Any(n=>!n.Web&&n.Package==AndroidClient.Package&&!n.Clickable&&n.Top<Height/5&&AndroidNavigation.Matches(n,"晚点名签到"))&&Nodes.Any(n=>n.Web);}}
 public bool Confirmed {get{return Attendance&&Nodes.Any(n=>n.Web&&(n.Label.Trim()=="已签到"||n.Label.Trim()=="签到成功"));}}
 public bool Locating {get{return Text.Contains("定位中");}}
 public bool Limited {get{return Text.Contains("定位服务流量限制");}}
 public bool LoginExpired {get{return Regex.IsMatch(Text,@"登录.*失效|请.*登录|token.*失效|身份验证失败",RegexOptions.IgnoreCase);}}
 public AndroidNode Ready {get{return Nodes.FirstOrDefault(n=>n.Web&&n.Enabled&&(n.Label.Trim()=="点击签到"||n.Label.Trim()=="立即签到"||n.Label.Trim()=="签到"));}}
 public bool CanSubmit(DateTime now){return Attendance&&Date==now.ToString("yyyy-MM-dd")&&Rules.InWindow(now)&&!Confirmed&&Ready!=null&&Reason=="可签到"&&!LoginExpired;}
 public string Reason {get{foreach(string message in new[]{"不在签到范围内","未到签到时间","定位失败","请开启定位","无法调用","定位服务流量限制"})if(Text.Contains(message))return message;return Locating?"定位中":Ready!=null?"可签到":Confirmed?"已签到":"页面尚未准备好";}}
}
static class AndroidNavigation {
 internal const string AdPackage="com.android.flysilkworm";
 public static bool HasEmulatorAd(AndroidPage page){return page.Nodes.Any(n=>n.Package==AdPackage);}
 public static AndroidNode AdClose(AndroidPage page){return page.Nodes.FirstOrDefault(n=>n.Package==AdPackage&&(n.ResourceId==AdPackage+":id/fl_close"||n.ResourceId==AdPackage+":id/iv_close")&&n.Enabled&&n.Clickable&&!n.Web&&n.Left>=0&&n.Top>=0&&n.Right<=page.Width&&n.Bottom<=page.Height&&n.Right>n.Left&&n.Bottom>n.Top);}
 public static int[] DisplaySize(string output){var sizes=Regex.Matches(output??"",@"(?:Physical|Override) size:\s*(\d+)x(\d+)");if(sizes.Count==0)throw new InvalidOperationException("未能读取安卓屏幕尺寸，请导出诊断。");var last=sizes[sizes.Count-1];int w=Int32.Parse(last.Groups[1].Value),h=Int32.Parse(last.Groups[2].Value);if(w<=0||h<=0)throw new InvalidOperationException("安卓屏幕尺寸无效。");return new[]{w,h};}
 public static string Clean(string value){return Regex.Replace((value??"").Normalize(NormalizationForm.FormKC),@"[\s\u200B-\u200F\uFEFF]","");}
 public static bool Matches(AndroidNode node,string label){return new[]{node.Text,node.Description}.Any(value=>{if(String.IsNullOrWhiteSpace(value))return false;string all=Clean(value);if(all==label||Regex.IsMatch(all,"^"+Regex.Escape(label)+@"(?:第\d+个标签.*|Tab\d+of\d+|按钮|Button)$",RegexOptions.IgnoreCase))return true;return value.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).Any(line=>Clean(line)==label);});}
 public static AndroidNode Find(AndroidPage page,string label){return page.Nodes.FirstOrDefault(n=>!n.Web&&n.Package==AndroidClient.Package&&n.Enabled&&n.Right>n.Left&&n.Bottom>n.Top&&n.X>=0&&n.X<page.Width&&n.Y>=0&&n.Y<page.Height&&Matches(n,label));}
 public static string Launcher(string output){foreach(string line in (output??"").Split('\n')){string candidate=line.Trim();if(Regex.IsMatch(candidate,"^"+Regex.Escape(AndroidClient.Package)+@"/[A-Za-z0-9_.$]+$"))return candidate;}return null;}
 public static bool LaunchFailed(string output){return Regex.IsMatch(output??"",@"Error\s*:|Error type\s*\d|Permission Denial|SecurityException|Background activity start denied|Activity not started,.*unable",RegexOptions.IgnoreCase);}
 public static string Hint(AndroidPage page){if(HasEmulatorAd(page))return "雷电广告遮挡了应用，正在检查广告关闭按钮。";var packages=page.Nodes.Select(n=>n.Package??"").Distinct().ToArray();if(packages.Any(p=>p.Contains("permissioncontroller")||p=="com.android.packageinstaller"))return "智汇福大停在 Android 权限提示，请在模拟器中完成首次权限确认。";if(packages.Any(p=>p=="com.android.launcher3"||p=="com.ldmnq.launcher3"))return "当前仍是安卓桌面，智汇福大没有进入前台。";if(!packages.Contains(AndroidClient.Package))return "当前前台不是智汇福大，请通过显示隐藏页核对模拟器画面。";if(page.Nodes.Any(n=>n.Package==AndroidClient.Package&&Regex.IsMatch(n.Label??"","验证码|手机号|密码登录|登录失效|一键登录|其他账户登录|上次使用过的账号")))return "当前停在登录页面，请在模拟器中完成登录。";if(page.Nodes.Count(n=>!String.IsNullOrWhiteSpace(n.Label))<2)return "应用画面为空或未提供可读取的页面控件。";if(page.Attendance)return "当前为晚点名签到页。";if(Find(page,"晚点名签到")!=null)return "已找到晚点名入口。";return "已打开智汇福大，但尚未识别到晚点名入口。";}
 public static string Summary(AndroidPage page){var b=new StringBuilder();b.AppendLine("画面大小="+page.Width+"x"+page.Height);b.AppendLine("控件数量="+page.Nodes.Count);b.AppendLine("应用控件数量="+page.Nodes.Count(n=>n.Package==AndroidClient.Package));b.AppendLine("网页控件数量="+page.Nodes.Count(n=>n.Web));b.AppendLine("前台应用="+String.Join(",",page.Nodes.Select(n=>n.Package??"").Distinct().Where(p=>Regex.IsMatch(p,@"^[A-Za-z0-9_.]+$"))));foreach(string label in new[]{"晚点名签到","业务","服务","办事","我的服务","消息","一键登录","其他账户登录"}){var node=Find(page,label);b.AppendLine("可见入口["+label+"]="+(node==null?"否":"是，范围 "+node.Left+","+node.Top+","+node.Right+","+node.Bottom));}b.AppendLine("页面状态="+Hint(page));b.AppendLine("已进入晚点名="+page.Attendance);b.AppendLine("模拟器广告遮挡="+HasEmulatorAd(page));b.AppendLine("广告关闭按钮可识别="+(AdClose(page)!=null));return b.ToString();}
}
static class AndroidClient {
 public const string Package="cn.edu.fzu.fdxypa";
 static readonly SemaphoreSlim DeviceGate=new SemaphoreSlim(1,1);
 static string lastNavigation="尚未执行导航",lastLauncher="尚未解析启动入口";
 public static bool ValidConsole(string path){return !String.IsNullOrWhiteSpace(path)&&File.Exists(path)&&(String.Equals(Path.GetFileName(path),"ldconsole.exe",StringComparison.OrdinalIgnoreCase)||MuMuClient.IsPath(path))&&File.Exists(Path.Combine(Path.GetDirectoryName(path),"adb.exe"));}
 public static string Detect(string current){return EmulatorDiscovery.Detect(current);}

 static string AdbPath(Config c){return Path.Combine(Path.GetDirectoryName(c.EmulatorPath),"adb.exe");}
 static string Device(Config c){return "emulator-"+(5554+c.EmulatorIndex*2).ToString(CultureInfo.InvariantCulture);}
 static string Index(Config c){if(c.EmulatorIndex<0||c.EmulatorIndex>99)throw new InvalidOperationException("模拟器编号应为 0–99。");return c.EmulatorIndex.ToString(CultureInfo.InvariantCulture);}
 public static string SavedPosition(Config c){if(!String.IsNullOrEmpty(c.SessionPosition))return MuMuClient.ValidatePosition(c.SessionPosition);if(!String.IsNullOrWhiteSpace(c.FixedPosition))return MuMuClient.ValidatePosition(c.FixedPosition);if(MuMuClient.Is(c))return MuMuClient.Position(c);Index(c);string path=Path.Combine(Path.GetDirectoryName(c.EmulatorPath),"vms","config","leidian"+Index(c)+".config");var cfg=Store.Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(path));object location;if(!cfg.TryGetValue("statusSettings.location",out location)||!(location is Dictionary<string,object>))throw new InvalidOperationException("请先在雷电右侧“定位”中选择当前位置并确认。");var p=(Dictionary<string,object>)location;double lng=Convert.ToDouble(p["lng"],CultureInfo.InvariantCulture),lat=Convert.ToDouble(p["lat"],CultureInfo.InvariantCulture);if(Double.IsNaN(lng)||Double.IsNaN(lat)||Double.IsInfinity(lng)||Double.IsInfinity(lat)||lng<-180||lng>180||lat<-90||lat>90)throw new InvalidOperationException("雷电保存的位置无效，请重新确认定位。");return lng.ToString("R",CultureInfo.InvariantCulture)+","+lat.ToString("R",CultureInfo.InvariantCulture);}
 internal static async Task<byte[]> Command(string path,string arguments,int seconds){Operation.Throw();var start=new ProcessStartInfo(path,arguments){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Path.GetDirectoryName(path)};using(var p=Process.Start(start))using(var cancel=Operation.KillOnCancel(p))using(var output=new MemoryStream()){var copy=p.StandardOutput.BaseStream.CopyToAsync(output);var errors=p.StandardError.ReadToEndAsync();var exited=Task.Run(()=>p.WaitForExit(seconds*1000));if(!await exited){try{p.Kill();}catch(OperationCanceledException){throw;}catch{}throw new TimeoutException("模拟器控制超时。");}await copy;await errors;Operation.Throw();if(p.ExitCode!=0)throw new InvalidOperationException("模拟器未响应，请确认该实例已启动且 ADB 调试允许本机连接。");return output.ToArray();}}
 static async Task<string> Console(Config c,string args){if(!ValidConsole(c.EmulatorPath))throw new InvalidOperationException("请在运行环境页选择雷电的 ldconsole.exe。");return Encoding.UTF8.GetString(await Command(c.EmulatorPath,args,15));}
 static async Task<byte[]> Adb(Config c,string args,int seconds){string device=MuMuClient.Is(c)?await MuMuClient.Device(c):Device(c);return await Command(AdbPath(c),"-s "+device+" "+args,seconds);}
 static async Task<string> Shell(Config c,string args){return Encoding.UTF8.GetString(await Adb(c,"shell "+args,15));}
 [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr h,int cmd);
 [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")]internal static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32.dll")]internal static extern IntPtr GetForegroundWindow();
 static bool InstanceRunning(string line,Config c,out IntPtr window){window=IntPtr.Zero;var fields=line.Trim().Split(',');int index,run,pid;long handle;if(fields.Length<7||!Int32.TryParse(fields[0],out index)||index!=c.EmulatorIndex||!Int32.TryParse(fields[4],out run)||run!=1)return false;if(Int64.TryParse(fields[2],out handle)&&Int32.TryParse(fields[5],out pid)){uint owner;var h=new IntPtr(handle);GetWindowThreadProcessId(h,out owner);if(owner==(uint)pid)window=h;}return true;}
 public static async Task Hide(Config c){if(MuMuClient.Is(c)){await MuMuClient.Hide(c);return;}var list=await Console(c,"list2");foreach(var line in list.Split('\n')){IntPtr window;if(InstanceRunning(line,c,out window)&&window!=IntPtr.Zero)ShowWindow(window,0);}}
 static async Task<IntPtr> InstanceWindow(Config c){
  foreach(string line in (await Console(c,"list2")).Split('\n')){
   var f=line.Trim().Split(',');int index,pid;long handle;
   if(f.Length<7||!Int32.TryParse(f[0],out index)||index!=c.EmulatorIndex||!Int32.TryParse(f[5],out pid)||pid<=0||!Int64.TryParse(f[2],out handle)||handle==0)continue;
   uint owner;var window=new IntPtr(handle);GetWindowThreadProcessId(window,out owner);if(owner==(uint)pid)return window;
  }
  return IntPtr.Zero;
 }
 public static async Task<bool> ManualWindowVisible(Config c){if(MuMuClient.Is(c))return await MuMuClient.Visible(c);var window=await InstanceWindow(c);return window!=IntPtr.Zero&&IsWindowVisible(window);}
 static async Task<bool> DeviceOnline(Config c){try{return (await Shell(c,"getprop sys.boot_completed")).Trim()=="1";}catch{return false;}}
 public static async Task StopEmulator(Config c,Action<string> state){if(MuMuClient.Is(c)){await MuMuClient.Stop(c,state);return;}
  Index(c);if(!ValidConsole(c.EmulatorPath))throw new InvalidOperationException("请先选择雷电控制程序。");
  state("正在关闭所选雷电模拟器…");
  // A console on the normal desktop cannot always find a hidden-desktop player.
  EmulatorHost.QuitHidden(c);await Console(c,"quit --index "+Index(c));
  for(int i=0;i<35;i++){
   if(!await DeviceOnline(c)&&await InstanceWindow(c)==IntPtr.Zero){await Operation.Delay(1000);return;}
   await Operation.Delay(700);
  }
  throw new InvalidOperationException("模拟器尚未完全关闭，请查看雷电是否显示退出提示，再重试。");
 }
 public static async Task OpenEmulatorWindow(Config c,Action<string> state){if(MuMuClient.Is(c)){await MuMuClient.Open(c,state);return;}
  Index(c);if(!ValidConsole(c.EmulatorPath))throw new InvalidOperationException("请先选择雷电控制程序。");
  var window=await InstanceWindow(c);
  if(window==IntPtr.Zero){
   if(await DeviceOnline(c)){state("正在将后台实例重新打开到当前桌面…");await StopEmulator(c,state);}
   state("正在打开雷电模拟器窗口…");
   // Do not redirect launch output: the player can inherit the pipe and keep it open.
   using(var launch=Process.Start(new ProcessStartInfo(c.EmulatorPath,"launch --index "+Index(c)){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=Path.GetDirectoryName(c.EmulatorPath)})){}
   for(int i=0;i<60&&window==IntPtr.Zero;i++){await Operation.Delay(1000);window=await InstanceWindow(c);}
  }
  if(window==IntPtr.Zero)throw new InvalidOperationException("未找到所选模拟器窗口，请导出诊断。");
  ShowWindow(window,9);SetForegroundWindow(window);
 }
 static async Task Ensure(Config c,Action<string> state){if(MuMuClient.Is(c)){await MuMuClient.Ensure(c,state);return;}Index(c);if(!ValidConsole(c.EmulatorPath))throw new InvalidOperationException("请在运行环境页选择雷电的 ldconsole.exe。");try{if((await Shell(c,"getprop sys.boot_completed")).Trim()=="1"){await Hide(c);return;}}catch(OperationCanceledException){throw;}catch{}string list=await Console(c,"list2");bool running=list.Split('\n').Any(line=>{IntPtr w;return InstanceRunning(line,c,out w);});if(!running){state("正在隐藏桌面启动雷电模拟器…");if(!EmulatorHost.Start(c))throw new InvalidOperationException("雷电未能在隐藏桌面启动。");}for(int i=0;i<60;i++){await Hide(c);try{string ready=(await Shell(c,"getprop sys.boot_completed")).Trim();if(ready=="1")return;}catch(OperationCanceledException){throw;}catch{}await Operation.Delay(1000);}throw new InvalidOperationException("模拟器启动尚未完成，请稍后重试。");}
 static int[] Bounds(string text){var matches=Regex.Matches(text??"",@"-?\d+");if(matches.Count!=4)return new int[4];return matches.Cast<Match>().Select(x=>Int32.Parse(x.Value,CultureInfo.InvariantCulture)).ToArray();}
 public static AndroidPage ParsePage(string xml,int screenWidth=0,int screenHeight=0){var doc=new XmlDocument{XmlResolver=null};doc.LoadXml(xml);var result=new AndroidPage();var root=doc.SelectSingleNode("/hierarchy/node") as XmlElement;if(root==null)throw new InvalidOperationException("没有读到模拟器画面。");var screen=Bounds(root.GetAttribute("bounds"));result.Width=screen[2]-screen[0];result.Height=screen[3]-screen[1];if(screenWidth>0&&screenHeight>0){string rotation=doc.DocumentElement.GetAttribute("rotation");bool rotated=rotation=="1"||rotation=="3";result.Width=rotated?screenHeight:screenWidth;result.Height=rotated?screenWidth:screenHeight;}foreach(XmlElement e in doc.SelectNodes("//node")){var b=Bounds(e.GetAttribute("bounds"));var n=new AndroidNode{Text=e.GetAttribute("text"),Description=e.GetAttribute("content-desc"),Package=e.GetAttribute("package"),ResourceId=e.GetAttribute("resource-id"),Enabled=e.GetAttribute("enabled")=="true",Clickable=e.GetAttribute("clickable")=="true",Left=b[0],Top=b[1],Right=b[2],Bottom=b[3]};if(String.IsNullOrEmpty(n.Package)){for(XmlNode ancestor=e.ParentNode;ancestor is XmlElement;ancestor=ancestor.ParentNode){string inherited=((XmlElement)ancestor).GetAttribute("package");if(!String.IsNullOrEmpty(inherited)){n.Package=inherited;break;}}}XmlElement web=null;for(XmlNode a=e;a is XmlElement;a=a.ParentNode){if(((XmlElement)a).GetAttribute("class")=="android.webkit.WebView"){web=(XmlElement)a;}}n.Web=web!=null&&n.Package==Package;if(n.Web){var wb=Bounds(web.GetAttribute("bounds"));var parent=web.ParentNode as XmlElement;if(parent!=null){var pb=Bounds(parent.GetAttribute("bounds"));if(wb[2]>wb[0]&&wb[3]>wb[1]&&pb[2]>pb[0]&&pb[3]>pb[1]){n.ScaleX=(pb[2]-pb[0])/(double)(wb[2]-wb[0]);n.ScaleY=(pb[3]-pb[1])/(double)(wb[3]-wb[1]);n.OffsetX=pb[0]-wb[0]*n.ScaleX;n.OffsetY=pb[1]-wb[1]*n.ScaleY;}}}result.Nodes.Add(n);}return result;}
 public static async Task<AndroidPage> Read(Config c){await DeviceGate.WaitAsync();try{
  int[] size=AndroidNavigation.DisplaySize(await Shell(c,"wm size"));
  string dump="/sdcard/fzu-attendance-ui-"+Process.GetCurrentProcess().Id+".xml";
  await Shell(c,"rm -f "+dump+"; uiautomator dump "+dump+"; test -s "+dump);string xml=await Shell(c,"cat "+dump);
  return ParsePage(xml,size[0],size[1]);
 }finally{DeviceGate.Release();}}
 // Only the verified emulator ad's close resource is allowed outside the school app.
 static async Task<bool> DismissAd(Config c,AndroidPage page,Action<string> state){
  if(!AndroidNavigation.HasEmulatorAd(page))return false;
  for(int attempt=0;attempt<3;attempt++){
   var live=await Read(c);if(!AndroidNavigation.HasEmulatorAd(live))return true;
   var close=AndroidNavigation.AdClose(live);
   if(close==null)throw new InvalidOperationException("雷电广告遮挡了应用，但未识别到可用的关闭按钮；本次停止操作，请导出诊断。");
   state("检测到雷电广告，正在后台关闭广告…");
   await Shell(c,"input tap "+close.X.ToString(CultureInfo.InvariantCulture)+" "+close.Y.ToString(CultureInfo.InvariantCulture));
   await Operation.Delay(800);
  }
  if(AndroidNavigation.HasEmulatorAd(await Read(c)))throw new InvalidOperationException("已尝试关闭雷电广告三次，广告仍未消失；本次停止操作，请查看隐藏页。");
  return true;
 }
 public static async Task<string> AdProbe(Config c){
  IntPtr foreground=GetForegroundWindow();var before=await Read(c);bool found=AndroidNavigation.HasEmulatorAd(before);
  bool handled=await DismissAd(c,before,Store.Log);var after=await Read(c);
  if(AndroidNavigation.HasEmulatorAd(after))throw new InvalidOperationException("广告仍未关闭。");
  return "发现广告="+found+"\n已处理="+handled+"\n前台窗口未改变="+(foreground==GetForegroundWindow())+"\n本次仅处理广告，未启动应用、登录或提交签到。\n"+AndroidNavigation.Summary(after);
 }
 static async Task Tap(Config c,AndroidPage page,AndroidNode node){if(node==null||node.Package!=Package||!node.Enabled||node.X<0||node.Y<0||node.X>=page.Width||node.Y>=page.Height||node.Right<=node.Left||node.Bottom<=node.Top)throw new InvalidOperationException("页面按钮不在可操作区域，请查看隐藏页。");await Shell(c,"input tap "+node.X.ToString(CultureInfo.InvariantCulture)+" "+node.Y.ToString(CultureInfo.InvariantCulture));}
 public static string OffsetPosition(string center,double radius,double angle){var p=MuMuClient.ValidatePosition(center).Split(',');double lon=Double.Parse(p[0],CultureInfo.InvariantCulture),lat=Double.Parse(p[1],CultureInfo.InvariantCulture);double north=radius*Math.Cos(angle),east=radius*Math.Sin(angle);return (lon+east/(6378137*Math.Cos(lat*Math.PI/180))*180/Math.PI).ToString("R",CultureInfo.InvariantCulture)+","+(lat+north/6378137*180/Math.PI).ToString("R",CultureInfo.InvariantCulture);}
 public static void PreparePosition(Config c){c.SessionPosition=null;string center=SavedPosition(c);byte[] bytes=new byte[8];using(var rng=System.Security.Cryptography.RandomNumberGenerator.Create())rng.GetBytes(bytes);double distance=4.9*Math.Sqrt(BitConverter.ToUInt32(bytes,0)/(double)UInt32.MaxValue),angle=2*Math.PI*BitConverter.ToUInt32(bytes,4)/(double)UInt32.MaxValue;c.SessionPosition=OffsetPosition(center,distance,angle);}
 public static async Task RefreshPosition(Config c){if(MuMuClient.Is(c)){await MuMuClient.Refresh(c);return;}string coordinates=SavedPosition(c);await Console(c,"action --index "+Index(c)+" --key call.locate --value "+coordinates);}
 static async Task LaunchApp(Config c){
  string installed=await Shell(c,"pm path "+Package);
  if(!installed.TrimStart().StartsWith("package:",StringComparison.Ordinal)){lastLauncher="该模拟器实例中未发现安卓版智汇福大";throw new InvalidOperationException("当前模拟器实例中未安装安卓版智汇福大，请核对实例编号。");}
  string resolved=await Shell(c,"cmd package resolve-activity --brief -a android.intent.action.MAIN -c android.intent.category.LAUNCHER "+Package);
  string launcher=AndroidNavigation.Launcher(resolved);
  if(launcher==null){resolved=await Shell(c,"cmd package resolve-activity --brief "+Package);launcher=AndroidNavigation.Launcher(resolved);}
  if(launcher==null){lastLauncher="已安装，但未解析到可启动的应用入口";throw new InvalidOperationException("智汇福大已安装，但没有找到可启动入口，请导出诊断。");}
  lastLauncher=launcher;
  string result=await Shell(c,"am start -W -n '"+launcher+"'");
  if(AndroidNavigation.LaunchFailed(result)){lastLauncher+="；安卓报告启动失败";throw new InvalidOperationException("Android 未能启动智汇福大，请导出诊断核对启动状态。");}
 }
 static async Task<AndroidPage> OpenAttendance(Config c,bool restart,Action<string> state){
  if(restart){state("正在重新打开模拟器内的智汇福大…");await CloseApp(c);}
  await LaunchApp(c);await Operation.Delay(1500);
  bool serviceTapped=false,relaunched=false;DateTime began=DateTime.UtcNow;AndroidPage last=null;
  for(int i=0;i<30&&DateTime.UtcNow-began<TimeSpan.FromSeconds(90);i++){
   var page=await Read(c);last=page;lastNavigation=AndroidNavigation.Summary(page);
   if(await DismissAd(c,page,state)){serviceTapped=false;await LaunchApp(c);await Operation.Delay(1200);continue;}
   if(page.Attendance){if(!String.IsNullOrEmpty(page.Date)&&page.Date!=Store.Now().ToString("yyyy-MM-dd")){await Shell(c,"input keyevent 4");await Operation.Delay(700);continue;}return page;}
   string hint=AndroidNavigation.Hint(page);
   if(hint.Contains("权限提示")||hint.Contains("登录页面"))throw new InvalidOperationException(hint);
   var entry=AndroidNavigation.Find(page,"晚点名签到");
   if(entry!=null){state("已找到晚点名入口，正在打开…");await Tap(c,page,entry);await Operation.Delay(1600);continue;}
   var service=AndroidNavigation.Find(page,"业务")??AndroidNavigation.Find(page,"服务")??AndroidNavigation.Find(page,"办事");
   if(service!=null){
    if(!serviceTapped){state("正在打开业务页并查找晚点名入口…");await Tap(c,page,service);serviceTapped=true;}
    else {int x=page.Width/2;await Shell(c,"input swipe "+x+" "+(page.Height*8/10)+" "+x+" "+(page.Height*3/10)+" 400");}
    await Operation.Delay(900);continue;
   }
   if(!relaunched&&DateTime.UtcNow-began>TimeSpan.FromSeconds(8)&&hint.Contains("安卓桌面")){state("应用尚未进入前台，正在重试启动…");await LaunchApp(c);relaunched=true;}
   if(i%4==0)state("正在等待智汇福大页面："+hint);
   await Operation.Delay(1200);
  }
  throw new InvalidOperationException((last==null?"未读到安卓页面。":AndroidNavigation.Hint(last))+" 请点“导出诊断”，本次未进入签到操作。");
 }
 public static async Task<string> Diagnostic(Config c){
  var report=new StringBuilder();report.AppendLine("晚点名助手诊断 v"+typeof(AndroidClient).Assembly.GetName().Version);
  report.AppendLine("生成时间="+Store.Now().ToString("yyyy-MM-dd HH:mm:ss"));report.AppendLine("Windows="+Environment.OSVersion.Version);
  report.AppendLine("模拟器编号="+c.EmulatorIndex);report.AppendLine("模拟器类型="+(MuMuClient.Is(c)?"MuMu":Path.GetFileName(c.EmulatorPath)=="ldconsole.exe"?"雷电":"未识别"));report.AppendLine("控制程序="+Path.GetFileName(c.EmulatorPath));report.AppendLine("控制程序可用="+ValidConsole(c.EmulatorPath));if(MuMuClient.Is(c)){try{var info=await MuMuClient.Info(c);foreach(string key in new[]{"is_process_started","is_android_started","adb_port"}){object value;if(info.TryGetValue(key,out value))report.AppendLine("MuMu "+key+"="+value);}}catch(Exception ex){report.AppendLine("MuMu实例查询失败="+ex.GetType().Name);}}
  report.AppendLine("手动操作模式="+c.ManualEmulator);report.AppendLine("关闭后定时等待="+Rules.Deferred(c,Store.Now()));report.AppendLine("自动签到启用="+c.Enabled);report.AppendLine("设定时间="+c.Time);
  report.AppendLine("已解析启动入口="+lastLauncher);report.AppendLine("最近导航摘要：");report.AppendLine(lastNavigation);
  if(ValidConsole(c.EmulatorPath)){
   try{string v=await Shell(c,"getprop ro.build.version.release");if(Regex.IsMatch(v.Trim(),@"^[0-9.]+$"))report.AppendLine("Android版本="+v.Trim());}catch(Exception e){report.AppendLine("Android版本读取失败="+e.GetType().Name);}
   try{string v=await Shell(c,"dumpsys package "+Package);var match=Regex.Match(v,@"versionName=([A-Za-z0-9._-]+)");report.AppendLine("智汇福大版本="+(match.Success?match.Groups[1].Value:"未识别"));}catch(Exception e){report.AppendLine("应用版本读取失败="+e.GetType().Name);}
   try{report.AppendLine("当前页面摘要：");report.AppendLine(AndroidNavigation.Summary(await Read(c)));}catch(Exception e){report.AppendLine("页面读取失败="+e.GetType().Name);}
   try{SavedPosition(c);report.AppendLine("已选位置配置可读取=是");}catch{report.AppendLine("已选位置配置可读取=否");}
  }
  report.AppendLine("此报告不包含账号、坐标、令牌、原始页面文字或截图；导出不提交签到。");return report.ToString();
 }
 public static async Task PreviewSwipe(Config c,Point a,Point b,Size size){if(!PreviewGeometry.Valid(size,a,b))throw new InvalidOperationException("请在画面内上下拖动。");var page=await Read(c);if(page.Width!=size.Width||page.Height!=size.Height)throw new InvalidOperationException("画面方向已改变，请等待刷新后重试。");if(!page.Nodes.Any(n=>n.Package==Package)||AndroidNavigation.HasEmulatorAd(page))throw new InvalidOperationException("当前不是智汇福大页面，请先打开模拟器窗口处理。");await Shell(c,"input swipe "+a.X+" "+a.Y+" "+b.X+" "+b.Y+" 400");}
 public static async Task<byte[]> Capture(Config c){await DeviceGate.WaitAsync();try{byte[] data=await Adb(c,"exec-out screencap -p",15);if(data.Length<8||data[0]!=137||data[1]!=80||data[2]!=78||data[3]!=71)throw new InvalidOperationException("模拟器画面暂不可用。");return data;}finally{DeviceGate.Release();}}
 public static async Task CloseApp(Config c){await Shell(c,"am force-stop "+Package);}
 public static async Task<bool> Check(Config c,bool dry,bool force,Func<bool> restartRequested,Action clearRestart,Action<string> state){
  PreparePosition(c);IntPtr foreground=GetForegroundWindow();await Ensure(c,state);state("模拟器已就绪，正在发送保存位置附近 5 米内的本次签到定位…");await RefreshPosition(c);await Operation.Delay(1000);state("定位命令已成功执行，正在打开智汇福大…");Store.Log("安卓后台连接已建立，前台窗口"+(foreground==GetForegroundWindow()?"未改变。":"由用户或其他程序改变；助手未请求前台切换。"));int restarts=0;
  while(true){bool restart=restartRequested();if(restart)clearRestart();var page=await OpenAttendance(c,restart,state);if(page.Attendance&&!page.Confirmed){await Shell(c,"input keyevent 4");await Operation.Delay(700);page=await OpenAttendance(c,false,state);}bool submit=!dry||(restart&&Rules.Due(c,Store.Now(),true));
   // A saved point is not a new GPS event. Publish it AFTER the page starts requesting location.
   await Operation.Delay(1200);await RefreshPosition(c);state("已在后台重新发送助手保存的签到位置，正在等待晚点名读取定位…");DateTime began=DateTime.UtcNow,nextPosition=began.AddSeconds(5);DateTime? limitSince=null;bool retryOpen=false;
   for(int attempts=0;attempts<40;attempts++){if(restartRequested()){retryOpen=true;break;}page=await Read(c);
    if(await DismissAd(c,page,state)){page=await OpenAttendance(c,false,state);await RefreshPosition(c);}
    string today=Store.Now().ToString("yyyy-MM-dd");if(page.Attendance&&page.Date==today&&page.Confirmed){c.SuccessDate=today;if(submit)c.ClosePending=true;state("今日已签到，已确认安卓页面结果。");return submit;}
    if(page.LoginExpired)throw new InvalidOperationException("智汇福大登录已失效，请在模拟器中重新登录。");if(!page.Attendance)throw new InvalidOperationException("模拟器当前已离开晚点名页，请重新检查连接。");
    if(submit&&!force&&!c.Enabled){state("自动签到已暂停，已停止本次操作。");return false;}
    if(page.Limited){if(!limitSince.HasValue)limitSince=DateTime.UtcNow;if(Engine.LimitExpired(limitSince,DateTime.UtcNow,true)){retryOpen=true;break;}}else limitSince=null;
    if(page.Date==today&&page.Ready!=null&&page.Reason=="可签到"&&!page.LoginExpired){if(!submit){state("定位刷新成功：安卓晚点名页面已显示“点击签到”；本次检查未提交。");return false;}if(!Rules.InWindow(Store.Now())){state("已超出签到时段，未提交。");return false;}
     // Read the live page again, including its date and enabled label, immediately before the tap.
     var live=await Read(c);if(await DismissAd(c,live,state)){page=await OpenAttendance(c,false,state);await RefreshPosition(c);continue;}if(restartRequested()){retryOpen=true;break;}if(!live.CanSubmit(Store.Now())||(!force&&!c.Enabled)){state("页面状态已变化，本次未提交；稍后重试。");return false;}await Tap(c,live,live.Ready);state("已点击安卓页面签到按钮，正在核验结果…");for(int i=0;i<10;i++){await Operation.Delay(1200);var checkedPage=await Read(c);if(await DismissAd(c,checkedPage,state)){checkedPage=await Read(c);}if(checkedPage.Attendance&&checkedPage.Date==today&&checkedPage.Confirmed){c.SuccessDate=today;c.ClosePending=true;state("签到成功，已确认安卓晚点名页面显示“已签到”。");try{File.WriteAllBytes(Store.FilePath("last-success.png"),await Capture(c));}catch(OperationCanceledException){throw;}catch{}return true;}}state("已点击安卓页面，但尚未确认成功；5 分钟后重新检查。");return false;}
    if((DateTime.UtcNow-began).TotalSeconds>=8&&page.Date==today&&!page.Locating&&!page.Limited&&page.Reason!="页面尚未准备好"){state("定位已刷新，当前页面提示："+page.Reason+"。稍后重试。");return false;}
    if(DateTime.UtcNow>=nextPosition){await RefreshPosition(c);nextPosition=DateTime.UtcNow.AddSeconds(5);}if((DateTime.UtcNow-began).TotalSeconds>=110)break;await Operation.Delay(1500);
   }
   if(retryOpen&&restarts++<2){state("定位等待已达到一分钟或收到重开请求，正在重新进入晚点名…");clearRestart();await CloseApp(c);await Operation.Delay(600);await LaunchApp(c);continue;}
   state("已重新发送定位，但页面仍未准备好；5 分钟后重试，可查看隐藏页。");return false;
  }
 }
}
}
