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
 public UserControl Root; public Action SaveSchedule,SaveClient,CheckConnection,AttemptNow,LoginHelp,ShowHiddenPage,RestartClient,RefreshLocation;
 public T Find<T>(string name)where T:class{return Root.FindName(name) as T;}
 public string ClientPath {get{return Find<TextBox>("ClientPath").Text.Trim().Trim('"');}}
 public bool UseAndroid {get{return Find<ComboBox>("BackendPicker").SelectedIndex==0;}}
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
  Find<CheckBox>("EnabledToggle").IsChecked=config.Enabled;Find<CheckBox>("StartupToggle").IsChecked=Startup.Enabled();Find<TextBox>("ClientPath").Text=config.ClientPath;
  Find<ComboBox>("BackendPicker").ItemsSource=new[]{"安卓模拟器（雷电）","Windows 客户端（旧模式）"};Find<ComboBox>("BackendPicker").SelectedIndex=AndroidClient.Selected(config)?0:1;Find<TextBox>("EmulatorPath").Text=config.EmulatorPath;Find<TextBox>("EmulatorIndex").Text=config.EmulatorIndex.ToString();Find<ComboBox>("BackendPicker").SelectionChanged+=delegate{PathStatus();};
  string[] nav={"NavOverview","NavSchedule","NavClient","NavLogs"};for(int i=0;i<nav.Length;i++){int index=i;Find<RadioButton>(nav[i]).Checked+=delegate{Page(index);};}
  Hook("SaveSchedule",delegate{if(SaveSchedule!=null)SaveSchedule();});Hook("SaveClient",delegate{if(SaveClient!=null)SaveClient();});Hook("CheckConnection",delegate{if(CheckConnection!=null)CheckConnection();});Hook("AttemptNow",delegate{if(AttemptNow!=null)AttemptNow();});Hook("LoginHelp",delegate{if(LoginHelp!=null)LoginHelp();});
  Hook("RestartClient",delegate{if(RestartClient!=null)RestartClient();});Hook("RefreshLocation",delegate{if(RefreshLocation!=null)RefreshLocation();});Hook("ShowHiddenPage",delegate{if(ShowHiddenPage!=null)ShowHiddenPage();});Hook("GoLogs",delegate{Find<RadioButton>("NavLogs").IsChecked=true;});Hook("RefreshLogs",LoadLogs);
  Hook("BrowseEmulator",delegate{using(var dialog=new System.Windows.Forms.OpenFileDialog{Title="选择雷电模拟器控制程序",Filter="雷电控制程序 (ldconsole.exe)|ldconsole.exe",CheckFileExists=true,FileName="ldconsole.exe"}){if(dialog.ShowDialog()==System.Windows.Forms.DialogResult.OK){Find<TextBox>("EmulatorPath").Text=dialog.FileName;Feedback("已选择雷电，保存运行环境后生效。",false);}}});
  Hook("DetectEmulator",delegate{string found=AndroidClient.Detect(EmulatorPath);if(found!=null){Find<TextBox>("EmulatorPath").Text=found;Feedback("已找到雷电，保存运行环境后生效。",false);}else Feedback("没有找到雷电，请点“选择雷电”指定 ldconsole.exe。",true);});
  Find<TextBox>("EmulatorPath").TextChanged+=delegate{PathStatus();};Find<TextBox>("EmulatorIndex").TextChanged+=delegate{PathStatus();};
  Hook("BrowseClient",delegate{using(var dialog=new System.Windows.Forms.OpenFileDialog{Title="选择智汇福大 Windows 客户端",Filter="智汇福大程序 (fdxy_desktop.exe)|fdxy_desktop.exe",CheckFileExists=true,FileName="fdxy_desktop.exe"}){if(dialog.ShowDialog()==System.Windows.Forms.DialogResult.OK){Find<TextBox>("ClientPath").Text=dialog.FileName;PathStatus();Feedback("已选择程序，点击“保存客户端路径”后生效。",false);}}});
  Hook("DetectClient",delegate{string found=Detect(config.ClientPath);if(found!=null){Find<TextBox>("ClientPath").Text=found;PathStatus();Feedback("已找到客户端，点击“保存客户端路径”后生效。",false);}else Feedback("没有找到默认安装位置，请先安装客户端，或点“选择程序”。",true);});
  Find<TextBox>("ClientPath").TextChanged+=delegate{PathStatus();};Page(0);Refresh(config);LoadLogs();
 }
 void Hook(string name,Action callback){Find<Button>(name).Click+=delegate{callback();};}
 public static bool ValidClient(string path){return !String.IsNullOrWhiteSpace(path)&&File.Exists(path)&&String.Equals(Path.GetFileName(path),"fdxy_desktop.exe",StringComparison.OrdinalIgnoreCase);}
 public static string Detect(string current){string[] candidates={current,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"cn.edu.fzu.fdxypd","fdxy_desktop.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"cn.edu.fzu.fdxypd","fdxy_desktop.exe")};return candidates.FirstOrDefault(ValidClient);}
 void PathStatus(){Find<FrameworkElement>("EmulatorOptions").Visibility=UseAndroid?Visibility.Visible:Visibility.Collapsed;Find<FrameworkElement>("WindowsOptions").Visibility=UseAndroid?Visibility.Collapsed:Visibility.Visible;bool ok=UseAndroid?AndroidClient.ValidConsole(EmulatorPath):ValidClient(ClientPath);var label=Find<TextBlock>("ClientStatus");label.Text=ok?"已找到程序":"尚未找到程序";label.Foreground=new SolidColorBrush(ok?Color.FromRgb(36,124,104):Color.FromRgb(161,99,45));}
 public void Feedback(string text,bool error){var label=Find<TextBlock>("Feedback");label.Text=text;label.Foreground=new SolidColorBrush(error?Color.FromRgb(166,74,64):Color.FromRgb(60,106,147));}
 public void Page(int index){string[] nav={"NavOverview","NavSchedule","NavClient","NavLogs"};if(Find<RadioButton>(nav[index]).IsChecked!=true)Find<RadioButton>(nav[index]).IsChecked=true;string[] pages={"OverviewPage","SchedulePage","ClientPage","LogsPage"};string[] titles={"签到概览","签到设置","运行环境","运行记录"};string[] subtitles={"今天的签到安排与运行状态","设定时间，剩下的交给后台","选择雷电模拟器，配置应用运行方式","每一次检查与签到，都有记录"};for(int i=0;i<pages.Length;i++)Find<FrameworkElement>(pages[i]).Visibility=i==index?Visibility.Visible:Visibility.Collapsed;Find<TextBlock>("PageTitle").Text=titles[index];Find<TextBlock>("PageSubtitle").Text=subtitles[index];if(index==3)LoadLogs();}
 public void Refresh(Config config){var now=Store.Now();bool done=config.SuccessDate==now.ToString("yyyy-MM-dd");Find<TextBlock>("RunBadge").Text=config.Enabled?"自动签到已开启":"自动签到已暂停";Find<TextBlock>("PlanValue").Text=config.Time;Find<TextBlock>("TodayValue").Text=done?"已签到":"待签到";Find<TextBlock>("StateText").Text=config.LastState;Find<TextBlock>("DateText").Text=now.ToString("MM月dd日 dddd",System.Globalization.CultureInfo.GetCultureInfo("zh-CN"));string next="下次计划：";TimeSpan planned;TimeSpan.TryParse(config.Time,out planned);next+=!config.Enabled?"自动签到已暂停":done?"明天 "+config.Time:now.TimeOfDay<=planned?"今天 "+config.Time:Rules.InWindow(now)?"当前在签到时段，将检查并尝试补签":"明天 "+config.Time;Find<TextBlock>("NextRun").Text=next;PathStatus();}
 public void LoadLogs(){try{string[] lines=File.Exists(Store.FilePath("运行日志.txt"))?File.ReadAllLines(Store.FilePath("运行日志.txt")):new string[0];Find<TextBox>("LogsText").Text=lines.Length==0?"暂无运行记录":String.Join(Environment.NewLine,lines.Reverse().Take(500).Reverse());Find<TextBox>("LogsText").ScrollToEnd();Find<TextBlock>("RecentLogs").Text=lines.Length==0?"暂无运行记录":String.Join(Environment.NewLine,lines.Reverse().Take(2).Reverse());}catch{Find<TextBlock>("RecentLogs").Text="记录暂时无法读取，请稍后刷新。";}}
 public static void CheckInteractions(Config config){var view=new Dashboard(config);Action<bool,string> require=delegate(bool ok,string message){if(!ok)throw new InvalidOperationException(message);};view.Find<RadioButton>("NavSchedule").IsChecked=true;require(view.Find<FrameworkElement>("SchedulePage").Visibility==Visibility.Visible&&view.Find<FrameworkElement>("OverviewPage").Visibility==Visibility.Collapsed,"导航没有切换");view.Find<ComboBox>("HourPicker").SelectedItem="23";view.Find<ComboBox>("MinutePicker").SelectedItem="59";string saved=null;view.SaveSchedule=delegate{saved=view.Time;};view.Find<Button>("SaveSchedule").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));require(saved=="23:59","保存按钮没有取得选择时间");view.Find<RadioButton>("NavClient").IsChecked=true;view.Find<Button>("DetectClient").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(Detect(config.ClientPath)!=null)require(ValidClient(view.ClientPath),"默认安装路径识别失败");view.Find<ComboBox>("BackendPicker").SelectedIndex=0;require(view.Find<FrameworkElement>("EmulatorOptions").Visibility==Visibility.Visible&&view.Find<FrameworkElement>("WindowsOptions").Visibility==Visibility.Collapsed,"安卓环境切换失败");int positions=0;view.RefreshLocation=delegate{positions++;};view.Find<Button>("RefreshLocation").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));require(positions==1,"重新定位按钮回调异常");int clicks=0;view.CheckConnection=delegate{clicks++;};view.Find<Button>("CheckConnection").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));require(clicks==1,"连接按钮回调异常");int restarts=0;view.RestartClient=delegate{restarts++;};view.Find<Button>("RestartClient").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));require(restarts==1,"重新打开按钮回调异常");File.WriteAllText(Store.FilePath("界面检查.txt"),"通过：分区导航、时间选择与保存按钮、运行环境切换、重新定位与连接按钮回调。未提交真实签到。");}
 public static void Render(string dir,Config config){Directory.CreateDirectory(dir);var view=new Dashboard(config);string[] names={"overview","schedule","client","logs"};for(int i=0;i<4;i++){view.Page(i);view.Root.Measure(new Size(1060,720));view.Root.Arrange(new Rect(0,0,1060,720));view.Root.UpdateLayout();view.Root.Dispatcher.Invoke(DispatcherPriority.Render,new Action(delegate{}));var bitmap=new RenderTargetBitmap(1060,720,96,96,PixelFormats.Pbgra32);bitmap.Render(view.Root);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(Path.Combine(dir,names[i]+".png")))encoder.Save(stream);}}
}
}



