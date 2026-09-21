$ErrorActionPreference='Stop'
$sdk = (& dotnet --list-sdks | Select-Object -Last 1)
if (!$sdk) { throw '.NET SDK is required.' }
$version=($sdk -split ' ')[0]
$base=($sdk -replace '^.*\[','' -replace '\]$','')
$compiler=Join-Path $base "$version\Roslyn\bincore\csc.dll"
$refs=Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8'
if (!(Test-Path $refs)) { throw '.NET Framework 4.8 Developer Pack is required.' }
$arguments=@('/nologo','/target:winexe','/optimize+','/langversion:8.0',"/win32manifest:$PSScriptRoot\src\app.manifest", "/out:$PSScriptRoot\PokaAntiAFK.exe")
foreach($name in @('mscorlib','System','System.Core','System.Drawing','System.Windows.Forms')) { $arguments+="/reference:$refs\$name.dll" }
$arguments+=(Get-ChildItem "$PSScriptRoot\src\*.cs").FullName
$arguments+="/win32icon:$PSScriptRoot\assets\poka.ico"
$arguments+="/resource:$PSScriptRoot\assets\poka.ico,PokaAntiAFK.AppIcon"
& dotnet $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
