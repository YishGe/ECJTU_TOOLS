# 重新编译两个 exe（可选，仅在你修改了 src\*.cs 之后需要）
# 用法：右键 -> 使用 PowerShell 运行，或在命令行执行
#   powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = Split-Path -Parent $here

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw '找不到 C# 编译器 csc.exe（.NET Framework 4.x）' }

$common = @(
  '/nologo', '/noconfig', '/codepage:65001', '/optimize+', '/target:winexe',
  '/platform:anycpu', "/win32manifest:$here\app.manifest",
  '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll'
)

$loginArgs = $common + @("/out:$root\校园网登录.exe", "$here\LoginCore.cs", "$here\LoginApp.cs")
& $csc $loginArgs
if ($LASTEXITCODE -ne 0) { throw '编译 校园网登录.exe 失败' }

$cfgArgs = $common + @("/out:$root\校园网配置.exe", "$here\LoginCore.cs", "$here\ConfigApp.cs")
& $csc $cfgArgs
if ($LASTEXITCODE -ne 0) { throw '编译 校园网配置.exe 失败' }

Get-ChildItem $root -Filter *.exe | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
