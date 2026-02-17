param(
  [string]$Path = ".\data\app.db"
)

if (Test-Path $Path) {
  Remove-Item -Force $Path
  Write-Host "Removed $Path"
} else {
  Write-Host "Database file not found: $Path"
}