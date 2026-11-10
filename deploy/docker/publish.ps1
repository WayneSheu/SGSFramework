<#
  PhysLIMS 方案 A 發佈腳本（須在持有簽署憑證的 Windows 開發 / 建置機執行）
  產出：artifacts\publish （API + plugins + wwwroot\blazor），供 Dockerfile COPY。

  用法：  .\publish.ps1 -PublicUrl http://localhost:8080/
  - 外掛 PostBuild 會以 Cert:\CurrentUser\My\<指紋> 簽署，故不可在 Docker build 內執行。
  - 簽署後會逐一驗證主模組 DLL 的簽章指紋，與 appsettings 的 TrustedThumbprint 不符即中止。
#>
[CmdletBinding()]
param(
    [string]$PublicUrl = 'http://localhost:8080/',   # Blazor 呼叫 API 的基底網址（單一容器時即站台自身網址）
    [string]$Configuration = 'Release',
    [string]$TrustedThumbprint = 'DB22B4CE33AE64287F9392F3025EBCB7880BE213'
)
$ErrorActionPreference = 'Stop'

$here    = $PSScriptRoot
$srcRoot = Resolve-Path (Join-Path $here '..\..\..')              # ...\PhysLIMS\src
$sgsRoot = Join-Path $srcRoot 'SGSFramework'   # ORG 模組的 PostBuild 以 $(SolutionDir) 定位 plugins，單獨建置時須明確帶入
$api     = Join-Path $srcRoot 'SGSFramework\Host\PhysLIMS.API'
$fe      = Join-Path $srcRoot 'FrontendFrameWork\FrontendFrameWork'
$out     = Join-Path $here 'artifacts'
$pub     = Join-Path $out 'publish'
$blazor  = Join-Path $out 'blazor'

if (-not $PublicUrl.EndsWith('/')) { $PublicUrl += '/' }
if (-not (Get-ChildItem Cert:\CurrentUser\My | Where-Object Thumbprint -eq $TrustedThumbprint)) {
    throw "找不到簽署憑證 $TrustedThumbprint（Cert:\CurrentUser\My），外掛無法簽署。"
}

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Path $pub, $blazor | Out-Null

Write-Host '==> 1/5 建置 FrontendFrameWork.Shared（API 以 HintPath 參照其 Release DLL）'
dotnet build (Join-Path $fe 'FrontendFrameWork.Shared') -c $Configuration
if ($LASTEXITCODE) { throw 'FrontendFrameWork.Shared 建置失敗' }

Write-Host '==> 2/5 發佈 Blazor WASM'
dotnet publish (Join-Path $fe 'BlazorWasmApp\BlazorWasmApp.csproj') -c $Configuration -o $blazor
if ($LASTEXITCODE) { throw 'BlazorWasmApp 發佈失敗' }
$wasmRoot = Join-Path $blazor 'wwwroot'

# API 以 /blazor 路徑託管：base href 改為 /blazor/，API 基底網址改為站台網址
$index = Join-Path $wasmRoot 'index.html'
(Get-Content $index -Raw -Encoding UTF8) -replace '<base href="/"\s*/>', '<base href="/blazor/" />' |
    Set-Content $index -Encoding UTF8 -NoNewline
$appsettings = Join-Path $wasmRoot 'appsettings.json'
(Get-Content $appsettings -Raw -Encoding UTF8) -replace '"BaseAddress"\s*:\s*"[^"]*"', ('"BaseAddress": "' + $PublicUrl + '"') |
    Set-Content $appsettings -Encoding UTF8 -NoNewline
# 改過的檔案其預壓縮版本已失效，移除以免伺服器送出舊內容
Get-ChildItem $wasmRoot -Recurse -Include 'index.html.br','index.html.gz','appsettings.json.br','appsettings.json.gz' |
    Remove-Item -Force

Write-Host '==> 3/5 發佈 API（連帶建置各外掛並由 PostBuild 簽署）'
dotnet publish (Join-Path $api 'PhysLIMS.API.csproj') -c $Configuration -r win-x64 --self-contained false -p:SolutionDir="$sgsRoot\" -o $pub
if ($LASTEXITCODE) { throw 'PhysLIMS.API 發佈失敗' }

Write-Host '==> 4/5 組裝 plugins 與前端'
$plugins = Join-Path $api "bin\$Configuration\net10.0\plugins"
if (-not (Test-Path $plugins)) { throw "找不到外掛目錄 $plugins" }
Copy-Item $plugins (Join-Path $pub 'plugins') -Recurse -Force
Copy-Item $wasmRoot (Join-Path $pub 'wwwroot\blazor') -Recurse -Force

Write-Host '==> 5/5 驗證主模組簽章指紋'
$mains = Get-ChildItem (Join-Path $pub 'plugins') -Filter '*.dll' |
    Where-Object { $_.Name -notmatch '\.(Application|Infrastructure)\.dll$' }
if (-not $mains) { throw 'plugins 內沒有主模組 DLL' }
foreach ($dll in $mains) {
    $sig = Get-AuthenticodeSignature $dll.FullName
    if (-not $sig.SignerCertificate -or $sig.SignerCertificate.Thumbprint -ne $TrustedThumbprint) {
        throw "$($dll.Name) 簽章指紋不符或未簽署：$($sig.Status)"
    }
    Write-Host "   OK  $($dll.Name)"
}

# 發佈目錄不得含機密或開發設定
Get-ChildItem $pub -Recurse -Include 'claudesettings*.json','appsettings.Development.json','*.snk' | Remove-Item -Force

Write-Host "完成：$pub"
