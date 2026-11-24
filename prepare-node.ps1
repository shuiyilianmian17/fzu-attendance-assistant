$ErrorActionPreference='Stop'
$nodeVersion='v24.14.1'
$runtimeDirectory=Join-Path $PSScriptRoot 'runtime'
$archivePath=Join-Path ([IO.Path]::GetTempPath()) ('fzu-node-'+[guid]::NewGuid().ToString('N')+'.zip')
$archiveName='node-'+$nodeVersion+'-win-x64.zip'
try {
 Invoke-WebRequest ('https://nodejs.org/dist/'+$nodeVersion+'/'+$archiveName) -OutFile $archivePath
 $checksums=(Invoke-WebRequest ('https://nodejs.org/dist/'+$nodeVersion+'/SHASUMS256.txt')).Content
 $match=[regex]::Match($checksums,'(?m)^([a-fA-F0-9]{64})\s+'+[regex]::Escape($archiveName)+'\s*$')
 if(!$match.Success -or (Get-FileHash -LiteralPath $archivePath).Hash -ne $match.Groups[1].Value){throw 'Node.js 校验失败'}
 Add-Type -AssemblyName System.IO.Compression.FileSystem
 $archive=[IO.Compression.ZipFile]::OpenRead($archivePath)
 try {
  New-Item -ItemType Directory -Path $runtimeDirectory -Force | Out-Null
  foreach($name in @('node.exe','LICENSE')){
   $entry=$archive.GetEntry('node-'+$nodeVersion+'-win-x64/'+$name)
   [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,(Join-Path $runtimeDirectory $name),$true)
  }
 } finally {$archive.Dispose()}
} finally {if(Test-Path -LiteralPath $archivePath){[IO.File]::Delete($archivePath)}}
