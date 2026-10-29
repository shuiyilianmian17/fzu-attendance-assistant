using System;
using System.IO;
using System.Threading.Tasks;
namespace FzuAttendance {
static class CompletionTests {
 static void Require(bool value,string text){if(!value)throw new InvalidOperationException(text);}
 public static async Task Run(){
  var c=new Config{Enabled=false};int checks=0,closes=0;var engine=new Engine(c,null);
  engine.AndroidCheck=(cfg,dry,force,requested,clear,state)=>{checks++;return Task.FromResult(false);};
  engine.EmulatorStop=(cfg,state)=>{closes++;return Task.FromResult(0);};
  await engine.Check(false,true,true);Require(checks==1&&closes==0&&!c.ClosePending,"未确认成功不应关闭模拟器");
  engine.AndroidCheck=(cfg,dry,force,requested,clear,state)=>{checks++;cfg.SuccessDate=Store.Now().ToString("yyyy-MM-dd");return Task.FromResult(true);};
  await engine.Check(false,true,true);Require(closes==1&&!c.ClosePending&&c.LastState.Contains("已自动关闭"),"确认成功后应关闭并记录完成状态");
  engine.EmulatorStop=(cfg,state)=>{closes++;throw new InvalidOperationException("测试关闭失败");};
  await engine.Check(false,true,true);Require(c.ClosePending&&c.SuccessDate==Store.Now().ToString("yyyy-MM-dd"),"关闭失败不应丢失签到结果或待关闭状态");
  int before=checks;await engine.Check(false,true,false);Require(checks==before,"待关闭时不应重复导航签到");
  var restored=Store.Read();Require(restored.ClosePending,"重启后应保留未完成的关闭操作");var resumed=new Engine(restored,null);
  resumed.AndroidCheck=(cfg,dry,force,requested,clear,state)=>{throw new Exception("关闭恢复不应再次签到");};
  resumed.EmulatorStop=(cfg,state)=>{closes++;return Task.FromResult(0);};await resumed.FinishCompletedSignIn();Require(!restored.ClosePending,"重启后应恢复关闭并清除标记");
  restored.ManualEmulator=true;restored.ClosePending=true;await resumed.FinishCompletedSignIn();Require(restored.ClosePending,"手动操作期间不得自动关闭窗口");
  var interrupted=new Config{Enabled=true,SuccessDate="previous"};var priority=new Engine(interrupted,null);bool stale=false;
  priority.AndroidCheck=async (cfg,dry,force,requested,clear,state)=>{await Operation.Delay(30000);stale=true;return false;};
  var running=priority.Check(false,true,true);var clock=System.Diagnostics.Stopwatch.StartNew();await priority.Interrupt();await running;
  Require(clock.ElapsedMilliseconds<2000&&!priority.IsBusy&&!stale,"取消应立即停止等待，旧流程不得继续执行");Require(interrupted.Enabled&&interrupted.SuccessDate=="previous","中断不得更改开关或已保存结果");
  int next=0;priority.AndroidCheck=(cfg,dry,force,requested,clear,state)=>{next++;return Task.FromResult(false);};await priority.Check(true,false,true);Require(next==1&&!priority.IsBusy,"中断后新操作应立即可执行");
  File.WriteAllText(Store.FilePath("签到后关闭检查.txt"),"通过：未确认不关闭、成功后关闭、关闭失败保留结果并重试、重启恢复且不重复签到、手动操作保护。使用模拟页面结果，未操作真实账号。");
 }
}
}
