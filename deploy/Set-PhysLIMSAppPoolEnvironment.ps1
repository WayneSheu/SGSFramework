#Requires -Version 5.1
#Requires -PSEdition Desktop
#Requires -RunAsAdministrator
<#
.SYNOPSIS
    設定 PhysLIMS IIS 應用程式集區的環境變數 (ISO 27001 / 資安法：機密不寫入原始碼、不顯示、不留在命令列)。

.DESCRIPTION
    - 以 Microsoft.Web.Administration 於程序內寫入 applicationHost.config，機密不會出現在命令列或處理程序清單。
    - 機密來源優先順序：SecretManagement 保管庫 (-VaultName) → 互動式安全輸入 (不回顯)。
    - 必要變數若集區已有值且未指定 -Force，保留既有值 (可重複執行)。
    - 寫入前驗證格式 (連線字串須 Encrypt=True、JWT 金鑰至少 32 位元組、兩個 SQL 帳號不可相同)。
    - 只輸出變數名稱與動作，絕不輸出值。

.EXAMPLE
    # 首次部署：互動輸入必要機密，並一併設定 Migration 連線與管理員初始密碼
    .\Set-PhysLIMSAppPoolEnvironment.ps1 -KeysPath 'D:\PhysLIMS\DataProtectionKeys' -OptionalSecrets MigrationConnection, SeedAdminPassword

.EXAMPLE
    # 管理員帳號建立完成後，移除初始密碼
    .\Set-PhysLIMSAppPoolEnvironment.ps1 -Remove SeedAdmin__Password

.EXAMPLE
    # 輪替 JWT 金鑰 (覆寫既有值)
    .\Set-PhysLIMSAppPoolEnvironment.ps1 -Force -OnlySecrets JwtSecretKey

.EXAMPLE
    # 預覽將進行的變更，不寫入
    .\Set-PhysLIMSAppPoolEnvironment.ps1 -WhatIf
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$AppPoolName = 'PhysLIMS.API',

    [ValidateSet('Production', 'Staging')]
    [string]$AspNetCoreEnvironment = 'Production',

    # Data Protection 金鑰環路徑 (建議位於站台目錄外)
    [string]$KeysPath,

    [ValidateSet('Auto', 'Dpapi', 'Certificate')]
    [string]$DataProtectionMode,

    [string]$CertificateThumbprint,

    # 選用機密 (依功能啟用情況)
    [ValidateSet('MigrationConnection', 'SeedAdminPassword', 'SmtpPassword', 'HmsSharedSecret', 'RabbitMqUsername', 'RabbitMqPassword', 'PfxPassword')]
    [string[]]$OptionalSecrets = @(),

    # 只處理指定的機密 (輪替時使用)；未指定則處理全部必要機密 + OptionalSecrets
    [ValidateSet('DefaultConnection', 'JwtSecretKey', 'MigrationConnection', 'SeedAdminPassword', 'SmtpPassword', 'HmsSharedSecret', 'RabbitMqUsername', 'RabbitMqPassword', 'PfxPassword')]
    [string[]]$OnlySecrets = @(),

    # Microsoft.PowerShell.SecretManagement 保管庫名稱；機密名稱為 'PhysLIMS-<環境變數名稱>'
    [string]$VaultName,

    # 要移除的環境變數名稱 (例如首次部署後移除 SeedAdmin__Password)
    [string[]]$Remove = @(),

    # 覆寫集區中已存在的機密
    [switch]$Force,

    # 完成後不回收應用程式集區
    [switch]$NoRecycle
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# -----------------------------------------------------------------------------
# 機密定義：Key = 參數代號；Name = 環境變數名稱；Required = 未設定時系統無法啟動
# -----------------------------------------------------------------------------
$SecretSpecs = [ordered]@{
    DefaultConnection   = @{ Name = 'PersistentSettings__ConnectionStrings__DefaultConnection';   Required = $true;  Kind = 'SqlConnection' }
    JwtSecretKey        = @{ Name = 'JwtSettings__SecretKey';                                     Required = $true;  Kind = 'JwtKey' }
    MigrationConnection = @{ Name = 'PersistentSettings__ConnectionStrings__MigrationConnection'; Required = $false; Kind = 'SqlConnection' }
    SeedAdminPassword   = @{ Name = 'SeedAdmin__Password';                                        Required = $false; Kind = 'Text' }
    SmtpPassword        = @{ Name = 'AlertSettings__Smtp__Password';                              Required = $false; Kind = 'Text' }
    HmsSharedSecret     = @{ Name = 'HmsSettings__SharedSecret';                                  Required = $false; Kind = 'Text' }
    RabbitMqUsername    = @{ Name = 'PersistentSettings__MessageBroker__RabbitMQ__Username';      Required = $false; Kind = 'Text' }
    RabbitMqPassword    = @{ Name = 'PersistentSettings__MessageBroker__RabbitMQ__Password';      Required = $false; Kind = 'Text' }
    PfxPassword         = @{ Name = 'DataProtection__Certificate__PfxPassword';                   Required = $false; Kind = 'Text' }
}

