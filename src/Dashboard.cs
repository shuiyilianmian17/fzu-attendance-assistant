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
 public UserControl Root; public Action SaveSchedule,SaveEnvironment,CheckConnection,AttemptNow,LoginHelp,ShowHiddenPage,RestartClient,RefreshLocation,ExportDiagnostics,OpenEmulator,CloseEmulator;
 public T Find<T>(string name)where T:class{return Root.FindName(name) as T;}
 public string EmulatorPath {get{return Find<TextBox>("EmulatorPath").Text.Trim().Trim('"');}}
 public string EmulatorIndex {get{return Find<TextBox>("EmulatorIndex").Text.Trim();}}
 public string Time {get{return Convert.ToString(Find<ComboBox>("HourPicker").SelectedItem)+":"+Convert.ToString(Find<ComboBox>("MinutePicker").SelectedItem);}}
 public bool Enabled {get{return Find<CheckBox>("EnabledToggle").IsChecked==true;}}
 public bool StartupEnabled {get{return Find<CheckBox>("StartupToggle").IsChecked==true;}}
 public Dashboard(Config config){
  using(var stream=typeof(Dashboard).Assembly.GetManifestResourceStream("FzuAttendance.Ui.xaml")){Root=(UserControl)XamlReader.Load(stream);}
  var image=Path.Combine(Store.Dir,"assets","app.png");if(File.Exists(image)){var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.UriSource=new Uri(image);bitmap.EndInit();Find<Image>("BrandIcon").Source=bitmap;}
  Find<ComboBox>("HourPicker").ItemsSource=new[]{"21","22","23"};Find<ComboBox>("MinutePicker").ItemsSource=Enumerable.Range(0,60).Select(x=>x.ToString("00")).ToArray();
  string chosen=Rules.ValidTime(config.Time)?config.Time:"21:05";Find<ComboBox>("HourPicker").SelectedItem=chosen.Split(':')[0];Find<ComboBox>("MinutePicker").SelectedItem=chosen.Split(':')[1];
  Find<CheckBox>("EnabledToggle").IsChecked=config.Enabled;Find<CheckBox>("StartupToggle").IsChecked=Startup.Enabled();
  Find<TextBox>("EmulatorPath").Text=config.EmulatorPath;Find<TextBox>("EmulatorIndex").Text=config.EmulatorIndex.ToString();
  string[] nav={"NavOverview","NavSchedule","NavEnvironment","NavLogs"};for(int i=0;i<nav.Length;i++){int index=i;Find<RadioButton>(nav[i]).Checked+=delegate{Page(index);};}
  Hook("SaveSchedule",delegate{if(SaveSchedule!=null)SaveSchedule();});Hook("SaveEnvironment",delegate{if(SaveEnvironment!=null)SaveEnvironment();});Hook("CheckConnection",delegate{if(CheckConnection!=null)CheckConnection();});Hook("AttemptNow",delegate{if(AttemptNow!=null)AttemptNow();});Hook("LoginHelp",delegate{if(LoginHelp!=null)LoginHelp();});
  Hook("OpenEmulator",delegate{if(OpenEmulator!=null)OpenEmulator();});Hook("CloseEmulator",delegate{if(CloseEmulator!=null)CloseEmulator();});
  Hook("RestartClient",delegate{if(RestartClient!=null)RestartClient();});Hook("RefreshLocation",delegate{if(RefreshLocation!=null)RefreshLocation();});Hook("ExportDiagnostics",delegate{if(ExportDiagnostics!=null)ExportDiagnostics();});Hook("ShowHiddenPage",delegate{if(ShowHiddenPage!=null)ShowHiddenPage();});Hook("GoLogs",delegate{Find<RadioButton>("NavLogs").IsChecked=true;});Hook("RefreshLogs",LoadLogs);
  Hook("BrowseEmulator",delegate{using(var dialog=new System.Windows.Forms.OpenFileDialog{Title="选择雷电或 MuMu 控制程序",Filter="模拟器控制程序|ldconsole.exe;mumu-cli.exe;MuMuManager.exe",CheckFileExists=true,FileName="ldconsole.exe"}){if(dialog.ShowDialog()==System.Windows.Forms.DialogResult.OK){Find<TextBox>("EmulatorPath").Text=dialog.FileName;Feedback("已选择雷电，保存运行环境后生效。",false);}}});
  Hook("DetectEmulator",delegate{string found=AndroidClient.Detect(EmulatorPath);if(found!=null){Find<TextBox>("EmulatorPath").Text=found;Feedback("已找到模拟器，保存运行环境后生效。",false);}else Feedback("没有找到雷电，请点“选择雷电”指定 ldconsole.exe。",true);});
  Find<TextBox>("EmulatorPath").TextChanged+=delegate{PathStatus();};Find<TextBox>("EmulatorIndex").TextChanged+=delegate{PathStatus();};
  Page(0);Refresh(config);LoadLogs();
 }
 void Hook(string name,Action callback){Find<Button>(name).Click+=delegate{callback();};}
 void PathStatus(){bool ok=AndroidClient.ValidConsole(EmulatorPath);var label=Find<TextBlock>("EnvironmentStatus");label.Text=ok?"已找到模拟器":"尚未找到模拟器";label.Foreground=new SolidColorBrush(ok?Color.FromRgb(36,124,104):Color.FromRgb(161,99,45));}
 public void Feedback(string text,bool error){var label=Find<TextBlock>("Feedback");label.Text=text;label.Foreground=new SolidColorBrush(error?Color.FromRgb(166,74,64):Color.FromRgb(60,106,147));}
 public void Page(int index){string[] nav={"NavOverview","NavSchedule","NavEnvironment","NavLogs"};if(Find<RadioButton>(nav[index]).IsChecked!=true)Find<RadioButton>(nav[index]).IsChecked=true;string[] pages={"OverviewPage","SchedulePage","EnvironmentPage","LogsPage"};string[] titles={"签到概览","签到设置","运行环境","运行记录"};string[] subtitles={"今天的签到安排与运行状态","设定时间，剩下的交给后台","选择雷电模拟器，配置应用运行方式","每一次检查与签到，都有记录"};for(int i=0;i<pages.Length;i++)Find<FrameworkElement>(pages[i]).Visibility=i==index?Visibility.Visible:Visibility.Collapsed;Find<TextBlock>("PageTitle").Text=titles[index];Find<TextBlock>("PageSubtitle").Text=subtitles[index];if(index==3)LoadLogs();}
 public void Refresh(Config config){var now=Store.Now();bool done=config.SuccessDate==now.ToString("yyyy-MM-dd");Find<TextBlock>("RunBadge").Text=config.ManualEmulator?"手动操作中":config.Enabled?"自动签到已开启":"自动签到已暂停";Find<TextBlock>("PlanValue").Text=config.Time;Find<TextBlock>("TodayValue").Text=done?"已签到":"待签到";Find<TextBlock>("StateText").Text=config.LastState;Find<TextBlock>("DateText").Text=now.ToString("MM月dd日 dddd",System.Globalization.CultureInfo.GetCultureInfo("zh-CN"));string next="下次计划：";TimeSpan planned;TimeSpan.TryParse(config.Time,out planned);next+=config.ManualEmulator?"手动操作中，关闭模拟器后恢复定时":!config.Enabled?"自动签到已暂停":Rules.Deferred(config,now)?config.EmulatorResumeAt:done?"明天 "+config.Time:now.TimeOfDay<=planned?"今天 "+config.Time:Rules.InWindow(now)?"当前在签到时段，将检查并尝试补签":"明天 "+config.Time;Find<TextBlock>("NextRun").Text=next;PathStatus();}
 public void LoadLogs(){try{string[] lines=File.Exists(Store.FilePath("运行日志.txt"))?File.ReadAllLines(Store.FilePath("运行日志.txt")):new string[0];Find<TextBox>("LogsText").Text=lines.Length==0?"暂无运行记录":String.Join(Environment.NewLine,lines.Reverse().Take(500).Reverse());Find<TextBox>("LogsText").ScrollToEnd();Find<TextBlock>("RecentLogs").Text=lines.Length==0?"暂无运行记录":String.Join(Environment.NewLine,lines.Reverse().Take(2).Reverse());}catch{Find<TextBlock>("RecentLogs").Text="记录暂时无法读取，请稍后刷新。";}}
 public static void CheckInteractions(Config config){
  var view=new Dashboard(config);Action<bool,string> require=delegate(bool ok,string message){if(!ok)throw new InvalidOperationException(message);};
  view.Find<RadioButton>("NavSchedule").IsChecked=true;require(view.Find<FrameworkElement>("SchedulePage").Visibility==Visibility.Visible,"导航没有切换");
  view.Find<ComboBox>("HourPicker").SelectedItem="23";view.Find<ComboBox>("MinutePicker").SelectedItem="59";string time=null;view.SaveSchedule=delegate{time=view.Time;};view.Find<Button>("SaveSchedule").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));require(time=="23:59","保存时间异常");
  view.Find<RadioButton>("NavEnvironment").IsChecked=true;require(view.Find<FrameworkElement>("EnvironmentPage").Visibility==Visibility.Visible,"环境导航异常");require(view.Root.FindName("WindowsOptions")==null&&view.Root.FindName("BackendPicker")==null,"仍残留旧客户端界面");
  int count=0;view.OpenEmulator=delegate{count++;};view.CloseEmulator=delegate{count++;};view.RefreshLocation=delegate{count++;};view.CheckConnection=delegate{count++;};view.RestartClient=delegate{count++;};view.ExportDiagnostics=delegate{count++;};view.SaveEnvironment=delegate{count++;};
  foreach(string name in new[]{"OpenEmulator","CloseEmulator","RefreshLocation","CheckConnection","RestartClient","ExportDiagnostics","SaveEnvironment"})view.Find<Button>(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));require(count==7,"按钮回调异常");
  File.WriteAllText(Store.FilePath("界面检查.txt"),"通过：导航、时间保存、安卓运行环境、旧模式移除与功能按钮回调。未提交真实签到。");
 }
 public static void Render(string dir,Config config){Directory.CreateDirectory(dir);var view=new Dashboard(config);string[] names={"overview","schedule","environment","logs"};for(int i=0;i<4;i++){view.Page(i);view.Root.Measure(new Size(1060,720));view.Root.Arrange(new Rect(0,0,1060,720));view.Root.UpdateLayout();view.Root.Dispatcher.Invoke(DispatcherPriority.Render,new Action(delegate{}));var bitmap=new RenderTargetBitmap(1060,720,96,96,PixelFormats.Pbgra32);bitmap.Render(view.Root);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(Path.Combine(dir,names[i]+".png")))encoder.Save(stream);}}
}
}



