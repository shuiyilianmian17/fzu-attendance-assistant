using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

// 单独与 MuMuClient.cs 编译；所有设备与桌面调用均由替身接管。
namespace FzuAttendance {
static class MuMuMultiInstanceTests {
 internal static readonly List<string> Commands = new List<string>();
 internal static bool Started, FailStart;
 internal static int OtherCount, Starts;
 static Config Reset(int others, bool started) {
  Commands.Clear(); Started = started; FailStart = false; OtherCount = others; Starts = 0;
  return new Config { EmulatorPath = @"C:\offline-fixture\mumu-cli.exe", EmulatorIndex = 2 };
 }
 static void Require(bool value, string message) { if (!value) throw new Exception(message); }
 static void NoGlobalChange() {
  Require(!Commands.Contains("main close"), "不得关闭 MuMu 共享主程序。");
  foreach (string command in Commands)
   if (command.StartsWith("control ") && !command.StartsWith("control -v 2 "))
    throw new Exception("操作了非选定实例：" + command);
 }
 static void StartWithOthers(int count) {
  var config = Reset(count, false);
  MuMuClient.Ensure(config, delegate(string message) {}).GetAwaiter().GetResult();
  Require(Started && Starts == 1 && config.OwnsMuMu, "应启动选定实例一次并记录归属。");
  Require(Commands.Contains("control -v 2 hide_window"), "应只隐藏所选实例。");
  Require(Commands.Contains("-s 127.0.0.1:16388 shell getprop sys.boot_completed"), "应检查选定实例的 ADB 设备。");
  foreach (string command in Commands)
   Require(!command.Contains("shutdown"), "启动流程不得关闭任何实例。");
  NoGlobalChange();
 }
 static int Main() {
  try {
   StartWithOthers(1);
   StartWithOthers(3);
   StartWithOthers(0);
   var config = Reset(2, true);
   MuMuClient.Ensure(config, delegate(string message) {}).GetAwaiter().GetResult();
   Require(Starts == 0 && !config.OwnsMuMu, "已运行的实例不应重复启动或认领。");
   NoGlobalChange();
   config = Reset(2, true); config.OwnsMuMu = true;
   MuMuClient.Stop(config, delegate(string message) {}).GetAwaiter().GetResult();
   Require(!Started && !config.OwnsMuMu && OtherCount == 2, "停止所选实例应保留其他实例。");
   Require(Commands.Contains("control -v 2 shutdown"), "应按编号关闭选定实例。");
   NoGlobalChange();
   config = Reset(1, false); FailStart = true;
   bool failed = false;
   try { MuMuClient.Ensure(config, delegate(string message) {}).GetAwaiter().GetResult(); }
   catch (InvalidOperationException) { failed = true; }
   Require(failed && !config.OwnsMuMu && !Started, "启动失败不得认领未启动的实例。");
   NoGlobalChange();
   Console.WriteLine("通过 6 项离线测试：单个/多个其他实例、无其他实例、目标已运行、只关闭目标、启动失败。未调用真实设备。");
   return 0;
  } catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }
 }
}
class Config {
 public string EmulatorPath, SessionPosition = "", FixedPosition = "";
 public int EmulatorIndex;
 public bool RestoreMuMuWindow, OwnsMuMu;
}
static class Store {
 internal static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
}
static class Operation {
 public static Task Delay(int milliseconds) { return Task.FromResult(0); }
}
static class EmulatorHost {
 public static bool OnCurrentDesktop(IntPtr window) { return true; }
 public static bool StartMuMu(Config config) {
  if (config.EmulatorIndex != 2) throw new Exception("启动了错误的实例。");
  MuMuMultiInstanceTests.Starts++;
  if (MuMuMultiInstanceTests.FailStart) return false;
  MuMuMultiInstanceTests.Started = true;
  return true;
 }
}
static class AndroidClient {
 public static bool IsWindowVisible(IntPtr window) { return false; }
 public static Task<byte[]> Command(string path, string arguments, int seconds) {
  MuMuMultiInstanceTests.Commands.Add(arguments);
  string result;
  if (arguments == "info -v all") {
   var instances = new Dictionary<string,object>();
   for (int i = 0; i < MuMuMultiInstanceTests.OtherCount; i++)
    instances[(i + 10).ToString()] = new { is_process_started = true };
   instances["2"] = new { is_process_started = MuMuMultiInstanceTests.Started };
   result = Store.Json.Serialize(instances);
  }
  else if (arguments == "info -v 2")
   result = MuMuMultiInstanceTests.Started
    ? "{\"is_process_started\":true,\"is_android_started\":true,\"adb_port\":16388}"
    : "{\"is_process_started\":false,\"is_android_started\":false}";
  else if (arguments == "control -v 2 hide_window" || arguments == "connect 127.0.0.1:16388") result = "";
  else if (arguments == "control -v 2 shutdown") { MuMuMultiInstanceTests.Started = false; result = ""; }
  else if (arguments == "-s 127.0.0.1:16388 shell getprop sys.boot_completed") result = "1";
  else if (arguments == "-s 127.0.0.1:16388 shell pm list packages cn.edu.fzu.fdxypa") result = "package:cn.edu.fzu.fdxypa";
  else throw new Exception("非预期设备命令：" + arguments);
  return Task.FromResult(Encoding.UTF8.GetBytes(result));
 }
}
}
