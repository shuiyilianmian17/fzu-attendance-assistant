using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace FzuAttendance {
static class Store {
 public static readonly string Dir=AppDomain.CurrentDomain.BaseDirectory;
 public static readonly string DataDir=CreateDataDir();
 public static JavaScriptSerializer Json=new JavaScriptSerializer();
 static string CreateDataDir(){string path=File.Exists(Path.Combine(Dir,"installed.flag"))?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FzuAttendanceAssistant"):Dir;Directory.CreateDirectory(path);return path;}
 public static string FilePath(string name){return Path.Combine(DataDir,name);}
 public static Config Read(){try{return Json.Deserialize<Config>(File.ReadAllText(FilePath("settings.json")));}catch{return new Config();}}
 public static void Save(Config c){string tmp=FilePath("settings.tmp");File.WriteAllText(tmp,Json.Serialize(c),Encoding.UTF8);if(File.Exists(FilePath("settings.json"))) File.Replace(tmp,FilePath("settings.json"),null);else File.Move(tmp,FilePath("settings.json"));}
 public static DateTime Now(){return DateTime.UtcNow.AddHours(8);}
 public static void Log(string s){try{File.AppendAllText(FilePath("运行日志.txt"),Now().ToString("yyyy-MM-dd HH:mm:ss ")+s+Environment.NewLine,Encoding.UTF8);}catch{}}
 public static string SafeError(Exception e){return e is WebException||e is WebSocketException?"本机页面连接中断，请重新连接客户端。":"操作未完成（"+e.GetType().Name+"），请查看连接状态。";}
}
public class Config {
 public string Time="21:05";
 public bool Enabled=false;
 public string SuccessDate="";
 public string LastState="请先在运行环境中选择雷电，并在签到设置中启用自动签到。";
 public string ClientPath=@"C:\Program Files (x86)\cn.edu.fzu.fdxypd\fdxy_desktop.exe";
 public string Backend="android";
 public string EmulatorPath="";
 public int EmulatorIndex=0;
}
static class Rules {
 public static bool InWindow(DateTime now){return now.TimeOfDay>=TimeSpan.FromHours(21)&&now.TimeOfDay<TimeSpan.FromDays(1);}
 public static bool ValidTime(string text){TimeSpan t;return TimeSpan.TryParseExact(text,"hh\\:mm",null,out t)&&t>=TimeSpan.FromHours(21)&&t<TimeSpan.FromDays(1);}
 public static bool Due(Config c,DateTime now,bool catchup){if(!c.Enabled||!InWindow(now)||c.SuccessDate==now.ToString("yyyy-MM-dd"))return false;TimeSpan at;return ValidTime(c.Time)&&TimeSpan.TryParse(c.Time,out at)&&(catchup||now.TimeOfDay>=at);}
}
sealed class Cdp:IDisposable {
 ClientWebSocket socket=new ClientWebSocket(); int sequence;
 public static async Task<object[]> Targets(){using(var w=new WebClient()){w.Proxy=null;var task=w.DownloadStringTaskAsync("http://127.0.0.1:19228/json/list");if(await Task.WhenAny(task,Task.Delay(1500))!=task){w.CancelAsync();throw new TimeoutException("本机接口未响应");}string s=await task;return Store.Json.Deserialize<object[]>(s);}}
 public static async Task<Dictionary<string,object>> Target(){foreach(var raw in await Targets()){var x=(Dictionary<string,object>)raw;Uri u; if(x.ContainsKey("url")&&Uri.TryCreate((string)x["url"],UriKind.Absolute,out u)&&u.Scheme=="https"&&u.Host=="yzsxg.fzu.edu.cn"&&u.AbsolutePath=="/plug-in/livecloud/project/fzu/attn/index.action")return x;}return null;}
 public async Task Open(Dictionary<string,object> target){var u=new Uri((string)target["webSocketDebuggerUrl"]);if(!u.IsLoopback)throw new InvalidOperationException("控制接口必须在本机");using(var timeout=new CancellationTokenSource(5000)){await socket.ConnectAsync(u,timeout.Token);}}
 public async Task<Dictionary<string,object>> Call(string method,object args){int id=++sequence;byte[] payload=Encoding.UTF8.GetBytes(Store.Json.Serialize(new {id=id,method=method,@params=args}));using(var timeout=new CancellationTokenSource(12000)){await socket.SendAsync(new ArraySegment<byte>(payload),WebSocketMessageType.Text,true,timeout.Token);while(true){using(var stream=new MemoryStream()){WebSocketReceiveResult r;do{byte[] buffer=new byte[16384];r=await socket.ReceiveAsync(new ArraySegment<byte>(buffer),timeout.Token);if(r.MessageType==WebSocketMessageType.Close)throw new IOException("页面已关闭");stream.Write(buffer,0,r.Count);if(stream.Length>4000000)throw new IOException("响应过大");}while(!r.EndOfMessage);var item=Store.Json.Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(stream.ToArray()));if(item.ContainsKey("id")&&Convert.ToInt32(item["id"])==id){if(item.ContainsKey("error"))throw new InvalidOperationException("网页控制失败");return (Dictionary<string,object>)item["result"];}}}}}
 public async Task<string> Eval(string expression){var r=await Call("Runtime.evaluate",new{expression=expression,returnByValue=true,awaitPromise=true});if(r.ContainsKey("exceptionDetails"))throw new InvalidOperationException("网页脚本执行失败");var obj=(Dictionary<string,object>)r["result"];return obj.ContainsKey("value")?Convert.ToString(obj["value"]):"";}
 public void Dispose(){socket.Dispose();}
}
static class Native {
 public delegate bool EnumProc(IntPtr h,IntPtr p);
 [DllImport("user32.dll")]static extern bool EnumWindows(EnumProc f,IntPtr p);
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr h,StringBuilder text,int count);
 [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr h,int command);
 public static List<IntPtr> Windows(string title){var ids=new HashSet<int>(Process.GetProcessesByName("fdxy_desktop").Select(x=>x.Id));var result=new List<IntPtr>();EnumWindows(delegate(IntPtr h,IntPtr p){uint pid;GetWindowThreadProcessId(h,out pid);var text=new StringBuilder(256);GetWindowText(h,text,256);if(ids.Contains((int)pid)&&text.ToString()==title)result.Add(h);return true;},IntPtr.Zero);return result;}
 public static void HidePages(){foreach(var h in Windows("晚点名签到"))ShowWindow(h,0);}
 public static void ShowClient(){foreach(var h in Windows("智汇福大"))ShowWindow(h,5);foreach(var h in Windows("晚点名签到"))ShowWindow(h,5);}
 public static void StartClient(Config cfg){if(Process.GetProcessesByName("fdxy_desktop").Length>0)return;var info=new ProcessStartInfo(cfg.ClientPath);info.WorkingDirectory=Path.GetDirectoryName(cfg.ClientPath);info.UseShellExecute=false;info.WindowStyle=ProcessWindowStyle.Hidden;info.EnvironmentVariables["WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS"]="--remote-debugging-port=19228 --remote-debugging-address=127.0.0.1";Process.Start(info);}
 public static async Task<bool> Bootstrap(Config cfg){var info=new ProcessStartInfo(Application.ExecutablePath,"--recover");info.UseShellExecute=false;info.CreateNoWindow=true;info.WindowStyle=ProcessWindowStyle.Hidden;using(var worker=Process.Start(info)){for(int i=0;i<150;i++){if(worker.HasExited)return worker.ExitCode==0;await Task.Delay(500);}if(!worker.HasExited)worker.Kill();}return false;}
}
class Engine {
 public Config Config; public Action<string> Update; public bool IsBusy {get{return busy;}} bool busy,restartRequested,restarting;DateTime retry=DateTime.MinValue;DateTime? limitedSince;
 public Engine(Config c,Action<string> update){Config=c;Update=update;}
 public void State(string s){Config.LastState=s;Store.Save(Config);Store.Log(s);if(Update!=null)Update(s);}
 const string ReadJs=@"JSON.stringify((()=>{const body=document.body.innerText;const b=document.querySelector('[class*=_signBtn_]:not([class*=_signBtnWrapper_])');const date=(body.match(/\b20\d{2}-\d{2}-\d{2}\b/)||[])[0]||'';const status=document.querySelector('[class*=_signStatus_]');return {date:date,status:status?status.innerText:'',button:b?b.innerText:'',blocked:!!(b&&/signUnactive|disabled/i.test(b.className)),locationError:/无法调用.*定位|定位失败|定位服务不可用|请开启定位/.test(body),loginError:/登录.*失效|请.*登录|token.*失效|身份验证失败/i.test(body),hasButton:!!b,locating:!!b&&/定位中/.test(b.innerText),reason:(document.querySelector('[class*=_disabledReason_]')||{}).innerText||(body.match(/受定位服务[^\n]+/)||[])[0]||''};})())";
 static Dictionary<string,object> Parse(string s){return Store.Json.Deserialize<Dictionary<string,object>>(s);}
 static bool Flag(Dictionary<string,object> s,string k){return s.ContainsKey(k)&&s[k] is bool&&(bool)s[k];}
 static bool Confirmed(Dictionary<string,object> s){string t=Convert.ToString(s["status"]);return t.Contains("已签到")&&!t.Contains("未签到");}
 public static bool LimitExpired(DateTime? since,DateTime now,bool limited){return limited&&since.HasValue&&(now-since.Value).TotalSeconds>=60;}
 bool UpdateLimit(Dictionary<string,object> state){bool limited=Flag(state,"locating")&&Convert.ToString(state["reason"]).Contains("定位服务流量限制");if(!limited){limitedSince=null;return false;}if(!limitedSince.HasValue)limitedSince=DateTime.UtcNow;return LimitExpired(limitedSince,DateTime.UtcNow,limited);}
 public async Task RestartClient(){if(restarting){State("正在重新打开智汇福大，请稍候。");return;}restartRequested=true;if(busy){State("已请求重新打开智汇福大，正在结束本次检查…");return;}await Check(true,true,true);}
 public async Task Check(bool dry,bool catchup,bool force){if(busy)return;if(!restartRequested&&!dry&&!force&&(!Rules.Due(Config,Store.Now(),catchup)||Store.Now()<retry))return;busy=true;try{if(AndroidClient.Selected(Config)){await AndroidClient.Check(Config,dry,force,()=>restartRequested,()=>restartRequested=false,State);return;}while(true){bool restart=await CheckCore(dry,catchup,force);if(!restart&&!restartRequested)break;restarting=true;restartRequested=false;State("正在关闭并重新打开后台智汇福大，随后自动进入晚点名签到…");if(!Recovery.StopOwned(Config.ClientPath)){State("无法确认后台客户端归属，未关闭其他客户端；请彻底退出普通客户端后重试。");break;}await Task.Delay(1000);if(!await Native.Bootstrap(Config)){State("客户端重新打开未完成，请查看连接状态。");break;}limitedSince=null;restarting=false;State("已重新打开智汇福大并进入晚点名签到页。");dry=!Rules.Due(Config,Store.Now(),true);force=false;catchup=true;}}catch(Exception e){State(AndroidClient.Selected(Config)&&e is InvalidOperationException?e.Message:Store.SafeError(e));}finally{busy=false;restarting=false;if(!dry)retry=Store.Now().AddMinutes(5);}}
 public async Task RefreshLocation(){if(!AndroidClient.Selected(Config)){State("请先在“运行环境”页选择安卓模拟器。");return;}if(busy){State("正在处理签到，稍后会自动重新发送定位。");return;}await Check(true,false,true);}
 async Task<bool> CheckCore(bool dry,bool catchup,bool force){try{if(restartRequested)return true;
  if(!File.Exists(Config.ClientPath)){State("未找到智汇福大客户端，请到“客户端”页选择已安装的程序路径。");return false;}
  Dictionary<string,object> target=null;try{target=await Cdp.Target();}catch{}if(target==null){State("正在独立隐藏桌面恢复签到页，不影响当前桌面…");if(!await Native.Bootstrap(Config)){State("后台恢复未成功：可能需要重新登录或关闭另行打开的客户端；5 分钟后重试。");return false;}target=await Cdp.Target();}
  Native.HidePages();using(var cdp=new Cdp()){await cdp.Open(target);Dictionary<string,object> s=Parse(await cdp.Eval(ReadJs));string currentDay=Store.Now().ToString("yyyy-MM-dd");bool stale=Convert.ToString(s["date"])!=currentDay||(Rules.InWindow(Store.Now())&&Convert.ToString(s["reason"]).Contains("未到签到时间"));if(!dry&&!Flag(s,"locating")&&stale)await cdp.Call("Page.reload",new{ignoreCache=true});for(int i=0;i<30;i++){s=Parse(await cdp.Eval(ReadJs));if(Flag(s,"hasButton")||Flag(s,"loginError"))break;await Task.Delay(500);}
   string today=Store.Now().ToString("yyyy-MM-dd");if(s==null||Convert.ToString(s["date"])!=today){State("需要重新连接：页面未显示今天的签到信息。");return false;}if(Flag(s,"locationError")){State("定位服务不可用，需要在原客户端确认定位。");return false;}
   if(Confirmed(s)){Config.SuccessDate=today;State("今日已签到，今天不会重复提交。");return false;}
   UpdateLimit(s);
   if(dry){State(Flag(s,"locating")?"页面已连接，正在定位："+Convert.ToString(s["reason"])+"（检查连接不会刷新页面）":"连接正常：当前按钮："+Convert.ToString(s["button"]));return false;}
   if(!Rules.InWindow(Store.Now())){State("尚未到签到时段，等待 21:00–23:59。");return false;}
   if(Flag(s,"loginError")||!Flag(s,"hasButton")){State("需要重新登录智汇福大并打开晚点名签到。");return false;}
   if(Flag(s,"locating")){State("正在定位；若流量限制连续达到一分钟，将自动重开客户端。页面提示："+Convert.ToString(s["reason"]));var until=DateTime.UtcNow.AddMinutes(3);while(Flag(s,"locating")&&DateTime.UtcNow<until&&Rules.InWindow(Store.Now())){if(restartRequested||UpdateLimit(s))return true;if(!force&&!Config.Enabled){State("自动签到已暂停，已停止等待。");return false;}await Task.Delay(2000);s=Parse(await cdp.Eval(ReadJs));}if(Flag(s,"locating")){State("定位尚未完成，将保留当前页面并稍后检查。可点“显示隐藏页”查看提示。");return false;}if(Confirmed(s)){Config.SuccessDate=today;State("今日已签到，已确认页面结果。");return false;}if(Flag(s,"locationError")){State("定位服务报告失败，请通过“显示隐藏页”查看具体提示。");return false;}}
   if(!Rules.InWindow(Store.Now())){State("已超出签到时段，未提交。");return false;}
   if(restartRequested)return true;
   if(Flag(s,"blocked")||Convert.ToString(s["button"]).Contains("无法")){State("页面暂不允许签到，将在 5 分钟后检查。");return false;}
   // Re-read date and enabled state inside the same evaluation as the DOM click.
   string result=await cdp.Eval(@"(()=>{const t=document.body.innerText;const d=(t.match(/\b20\d{2}-\d{2}-\d{2}\b/)||[])[0];const b=document.querySelector('[class*=_signBtn_]:not([class*=_signBtnWrapper_])');if(d!=="+Store.Json.Serialize(today)+@"||!b||/signUnactive|disabled/i.test(b.className)||!/^签到$|^立即签到$|^点击签到$/.test(b.innerText.trim()))return 'blocked';b.click();return 'clicked';})()");
   if(result!="clicked"){State("按钮状态变化，未提交；稍后重新检查。");return false;}
   for(int i=0;i<20;i++){await Task.Delay(1000);s=Parse(await cdp.Eval(ReadJs));if(Convert.ToString(s["date"])==today&&Confirmed(s)){Config.SuccessDate=today;State("签到成功，已确认页面显示“已签到”。");return false;}}
   State("已点击，但没有确认签到成功；5 分钟后重新检查状态。");
  }
 return false;}catch(Exception e){State(Store.SafeError(e));return false;}}
}
class MainForm:Form {
 Config config;Engine engine;Dashboard ui;NotifyIcon tray;HiddenPagePreview preview;System.Windows.Forms.Timer timer,showRequests;bool quit,initial=true,hideInitial;DateTime lastTick=DateTime.MinValue;
 public MainForm(bool hidden){
  hideInitial=hidden;config=Store.Read();if(!Rules.ValidTime(config.Time))config.Time="21:05";if(AndroidClient.Selected(config)){if(!AndroidClient.ValidConsole(config.EmulatorPath)){string found=AndroidClient.Detect(config.EmulatorPath);if(found!=null){config.EmulatorPath=found;Store.Save(config);}}}else if(!Dashboard.ValidClient(config.ClientPath)){string found=Dashboard.Detect(config.ClientPath);if(found!=null){config.ClientPath=found;Store.Save(config);}}
  Text="晚点名助手";Icon=System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath)??SystemIcons.Application;
  ClientSize=new Size(1060,720);MinimumSize=new Size(950,680);StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(242,245,250);
  ui=new Dashboard(config);var host=new System.Windows.Forms.Integration.ElementHost{Dock=DockStyle.Fill,Child=ui.Root};Controls.Add(host);
  engine=new Engine(config,delegate(string state){ui.Refresh(config);ui.LoadLogs();tray.Text=("晚点名助手 · "+state).Substring(0,Math.Min(63,("晚点名助手 · "+state).Length));});
  ui.SaveSchedule=SaveSchedule;ui.SaveClient=SaveClient;ui.CheckConnection=async delegate{await engine.Check(true,false,true);};ui.AttemptNow=async delegate{await engine.Check(false,true,true);};ui.LoginHelp=LoginHelp;ui.ShowHiddenPage=ShowHiddenPage;ui.RestartClient=async delegate{await engine.RestartClient();};ui.RefreshLocation=async delegate{await engine.RefreshLocation();};
  var menu=new ContextMenuStrip();menu.Items.Add("打开设置",null,delegate{Show();WindowState=FormWindowState.Normal;Activate();});menu.Items.Add("检查连接",null,async delegate{await engine.Check(true,false,true);});menu.Items.Add("显示隐藏页",null,delegate{ShowHiddenPage();});menu.Items.Add("重新定位",null,async delegate{await engine.RefreshLocation();});menu.Items.Add("重新打开智汇福大",null,async delegate{await engine.RestartClient();});menu.Items.Add("退出助手",null,delegate{quit=true;Close();});menu.Items.Add("退出并关闭后台客户端",null,async delegate{if(engine.IsBusy){engine.State("正在处理签到，请暂停自动签到并等本次操作结束后退出。");return;}if(AndroidClient.Selected(config))await AndroidClient.CloseApp(config);else Recovery.StopOwned(config.ClientPath);quit=true;Close();});
  tray=new NotifyIcon{Text="晚点名助手",Icon=this.Icon,Visible=true,ContextMenuStrip=menu};tray.DoubleClick+=delegate{Show();Activate();};
  FormClosing+=delegate(object sender,FormClosingEventArgs e){if(!quit&&e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();}else{tray.Visible=false;timer.Stop();showRequests.Stop();}};
  showRequests=new System.Windows.Forms.Timer{Interval=500};showRequests.Tick+=delegate{if(Program.ShowRequest.WaitOne(0)){Show();WindowState=FormWindowState.Normal;Activate();}};showRequests.Start();
  timer=new System.Windows.Forms.Timer{Interval=15000};timer.Tick+=async delegate{DateTime now=Store.Now();bool wake=lastTick!=DateTime.MinValue&&(now-lastTick).TotalSeconds>90;lastTick=now;ui.Refresh(config);await engine.Check(false,wake,false);};
  Shown+=delegate{StartService();};
 }
 protected override void SetVisibleCore(bool value){if(value&&hideInitial){hideInitial=false;if(!IsHandleCreated)CreateHandle();BeginInvoke(new Action(StartService));return;}base.SetVisibleCore(value);}
 async void StartService(){if(!initial)return;initial=false;timer.Start();lastTick=Store.Now();if(Rules.Due(config,Store.Now(),true))await engine.Check(false,true,false);else engine.State(config.Enabled?"助手已在后台等待，每天 "+config.Time+" 启动或连接模拟器并签到。":"自动签到已暂停。");}
 void SaveSchedule(){string chosen=ui.Time;if(!Rules.ValidTime(chosen)){ui.Feedback("请选择 21:00–23:59 内的时间。",true);return;}try{Startup.Set(ui.StartupEnabled);config.Time=chosen;config.Enabled=ui.Enabled;Store.Save(config);ui.Refresh(config);ui.Feedback("已保存：每天 "+chosen+"，"+(config.Enabled?"自动签到已开启。":"自动签到已暂停。"),false);}catch{ui.Feedback("保存未完成，请检查当前账户的写入权限。",true);}}
 void SaveClient(){if(engine.IsBusy){ui.Feedback("正在检查签到状态，请等待完成后再保存运行环境。",true);return;}try{if(ui.UseAndroid){int index;if(!AndroidClient.ValidConsole(ui.EmulatorPath)||!Int32.TryParse(ui.EmulatorIndex,out index)||index<0||index>99){ui.Feedback("请选择雷电的 ldconsole.exe，实例编号为 0–99。",true);return;}var candidate=new Config{EmulatorPath=Path.GetFullPath(ui.EmulatorPath),EmulatorIndex=index};AndroidClient.SavedPosition(candidate);if(!AndroidClient.Selected(config))Recovery.StopOwned(config.ClientPath);config.Backend="android";config.EmulatorPath=candidate.EmulatorPath;config.EmulatorIndex=index;}else{string path=ui.ClientPath;if(!Dashboard.ValidClient(path)){ui.Feedback("请选择已安装的智汇福大程序 fdxy_desktop.exe。",true);return;}path=Path.GetFullPath(path);if(!String.Equals(path,config.ClientPath,StringComparison.OrdinalIgnoreCase))Recovery.StopOwned(config.ClientPath);config.ClientPath=path;config.Backend="windows";}Store.Save(config);ui.Refresh(config);ui.Feedback("运行环境已保存，可到概览检查连接。安卓模式会在进入晚点名后自动刷新定位。",false);}catch(InvalidOperationException e){ui.Feedback(e.Message,true);}catch{ui.Feedback("保存未完成，请检查程序路径和雷电实例编号。",true);}}
 void ShowHiddenPage(){if(preview==null||preview.IsDisposed)preview=new HiddenPagePreview();if(!preview.Visible)preview.Show(this);preview.WindowState=FormWindowState.Normal;preview.Activate();}
 void LoginHelp(){MessageBox.Show(this,AndroidClient.Selected(config)?"1. 从托盘退出助手。\n2. 打开雷电，在模拟器内登录智汇福大。\n3. 在雷电右侧“定位”中确认当前位置。\n4. 重新启动助手，会进入晚点名并自动刷新定位。":"正常运行无需手动打开签到页。\n\n如果提示登录失效：\n1. 从托盘菜单选择“退出并关闭后台客户端”。\n2. 用原来的快捷方式打开智汇福大，自行完成登录，然后彻底退出它。\n3. 重新运行助手，会自动在后台恢复签到页。","重新登录");}
}
static class Startup {
 const string Key=@"Software\Microsoft\Windows\CurrentVersion\Run";const string Name="FzuAttendanceAssistant";
 public static bool Enabled(){using(var k=Registry.CurrentUser.OpenSubKey(Key)){return k!=null&&k.GetValue(Name)!=null;}}
 public static void Set(bool enabled){using(var k=Registry.CurrentUser.CreateSubKey(Key)){if(enabled)k.SetValue(Name,"\""+Application.ExecutablePath+"\" --background");else k.DeleteValue(Name,false);}}
}
static class Program {
 internal static readonly EventWaitHandle ShowRequest=new EventWaitHandle(false,EventResetMode.AutoReset,@"Local\FzuAttendanceAssistant.ShowSettings.v1");
 [STAThread]static int Main(string[] args){try{
  if(args.Contains("--capture-hidden")){File.WriteAllBytes(Store.FilePath("hidden-preview.png"),HiddenPagePreview.CaptureFrame().GetAwaiter().GetResult());return 0;}
  if(args.Contains("--render-ui")){Dashboard.Render(Path.Combine(Store.Dir,"ui-preview"),Store.Read());return 0;}
  if(args.Contains("--ui-test")){Dashboard.CheckInteractions(Store.Read());return 0;}
  if(args.Contains("--self-test")){Test();return 0;}
  if(args.Contains("--android-page-test")){AndroidTests.Run();return 0;}
  if(args.Contains("--android-probe")){var c=Store.Read();c.Backend="android";IntPtr before=AndroidClient.GetForegroundWindow();new Engine(c,null).Check(true,false,true).GetAwaiter().GetResult();File.WriteAllText(Store.FilePath("安卓检查.txt"),c.LastState+"\n前台窗口"+(before==AndroidClient.GetForegroundWindow()?"未改变":"由用户或其他程序改变")+"。本次未提交签到。");return 0;}
  if(args.Contains("--recover")){return Recovery.Run(Store.Read()).GetAwaiter().GetResult();}
  if(args.Contains("--enable-startup")){Startup.Set(true);return 0;}
  if(args.Contains("--disable-startup")){Startup.Set(false);return 0;}
  if(args.Contains("--probe")){new Engine(Store.Read(),null).Check(true,false,true).GetAwaiter().GetResult();return 0;}
  if(args.Contains("--bootstrap-test")){bool ok=Native.Bootstrap(Store.Read()).GetAwaiter().GetResult();File.WriteAllText(Store.FilePath("后台恢复测试.txt"),ok?"后台已找到原客户端签到页":"无法后台恢复签到页，需要手动连接");return ok?0:2;}
  if(!Standalone.CorrectSession(args))return 2;
  if(Standalone.HandOffIfNeeded(args))return 0;
  if(args.Contains("--lifetime-canary")){Standalone.Canary();return 0;}
  bool created;using(var mutex=new Mutex(true,@"Local\FzuAttendanceAssistant.v1",out created)){if(!created){if(!args.Contains("--background"))ShowRequest.Set();return 0;}Standalone.Record("runtime-lifetime.json");Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new MainForm(args.Contains("--background")));}return 0;
 }catch(Exception e){Store.Log(args.Contains("--lifetime-canary")?e.ToString():Store.SafeError(e));return 1;}}
 static void Test(){var c=new Config{Enabled=true};Action<bool,string> assert=delegate(bool value,string label){if(!value)throw new Exception(label);};DateTime d=new DateTime(2026,10,28,20,59,59);assert(!Rules.Due(c,d,true),"early");assert(Rules.Due(c,d.AddSeconds(1),true),"start catchup");assert(!Rules.Due(c,d.AddSeconds(1),false),"scheduled wait");assert(Rules.Due(c,new DateTime(2026,10,28,21,5,0),false),"scheduled time");assert(Rules.Due(c,new DateTime(2026,10,28,23,59,59),true),"end");assert(!Rules.Due(c,new DateTime(2026,10,29,0,0,0),true),"midnight");c.SuccessDate="2026-10-28";assert(!Rules.Due(c,new DateTime(2026,10,28,22,0,0),true),"dedup");assert(!Rules.ValidTime("20:59")&&Rules.ValidTime("21:00")&&Rules.ValidTime("23:59")&&!Rules.ValidTime("24:00"),"validation");var limitStart=new DateTime(2026,10,28,21,0,0);assert(!Engine.LimitExpired(limitStart,limitStart.AddSeconds(59),true)&&Engine.LimitExpired(limitStart,limitStart.AddSeconds(60),true)&&!Engine.LimitExpired(limitStart,limitStart.AddSeconds(65),false),"one-minute restart policy");File.WriteAllText(Store.FilePath("逻辑测试.txt"),"通过：时段边界、设定时间、启动补签、跨日、每日去重、时间范围校验、连续限流59秒不重启及60秒触发规则。");}
}
}





