param([switch]$SqlOnly, [switch]$IncludeSql)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $taskRoot
try {
    New-Item -ItemType Directory -Force -Path TestResults | Out-Null
    if (-not $SqlOnly) {
        # Default: only the three feature unit suites, with inline in-memory fakes.
        # No LocalDB discovery/start, SQL environment flag or database operation occurs here.
        & dotnet test tests/Unit/AgriSustain.UnitTests/AgriSustain.UnitTests.csproj -m:1 -p:UseSharedCompilation=false --filter 'FullyQualifiedName~AgriSustain.UnitTests.Farmer.|FullyQualifiedName~AgriSustain.UnitTests.RetailCustomer.' --logger 'trx;LogFileName=farmer-retail-unit.trx' --results-directory TestResults
        if ($LASTEXITCODE -ne 0) { throw 'Unit tests failed. See TestResults/farmer-retail-unit.trx.' }
    }
    if (-not ($SqlOnly -or $IncludeSql)) { return }

    # Real SQL integration coverage is explicitly opt-in, separate from the fake unit suite.
    $taskPreviousSqlTests = $env:THREE_UC_SQL_TESTS
    $taskPreviousServer = $env:THREE_UC_SQL_SERVER
    try {
        $taskInfo = (& sqllocaldb info MSSQLLocalDB 2>&1 | Out-String)
        $taskPipe = [regex]::Match($taskInfo, 'np:\\\\\.\\pipe\\LOCALDB#[A-Za-z0-9]+\\tsql\\query').Value
        if (-not $taskPipe) {
            & sqllocaldb start MSSQLLocalDB | Out-Null
            if ($LASTEXITCODE -ne 0) { throw 'LocalDB could not start.' }
            $taskInfo = (& sqllocaldb info MSSQLLocalDB 2>&1 | Out-String)
            $taskPipe = [regex]::Match($taskInfo, 'np:\\\\\.\\pipe\\LOCALDB#[A-Za-z0-9]+\\tsql\\query').Value
        }
        if (-not $taskPipe) { throw 'Cannot find the running LocalDB pipe.' }
        $env:THREE_UC_SQL_SERVER = $taskPipe
        $env:THREE_UC_SQL_TESTS = '1'
        & dotnet test tests/Integration/AgriSustain.IntegrationTests/AgriSustain.IntegrationTests.csproj -m:1 -p:UseSharedCompilation=false --filter 'FullyQualifiedName~ProductSqlTests|FullyQualifiedName~OrderFulfillmentSqlTests|FullyQualifiedName~CartSqlTests' --logger 'trx;LogFileName=farmer-retail-sql.trx' --results-directory TestResults
        if ($LASTEXITCODE -ne 0) { throw 'SQL integration tests failed; inspect the TRX before updating report status.' }
        Write-Output 'Results: TestResults/farmer-retail-sql.trx (56 report cases + 1 marketplace/cart regression).'
    } finally {
        $env:THREE_UC_SQL_TESTS = $taskPreviousSqlTests
        $env:THREE_UC_SQL_SERVER = $taskPreviousServer
    }
} finally { Pop-Location }
