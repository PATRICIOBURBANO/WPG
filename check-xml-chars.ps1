$filePath = "C:\DOCUMENTS\CONTABILIDADES\CHIAREZZA\2026\01ENERO\ATS2026011792468264001.xml"
$content = Get-Content $filePath -Raw

# Check for special XML characters
if ($content -match '[&<>\"'']') {
    Write-Host "Found special XML chars that need escaping"
} else {
    Write-Host "No unescaped special chars found"
}

# Check for any non-ASCII characters
$nonAscii = $content | Select-String -Pattern "[^\x00-\x7F]" -AllMatches
if ($nonAscii) {
    Write-Host "Found non-ASCII characters"
    $nonAscii | ForEach-Object { Write-Host "Line $($_.LineNumber): $($_.Matches.Value)" }
} else {
    Write-Host "All characters are ASCII"
}

# Show a sample of company names
Write-Host "`nSample denoProv values:"
$content -split "`n" | Where-Object { $_ -match "<denoProv>" } | Select-Object -First 5
