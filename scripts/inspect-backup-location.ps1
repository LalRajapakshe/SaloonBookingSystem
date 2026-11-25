$ErrorActionPreference = 'Stop'
$cs = (Get-Content -Raw (Join-Path $PSScriptRoot 'ConnectionStr.txt')).Trim()
$conn = New-Object System.Data.SqlClient.SqlConnection $cs
$conn.Open()
function Invoke-Table([string]$sql) {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    $cmd.CommandTimeout = 60
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter $cmd
    $table = New-Object System.Data.DataTable
    [void]$adapter.Fill($table)
    return ,$table
}
$check = Invoke-Table "SELECT CONVERT(nvarchar(128), @@SERVERNAME) AS ServerName, DB_NAME() AS DbName"
if ([string]$check.Rows[0]['ServerName'] -ne 'NAGSERVER' -or [string]$check.Rows[0]['DbName'] -ne 'saloonDB') {
    $conn.Close()
    throw 'Server or database check failed.'
}
$history = Invoke-Table @"
SELECT TOP 8
    b.database_name,
    CONVERT(varchar(19), b.backup_finish_date, 120) AS Finished,
    m.physical_device_name
FROM msdb.dbo.backupset b
JOIN msdb.dbo.backupmediafamily m ON m.media_set_id = b.media_set_id
WHERE b.database_name = N'saloonDB'
ORDER BY b.backup_finish_date DESC
"@
$out = New-Object System.Collections.Generic.List[string]
$out.Add('target=NAGSERVER/saloonDB')
if ($history.Rows.Count -eq 0) { $out.Add('history=none') }
foreach ($row in $history.Rows) {
    $out.Add("$($row.Finished) $($row.physical_device_name)")
}
$out | Set-Content (Join-Path $PSScriptRoot 'backup-location-inspect.txt')
$conn.Close()
Write-Output 'backup-location-inspected'
