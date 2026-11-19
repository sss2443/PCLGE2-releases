# PCLGE 构建脚本：发布单文件 Windows exe，并把产物复制到 release 目录
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
$project = Join-Path $root "src\PCLGE\PCLGE.csproj"

Write-Host "== 构建 PCLGE ==" -ForegroundColor Cyan
dotnet publish $project -c $Configuration -r win-x64 `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true

$output = Join-Path $root "src\PCLGE\bin\$Configuration\net10.0-windows\win-x64\publish\PCLGE.exe"
if (-not (Test-Path $output)) { throw "没有找到构建产物：$output" }

$release = Join-Path $root "release"
New-Item -ItemType Directory -Force -Path $release | Out-Null
Copy-Item $output (Join-Path $release "PCLGE.exe") -Force

Write-Host "完成：release\PCLGE.exe" -ForegroundColor Green
