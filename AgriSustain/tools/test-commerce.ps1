param([switch]$SqlOnly)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $taskRoot
try {
    New-Item -ItemType Directory -Force -Path TestResults | Out-Null
    if (-not $SqlOnly) {
        & dotnet test tests/Unit/AgriSustain.UnitTests/AgriSustain.UnitTests.csproj -m:1 -p:UseSharedCompilation=false --logger 'trx;LogFileName=commerce-unit.trx' --results-directory TestResults
        if ($LASTEXITCODE -ne 0) { throw 'Unit tests failed. See TestResults/commerce-unit.trx.' }
    }
    $taskInfo = (& sqllocaldb info MSSQLLocalDB 2>&1 | Out-String)
    $taskPipe = [regex]::Match($taskInfo, 'np:\\\\\.\\pipe\\LOCALDB#[A-Za-z0-9]+\\tsql\\query').Value
    if (-not $taskPipe) {
        & sqllocaldb start MSSQLLocalDB | Out-Null
        $taskInfo = (& sqllocaldb info MSSQLLocalDB 2>&1 | Out-String)
        $taskPipe = [regex]::Match($taskInfo, 'np:\\\\\.\\pipe\\LOCALDB#[A-Za-z0-9]+\\tsql\\query').Value
    }
    if (-not $taskPipe) { throw 'Cannot find the running LocalDB pipe.' }
    $env:THREE_UC_SQL_SERVER = $taskPipe
    $env:THREE_UC_SQL_TESTS = '1'
    & dotnet test tests/Integration/AgriSustain.IntegrationTests/AgriSustain.IntegrationTests.csproj -m:1 -p:UseSharedCompilation=false --filter 'FullyQualifiedName~CommerceSqlDesignTests' --logger 'trx;LogFileName=commerce-sql.trx' --results-directory TestResults
    $taskSqlExit = $LASTEXITCODE
    Write-Output 'Results: TestResults/commerce-sql.trx (56 report-mapped SQL/API cases).'
    if ($taskSqlExit -ne 0) { throw 'SQL integration tests failed; inspect the TRX before updating report status.' }
} finally {
    Remove-Item Env:THREE_UC_SQL_TESTS -ErrorAction SilentlyContinue
    Remove-Item Env:THREE_UC_SQL_SERVER -ErrorAction SilentlyContinue
    Pop-Location
}
