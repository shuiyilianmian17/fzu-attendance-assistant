using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace FzuAttendance {
sealed class HiddenPagePreview:Form {
 PictureBox picture;Label message;System.Windows.Forms.Timer timer;bool updating,closing;
 public HiddenPagePreview(){Text="后台签到页 · 实时预览";Icon=System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);ClientSize=new Size(850,730);MinimumSize=new Size(650,500);StartPosition=FormStartPosition.CenterParent;BackColor=Color.FromArgb(242,245,250);Font=new Font("Microsoft YaHei UI",10);
  var top=new Panel{Dock=DockStyle.Top,Height=88,Padding=new Padding(18,12,18,8),BackColor=Color.White};message=new Label{Dock=DockStyle.Fill,Text="正在读取后台真实页面…",ForeColor=Color.FromArgb(23,54,95)};top.Controls.Add(message);
  picture=new PictureBox{Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.FromArgb(235,240,248)};Controls.Add(picture);Controls.Add(top);
  var footer=new Label{Dock=DockStyle.Bottom,Height=40,Text="只读实时画面，每 2 秒更新；不会重新加载页面，也不会打断定位。",Padding=new Padding(16,10,0,0),ForeColor=Color.FromArgb(103,124,151)};Controls.Add(footer);
  timer=new System.Windows.Forms.Timer{Interval=2000};timer.Tick+=async delegate{await UpdateFrame();};Shown+=async delegate{timer.Start();await UpdateFrame();};FormClosing+=delegate{closing=true;timer.Stop();};FormClosed+=delegate{timer.Dispose();if(picture.Image!=null)picture.Image.Dispose();};
 }
 public static async Task<byte[]> CaptureFrame(){var config=Store.Read();if(AndroidClient.Selected(config))return await AndroidClient.Capture(config);var target=await Cdp.Target();if(target==null)throw new InvalidOperationException("没有找到后台签到页，请先在概览中检查连接。");using(var cdp=new Cdp()){await cdp.Open(target);var result=await cdp.Call("Page.captureScreenshot",new{format="png",fromSurface=true,captureBeyondViewport=false});return Convert.FromBase64String((string)result["data"]);}}
 async Task UpdateFrame(){if(updating||closing||WindowState==FormWindowState.Minimized)return;updating=true;try{byte[] data=await CaptureFrame();if(closing||IsDisposed)return;using(var stream=new MemoryStream(data))using(var image=Image.FromStream(stream)){var previous=picture.Image;picture.Image=new Bitmap(image);if(previous!=null)previous.Dispose();}message.Text="后台真实页面（只读）\n画面更新时间："+Store.Now().ToString("HH:mm:ss")+"　如一直定位中，请查看页面提示。";}catch(Exception e){if(!closing&&!IsDisposed)message.Text=e is InvalidOperationException?e.Message:"暂时无法读取后台画面，将自动重试。";}finally{updating=false;}}
}
}

