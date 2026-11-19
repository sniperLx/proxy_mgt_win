$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$compiler = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $compiler)) {
    $compiler = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
}

$testOutputDirectory = Join-Path $projectRoot "tests\bin"
New-Item -ItemType Directory -Force -Path $testOutputDirectory | Out-Null
$testExecutable = Join-Path $testOutputDirectory "ProxyEnvManager.Tests.exe"

$sources = @()
$sources += Get-ChildItem (Join-Path $projectRoot "src\*.cs") |
    Where-Object { $_.Name -ne "Program.cs" } |
    ForEach-Object { $_.FullName }
$sources += (Join-Path $projectRoot "tests\CoreTests.cs")

$framework = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319"
if (-not (Test-Path (Join-Path $framework "System.dll"))) {
    $framework = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319"
}

$arguments = @(
    "/nologo",
    "/codepage:65001",
    "/target:exe",
    "/platform:anycpu",
    "/main:ProxyEnvManager.Tests.Program",
    "/out:$testExecutable",
    "/reference:$(Join-Path $framework 'System.dll')",
    "/reference:$(Join-Path $framework 'System.Core.dll')",
    "/reference:$(Join-Path $framework 'System.Drawing.dll')",
    "/reference:$(Join-Path $framework 'System.Windows.Forms.dll')"
) + $sources

& $compiler $arguments
if ($LASTEXITCODE -ne 0) {
    throw "Test build failed with exit code $LASTEXITCODE."
}

& $testExecutable
if ($LASTEXITCODE -ne 0) {
    throw "Tests failed with exit code $LASTEXITCODE."
}

Add-Type -AssemblyName System.Drawing
$iconPath = Join-Path $projectRoot "assets\proxy-env-manager.ico"
if (-not (Test-Path $iconPath)) {
    throw "Application icon is missing: $iconPath"
}

$iconStream = [System.IO.File]::OpenRead($iconPath)
try {
    $smallIcon = New-Object System.Drawing.Icon($iconStream, 16, 16)
    if ($smallIcon.Width -ne 16 -or $smallIcon.Height -ne 16) {
        throw "Application icon does not contain a usable 16x16 tray frame."
    }
    $smallIcon.Dispose()
}
finally {
    $iconStream.Dispose()
}

Write-Host "Application icon validated."