# -----------------------------------------------------------------------------
# 驗證函式 (不輸出任何機密值)
# -----------------------------------------------------------------------------
function Test-SqlConnectionString {
    param([string]$Label, [string]$Value)

    try {
        $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($Value)
    }
    catch {
        throw "$Label 不是有效的 SQL Server 連線字串。"
    }

    if (-not $builder.Encrypt) {
        throw "$Label 必須包含 Encrypt=True (傳輸加密)。"
    }
    if ($builder.TrustServerCertificate) {
        Write-Warning "$Label 設定 TrustServerCertificate=True，將略過伺服器憑證驗證；正式環境建議改用受信任憑證。"
    }
    if ($builder.IntegratedSecurity -eq $false -and [string]::IsNullOrWhiteSpace($builder.UserID)) {
        throw "$Label 未指定登入帳號 (User Id) 或 Integrated Security。"
    }

    return $builder.UserID
}

function Test-JwtSecretKey {
    param([string]$Value)

    if ([System.Text.Encoding]::UTF8.GetByteCount($Value) -lt 32) {
        throw 'JwtSettings__SecretKey 長度不足：UTF-8 至少需 32 位元組 (256 位元)。'
    }
}

function ConvertFrom-SecureStringPlain {
    param([System.Security.SecureString]$Secure)

    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Secure)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
}

function Get-SecretValue {
    param([string]$EnvName)

    if ($VaultName) {
        $secretName = "PhysLIMS-$EnvName"
        $secret = Get-Secret -Vault $VaultName -Name $secretName -ErrorAction SilentlyContinue
        if ($null -ne $secret) {
            if ($secret -is [System.Security.SecureString]) { return ConvertFrom-SecureStringPlain $secret }
            return [string]$secret
        }
        Write-Warning "保管庫 $VaultName 中找不到 $secretName，改為互動輸入。"
    }

    if (-not [Environment]::UserInteractive) {
        throw "非互動模式且無法自保管庫取得 $EnvName。"
    }

    $first = Read-Host -AsSecureString "請輸入 $EnvName (不顯示)"
    $second = Read-Host -AsSecureString "請再次輸入 $EnvName 確認"
    $a = ConvertFrom-SecureStringPlain $first
    $b = ConvertFrom-SecureStringPlain $second
    if ($a -cne $b) { throw "$EnvName 兩次輸入不一致。" }
    if ([string]::IsNullOrWhiteSpace($a)) { throw "$EnvName 不可為空白。" }
    return $a
}

# -----------------------------------------------------------------------------
# 載入 IIS 設定 (程序內寫入 applicationHost.config，不經命令列)
# -----------------------------------------------------------------------------
if ($VaultName -and -not (Get-Command Get-Secret -ErrorAction SilentlyContinue)) {
    throw '指定了 -VaultName，但未安裝 Microsoft.PowerShell.SecretManagement 模組。'
}

$mwaPath = Join-Path $env:windir 'System32\inetsrv\Microsoft.Web.Administration.dll'
if (-not (Test-Path $mwaPath)) { throw "找不到 IIS 管理元件：$mwaPath (請確認已安裝 IIS)。" }
Add-Type -Path $mwaPath

