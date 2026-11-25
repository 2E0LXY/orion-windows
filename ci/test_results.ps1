$trx = Get-ChildItem UnitTest/TestResults/*.trx |
  Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $trx) { Write-Warning "No TRX file found"; exit 0 }
[xml]$xml = Get-Content $trx
$c = $xml.TestRun.ResultSummary.Counters
$total = [int]$c.total; $passed = [int]$c.passed
$failed = [int]$c.failed; $skipped = $total - [int]$c.executed
$outcome = $xml.TestRun.ResultSummary.outcome
$icon = if ($outcome -eq 'Completed' -and $failed -eq 0) { '✅' } else { '❌' }
$lines = @(
  "## $icon Unit Tests"
  ""
  "| Total | Passed | Failed | Skipped |"
  "|-------|--------|--------|---------|"
  "| $total | $passed | $failed | $skipped |"
)
if ($failed -gt 0) {
  $lines += @("", "### Failed Tests", "")
  $xml.TestRun.Results.UnitTestResult |
    Where-Object outcome -eq 'Failed' | ForEach-Object {
      $lines += "- **$($_.testName)**: ``$($_.Output.ErrorInfo.Message)``"
    }
}
$lines -join "`n"
