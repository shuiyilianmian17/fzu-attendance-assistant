$ErrorActionPreference = 'Stop'
$fzuRoot = Split-Path -Parent $PSScriptRoot
$fzuCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$fzuTestExe = Join-Path $PSScriptRoot 'MuMuMultiInstanceTests.exe'
& $fzuCompiler /nologo /target:exe /warnaserror+ /r:System.Web.Extensions.dll "/out:$fzuTestExe" (Join-Path $fzuRoot 'src\MuMuClient.cs') (Join-Path $PSScriptRoot 'MuMuMultiInstanceTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'MuMu 离线测试编译失败。' }
& $fzuTestExe
if ($LASTEXITCODE -ne 0) { throw 'MuMu 离线回归测试失败。' }
