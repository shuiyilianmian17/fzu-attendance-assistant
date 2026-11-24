using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows.Forms;

namespace FzuAttendance {
static class ApiBackend {
 public static string Protect(string value) {
  return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value),null,DataProtectionScope.CurrentUser));
 }
 internal static string Password(Config config) {
  try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(config.ApiSecret),null,DataProtectionScope.CurrentUser)); }
  catch { throw new InvalidOperationException("请在签到设置中重新保存接口签到密码。"); }
 }
 static string SmsCode() {
  using(var form=new Form{Text="统一认证短信验证",Width=360,Height=190,StartPosition=FormStartPosition.CenterScreen,TopMost=true}) {
   var label=new Label{Text="请输入统一身份认证发来的短信验证码",Left=18,Top=15,Width=310};
   var code=new TextBox{Left=18,Top=48,Width=300};
   var ok=new Button{Text="确认",Left=220,Top=90,DialogResult=DialogResult.OK};
   form.Controls.AddRange(new Control[]{label,code,ok});form.AcceptButton=ok;
   return form.ShowDialog()==DialogResult.OK?code.Text.Trim():"";
  }
 }
 public static async Task<bool> Check(Config config,bool dry,bool interactive,Action<string> state) {
  if(String.IsNullOrEmpty(config.ApiAccount)||String.IsNullOrEmpty(config.ApiSecret))throw new InvalidOperationException("请在签到设置中填写接口签到账号和密码。");
  if(!dry&&!Rules.InWindow(Store.Now())){state("当前不在签到时段。可点击检查连接验证登录。");return false;}
  var position=config.FixedPosition.Split(',');
  var culture=System.Globalization.CultureInfo.InvariantCulture;
  var request=new {account=config.ApiAccount,password=Password(config),lng=Double.Parse(position[0],culture),lat=Double.Parse(position[1],culture),dry=dry,interactive=interactive,dataDir=Path.Combine(Store.DataDir,"api-data")};
  string script=Path.Combine(Store.Dir,"api","bridge.js");
  var start=new ProcessStartInfo(File.Exists(Path.Combine(Store.Dir,"runtime","node.exe"))?Path.Combine(Store.Dir,"runtime","node.exe"):"node.exe","\""+script+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8,WorkingDirectory=Store.Dir};
  state("正在连接晚点名接口并检查登录状态…");
  using(var process=Process.Start(start))using(Operation.KillOnCancel(process)) {
   var errors=process.StandardError.ReadToEndAsync();
   await process.StandardInput.WriteLineAsync(Store.Json.Serialize(request));
   bool success=false;
   var deadline=Task.Delay(120000);
   while(true) {
    var read=process.StandardOutput.ReadLineAsync();
    if(await Task.WhenAny(read,deadline)!=read){try{process.Kill();}catch{}throw new InvalidOperationException("接口响应超时，请稍后重试。");}
    string line=await read;Operation.Throw();if(line==null)break;
    var result=Store.Json.Deserialize<Dictionary<string,object>>(line);
    if(result.ContainsKey("sms")) {
     string code=interactive?SmsCode():"";
     await process.StandardInput.WriteLineAsync(Store.Json.Serialize(new {code=code}));
     deadline=Task.Delay(120000);
    } else {
     success=result.ContainsKey("success")&&Convert.ToBoolean(result["success"]);
     if(result.ContainsKey("message"))state(Convert.ToString(result["message"]));
    }
   }
   await errors;return success;
  }
 }
}
}