$serverManager = New-Object Microsoft.Web.Administration.ServerManager
try {
    $pool = $serverManager.ApplicationPools[$AppPoolName]
    if ($null -eq $pool) { throw "找不到應用程式集區 '$AppPoolName'，請先建立 IIS 站台與集區。" }

    $poolsCollection = $serverManager.GetApplicationHostConfiguration().GetSection('system.applicationHost/applicationPools').GetCollection()
    $poolElement = $poolsCollection | Where-Object { $_.GetAttributeValue('name') -eq $AppPoolName } | Select-Object -First 1
    $envCollection = $poolElement.GetCollection('environmentVariables')

    function Find-EnvElement([string]$Name) {
        $envCollection | Where-Object { $_.GetAttributeValue('name') -eq $Name } | Select-Object -First 1
    }

    $pending = [ordered]@{}   # 環境變數名稱 → 值 (僅存在記憶體)
    $report = New-Object System.Collections.Generic.List[object]

    # --- 非機密設定 ---
    $pending['ASPNETCORE_ENVIRONMENT'] = $AspNetCoreEnvironment
    if ($KeysPath) { $pending['DataProtection__KeysPath'] = $KeysPath }
    if ($DataProtectionMode) { $pending['DataProtection__Mode'] = $DataProtectionMode }
    if ($CertificateThumbprint) { $pending['DataProtection__Certificate__Thumbprint'] = ($CertificateThumbprint -replace '\s', '') }

    # --- 機密 ---
    $targets = if ($OnlySecrets.Count -gt 0) { $OnlySecrets }
               else { @($SecretSpecs.Keys | Where-Object { $SecretSpecs[$_].Required }) + $OptionalSecrets }

    $sqlUsers = @{}
    foreach ($key in ($targets | Select-Object -Unique)) {
        $spec = $SecretSpecs[$key]
        $envName = $spec.Name

        if ((Find-EnvElement $envName) -and -not $Force) {
            $report.Add([pscustomobject]@{ 變數 = $envName; 動作 = '保留既有值' })
            continue
        }

        $value = Get-SecretValue $envName
        switch ($spec.Kind) {
            'SqlConnection' { $sqlUsers[$key] = Test-SqlConnectionString -Label $envName -Value $value }
            'JwtKey'        { Test-JwtSecretKey -Value $value }
        }
        $pending[$envName] = $value
    }

    if ($sqlUsers.ContainsKey('DefaultConnection') -and $sqlUsers.ContainsKey('MigrationConnection') `
        -and $sqlUsers['DefaultConnection'] -and $sqlUsers['DefaultConnection'] -eq $sqlUsers['MigrationConnection']) {
        throw 'DefaultConnection 與 MigrationConnection 使用同一個 SQL 帳號，違反最小權限原則 (執行期應使用僅具 DML 權限的帳號)。'
    }

    # --- 寫入 ---
    foreach ($envName in $pending.Keys) {
        $existing = Find-EnvElement $envName
        $action = if ($existing) { '更新' } else { '新增' }
        if ($PSCmdlet.ShouldProcess("$AppPoolName / $envName", $action)) {
            if ($existing) {
                $existing.SetAttributeValue('value', $pending[$envName])
            }
            else {
                $element = $envCollection.CreateElement('add')
                $element.SetAttributeValue('name', $envName)
                $element.SetAttributeValue('value', $pending[$envName])
                [void]$envCollection.Add($element)
            }
        }
        $report.Add([pscustomobject]@{ 變數 = $envName; 動作 = $action })
    }

    foreach ($envName in $Remove) {
        $existing = Find-EnvElement $envName
        if (-not $existing) {
            $report.Add([pscustomobject]@{ 變數 = $envName; 動作 = '不存在，略過' })
            continue
        }
        if ($PSCmdlet.ShouldProcess("$AppPoolName / $envName", '移除')) {
            $envCollection.Remove($existing)
        }
        $report.Add([pscustomobject]@{ 變數 = $envName; 動作 = '移除' })
    }

    if ($WhatIfPreference) {
        Write-Host '[WhatIf] 未寫入任何變更。'
    }
    else {
        $serverManager.CommitChanges()
        if (-not $NoRecycle -and $pool.State -eq 'Started') {
            [void]$pool.Recycle()
            Write-Host "已回收應用程式集區 $AppPoolName，新設定已生效。"
        }
    }

    $report | Format-Table -AutoSize

    # 檢查必要變數是否齊全 (僅檢查是否存在，不讀取值)
    $missing = $SecretSpecs.Values | Where-Object { $_.Required -and -not $pending.Contains($_.Name) -and -not (Find-EnvElement $_.Name) }
    foreach ($m in $missing) { Write-Warning "必要環境變數 $($m.Name) 尚未設定，系統將無法啟動。" }
}
finally {
    $pending = $null
    $serverManager.Dispose()
}
