param([string]$IsccPath = '', [string]$InstallerAppId = 'FzuAttendanceAssistant')
$ErrorActionPreference = 'Stop'
$fzuVersion = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'VERSION') -Raw).Trim()
if ($fzuVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'VERSION 格式应为 x.y.z。' }
& (Join-Path $PSScriptRoot 'build.ps1')
$fzuPortable = Join-Path $PSScriptRoot 'dist\portable'
$fzuTests = Join-Path $PSScriptRoot 'dist\test-run'
New-Item -ItemType Directory -Path $fzuTests -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $fzuPortable '晚点名助手.exe') -Destination $fzuTests -Force
foreach ($fzuMode in @('--self-test','--android-page-test','--ui-test','--update-test')) {
  $fzuProcess = Start-Process -FilePath (Join-Path $fzuTests '晚点名助手.exe') -ArgumentList $fzuMode -WindowStyle Hidden -PassThru -Wait
  if ($fzuProcess.ExitCode -ne 0) { throw ('检查未通过：' + $fzuMode) }
}
if (-not $IsccPath) {
  $fzuCandidates = @('C:\Program Files (x86)\Inno Setup 6\ISCC.exe','C:\Program Files\Inno Setup 6\ISCC.exe','C:\Program Files\Inno Setup 7\ISCC.exe',"$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe")
  $IsccPath = $fzuCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
  if (-not $IsccPath) { $fzuCommand = Get-Command ISCC.exe -ErrorAction SilentlyContinue; if ($fzuCommand) { $IsccPath = $fzuCommand.Source } }
}
if (-not $IsccPath -or -not (Test-Path -LiteralPath $IsccPath)) { throw '请先安装 Inno Setup，或传入 -IsccPath。' }
$fzuRelease = Join-Path $PSScriptRoot 'dist\release'
New-Item -ItemType Directory -Path $fzuRelease -Force | Out-Null
& $IsccPath /Q "/DAppVersion=$fzuVersion" "/DPayloadDir=$fzuPortable" "/DReleaseDir=$fzuRelease" "/DAppIdValue=$InstallerAppId" (Join-Path $PSScriptRoot 'installer\setup.iss')
if ($LASTEXITCODE -ne 0) { throw '安装包编译失败。' }
$fzuZip = Join-Path $fzuRelease "FzuAttendanceAssistant-$fzuVersion-win-x64.zip"
# Strict allowlist: never include settings, diagnostics, accounts or screenshots.
$fzuStage = Join-Path $PSScriptRoot 'dist\zip-content'
New-Item -ItemType Directory -Path (Join-Path $fzuStage 'assets') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $fzuPortable '晚点名助手.exe'),(Join-Path $fzuPortable '使用说明.md') -Destination $fzuStage -Force
foreach ($fzuAsset in @('app.ico','app.png','app.svg')) { Copy-Item -LiteralPath (Join-Path $fzuPortable ('assets\'+$fzuAsset)) -Destination (Join-Path $fzuStage 'assets') -Force }
$fzuZipEntries = @((Join-Path $fzuStage '晚点名助手.exe'),(Join-Path $fzuStage '使用说明.md'),(Join-Path $fzuStage 'assets'))
Compress-Archive -LiteralPath $fzuZipEntries -DestinationPath $fzuZip -Force
$fzuSetup = Join-Path $fzuRelease "FzuAttendanceAssistant-Setup-$fzuVersion.exe"
$fzuChecksums = @($fzuSetup,$fzuZip) | ForEach-Object { $fzuHash=Get-FileHash -LiteralPath $_ -Algorithm SHA256; $fzuHash.Hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($_) }
$fzuChecksums | Set-Content -LiteralPath (Join-Path $fzuRelease 'SHA256SUMS.txt') -Encoding ascii
Write-Output ('发布文件：' + $fzuRelease)
