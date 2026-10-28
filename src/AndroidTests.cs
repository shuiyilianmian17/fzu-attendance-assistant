using System;
using System.IO;
using System.Xml;
namespace FzuAttendance {
static class AndroidTests {
 static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
 static string Fixture(string date,string status,string button,string reason){return "<hierarchy><node bounds='[0,0][1600,900]' package='cn.edu.fzu.fdxypa'><node content-desc='晚点名签到' bounds='[700,36][900,102]' package='cn.edu.fzu.fdxypa'/><node bounds='[0,102][1600,900]'><node class='android.webkit.WebView' bounds='[0,88][1387,780]' package='cn.edu.fzu.fdxypa'><node class='android.webkit.WebView' bounds='[0,88][1387,780]' package='cn.edu.fzu.fdxypa'><node text='"+date+" 星期三' package='cn.edu.fzu.fdxypa' bounds='[620,270][790,300]'/><node text='"+status+"' package='cn.edu.fzu.fdxypa'/><node text='"+button+"' package='cn.edu.fzu.fdxypa' enabled='true' bounds='[633,640][754,677]'/><node text='"+reason+"' package='cn.edu.fzu.fdxypa'/></node></node></node></node></hierarchy>";}
 static void Navigation(){
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
