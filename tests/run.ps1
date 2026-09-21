$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
& "$root\build.ps1"
$sdk = (& dotnet --list-sdks | Select-Object -Last 1)
$version = ($sdk -split ' ')[0]
$base = ($sdk -replace '^.*\[','' -replace '\]$','')
$compiler = Join-Path $base "$version\Roslyn\bincore\csc.dll"
$refs = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8'
$resultDir = Join-Path $root 'TestResults'
New-Item -ItemType Directory -Force $resultDir | Out-Null
$arguments = @('/nologo','/target:exe',"/out:$resultDir\Verify.exe","/reference:$root\PokaAntiAFK.exe")
foreach ($name in @('mscorlib','System','System.Core','System.Drawing','System.Windows.Forms')) {
    $arguments += "/reference:$refs\$name.dll"
}
$arguments += "$PSScriptRoot\Verify.cs"
& dotnet $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
Copy-Item "$root\PokaAntiAFK.exe" "$resultDir\PokaAntiAFK.exe" -Force
& "$resultDir\Verify.exe" "$resultDir\preview.png"
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
