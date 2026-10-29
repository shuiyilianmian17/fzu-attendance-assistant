using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;
namespace FzuAttendance {
static class EmulatorDiscovery {
 static void Add(List<string> paths,string value){
  if(String.IsNullOrWhiteSpace(value))return;value=Environment.ExpandEnvironmentVariables(value.Trim());
  if(value.StartsWith("\"")){int end=value.IndexOf('"',1);if(end>0)value=value.Substring(1,end-1);}else value=Regex.Replace(value,@",-?\d+$","");
  try{string dir=Directory.Exists(value)?value:Path.GetDirectoryName(value);if(String.IsNullOrEmpty(dir))return;
   foreach(string baseDir in new[]{dir,Path.GetDirectoryName(dir)}){if(String.IsNullOrEmpty(baseDir))continue;foreach(string sub in new[]{"", "nx_main",@"MuMu\nx_main",@"Netease\MuMu\nx_main",@"NetEase\MuMuPlayer-12.0\shell"}){
    string folder=Path.Combine(baseDir,sub);foreach(string exe in new[]{"mumu-cli.exe","MuMuManager.exe","ldconsole.exe"})paths.Add(Path.Combine(folder,exe));}}
  }catch{}
 }
 public static string Choose(System.Collections.Generic.IEnumerable<string> paths){var valid=paths.Where(AndroidClient.ValidConsole).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();return valid.FirstOrDefault(MuMuClient.IsPath)??valid.FirstOrDefault();}
 public static string FromHints(IEnumerable<string> hints){var paths=new List<string>();foreach(string hint in hints)Add(paths,hint);return Choose(paths);}
 public static string Detect(string current){var paths=new List<string>();Add(paths,current);
  foreach(string name in new[]{"MuMuNxMain","MuMuNxDevice","MuMuPlayer","dnplayer"})foreach(var process in Process.GetProcessesByName(name))using(process){try{Add(paths,process.MainModule.FileName);}catch{}}
  foreach(var hive in new[]{RegistryHive.CurrentUser,RegistryHive.LocalMachine})foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32})try{using(var registry=RegistryKey.OpenBaseKey(hive,view)){
   using(var uninstall=registry.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")){if(uninstall!=null)foreach(string key in uninstall.GetSubKeyNames())using(var app=uninstall.OpenSubKey(key)){if(app==null||!Regex.IsMatch(Convert.ToString(app.GetValue("DisplayName"))+" "+key,"MuMu|网易.*模拟器|雷电|LDPlayer",RegexOptions.IgnoreCase))continue;foreach(string field in new[]{"InstallLocation","DisplayIcon","UninstallString"})Add(paths,Convert.ToString(app.GetValue(field)));}}
   foreach(string name in new[]{"MuMuNxMain.exe","MuMuPlayer.exe","mumu-cli.exe"})using(var app=registry.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\"+name)){if(app!=null){Add(paths,Convert.ToString(app.GetValue("")));Add(paths,Convert.ToString(app.GetValue("Path")));}}
  }}catch{}
  foreach(var drive in DriveInfo.GetDrives())try{if(drive.DriveType!=DriveType.Fixed||!drive.IsReady)continue;string root=drive.RootDirectory.FullName;
   foreach(string folder in new[]{@"Program Files\Netease\MuMu",@"Program Files\MuMu",@"Program Files (x86)\Netease\MuMu",@"Netease\MuMu","MuMu","MuMuPlayer",@"leidian\LDPlayer14",@"leidian\LDPlayer9"})Add(paths,Path.Combine(root,folder));
   foreach(string folder in Directory.GetDirectories(root)){if(Regex.IsMatch(Path.GetFileName(folder),"MuMu|Netease",RegexOptions.IgnoreCase))Add(paths,folder);}
  }catch{}
  foreach(string folder in new[]{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)})Add(paths,Path.Combine(folder,@"Netease\MuMu"));
  // MuMu shortcuts cover custom installation directories absent from uninstall records.
  foreach(string menu in new[]{Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)})try{
   foreach(string file in Directory.EnumerateFiles(menu,"*.lnk",SearchOption.AllDirectories)){if(!Regex.IsMatch(Path.GetFileName(file),"MuMu",RegexOptions.IgnoreCase))continue;dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));dynamic shortcut=shell.CreateShortcut(file);Add(paths,(string)shortcut.TargetPath);}
  }catch{}
  return Choose(paths);
 }
}
}
