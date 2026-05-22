$filePath = "C:\DOCUMENTS\CONTABILIDADES\CHIAREZZA\2026\01ENERO\ATS2026011792468264001.xml"
$content = Get-Content $filePath -Raw

# Check for ampersand that might not be properly escaped
$ampersandNotEscaped = $content -split "`n" | Where-Object { $_ -match " &[a-zA-Z]" -and $_ -notmatch "&amp;" -and $_ -notmatch "&lt;" -and $_ -notmatch "&gt;" }
if ($ampersandNotEscaped) {
    Write-Host "Found unescaped ampersands:"
    $ampersandNotEscaped | ForEach-Object { Write-Host "  $_" }
} else {
    Write-Host "No unescaped ampersands found"
}

# Check for &amp; pattern
$escapedAmp = ($content -split "`n" | Where-Object { $_ -match "&amp;" }).Count
Write-Host "Escaped ampersands: $escapedAmp"

# Show the actual characters
Write-Host "`nCharacters that might need escaping:"
$chars = [char[]]($content.ToCharArray() | Where-Object { $_ -eq '&' -or $_ -eq '<' -or $_ -eq '>' }) | Select-Object -Unique
$chars | ForEach-Object { Write-Host "  '$_' (ASCII $($_.Value))" }
