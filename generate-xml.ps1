$r = Invoke-WebRequest -Uri 'http://localhost:5216/Reportes/Exportar' -SessionVariable 's'
$token = $r.InputFields | Where-Object {$_.Name -eq '__RequestVerificationToken'}
$body = @{
    '__RequestVerificationToken' = $token.Value
    'Anio' = '2026'
    'Mes' = '02'
}
$r2 = Invoke-WebRequest -Uri 'http://localhost:5216/Reportes/Exportar?handler=Exportar' -WebSession $s -Method POST -Body $body -ContentType 'application/x-www-form-urlencoded' -OutFile 'ATS202602.xml'
Write-Host "XML generated"
