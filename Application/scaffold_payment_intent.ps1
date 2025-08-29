# From the folder where you want the feature created
# 1) Save this script as Scaffold-CreateIntentToPay.ps1
# 2) Run:  pwsh ./Scaffold-CreateIntentToPay.ps1   (or: powershell -File .\Scaffold-CreateIntentToPay.ps1)

$base = "intent_to_pay_feature"
$dirs = @(
  "Application/Payments/CreateIntentToPay",
  "Domain/Payments",
  "Infrastructure/Payments/Providers",
  "Infrastructure/Payments/Pricing",
  "Infrastructure/Payments/Inventory",
  "Infrastructure/Payments/Idempotency",
  "Observability",
  "Application/Abstractions"
)

foreach ($d in $dirs) { New-Item -ItemType Directory -Force -Path (Join-Path $base $d) | Out-Null }

function Write-File($rel, $content) {
  $path = Join-Path $base $rel
  $dir  = Split-Path $path -Parent
  if (!(Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
  $content | Set-Content -Encoding UTF8 -Path $path
}

# ---------------- README ----------------
Write-File "README.md" @'
# Create Intent To Pay Feature

This archive contains a complete, ready-to-adapt skeleton for a **Create Intent To Pay** feature using **LanguageExt v5** (`IO<Fin<T>>`), with hooks for **OpenTelemetry** tracing and **Serilog** structured logging via a custom enricher.

## Structure