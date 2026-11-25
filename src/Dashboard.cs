using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Markup;
using System.Windows.Threading;

namespace FzuAttendance {
sealed class Dashboard {
 public UserControl Root; public Action SaveSchedule,SaveEnvironment,CheckConnection,AttemptNow,LoginHelp,ShowHiddenPage,RestartClient,RefreshLocation,ExportDiagnostics,OpenEmulator,CloseEmulator,CheckUpdate;
 public Action SaveBackend,SavePosition;
 public bool ApiSelected {get{return Find<ComboBox>("SignInMethod").SelectedIndex==1;}}
 public string ApiAccount {get{return Find<TextBox>("ApiAccount").Text.Trim();}}
 public string ApiPassword {get{return Find<TextBox>("ApiPasswordVisible").Visibility==Visibility.Visible?Find<TextBox>("ApiPasswordVisible").Text:Find<PasswordBox>("ApiPassword").Password;}}
 public void ClearApiPassword(){Find<PasswordBox>("ApiPassword").Clear();Find<TextBox>("ApiPasswordVisible").Clear();SetPasswordVisible(false);}
 public void LoadSavedApiPassword(Config config){
  ClearApiPassword();
  if(String.IsNullOrEmpty(config.ApiSecret))return;
  try{Find<PasswordBox>("ApiPassword").Password=ApiBackend.Password(config);}
  catch(InvalidOperationException){Feedback("已保存的密码无法在当前 Windows 用户下解密，请重新填写。",true);}
 }
 void SetPasswordVisible(bool visible){
  var hidden=Find<PasswordBox>("ApiPassword");var plain=Find<TextBox>("ApiPasswordVisible");
  if((plain.Visibility==Visibility.Visible)==visible)return;
  if(visible){plain.Text=hidden.Password;hidden.Clear();}else{hidden.Password=plain.Text;plain.Clear();}
  hidden.Visibility=visible?Visibility.Collapsed:Visibility.Visible;
  plain.Visibility=visible?Visibility.Visible:Visibility.Collapsed;
  Find<System.Windows.Shapes.Path>("PasswordEyeSlash").Visibility=visible?Visibility.Visible:Visibility.Collapsed;
  var button=Find<Button>("ToggleApiPassword");string label=visible?"隐藏密码":"显示密码";button.ToolTip=label;
  System.Windows.Automation.AutomationProperties.SetName(button,label);
 }
 public Action<bool> SetAutoCloseEmulator;
 public T Find<T>(string name)where T:class{return Root.FindName(name) as T;}
 public string EmulatorPath {get{return Find<TextBox>("EmulatorPath").Text.Trim().Trim('"');}}
 public string EmulatorIndex {get{return Find<TextBox>("EmulatorIndex").Text.Trim();}}
 public string FixedPosition {get{return Find<TextBox>("FixedLongitude").Text.Trim()+","+Find<TextBox>("FixedLatitude").Text.Trim();}}
 public string Time {get{return Convert.ToString(Find<ComboBox>("HourPicker").SelectedItem)+":"+Convert.ToString(Find<ComboBox>("MinutePicker").SelectedItem);}}
 public bool Enabled {get{return Find<CheckBox>("EnabledToggle").IsChecked==true;}}
 public bool StartupEnabled {get{return Find<CheckBox>("StartupToggle").IsChecked==true;}}
 public Dashboard(Config config){
  using(var stream=typeof(Dashboard).Assembly.GetManifestResourceStream("FzuAttendance.Ui.xaml")){Root=(UserControl)XamlReader.Load(stream);}
  Find<ComboBox>("SignInMethod").ItemsSource=new[]{"模拟器签到","接口签到"};
  Find<ComboBox>("SignInMethod").SelectedIndex=config.Backend=="api"?1:0;
  Find<TextBox>("ApiAccount").Text=config.ApiAccount;LoadSavedApiPassword(config);
  Hook("ToggleApiPassword",delegate{SetPasswordVisible(Find<TextBox>("ApiPasswordVisible").Visibility!=Visibility.Visible);});
  Hook("SavePosition",delegate{if(SavePosition!=null)SavePosition();});
  Hook("SaveBackend",delegate{if(SaveBackend!=null)SaveBackend();});
  var image=Path.Combine(Store.Dir,"assets","app.png");if(File.Exists(image)){var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.UriSource=new Uri(image);bitmap.EndInit();Find<Image>("BrandIcon").Source=bitmap;}
  Find<ComboBox>("HourPicker").ItemsSource=new[]{"21","22","23"};Find<ComboBox>("MinutePicker").ItemsSource=Enumerable.Range(0,60).Select(x=>x.ToString("00")).ToArray();
  string chosen=Rules.ValidTime(config.Time)?config.Time:"21:05";Find<ComboBox>("HourPicker").SelectedItem=chosen.Split(':')[0];Find<ComboBox>("MinutePicker").SelectedItem=chosen.Split(':')[1];
  Find<CheckBox>("EnabledToggle").IsChecked=config.Enabled;Find<CheckBox>("StartupToggle").IsChecked=Startup.Enabled();
  Find<TextBox>("EmulatorPath").Text=config.EmulatorPath;Find<TextBox>("EmulatorIndex").Text=config.EmulatorIndex.ToString();
  var point=(config.FixedPosition??"").Split(',');if(point.Length==2){Find<TextBox>("FixedLongitude").Text=point[0];Find<TextBox>("FixedLatitude").Text=point[1];}
  Hook("ResetPosition",delegate{var p=Config.DefaultPosition.Split(',');Find<TextBox>("FixedLongitude").Text=p[0];Find<TextBox>("FixedLatitude").Text=p[1];Feedback("已填入旗山校区默认位置，点击保存签到位置后生效。",false);});
  Hook("ReadPosition",delegate{try{int index;if(!Int32.TryParse(EmulatorIndex,out index))throw new InvalidOperationException("请确认模拟器编号。");var c=new Config{EmulatorPath=EmulatorPath,EmulatorIndex=index,FixedPosition=""};string raw=MuMuClient.Is(c)?MuMuClient.ReadDefaultPosition(c):AndroidClient.SavedPosition(c);var p=raw.Split(',');Find<TextBox>("FixedLongitude").Text=p[0];Find<TextBox>("FixedLatitude").Text=p[1];Feedback("已读取默认位置，请核对是你实际选择的位置后，点击保存签到位置。",false);}catch(Exception e){Feedback(e is InvalidOperationException?e.Message:"未能读取，请填写经度和纬度。",true);}});
  string[] nav={"NavOverview","NavSchedule","NavEnvironment","NavLogs"};for(int i=0;i<nav.Length;i++){int index=i;Find<RadioButton>(nav[i]).Checked+=delegate{Page(index);};}
  Hook("SaveSchedule",delegate{if(SaveSchedule!=null)SaveSchedule();});Hook("SaveEnvironment",delegate{if(SaveEnvironment!=null)SaveEnvironment();});Hook("CheckConnection",delegate{if(CheckConnection!=null)CheckConnection();});Hook("AttemptNow",delegate{if(AttemptNow!=null)AttemptNow();});Hook("LoginHelp",delegate{if(LoginHelp!=null)LoginHelp();});
  Hook("CheckUpdate",delegate{if(CheckUpdate!=null)CheckUpdate();});
  Hook("OpenEmulator",delegate{if(OpenEmulator!=null)OpenEmulator();});Hook("CloseEmulator",delegate{if(CloseEmulator!=null)CloseEmulator();});
  Hook("RestartClient",delegate{if(RestartClient!=null)RestartClient();});Hook("RefreshLocation",delegate{if(RefreshLocation!=null)RefreshLocation();});Hook("ExportDiagnostics",delegate{if(ExportDiagnostics!=null)ExportDiagnostics();});Hook("ShowHiddenPage",delegate{if(ShowHiddenPage!=null)ShowHiddenPage();});Hook("GoLogs",delegate{Find<RadioButton>("NavLogs").IsChecked=true;});Hook("RefreshLogs",LoadLogs);
  Hook("BrowseEmulator",delegate{using(var dialog=new System.Windows.Forms.OpenFileDialog{Title="选择 MuMu 控制程序（兼容雷电）",Filter="模拟器控制程序|ldconsole.exe;mumu-cli.exe;MuMuManager.exe",CheckFileExists=true,FileName="mumu-cli.exe"}){if(dialog.ShowDialog()==System.Windows.Forms.DialogResult.OK){Find<TextBox>("EmulatorPath").Text=dialog.FileName;Feedback("已选择模拟器，点击保存签到位置后生效。",false);}}});
  Hook("DetectEmulator",async delegate{var button=Find<Button>("DetectEmulator");string previous=EmulatorPath;button.IsEnabled=false;Feedback("正在优先查找 MuMu…",false);try{string found=await System.Threading.Tasks.Task.Run(()=>AndroidClient.Detect(previous));if(found!=null){Find<TextBox>("EmulatorPath").Text=found;Feedback((MuMuClient.IsPath(found)?"已找到 MuMu":"已找到雷电（兼容模式）")+"，点击保存签到位置后生效。",false);}else Feedback("未找到 MuMu，请点“选择模拟器”选择 nx_main 中的 mumu-cli.exe。",true);}catch{Feedback("查找未完成，请手动选择 MuMu 控制程序。",true);}finally{button.IsEnabled=true;}});
  Find<TextBox>("EmulatorPath").TextChanged+=delegate{PathStatus();};Find<TextBox>("EmulatorIndex").TextChanged+=delegate{PathStatus();};
  var autoClose=Find<CheckBox>("AutoCloseEmulator");autoClose.Click+=delegate{if(SetAutoCloseEmulator!=null)SetAutoCloseEmulator(autoClose.IsChecked==true);};
  Page(0);Refresh(config);LoadLogs();
 }
 void Hook(string name,Action callback){Find<Button>(name).Click+=delegate{callback();};}
 void PathStatus(){bool ok=AndroidClient.ValidConsole(EmulatorPath);var label=Find<TextBlock>("EnvironmentStatus");label.Text=ok?"已找到模拟器":"尚未找到模拟器";label.Foreground=new SolidColorBrush(ok?Color.FromRgb(36,124,104):Color.FromRgb(161,99,45));}
 public void Feedback(string text,bool error){var label=Find<TextBlock>("Feedback");label.Text=text;label.Foreground=new SolidColorBrush(error?Color.FromRgb(166,74,64):Color.FromRgb(60,106,147));}
 public void Page(int index){string[] nav={"NavOverview","NavSchedule","NavEnvironment","NavLogs"};if(Find<RadioButton>(nav[index]).IsChecked!=true)Find<RadioButton>(nav[index]).IsChecked=true;string[] pages={"OverviewPage","SchedulePage","EnvironmentPage","LogsPage"};string[] titles={"签到概览","签到设置","运行环境","运行记录"};string[] subtitles={"今天的签到安排与运行状态","设定时间，剩下的交给后台","优先使用 MuMu，兼容雷电模拟器","每一次检查与签到，都有记录"};for(int i=0;i<pages.Length;i++)Find<FrameworkElement>(pages[i]).Visibility=i==index?Visibility.Visible:Visibility.Collapsed;Find<TextBlock>("PageTitle").Text=titles[index];Find<TextBlock>("PageSubtitle").Text=subtitles[index];if(index==3)LoadLogs();}
 public void Refresh(Config config){
  bool api=config.Backend=="api";
  foreach(string name in new[]{"ShowHiddenPage","RestartClient","RefreshLocation","OpenEmulator","CloseEmulator"})Find<Button>(name).IsEnabled=!api;
  Find<TextBlock>("ApiAccountState").Text=String.IsNullOrEmpty(config.ApiSecret)?"请填写账号与密码":"密码已加密保存，点击眼睛可查看";
Find<CheckBox>("AutoCloseEmulator").IsChecked=config.AutoCloseEmulator;var now=Store.Now();bool done=config.SuccessDate==now.ToString("yyyy-MM-dd");
var greenBackground=new SolidColorBrush(Color.FromRgb(230,245,237));
var greenText=new SolidColorBrush(Color.FromRgb(28,111,70));
bool enabled=config.Enabled&&!config.ManualEmulator;
Find<Border>("RunBadgeCard").Background=enabled?greenBackground:new SolidColorBrush(Color.FromRgb(228,237,249));
Find<TextBlock>("RunBadge").Foreground=enabled?greenText:new SolidColorBrush(Color.FromRgb(24,52,82));
Find<Border>("TodayCard").Background=done?greenBackground:Brushes.White;
Find<Border>("TodayCard").BorderBrush=done?new SolidColorBrush(Color.FromRgb(170,217,190)):new SolidColorBrush(Color.FromRgb(225,232,241));
Find<TextBlock>("TodayValue").Foreground=done?greenText:new SolidColorBrush(Color.FromRgb(24,52,82));
foreach(string label in new[]{"TodayLabel","TodayHint"})Find<TextBlock>(label).Foreground=done?greenText:new SolidColorBrush(Color.FromRgb(120,137,159));Find<TextBlock>("RunBadge").Text=config.ManualEmulator?"手动操作中":config.Enabled?"自动签到已开启":"自动签到已暂停";Find<TextBlock>("PlanValue").Text=config.Time;Find<TextBlock>("TodayValue").Text=done?"已签到":"待签到";Find<TextBlock>("StateText").Text=config.LastState;Find<TextBlock>("DateText").Text=now.ToString("MM月dd日 dddd",System.Globalization.CultureInfo.GetCultureInfo("zh-CN"));string next="下次计划：";TimeSpan planned;TimeSpan.TryParse(config.Time,out planned);next+=config.ManualEmulator?"手动操作中，关闭模拟器后恢复定时":!config.Enabled?"自动签到已暂停":done?"明天 "+config.Time:now.TimeOfDay<=planned?"今天 "+config.Time:Rules.InWindow(now)?"当前在签到时段，将检查并尝试补签":"明天 "+config.Time;Find<TextBlock>("NextRun").Text=next;PathStatus();}
 public void LoadLogs(){try{string[] lines=File.Exists(Store.FilePath("运行日志.txt"))?File.ReadAllLines(Store.FilePath("运行日志.txt")):new string[0];Find<TextBox>("LogsText").Text=lines.Length==0?"暂无运行记录":String.Join(Environment.NewLine,lines.Reverse().Take(500).Reverse());Find<TextBox>("LogsText").ScrollToEnd();Find<TextBlock>("RecentLogs").Text=lines.Length==0?"暂无运行记录":String.Join(Environment.NewLine,lines.Reverse().Take(2).Reverse());}catch{Find<TextBlock>("RecentLogs").Text="记录暂时无法读取，请稍后刷新。";}}
 public static void CheckInteractions(Config config){
  var view=new Dashboard(config);Action<bool,string> require=delegate(bool ok,string message){if(!ok)throw new InvalidOperationException(message);};
  view.Find<PasswordBox>("ApiPassword").Password="test-password";view.Find<Button>("ToggleApiPassword").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));require(view.ApiPassword=="test-password"&&view.Find<TextBox>("ApiPasswordVisible").Visibility==Visibility.Visible,"显示密码失败");view.Find<TextBox>("ApiPasswordVisible").Text="edited-password";view.Find<Button>("ToggleApiPassword").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));require(view.ApiPassword=="edited-password"&&view.Find<TextBox>("ApiPasswordVisible").Text=="","隐藏密码未保留编辑或清除明文");view.Find<Button>("ToggleApiPassword").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));view.ClearApiPassword();require(view.ApiPassword==""&&view.Find<TextBox>("ApiPasswordVisible").Visibility==Visibility.Collapsed,"保存后应清空并隐藏密码");
  var savedPassword=new Config{ApiSecret=ApiBackend.Protect("SavedExample123!")};view.LoadSavedApiPassword(savedPassword);require(view.ApiPassword=="SavedExample123!"&&view.Find<PasswordBox>("ApiPassword").Visibility==Visibility.Visible,"保存后应以黑点保留密码");view.Find<Button>("ToggleApiPassword").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));require(view.Find<TextBox>("ApiPasswordVisible").Text=="SavedExample123!","保存后眼睛应显示原文");var reopened=new Dashboard(savedPassword);require(reopened.ApiPassword=="SavedExample123!"&&reopened.Find<PasswordBox>("ApiPassword").Visibility==Visibility.Visible,"重新打开应加载已保存密码");view.ClearApiPassword();
  view.Page(2);view.Find<PasswordBox>("ApiPassword").Password="Example123!";view.SetPasswordVisible(true);
  for(int passwordState=0;passwordState<2;passwordState++){
   view.Root.Measure(new Size(1060,720));view.Root.Arrange(new Rect(0,0,1060,720));view.Root.UpdateLayout();
   var passwordImage=new RenderTargetBitmap(1060,720,96,96,PixelFormats.Pbgra32);passwordImage.Render(view.Root);
   var passwordEncoder=new PngBitmapEncoder();passwordEncoder.Frames.Add(BitmapFrame.Create(passwordImage));
   using(var passwordStream=File.Create(Store.FilePath(passwordState==0?"password-visible-test.png":"password-hidden-test.png")))passwordEncoder.Save(passwordStream);
   view.SetPasswordVisible(false);
  }
  view.ClearApiPassword();
  view.Refresh(new Config{Enabled=true,SuccessDate=Store.Now().ToString("yyyy-MM-dd")});
  require(((SolidColorBrush)view.Find<Border>("RunBadgeCard").Background).Color==Color.FromRgb(230,245,237),"开启时应显示绿色");
  require(((SolidColorBrush)view.Find<Border>("TodayCard").Background).Color==Color.FromRgb(230,245,237),"已签到卡片应整体绿色");
  view.Refresh(new Config{Enabled=false,SuccessDate=Store.Now().AddDays(-1).ToString("yyyy-MM-dd")});
  require(((SolidColorBrush)view.Find<Border>("TodayCard").Background).Color==Colors.White,"跨日应恢复待签到卡片");
  view.Find<RadioButton>("NavSchedule").IsChecked=true;require(view.Find<FrameworkElement>("SchedulePage").Visibility==Visibility.Visible,"导航没有切换");
  view.Find<ComboBox>("HourPicker").SelectedItem="23";view.Find<ComboBox>("MinutePicker").SelectedItem="59";string time=null;view.SaveSchedule=delegate{time=view.Time;};view.Find<Button>("SaveSchedule").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));require(time=="23:59","保存时间异常");
  view.Find<RadioButton>("NavEnvironment").IsChecked=true;require(view.Find<FrameworkElement>("EnvironmentPage").Visibility==Visibility.Visible,"环境导航异常");require(view.Root.FindName("WindowsOptions")==null&&view.Root.FindName("BackendPicker")==null,"仍残留旧客户端界面");
  view.Find<Button>("ResetPosition").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));require(view.FixedPosition==Config.DefaultPosition,"恢复默认位置按钮异常");
  bool apiSaved=false;view.SaveBackend=delegate{apiSaved=view.ApiSelected;};view.Find<ComboBox>("SignInMethod").SelectedIndex=1;view.Find<Button>("SaveBackend").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));require(apiSaved,"接口方式保存回调异常");view.Refresh(new Config{Backend="api"});require(!view.Find<Button>("OpenEmulator").IsEnabled,"接口模式应禁用模拟器入口");view.Refresh(new Config());require(view.Find<Button>("OpenEmulator").IsEnabled,"模拟器模式应恢复入口");
  int count=0;view.CheckUpdate=delegate{count++;};view.OpenEmulator=delegate{count++;};view.CloseEmulator=delegate{count++;};view.RefreshLocation=delegate{count++;};view.CheckConnection=delegate{count++;};view.RestartClient=delegate{count++;};view.ExportDiagnostics=delegate{count++;};view.SaveEnvironment=delegate{count++;};
  foreach(string name in new[]{"CheckUpdate","OpenEmulator","CloseEmulator","RefreshLocation","CheckConnection","RestartClient","ExportDiagnostics","SaveEnvironment"})view.Find<Button>(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));require(count==8,"按钮回调异常");
  File.WriteAllText(Store.FilePath("界面检查.txt"),"通过：导航、时间保存、安卓运行环境、旧模式移除与功能按钮回调。未提交真实签到。");
 }
 public static void Render(string dir,Config config){Directory.CreateDirectory(dir);var view=new Dashboard(config);string[] names={"overview","schedule","environment","logs"};for(int i=0;i<4;i++){view.Page(i);view.Root.Measure(new Size(1060,720));view.Root.Arrange(new Rect(0,0,1060,720));view.Root.UpdateLayout();view.Root.Dispatcher.Invoke(DispatcherPriority.Render,new Action(delegate{}));var bitmap=new RenderTargetBitmap(1060,720,96,96,PixelFormats.Pbgra32);bitmap.Render(view.Root);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(Path.Combine(dir,names[i]+".png")))encoder.Save(stream);}}
}
}



