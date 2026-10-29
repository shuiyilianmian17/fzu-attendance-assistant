using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
namespace FzuAttendance {
sealed class ReleaseUpdate {public Version Version;public string Name,Url,Hash;}
static class UpdateClient {
 const string Repository="https://github.com/shuiyilianmian17/fzu-attendance-assistant/";
 static HttpWebResponse Response(string url){ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;var request=(HttpWebRequest)WebRequest.Create(url);request.UserAgent="FzuAttendanceAssistant";request.Timeout=30000;request.ReadWriteTimeout=60000;try{return (HttpWebResponse)request.GetResponse();}catch(WebException error){var reply=error.Response as HttpWebResponse;Store.Log("更新连接失败："+new Uri(url).Host+"；"+error.Status+"；HTTP="+(reply==null?"无":((int)reply.StatusCode).ToString())+"；"+error.Message);throw;}}
 public static ReleaseUpdate Parse(string json){
  var data=Store.Json.Deserialize<Dictionary<string,object>>(json);if(Convert.ToBoolean(data["draft"])||Convert.ToBoolean(data["prerelease"]))throw new InvalidOperationException("此版本尚未正式发布。");
  string tag=Convert.ToString(data["tag_name"]);if(!Regex.IsMatch(tag,@"^v\d+\.\d+\.\d+$"))throw new InvalidOperationException("更新版本号无效。");
  string name="FzuAttendanceAssistant-Setup-"+tag.Substring(1)+".exe";
  foreach(var value in (IEnumerable)data["assets"]){var asset=(Dictionary<string,object>)value;if(Convert.ToString(asset["name"])!=name||Convert.ToString(asset["state"])!="uploaded")continue;
   string url=Convert.ToString(asset["browser_download_url"]),hash=asset.ContainsKey("digest")?Convert.ToString(asset["digest"]):"";
   if(url!=Repository+"releases/download/"+tag+"/"+name||!Regex.IsMatch(hash,@"^sha256:[a-fA-F0-9]{64}$"))throw new InvalidOperationException("安装包来源或校验信息无效，未进行更新。");
   return new ReleaseUpdate{Version=new Version(tag.Substring(1)),Name=name,Url=url,Hash=hash.Substring(7)};
  }throw new InvalidOperationException("最新版安装包尚未上传完成，请稍后再试。");
 }
 public static ReleaseUpdate Latest(){try{using(var response=Response("https://api.github.com/repos/shuiyilianmian17/fzu-attendance-assistant/releases/latest"))using(var stream=response.GetResponseStream())using(var memory=new MemoryStream()){CopyBounded(stream,memory,2*1024*1024);return Parse(Encoding.UTF8.GetString(memory.ToArray()));}}catch(WebException){
  // Public releases also work when GitHub's anonymous API quota is exhausted.
  string tag;using(var page=Response(Repository+"releases/latest")){string url=page.ResponseUri.AbsoluteUri;if(!url.StartsWith(Repository+"releases/tag/",StringComparison.Ordinal))throw new InvalidOperationException("尚未找到正式发布版本。");tag=url.Substring((Repository+"releases/tag/").Length);}
  if(!Regex.IsMatch(tag,@"^v\d+\.\d+\.\d+$"))throw new InvalidOperationException("更新版本号无效。");string name="FzuAttendanceAssistant-Setup-"+tag.Substring(1)+".exe";
  using(var response=Response(Repository+"releases/download/"+tag+"/SHA256SUMS.txt"))using(var stream=response.GetResponseStream())using(var memory=new MemoryStream()){CopyBounded(stream,memory,65536);var match=Regex.Match(Encoding.UTF8.GetString(memory.ToArray()),@"^([a-fA-F0-9]{64})[ \t]+\*?"+Regex.Escape(name)+@"[ \t]*\r?$",RegexOptions.Multiline);if(!match.Success)throw new InvalidOperationException("缺少安装包校验信息。");return new ReleaseUpdate{Version=new Version(tag.Substring(1)),Name=name,Url=Repository+"releases/download/"+tag+"/"+name,Hash=match.Groups[1].Value};}
 }}
 static void CopyBounded(Stream input,Stream output,long limit){var buffer=new byte[65536];long total=0;int read;while((read=input.Read(buffer,0,buffer.Length))>0){total+=read;if(total>limit)throw new InvalidOperationException("更新文件超出允许大小。");output.Write(buffer,0,read);}}
 public static bool Verify(string path,string expected){using(var input=File.OpenRead(path))using(var sha=SHA256.Create()){return String.Equals(BitConverter.ToString(sha.ComputeHash(input)).Replace("-",""),expected,StringComparison.OrdinalIgnoreCase);}}
 public static string Download(ReleaseUpdate release){string dir=Store.FilePath("updates");Directory.CreateDirectory(dir);string file=Path.Combine(dir,release.Name),temp=file+".partial";
  try{using(var response=Response(release.Url))using(var input=response.GetResponseStream())using(var output=File.Create(temp))CopyBounded(input,output,64*1024*1024);
   if(!Verify(temp,release.Hash))throw new InvalidOperationException("安装包校验失败，未启动安装，请重新检查更新。");File.Copy(temp,file,true);return file;
  }finally{if(File.Exists(temp))File.Delete(temp);}
 }
 static string Quote(string value){if(value.Contains("\"")||value.Contains("\r")||value.Contains("\n"))throw new InvalidOperationException("更新路径无效。");return "\""+value.TrimEnd('\\')+"\"";}
 public static void Broker(string exe,string args,string directory){dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));dynamic windows=shell.Windows();int hwnd;dynamic desktop=windows.FindWindowSW(0,0,8,out hwnd,1);if(desktop==null)throw new InvalidOperationException("无法启动更新程序，请稍后重试。");desktop.Document.Application.ShellExecute(exe,args,directory,"open",0);}
 public static void StartInstall(string installer){
  string runner=Store.FilePath("updates\\UpdateRunner-"+DateTime.UtcNow.Ticks+".exe");File.Copy(System.Windows.Forms.Application.ExecutablePath,runner,true);
  string args="--install-update "+Quote(installer)+" "+Quote(Store.Dir)+" "+Process.GetCurrentProcess().Id+" "+(Startup.Enabled()?"1":"0");Broker(runner,args,Path.GetDirectoryName(runner));
 }
 public static int Worker(string[] args){
  if(args.Length!=5)throw new InvalidOperationException("更新参数无效。");string installer=Path.GetFullPath(args[1]),directory=Path.GetFullPath(args[2]);
  if(Path.GetDirectoryName(installer)!=Store.Dir.TrimEnd('\\')||!Regex.IsMatch(Path.GetFileName(installer),@"^FzuAttendanceAssistant-Setup-\d+\.\d+\.\d+\.exe$"))throw new InvalidOperationException("更新文件位置无效。");
  int pid;if(!Int32.TryParse(args[3],out pid))throw new InvalidOperationException("更新进程参数无效。");string original=Path.Combine(directory,"晚点名助手.exe");
  try{using(var source=Process.GetProcessById(pid)){if(!String.Equals(source.MainModule.FileName,original,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("源程序不匹配。");if(!source.WaitForExit(45000))throw new InvalidOperationException("助手尚未退出，未开始安装。");}}catch(ArgumentException){}
  try{using(var setup=Process.Start(new ProcessStartInfo(installer,"/SILENT /SUPPRESSMSGBOXES /NORESTART /SP- /DIR="+Quote(directory)+" /TASKS="+Quote(args[4]=="1"?"desktopicon,startup":"desktopicon")){UseShellExecute=false,WindowStyle=ProcessWindowStyle.Hidden})){if(!setup.WaitForExit(300000))throw new InvalidOperationException("安装仍在进行，请等待安装完成。");if(setup.ExitCode!=0)throw new InvalidOperationException("更新未完成，安装返回代码 "+setup.ExitCode);}}
  finally{Broker(original,"--background",directory);}return 0;
 }
 public static void Tests(){
  string hash=new string('a',64);string json="{\"draft\":false,\"prerelease\":false,\"tag_name\":\"v9.8.7\",\"assets\":[{\"name\":\"FzuAttendanceAssistant-Setup-9.8.7.exe\",\"state\":\"uploaded\",\"browser_download_url\":\""+Repository+"releases/download/v9.8.7/FzuAttendanceAssistant-Setup-9.8.7.exe\",\"digest\":\"sha256:"+hash+"\"}]}";
  if(Parse(json).Version!=new Version("9.8.7"))throw new Exception("版本解析失败");bool rejected=false;try{Parse(json.Replace("https://github.com/","https://example.com/"));}catch(InvalidOperationException){rejected=true;}if(!rejected)throw new Exception("外部安装包未被拒绝");
  rejected=false;try{Parse(json.Replace("sha256:"+hash,""));}catch(InvalidOperationException){rejected=true;}if(!rejected)throw new Exception("缺少校验信息未被拒绝");
  string file=Store.FilePath("update-integrity-test.bin");File.WriteAllText(file,"test");try{if(Verify(file,hash))throw new Exception("损坏内容未被拒绝");}finally{File.Delete(file);}
  File.WriteAllText(Store.FilePath("更新检查.txt"),"通过：版本解析、安装包来源、校验缺失及文件损坏保护。未安装更新。");
 }
}
}
