param()
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskInfo = (& sqllocaldb info MSSQLLocalDB 2>&1 | Out-String)
$taskPipe = [regex]::Match($taskInfo, 'np:\\\\\.\\pipe\\LOCALDB#[A-Za-z0-9]+\\tsql\\query').Value
if (-not $taskPipe) {
    & sqllocaldb start MSSQLLocalDB | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'LocalDB could not start. No database changes made.' }
    $taskInfo = (& sqllocaldb info MSSQLLocalDB 2>&1 | Out-String)
    $taskPipe = [regex]::Match($taskInfo, 'np:\\\\\.\\pipe\\LOCALDB#[A-Za-z0-9]+\\tsql\\query').Value
}
if (-not $taskPipe) { throw 'The running LocalDB pipe could not be resolved. No database changes made.' }
$taskServer = '(localdb)\MSSQLLocalDB'
$taskCheck = (& sqlcmd -S $taskServer -d AgriSustain -E -b -l 8 -h -1 -W -Q "SET NOCOUNT ON; SELECT CASE WHEN DB_NAME()='AgriSustain' AND OBJECT_ID('dbo.Product','U') IS NOT NULL AND OBJECT_ID('dbo.RetailCheckoutQuote','U') IS NOT NULL THEN 'COMMERCE_SCHEMA_OK' ELSE 'WRONG_DATABASE' END;" 2>&1 | Out-String)
if ($LASTEXITCODE -ne 0 -or $taskCheck.Trim() -ne 'COMMERCE_SCHEMA_OK') { throw 'The existing AgriSustain database could not be verified. No upgrade applied.' }
& sqlcmd -S $taskPipe -d AgriSustain -E -b -f 65001 -i (Join-Path $taskRoot 'database/farmer-retail-features.sql')
if ($LASTEXITCODE -ne 0) { throw 'Additive upgrade failed; inspect SQL output.' }
$taskLocalFile = Join-Path $taskRoot 'src/AgriSustain.Api/appsettings.Local.json'
$taskConfig = if (Test-Path -LiteralPath $taskLocalFile) { Get-Content -Raw -LiteralPath $taskLocalFile | ConvertFrom-Json } else { [pscustomobject]@{} }
$taskConnection = "Server=$taskServer;Database=AgriSustain;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=8;"
if (-not $taskConfig.PSObject.Properties['ConnectionStrings']) { $taskConfig | Add-Member NoteProperty ConnectionStrings ([pscustomobject]@{}) }
if ($taskConfig.ConnectionStrings.PSObject.Properties['AgriSustain']) { $taskConfig.ConnectionStrings.AgriSustain = $taskConnection }
else { $taskConfig.ConnectionStrings | Add-Member NoteProperty AgriSustain $taskConnection }
[IO.File]::WriteAllText($taskLocalFile, ($taskConfig | ConvertTo-Json -Depth 20) + "`n", (New-Object Text.UTF8Encoding($false)))
& sqlcmd -S $taskPipe -d AgriSustain -E -b -Q "SET NOCOUNT ON; SELECT 'Commerce schema ready' AS Result, COL_LENGTH('dbo.CartItem','HarvestBatchId') AS CartBatchColumnBytes, COUNT(*) AS AddedTables FROM sys.tables WHERE name IN('CommerceMedia','CommerceSubmission','CommerceEvidenceRequest','CommerceRefundRequest');"
Write-Output 'Existing data preserved. Restart the API to load the local connection override.'
