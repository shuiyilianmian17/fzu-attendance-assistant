param([string]$OutputDir = (Join-Path $PSScriptRoot 'dist\portable'))
$ErrorActionPreference = 'Stop'
$fzuCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $fzuCompiler)) { throw '需要 Windows x64 与 .NET Framework 4.8。' }
New-Item -ItemType Directory -Path $OutputDir,(Join-Path $OutputDir 'assets') -Force | Out-Null
$fzuFramework = Split-Path -Parent $fzuCompiler
$fzuReferences = @('System.Windows.Forms.dll','System.Drawing.dll','System.Web.Extensions.dll','Microsoft.CSharp.dll','System.Xaml.dll','System.Xml.dll','WPF\PresentationFramework.dll','WPF\PresentationCore.dll','WPF\WindowsBase.dll','WPF\WindowsFormsIntegration.dll') | ForEach-Object { '/r:' + (Join-Path $fzuFramework $_) }
$fzuSources = @('Assistant.cs','EmulatorHost.cs','Dashboard.cs','Standalone.cs','HiddenPagePreview.cs','AndroidClient.cs','MuMuClient.cs','EmulatorDiscovery.cs','UpdateClient.cs','AndroidTests.cs','AssemblyInfo.cs') | ForEach-Object { Join-Path $PSScriptRoot ('src\' + $_) }
& $fzuCompiler /nologo /target:winexe /optimize+ "/out:$(Join-Path $OutputDir '晚点名助手.exe')" "/win32icon:$(Join-Path $PSScriptRoot 'assets\app.ico')" "/resource:$(Join-Path $PSScriptRoot 'src\Ui.xaml'),FzuAttendance.Ui.xaml" @fzuReferences @fzuSources
if ($LASTEXITCODE -ne 0) { throw '编译失败。' }
foreach ($fzuAsset in @('app.ico','app.png','app.svg')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('assets\' + $fzuAsset)) -Destination (Join-Path $OutputDir 'assets') -Force }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '使用说明.md') -Destination $OutputDir -Force
Write-Output ('已生成：' + (Join-Path $OutputDir '晚点名助手.exe'))
