using System;
using System.IO;
using System.Xml;
namespace FzuAttendance {
static class AndroidTests {
 static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
 static string Fixture(string date,string status,string button,string reason){return "<hierarchy><node bounds='[0,0][1600,900]' package='cn.edu.fzu.fdxypa'><node content-desc='晚点名签到' bounds='[700,36][900,102]' package='cn.edu.fzu.fdxypa'/><node bounds='[0,102][1600,900]'><node class='android.webkit.WebView' bounds='[0,88][1387,780]' package='cn.edu.fzu.fdxypa'><node class='android.webkit.WebView' bounds='[0,88][1387,780]' package='cn.edu.fzu.fdxypa'><node text='"+date+" 星期三' package='cn.edu.fzu.fdxypa' bounds='[620,270][790,300]'/><node text='"+status+"' package='cn.edu.fzu.fdxypa'/><node text='"+button+"' package='cn.edu.fzu.fdxypa' enabled='true' bounds='[633,640][754,677]'/><node text='"+reason+"' package='cn.edu.fzu.fdxypa'/></node></node></node></node></hierarchy>";}
 static void Ads(){
  // Real emulator ad structure: an inset window, blank labels and explicit close IDs.
  string xml="<hierarchy><node package='com.android.flysilkworm' bounds='[465,133][1135,803]'><node resource-id='com.android.flysilkworm:id/banner' bounds='[485,153][1115,783]'/><node resource-id='com.android.flysilkworm:id/fl_close' clickable='true' enabled='true' bounds='[1039,133][1135,229]'><node resource-id='com.android.flysilkworm:id/iv_close' clickable='true' enabled='true' bounds='[1063,157][1111,205]'/></node></node></hierarchy>";
  var ad=AndroidClient.ParsePage(xml,1600,900);var close=AndroidNavigation.AdClose(ad);
  Require(ad.Width==1600&&ad.Height==900,"不应把广告弹窗大小当成屏幕大小");
  Require(AndroidNavigation.HasEmulatorAd(ad)&&close!=null&&close.X==1087&&close.Y==181,"未识别实机无文字关闭控件或真实屏幕坐标");
  Require(AndroidNavigation.Hint(ad).Contains("广告遮挡"),"广告不应被报为晚点名入口缺失");
  Require(AndroidNavigation.AdClose(AndroidClient.ParsePage(xml.Replace("com.android.flysilkworm","other.app"),1600,900))==null,"其他应用同名控件不应点击");
  Require(AndroidNavigation.AdClose(AndroidClient.ParsePage(xml.Replace("enabled='true'","enabled='false'"),1600,900))==null,"禁用关闭控件不应点击");
  Require(AndroidNavigation.AdClose(AndroidClient.ParsePage(xml.Replace("id/fl_close","id/download").Replace("id/iv_close","id/unknown"),1600,900))==null,"不应将下载或未知控件作为关闭按钮");
  Require(AndroidNavigation.AdClose(AndroidClient.ParsePage(xml,800,450))==null,"关闭按钮越界时不应点击");
  var noClose=AndroidClient.ParsePage(xml.Replace("id/fl_close","id/download").Replace("id/iv_close","id/unknown"),1600,900);Require(AndroidNavigation.HasEmulatorAd(noClose),"无关闭按钮的广告也应阻止导航");
  string ready=Fixture("2026-10-28","未签到","点击签到","");string covered=ready.Replace("</hierarchy>",xml.Replace("<hierarchy>","").Replace("</hierarchy>","")+"</hierarchy>");
  Require(!AndroidClient.ParsePage(covered,1600,900).CanSubmit(new DateTime(2026,10,28,22,0,0)),"广告覆盖签到页时不应提交");
  var size=AndroidNavigation.DisplaySize("Physical size: 1920x1080\nOverride size: 1600x900\n");Require(size[0]==1600&&size[1]==900,"应使用实际覆盖分辨率");
 }
 static void Navigation(){
  Ads();foreach(int height in new[]{720,1080,1920}){var statusPage=new AndroidPage{Width=1280,Height=height};var node=new AndroidNode{Text="定位中...",Package=AndroidClient.Package,Web=true,Left=500,Right=700,Top=height/2,Bottom=height/2+30};statusPage.Nodes.Add(node);Require(AndroidClient.ButtonInView(statusPage),"可见定位状态按钮应被识别");node.Top=height;node.Bottom=height+30;Require(!AndroidClient.ButtonInView(statusPage),"屏幕外的按钮应触发滚动");node.Text="点击签到";Require(AndroidClient.StatusButton(statusPage)==node,"可签到状态也应定位");}var begin=new DateTime(2026,10,30,21,0,0);Require(!Engine.LocationExpired(begin,begin.AddSeconds(29))&&Engine.LocationExpired(begin,begin.AddSeconds(30)),"定位30秒边界错误");foreach(var dimensions in new[]{new[]{1280,720},new[]{1920,1080},new[]{2560,1440},new[]{720,1280},new[]{1080,1920}}){var screenXml="<hierarchy rotation='1'><node bounds='[0,0][1280,720]'/></hierarchy>";var screen=AndroidClient.ParsePage(screenXml,dimensions[0],dimensions[1],true);Require(screen.Width==dimensions[0]&&screen.Height==dimensions[1],"实际截图尺寸不得重复旋转");}
  string detectionRoot=Path.Combine(Store.DataDir,"discovery-fixture");string mumu=Path.Combine(detectionRoot,"Custom Apps","MuMu","nx_main"),ld=Path.Combine(detectionRoot,"OtherDisk","LD");Directory.CreateDirectory(mumu);Directory.CreateDirectory(ld);
  string muExe=Path.Combine(mumu,"mumu-cli.exe"),ldExe=Path.Combine(ld,"ldconsole.exe");if(File.Exists(Path.Combine(mumu,"adb.exe")))File.Delete(Path.Combine(mumu,"adb.exe"));File.WriteAllText(muExe,"");File.WriteAllText(ldExe,"");File.WriteAllText(Path.Combine(ld,"adb.exe"),"");
  Require(EmulatorDiscovery.Choose(new[]{muExe,ldExe})==ldExe,"缺少 ADB 的安装目录不应被采用");File.WriteAllText(Path.Combine(mumu,"adb.exe"),"");Require(EmulatorDiscovery.Choose(new[]{ldExe,muExe})==muExe,"自定义安装目录中的 MuMu 应优先于雷电");Require(EmulatorDiscovery.FromHints(new[]{Path.GetDirectoryName(mumu)})==muExe,"安装记录根目录应定位到 nx_main 控制程序");Require(EmulatorDiscovery.FromHints(new[]{"\""+Path.Combine(mumu,"MuMuNxMain.exe")+"\",0"})==muExe,"带引号图标路径应识别 MuMu 控制程序");

  Require(MuMuClient.IsPath(@"D:\MuMu\mumu-cli.exe")&&!MuMuClient.IsPath(@"D:\leidian\ldconsole.exe"),"模拟器类型识别错误");
  var landscape=AndroidClient.ParsePage("<hierarchy rotation='1'><node bounds='[0,0][1920,1080]'/></hierarchy>",1080,1920);Require(landscape.Width==1920&&landscape.Height==1080,"旋转屏幕尺寸换算错误");
  var fresh=new Config();Require(MuMuClient.ValidatePosition(fresh.FixedPosition)==Config.DefaultPosition,"新用户应使用内置位置");foreach(string old in new[]{"{}","{\"FixedPosition\":\"\"}","{\"FixedPosition\":null}"}){var migrated=Store.Json.Deserialize<Config>(old);migrated.ApplyDefaults();Require(migrated.FixedPosition==Config.DefaultPosition,"旧配置未保存位置时应补齐默认位置");}var custom=new Config{FixedPosition="120.1,30.2"};custom.ApplyDefaults();Require(custom.FixedPosition=="120.1,30.2","用户自行保存的位置不得被默认值覆盖");
  for(int i=0;i<100;i++){var probe=new Config{FixedPosition="119.2,26.07"};AndroidClient.PreparePosition(probe);var q=probe.SessionPosition.Split(',');double dx=(Double.Parse(q[0],System.Globalization.CultureInfo.InvariantCulture)-119.2)*Math.PI/180*6378137*Math.Cos(26.07*Math.PI/180),dy=(Double.Parse(q[1],System.Globalization.CultureInfo.InvariantCulture)-26.07)*Math.PI/180*6378137;Require(Math.Sqrt(dx*dx+dy*dy)<=5,"位置偏移不得超过5米");Require(!Store.Json.Serialize(probe).Contains("SessionPosition"),"临时位置不得覆盖固定位置");}
  var fixedConfig=new Config{EmulatorPath=@"Z:\MuMu\mumu-cli.exe",FixedPosition="120.1,30.2"};Require(MuMuClient.Position(fixedConfig)=="120.1,30.2","固定位置不应依赖模拟器默认配置");var restoredPoint=Store.Json.Deserialize<Config>(Store.Json.Serialize(fixedConfig));Require(MuMuClient.Position(restoredPoint)=="120.1,30.2","重启后应保留固定位置");foreach(string invalid in new[]{"", "181,30", "120,91", "NaN,30", "120,Infinity", "0,0"}){bool rejected=false;try{MuMuClient.ValidatePosition(invalid);}catch(InvalidOperationException){rejected=true;}Require(rejected,"无效位置不得进入签到流程");}
  var manual=new Config{Enabled=true,Time="21:05",ManualEmulator=true};var now=new DateTime(2026,10,28,22,0,0);
  Require(!Rules.Due(manual,now,true),"手动操作期间不应自动接管模拟器");
  manual.ManualEmulator=false;manual.EmulatorResumeAt="2026-10-29 21:05:00";
  Require(Rules.Due(manual,now,true)&&!Rules.Due(manual,new DateTime(2026,10,29,21,4,59),true),"过期等待标记不得阻止补签，设定时间前不得提前签到");
  Require(Rules.Due(manual,new DateTime(2026,10,29,21,5,0),false),"下次设定时间应恢复自动运行");
  Require(Rules.NextPlan(manual,now)==new DateTime(2026,10,29,21,5,0)&&Rules.NextPlan(manual,now.Date.AddHours(20))==new DateTime(2026,10,28,21,5,0),"关闭后的下一次执行日期计算错误");
  Require(Rules.NextPlan(manual,new DateTime(2026,10,28,21,5,0))==new DateTime(2026,10,29,21,5,0),"刚到计划时间关闭时不应立即重开");
  string xml="<hierarchy><node bounds='[0,0][900,1600]' package='cn.edu.fzu.fdxypa'><node text='  ' content-desc='晚点名 签到&#10;按钮' enabled='true' clickable='true' bounds='[40,500][300,700]'/><node content-desc='业务&#10;第 4 个标签，共 4 个' enabled='true' bounds='[680,1480][900,1600]'/></node></hierarchy>";
  var page=AndroidClient.ParsePage(xml);Require(AndroidNavigation.Find(page,"晚点名签到")!=null,"未处理空白文字、组合标签或继承包名");Require(AndroidNavigation.Find(page,"业务")!=null,"未识别带标签提示的业务入口");
  Require(AndroidNavigation.Find(AndroidClient.ParsePage(xml.Replace("[40,500][300,700]","[40,-400][300,-200]")),"晚点名签到")==null,"不应点击屏幕外的入口");
  string ad="<hierarchy><node bounds='[0,0][900,1600]' package='cn.edu.fzu.fdxypa'><node content-desc='晚点名签到' clickable='true' bounds='[20,50][200,140]'/><node class='android.webkit.WebView' bounds='[0,200][900,400]' /></node></hierarchy>";
  Require(!AndroidClient.ParsePage(ad).Attendance,"业务页的广告网页不应被误识别为签到页");
  string launcher="priority=0\ncn.edu.fzu.fdxypa/com.example.NewEntryActivity\n";Require(AndroidNavigation.Launcher(launcher)=="cn.edu.fzu.fdxypa/com.example.NewEntryActivity","未动态解析应用入口");Require(AndroidNavigation.Launcher("other.app/.Main")==null,"不应启动其他应用");
  Require(AndroidNavigation.LaunchFailed("Error type 3\nError: Activity class does not exist"),"未识别 Android 启动失败");Require(!AndroidNavigation.LaunchFailed("Warning: Activity not started, intent has been delivered to currently running top-most instance."),"正常已运行提示不应视为失败");
  var permission=AndroidClient.ParsePage(xml.Replace("cn.edu.fzu.fdxypa","com.android.permissioncontroller"));Require(AndroidNavigation.Hint(permission).Contains("权限提示"),"未区分系统权限弹窗");
  Require(AndroidNavigation.Hint(AndroidClient.ParsePage(xml.Replace("晚点名 签到&#10;按钮","一键登录"))).Contains("登录页面"),"未识别缓存账号登录页面");
  string summary=AndroidNavigation.Summary(AndroidClient.ParsePage(xml.Replace("text='  '","text='PRIVATE_NAME_123 119.123456,26.123456'")));Require(!summary.Contains("PRIVATE_NAME_123")&&!summary.Contains("119.123456"),"诊断不应包含原始身份或坐标文字");
 }
 public static void Run(){Navigation();var now=new DateTime(2026,10,28,22,0,0);string xml=Fixture("2026-10-28","未签到","点击签到","");var ready=AndroidClient.ParsePage(xml);Require(ready.CanSubmit(now),"正常安卓签到页未识别");Require(Math.Abs(ready.Ready.X-800)<=2&&Math.Abs(ready.Ready.Y-760)<=2,"WebView 缩放后的触点不正确");Require(!ready.CanSubmit(now.Date.AddHours(20))&&!ready.CanSubmit(now.Date.AddDays(1)),"时段或跨日保护失效");Require(!AndroidClient.ParsePage(Fixture("2026-10-27","未签到","点击签到","")).CanSubmit(now),"旧日期页面不应提交");Require(!AndroidClient.ParsePage(Fixture("2026-10-28","未签到","点击签到","不在签到范围内")).CanSubmit(now),"范围限制未阻止提交");Require(!AndroidClient.ParsePage(Fixture("2026-10-28","未签到","定位中...","定位服务流量限制")).CanSubmit(now),"定位等待中不应提交");Require(AndroidClient.ParsePage(Fixture("2026-10-28","已签到","已签到","")).Confirmed,"未识别成功状态");Require(!AndroidClient.ParsePage(Fixture("2026-10-28","未签到成功","无法签到","")).Confirmed,"错误识别为已签到");Require(!AndroidClient.ParsePage(xml.Replace("cn.edu.fzu.fdxypa","other.app")).CanSubmit(now),"不应操作其他应用");Require(!AndroidClient.ParsePage(xml.Replace("enabled='true'","enabled='false'")).CanSubmit(now),"禁用按钮不应提交");File.WriteAllText(Store.FilePath("安卓逻辑检查.txt"),"通过：真实 WebView 缩放坐标、签到时段、跨日与旧页面、范围限制、定位等待、成功确认、其他应用及禁用按钮保护、动态启动入口、组合导航标签、系统弹窗分类与诊断脱敏。未操作真实设备。");}
}
}

