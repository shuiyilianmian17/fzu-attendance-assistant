using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
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
 public static string SafeError(Exception e){return e is TimeoutException?"模拟器响应超时，请导出诊断查看连接状态。":"操作未完成（"+e.GetType().Name+"），请导出诊断。";}
}
public class Config {
 public string Time="21:05";
 public bool Enabled=false;
 public string SuccessDate="";
 public string LastState="请先在运行环境中选择雷电，并在签到设置中启用自动签到。";
 public string EmulatorPath="";
 public int EmulatorIndex=0;
}
static class Rules {
 public static bool InWindow(DateTime now){return now.TimeOfDay>=TimeSpan.FromHours(21)&&now.TimeOfDay<TimeSpan.FromDays(1);}
 public static bool ValidTime(string text){TimeSpan t;return TimeSpan.TryParseExact(text,"hh\\:mm",null,out t)&&t>=TimeSpan.FromHours(21)&&t<TimeSpan.FromDays(1);}
 public static bool Due(Config c,DateTime now,bool catchup){if(!c.Enabled||!InWindow(now)||c.SuccessDate==now.ToString("yyyy-MM-dd"))return false;TimeSpan at;return ValidTime(c.Time)&&TimeSpan.TryParse(c.Time,out at)&&(catchup||now.TimeOfDay>=at);}
}
class Engine {
 public Config Config; public Action<string> Update; public bool IsBusy {get{return busy;}}
 bool busy,restartRequested;DateTime retry=DateTime.MinValue;
 public Engine(Config c,Action<string> update){Config=c;Update=update;}
 public void State(string text){Config.LastState=text;Store.Save(Config);Store.Log(text);if(Update!=null)Update(text);}
 public static bool LimitExpired(DateTime? since,DateTime now,bool limited){return limited&&since.HasValue&&(now-since.Value).TotalSeconds>=60;}
 public async Task RestartClient(){restartRequested=true;if(busy){State("已请求重新打开智汇福大，正在结束本次检查…");return;}await Check(true,true,true);}
 public async Task Check(bool dry,bool catchup,bool force){
  if(busy)return;if(!restartRequested&&!dry&&!force&&(!Rules.Due(Config,Store.Now(),catchup)||Store.Now()<retry))return;
  busy=true;try{await AndroidClient.Check(Config,dry,force,()=>restartRequested,()=>restartRequested=false,State);}
  catch(Exception e){State(e is InvalidOperationException?e.Message:Store.SafeError(e));}
  finally{busy=false;if(!dry)retry=Store.Now().AddMinutes(5);}
 }
 public async Task RefreshLocation(){if(busy){State("正在处理签到，稍后会自动重新发送定位。");return;}await Check(true,false,true);}
}
class MainForm:Form {
 Config config;Engine engine;Dashboard ui;NotifyIcon tray;HiddenPagePreview preview;System.Windows.Forms.Timer timer,showRequests;bool quit,initial=true,hideInitial;DateTime lastTick=DateTime.MinValue;
 public MainForm(bool hidden){
  hideInitial=hidden;config=Store.Read();if(!Rules.ValidTime(config.Time))config.Time="21:05";if(!AndroidClient.ValidConsole(config.EmulatorPath)){string found=AndroidClient.Detect(config.EmulatorPath);if(found!=null){config.EmulatorPath=found;Store.Save(config);}}
  Text="晚点名助手 · v"+typeof(MainForm).Assembly.GetName().Version.ToString(3);Icon=System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath)??SystemIcons.Application;
  ClientSize=new Size(1060,720);MinimumSize=new Size(950,680);StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(242,245,250);
  ui=new Dashboard(config);var host=new System.Windows.Forms.Integration.ElementHost{Dock=DockStyle.Fill,Child=ui.Root};Controls.Add(host);
  engine=new Engine(config,delegate(string state){ui.Refresh(config);ui.LoadLogs();tray.Text=("晚点名助手 · "+state).Substring(0,Math.Min(63,("晚点名助手 · "+state).Length));});
  ui.SaveSchedule=SaveSchedule;ui.SaveEnvironment=SaveEnvironment;ui.CheckConnection=async delegate{await engine.Check(true,false,true);};ui.AttemptNow=async delegate{await engine.Check(false,true,true);};ui.LoginHelp=LoginHelp;ui.ShowHiddenPage=ShowHiddenPage;ui.RestartClient=async delegate{await engine.RestartClient();};ui.RefreshLocation=async delegate{await engine.RefreshLocation();};ui.ExportDiagnostics=ExportDiagnostics;
  var menu=new ContextMenuStrip();menu.Items.Add("打开设置",null,delegate{Show();WindowState=FormWindowState.Normal;Activate();});menu.Items.Add("检查连接",null,async delegate{await engine.Check(true,false,true);});menu.Items.Add("显示隐藏页",null,delegate{ShowHiddenPage();});menu.Items.Add("重新定位",null,async delegate{await engine.RefreshLocation();});menu.Items.Add("重新打开智汇福大",null,async delegate{await engine.RestartClient();});menu.Items.Add("导出诊断",null,delegate{ExportDiagnostics();});menu.Items.Add("退出助手",null,delegate{quit=true;Close();});menu.Items.Add("退出并关闭智汇福大",null,async delegate{if(engine.IsBusy){engine.State("正在处理签到，请暂停自动签到并等本次操作结束后退出。");return;}await AndroidClient.CloseApp(config);quit=true;Close();});
  tray=new NotifyIcon{Text="晚点名助手",Icon=this.Icon,Visible=true,ContextMenuStrip=menu};tray.DoubleClick+=delegate{Show();Activate();};
  FormClosing+=delegate(object sender,FormClosingEventArgs e){if(!quit&&e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();}else{tray.Visible=false;timer.Stop();showRequests.Stop();}};
  showRequests=new System.Windows.Forms.Timer{Interval=500};showRequests.Tick+=delegate{if(Program.ShowRequest.WaitOne(0)){Show();WindowState=FormWindowState.Normal;Activate();}};showRequests.Start();
  timer=new System.Windows.Forms.Timer{Interval=15000};timer.Tick+=async delegate{DateTime now=Store.Now();bool wake=lastTick!=DateTime.MinValue&&(now-lastTick).TotalSeconds>90;lastTick=now;ui.Refresh(config);await engine.Check(false,wake,false);};
  Shown+=delegate{StartService();};
 }
 protected override void SetVisibleCore(bool value){if(value&&hideInitial){hideInitial=false;if(!IsHandleCreated)CreateHandle();BeginInvoke(new Action(StartService));return;}base.SetVisibleCore(value);}
 async void StartService(){if(!initial)return;initial=false;timer.Start();lastTick=Store.Now();if(Rules.Due(config,Store.Now(),true))await engine.Check(false,true,false);else engine.State(config.Enabled?"助手已在后台等待，每天 "+config.Time+" 启动或连接模拟器并签到。":"自动签到已暂停。");}
 void SaveSchedule(){string chosen=ui.Time;if(!Rules.ValidTime(chosen)){ui.Feedback("请选择 21:00–23:59 内的时间。",true);return;}try{Startup.Set(ui.StartupEnabled);config.Time=chosen;config.Enabled=ui.Enabled;Store.Save(config);ui.Refresh(config);ui.Feedback("已保存：每天 "+chosen+"，"+(config.Enabled?"自动签到已开启。":"自动签到已暂停。"),false);}catch{ui.Feedback("保存未完成，请检查当前账户的写入权限。",true);}}
 void SaveEnvironment(){if(engine.IsBusy){ui.Feedback("正在检查签到状态，请等待完成后再保存运行环境。",true);return;}try{int index;if(!AndroidClient.ValidConsole(ui.EmulatorPath)||!Int32.TryParse(ui.EmulatorIndex,out index)||index<0||index>99){ui.Feedback("请选择雷电的 ldconsole.exe，实例编号为 0–99。",true);return;}var candidate=new Config{EmulatorPath=Path.GetFullPath(ui.EmulatorPath),EmulatorIndex=index};AndroidClient.SavedPosition(candidate);config.EmulatorPath=candidate.EmulatorPath;config.EmulatorIndex=index;Store.Save(config);ui.Refresh(config);ui.Feedback("运行环境已保存，可到概览检查连接。",false);}catch(InvalidOperationException e){ui.Feedback(e.Message,true);}catch{ui.Feedback("保存未完成，请检查程序路径和雷电实例编号。",true);}}
 void ShowHiddenPage(){if(preview==null||preview.IsDisposed)preview=new HiddenPagePreview();if(!preview.Visible)preview.Show(this);preview.WindowState=FormWindowState.Normal;preview.Activate();}
 async void ExportDiagnostics(){using(var dialog=new System.Windows.Forms.SaveFileDialog{Title="导出诊断",Filter="诊断文本 (*.txt)|*.txt",FileName="晚点名助手诊断.txt"}){if(dialog.ShowDialog(this)!=DialogResult.OK)return;try{File.WriteAllText(dialog.FileName,await AndroidClient.Diagnostic(config),Encoding.UTF8);ui.Feedback("诊断已导出，不含账号、坐标或截图；本次未提交签到。",false);}catch{ui.Feedback("诊断导出未完成，请确认保存位置可写。",true);}}}
 void LoginHelp(){MessageBox.Show(this,"1. 从托盘退出助手。\n2. 在雷电中打开智汇福大，自行完成登录。\n3. 确认晚点名签到已加入我的服务。\n4. 重新运行助手，再检查连接。","重新登录");}
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
  if(args.Contains("--export-diagnostics")){File.WriteAllText(Store.FilePath("晚点名助手诊断.txt"),AndroidClient.Diagnostic(Store.Read()).GetAwaiter().GetResult(),Encoding.UTF8);return 0;}
  if(args.Contains("--android-probe")){var c=Store.Read();IntPtr before=AndroidClient.GetForegroundWindow();new Engine(c,null).Check(true,false,true).GetAwaiter().GetResult();File.WriteAllText(Store.FilePath("安卓检查.txt"),c.LastState+"\n前台窗口"+(before==AndroidClient.GetForegroundWindow()?"未改变":"由用户或其他程序改变")+"。本次未提交签到。");return 0;}
  if(args.Contains("--enable-startup")){Startup.Set(true);return 0;}
  if(args.Contains("--disable-startup")){Startup.Set(false);return 0;}
  if(!Standalone.CorrectSession(args))return 2;
  if(Standalone.HandOffIfNeeded(args))return 0;
  if(args.Contains("--lifetime-canary")){Standalone.Canary();return 0;}
  bool created;using(var mutex=new Mutex(true,@"Local\FzuAttendanceAssistant.v1",out created)){if(!created){if(!args.Contains("--background"))ShowRequest.Set();return 0;}Standalone.Record("runtime-lifetime.json");Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new MainForm(args.Contains("--background")));}return 0;
 }catch(Exception e){Store.Log(args.Contains("--lifetime-canary")?e.ToString():Store.SafeError(e));return 1;}}
 static void Test(){var legacy=Store.Json.Deserialize<Config>("{\"Time\":\"22:35\",\"Enabled\":true,\"Backend\":\"android\",\"ClientPath\":\"obsolete\",\"EmulatorIndex\":2}");if(legacy.Time!="22:35"||!legacy.Enabled||legacy.EmulatorIndex!=2)throw new Exception("settings migration");var c=new Config{Enabled=true};Action<bool,string> assert=delegate(bool value,string label){if(!value)throw new Exception(label);};DateTime d=new DateTime(2026,10,28,20,59,59);assert(!Rules.Due(c,d,true),"early");assert(Rules.Due(c,d.AddSeconds(1),true),"start catchup");assert(!Rules.Due(c,d.AddSeconds(1),false),"scheduled wait");assert(Rules.Due(c,new DateTime(2026,10,28,21,5,0),false),"scheduled time");assert(Rules.Due(c,new DateTime(2026,10,28,23,59,59),true),"end");assert(!Rules.Due(c,new DateTime(2026,10,29,0,0,0),true),"midnight");c.SuccessDate="2026-10-28";assert(!Rules.Due(c,new DateTime(2026,10,28,22,0,0),true),"dedup");assert(!Rules.ValidTime("20:59")&&Rules.ValidTime("21:00")&&Rules.ValidTime("23:59")&&!Rules.ValidTime("24:00"),"validation");var limitStart=new DateTime(2026,10,28,21,0,0);assert(!Engine.LimitExpired(limitStart,limitStart.AddSeconds(59),true)&&Engine.LimitExpired(limitStart,limitStart.AddSeconds(60),true)&&!Engine.LimitExpired(limitStart,limitStart.AddSeconds(65),false),"one-minute restart policy");File.WriteAllText(Store.FilePath("逻辑测试.txt"),"通过：时段边界、设定时间、启动补签、跨日、每日去重、时间范围校验、连续限流59秒不重启及60秒触发规则。");}
}
}





