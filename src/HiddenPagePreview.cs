using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace FzuAttendance {
sealed class PreviewPicture:PictureBox {
 public PreviewPicture(){SetStyle(ControlStyles.Selectable,true);TabStop=true;}
 protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);Focus();}
}
static class PreviewGeometry {
 public static bool Map(Size viewport,Size image,Point mouse,out Point point){
  point=Point.Empty;if(viewport.Width<=0||viewport.Height<=0||image.Width<=0||image.Height<=0)return false;
  double scale=Math.Min(viewport.Width/(double)image.Width,viewport.Height/(double)image.Height);
  double left=(viewport.Width-image.Width*scale)/2,top=(viewport.Height-image.Height*scale)/2;
  if(mouse.X<left||mouse.Y<top||mouse.X>=left+image.Width*scale||mouse.Y>=top+image.Height*scale)return false;
  point=new Point(Math.Min(image.Width-1,(int)((mouse.X-left)/scale)),Math.Min(image.Height-1,(int)((mouse.Y-top)/scale)));return true;
 }
 public static bool Valid(Size image,Point a,Point b){return image.Width>0&&image.Height>0&&a.X>=0&&a.X<image.Width&&a.Y>=0&&a.Y<image.Height&&b.X>=0&&b.X<image.Width&&b.Y>=0&&b.Y<image.Height&&Math.Abs(a.Y-b.Y)>=Math.Max(32,image.Height/25);}
 public static void Wheel(Size image,int delta,out Point a,out Point b){int top=image.Height*35/100,bottom=image.Height*75/100;a=new Point(image.Width/2,delta<0?bottom:top);b=new Point(image.Width/2,delta<0?top:bottom);}
 public static void Tests(){
  Action<bool,string> check=(ok,s)=>{if(!ok)throw new Exception(s);};Point p,a,b;
  check(Map(new Size(800,600),new Size(1280,720),new Point(400,300),out p)&&p==new Point(640,360),"landscape mapping");
  check(!Map(new Size(800,600),new Size(1280,720),new Point(400,50),out p),"letterbox excluded");
  check(Map(new Size(800,600),new Size(720,1280),new Point(400,300),out p)&&p==new Point(360,640),"portrait mapping");
  check(!Valid(new Size(1280,720),new Point(500,400),new Point(501,401)),"click must not become swipe");
  check(!Valid(new Size(1280,720),new Point(-1,500),new Point(100,100)),"bounds");
  Wheel(new Size(1280,720),-120,out a,out b);check(a.Y>b.Y&&Valid(new Size(1280,720),a,b),"scroll down");
  Wheel(new Size(1280,720),120,out a,out b);check(a.Y<b.Y&&Valid(new Size(1280,720),a,b),"scroll up");
  File.WriteAllText(Store.FilePath("隐藏页滑动检查.txt"),"通过：横竖屏缩放、留白排除、坐标边界、上下滚动方向、小位移不发送点击。");
 }
}
sealed class HiddenPagePreview:Form {
 PreviewPicture picture;Label message;System.Windows.Forms.Timer timer;bool updating,closing,swiping,dragging;Point dragStart;Size dragSize;DateTime captured=DateTime.MinValue;
 readonly Func<Point,Point,Size,Task> swipe;
 public HiddenPagePreview(Func<Point,Point,Size,Task> scroll){swipe=scroll;Text="后台签到页 · 实时预览";Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);ClientSize=new Size(850,730);MinimumSize=new Size(650,500);StartPosition=FormStartPosition.CenterParent;BackColor=Color.FromArgb(242,245,250);Font=new Font("Microsoft YaHei UI",10);
  var top=new Panel{Dock=DockStyle.Top,Height=88,Padding=new Padding(18,12,18,8),BackColor=Color.White};message=new Label{Dock=DockStyle.Fill,Text="正在读取后台真实页面…",ForeColor=Color.FromArgb(23,54,95)};top.Controls.Add(message);
  picture=new PreviewPicture{Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.FromArgb(235,240,248)};Controls.Add(picture);Controls.Add(top);
  var footer=new Label{Dock=DockStyle.Bottom,Height=48,Text="滚轮上下滚动，按住左键上下拖动；自动签到进行中会暂缓滑动。",Padding=new Padding(16,10,8,0),ForeColor=Color.FromArgb(103,124,151)};Controls.Add(footer);
  picture.MouseWheel+=async delegate(object sender,MouseEventArgs e){if(picture.Image==null||dragging||e.Delta==0)return;Point hit;if(!PreviewGeometry.Map(picture.ClientSize,picture.Image.Size,e.Location,out hit))return;Point a,b;var size=picture.Image.Size;PreviewGeometry.Wheel(size,e.Delta,out a,out b);await SendSwipe(a,b,size);};
  picture.MouseDown+=delegate(object sender,MouseEventArgs e){if(e.Button!=MouseButtons.Left||picture.Image==null||swiping||updating)return;Point p;if(!PreviewGeometry.Map(picture.ClientSize,picture.Image.Size,e.Location,out p))return;dragging=true;dragStart=p;dragSize=picture.Image.Size;picture.Capture=true;};
  picture.MouseUp+=async delegate(object sender,MouseEventArgs e){if(!dragging)return;dragging=false;picture.Capture=false;Point end;if(e.Button==MouseButtons.Left&&PreviewGeometry.Map(picture.ClientSize,dragSize,e.Location,out end))await SendSwipe(dragStart,end,dragSize);};
  picture.MouseCaptureChanged+=delegate{if(!picture.Capture)dragging=false;};ResizeBegin+=delegate{dragging=false;picture.Capture=false;};
  timer=new System.Windows.Forms.Timer{Interval=2000};timer.Tick+=async delegate{await UpdateFrame();};Shown+=async delegate{timer.Start();await UpdateFrame();};FormClosing+=delegate{closing=true;timer.Stop();};FormClosed+=delegate{timer.Dispose();if(picture.Image!=null)picture.Image.Dispose();};
 }
 async Task SendSwipe(Point a,Point b,Size size){if(closing||swiping||swipe==null||!PreviewGeometry.Valid(size,a,b))return;if((DateTime.UtcNow-captured).TotalSeconds>10){message.Text="画面已过期，请等待画面刷新后再滑动。";await UpdateFrame();return;}swiping=true;string result=null;try{await swipe(a,b,size);await Task.Delay(350);}catch(Exception e){result=e is InvalidOperationException?e.Message:"滑动未完成，请等待画面更新后重试。";}finally{swiping=false;}await UpdateFrame();if(result!=null&&!closing&&!IsDisposed)message.Text=result;}
 public static async Task<byte[]> CaptureFrame(){return await AndroidClient.Capture(Store.Read());}
 async Task UpdateFrame(){if(updating||closing||dragging||swiping||WindowState==FormWindowState.Minimized)return;updating=true;try{byte[] data=await CaptureFrame();if(closing||IsDisposed)return;using(var stream=new MemoryStream(data))using(var image=Image.FromStream(stream)){var previous=picture.Image;picture.Image=new Bitmap(image);if(previous!=null)previous.Dispose();}captured=DateTime.UtcNow;message.Text="后台真实页面（支持滚轮与上下拖动）\n画面更新时间："+Store.Now().ToString("HH:mm:ss")+"　滑动会同步操作模拟器内的页面。";}catch(Exception e){if(!closing&&!IsDisposed)message.Text=e is InvalidOperationException?e.Message:"暂时无法读取后台画面，将自动重试。";}finally{updating=false;}}
}
}
