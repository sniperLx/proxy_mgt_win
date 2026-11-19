$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$compiler = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $compiler)) {
    $compiler = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
}
if (-not (Test-Path $compiler)) {
    throw "Could not find the .NET Framework C# compiler (csc.exe)."
}

$outputDirectory = Join-Path $projectRoot "dist"
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$outputPath = Join-Path $outputDirectory "ProxyEnvManager.exe"
$iconPath = Join-Path $projectRoot "assets\proxy-env-manager.ico"
if (-not (Test-Path $iconPath)) {
    throw "Could not find the application icon. Run tools\Generate-AppIcon.ps1 first."
}

$sources = Get-ChildItem (Join-Path $projectRoot "src\*.cs") | ForEach-Object { $_.FullName }
$framework = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319"
if (-not (Test-Path (Join-Path $framework "System.dll"))) {
    $framework = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319"
}

$arguments = @(
    "/nologo",
    "/codepage:65001",
    "/target:winexe",
    "/platform:anycpu",
    "/optimize+",
    "/out:$outputPath",
    "/win32manifest:$(Join-Path $projectRoot 'assets\app.manifest')",
    "/win32icon:$iconPath",
    "/resource:$iconPath,ProxyEnvManager.app.ico",
    "/reference:$(Join-Path $framework 'System.dll')",
    "/reference:$(Join-Path $framework 'System.Core.dll')",
    "/reference:$(Join-Path $framework 'System.Drawing.dll')",
    "/reference:$(Join-Path $framework 'System.Windows.Forms.dll')"
) + $sources

& $compiler $arguments
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

Write-Host "Built: $outputPath"
