$ErrorActionPreference = "Stop"
$filePath = "C:\DOCUMENTS\CONTABILIDADES\CHIAREZZA\2026\01ENERO\ATS2026011792468264001.xml"

try {
    $content = Get-Content $filePath -Raw -Encoding UTF8
    Write-Host "File size: $($content.Length) characters"
    Write-Host "Line count: $(($content -split '`n').Count)"
    
    # Check for BOM
    $bytes = [System.IO.File]::ReadAllBytes($filePath)
    if ($bytes.Length -gt 0 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        Write-Host "File has UTF-8 BOM"
    } else {
        Write-Host "File has NO BOM"
    }
    
    # Try to parse XML
    $doc = New-Object System.Xml.XmlDocument
    $doc.LoadXml($content)
    Write-Host "XML is VALID"
    Write-Host "Root element: $($doc.DocumentElement.LocalName)"
    Write-Host "Children count: $($doc.DocumentElement.ChildNodes.Count)"
} catch {
    Write-Host "XML ERROR: $($_.Exception.Message)"
    Write-Host "Line: $($_.Exception.LineNumber)"
    Write-Host "Position: $($_.Exception.LinePosition)"
}
