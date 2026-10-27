param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repo 'artifacts\publish' }
$feed = Join-Path $repo 'artifacts\nuget'
$cache = Join-Path $repo 'artifacts\packages'
$env:DOTNET_CLI_HOME = Join-Path (Split-Path -Parent $repo) 'dotnet-cli'
$env:DOTNET_NOLOGO = '1'
if (-not (Test-Path -LiteralPath $feed)) { throw '先运行 python scripts/fetch_runtime.py 获取官方运行时包。' }
dotnet publish (Join-Path $repo 'src\RiftMusic.App\RiftMusic.App.csproj') --disable-build-servers -m:1 -p:UseSharedCompilation=false -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=embedded -p:RuntimeFrameworkVersion=10.0.5 -p:NuGetAudit=false --source $feed --packages $cache -o $OutputDirectory
if ($LASTEXITCODE -ne 0) { throw '发布失败。' }
python (Join-Path $repo 'scripts\runtime_notices.py') $OutputDirectory
if ($LASTEXITCODE -ne 0) { throw '复制运行时说明失败。' }
Copy-Item -LiteralPath (Join-Path $repo 'README.md') -Destination (Join-Path $OutputDirectory '说明\使用说明.md')
$documentation = Join-Path $OutputDirectory '说明'
Copy-Item -LiteralPath (Join-Path $repo 'docs\release-v01.md') -Destination (Join-Path $documentation '0.1测试说明.md')
Copy-Item -LiteralPath (Join-Path $repo 'docs\music-sources.md') -Destination (Join-Path $documentation '音乐来源.md')
Copy-Item -LiteralPath (Join-Path $repo 'docs\music-discovery.md') -Destination (Join-Path $documentation '音乐档案与补充歌单.md')
Copy-Item -LiteralPath (Join-Path $repo 'docs\music-additions-v050.json') -Destination (Join-Path $documentation '新增音乐发行清单.json')
Copy-Item -LiteralPath (Join-Path $repo 'docs\verification.md') -Destination (Join-Path $documentation '验证记录.md')
Copy-Item -LiteralPath (Join-Path $repo 'docs\artwork-sources.json') -Destination (Join-Path $documentation '主题图像来源.json')
Copy-Item -LiteralPath (Join-Path $repo 'docs\third-party-ui.md') -Destination (Join-Path $documentation '第三方界面资源.md')
Copy-Item -LiteralPath (Join-Path $repo 'docs\light-design.md') -Destination (Join-Path $documentation '设计与架构.md')
Copy-Item -LiteralPath (Join-Path $repo 'docs\ai-atmosphere-v054.md') -Destination (Join-Path $documentation 'AI配图与提示.md')
Copy-Item -LiteralPath (Join-Path $repo 'src\RiftMusic.App\Assets\Art\FLUENT-ICONS-LICENSE.txt') -Destination (Join-Path $documentation 'ThirdParty\Fluent-Icons-MIT.txt')
Copy-Item -LiteralPath (Join-Path $repo 'src\RiftMusic.App\Assets\Fonts\SOURCE-HAN-SANS-OFL.txt') -Destination (Join-Path $documentation 'ThirdParty\Source-Han-Sans-OFL.txt')
foreach ($fontLicense in Get-ChildItem -LiteralPath (Join-Path $repo 'src\RiftMusic.App\Assets\Fonts') -Filter '*OFL.txt') {
    Copy-Item -LiteralPath $fontLicense.FullName -Destination (Join-Path $documentation ('ThirdParty\' + $fontLicense.Name))
}
Copy-Item -LiteralPath (Join-Path $repo 'docs\worlds22-reference.md') -Destination (Join-Path $documentation '2022视觉与字体.md')
Write-Output "发布目录：$OutputDirectory"

foreach ($notice in Get-ChildItem -LiteralPath (Join-Path $repo 'src\RiftMusic.App\Assets\Fonts') -Filter 'ALIMAMA-SHUHEITI-*.txt') { Copy-Item -LiteralPath $notice.FullName -Destination (Join-Path $documentation ('ThirdParty\' + $notice.Name)) }

$previewDirectory = Join-Path $documentation "docs\images"
New-Item -ItemType Directory -Path $previewDirectory -Force | Out-Null
Copy-Item -Path (Join-Path $repo "docs\images\*.png") -Destination $previewDirectory -Force
