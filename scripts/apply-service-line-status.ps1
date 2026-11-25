$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$cs = (Get-Content -Raw (Join-Path $PSScriptRoot 'ConnectionStr.txt')).Trim()
$conn = New-Object System.Data.SqlClient.SqlConnection $cs
$conn.Open()

function Invoke-Table([string]$sql) {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    $cmd.CommandTimeout = 180
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter $cmd
    $table = New-Object System.Data.DataTable
    [void]$adapter.Fill($table)
    return ,$table
}

$check = Invoke-Table "SELECT CONVERT(nvarchar(128), @@SERVERNAME) AS ServerName, DB_NAME() AS DbName"
$server = [string]$check.Rows[0]['ServerName']
$database = [string]$check.Rows[0]['DbName']
if ($server -ne 'NAGSERVER' -or $database -ne 'saloonDB') {
    $conn.Close()
    throw 'Server or database check failed. Migration was not applied.'
}

$report = Join-Path $PSScriptRoot 'service-line-migration-result.txt'
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('target=NAGSERVER/saloonDB')
$beforeServices = [int](Invoke-Table "SELECT COUNT(*) AS C FROM AppointmentServices").Rows[0]['C']
$beforeAppointments = [int](Invoke-Table "SELECT COUNT(*) AS C FROM Appointments").Rows[0]['C']
$lines.Add("beforeAppointments=$beforeAppointments")
$lines.Add("beforeServices=$beforeServices")

$column = Invoke-Table "SELECT COUNT(*) AS C FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = N'AppointmentServices' AND COLUMN_NAME = N'LineStatus'"
if ([int]$column.Rows[0]['C'] -ne 0) {
    $lines.Add('blocked=LineStatus column already exists')
    $lines | Set-Content $report
    $conn.Close()
    throw 'LineStatus already exists. Migration was not applied.'
}

$known = Invoke-Table @"
SELECT TOP 1 m.physical_device_name
FROM msdb.dbo.backupset b
JOIN msdb.dbo.backupmediafamily m ON m.media_set_id = b.media_set_id
WHERE b.database_name = N'saloonDB' AND m.physical_device_name LIKE N'G:\DB_Backup\%'
ORDER BY b.backup_finish_date DESC
"@
if ($known.Rows.Count -eq 0) {
    $lines.Add('blocked=no verified backup directory for saloonDB')
    $lines | Set-Content $report
    $conn.Close()
    throw 'No existing saloonDB backup location was found. Migration was not applied.'
}

$backupPath = 'G:\DB_Backup\saloonDB-before-AddServiceLineStatus.bak'
$escaped = $backupPath.Replace("'", "''")
$backup = $conn.CreateCommand()
$backup.CommandTimeout = 600
$backup.CommandText = "BACKUP DATABASE [saloonDB] TO DISK = N'$escaped' WITH COPY_ONLY, INIT, CHECKSUM"
try {
    [void]$backup.ExecuteNonQuery()
} catch {
    $lines.Add('blocked=database backup failed')
    $lines.Add($_.Exception.Message)
    $lines | Set-Content $report
    $conn.Close()
    throw
}

$verify = $conn.CreateCommand()
$verify.CommandTimeout = 600
$verify.CommandText = "RESTORE VERIFYONLY FROM DISK = N'$escaped' WITH CHECKSUM"
try {
    [void]$verify.ExecuteNonQuery()
} catch {
    $lines.Add('blocked=backup verification failed')
    $lines.Add($_.Exception.Message)
    $lines | Set-Content $report
    $conn.Close()
    throw
}
$conn.Close()

$lines.Add("backup=$backupPath")
$lines.Add('backupVerified=true')
$lines | Set-Content $report

Push-Location $root
try {
    dotnet ef database update --project src/SalonBooking.Persistence/SalonBooking.Persistence.csproj --startup-project src/SalonBooking.Persistence/SalonBooking.Persistence.csproj
    if ($LASTEXITCODE -ne 0) { throw 'EF database update failed. Review the migration before trying again.' }
} finally {
    Pop-Location
}

$conn.Open()
$afterServices = [int](Invoke-Table "SELECT COUNT(*) AS C FROM AppointmentServices").Rows[0]['C']
$afterAppointments = [int](Invoke-Table "SELECT COUNT(*) AS C FROM Appointments").Rows[0]['C']
$history = [int](Invoke-Table "SELECT COUNT(*) AS C FROM AppointmentServiceStatusHistories WHERE Action = N'Backfill'").Rows[0]['C']
$statuses = Invoke-Table "SELECT LineStatus, COUNT(*) AS C FROM AppointmentServices GROUP BY LineStatus ORDER BY LineStatus"
$supervisor = [int](Invoke-Table @"
SELECT COUNT(*) AS C
FROM Roles r
JOIN RolePermissions rp ON rp.RoleId = r.RoleId
JOIN Permissions p ON p.PermissionId = rp.PermissionId
WHERE r.RoleCode = N'SUPERVISOR' AND r.IsDeleted = 0 AND p.PermissionCode = N'appointments.lines.progress' AND p.IsDeleted = 0
"@).Rows[0]['C']
$applied = Invoke-Table "SELECT TOP 1 MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC"
$lines.Add("afterAppointments=$afterAppointments")
$lines.Add("afterServices=$afterServices")
$lines.Add("backfillHistory=$history")
$lines.Add("supervisorLinks=$supervisor")
$lines.Add('latestMigration=' + $applied.Rows[0]['MigrationId'])
foreach ($row in $statuses.Rows) { $lines.Add("status $($row.LineStatus)=$($row.C)") }
if ($afterAppointments -ne $beforeAppointments -or $afterServices -ne $beforeServices -or $history -ne $afterServices) {
    $lines.Add('verification=failed')
    $lines | Set-Content $report
    $conn.Close()
    throw 'Row counts changed or backfill did not cover every service line.'
}
$lines.Add('verification=passed')
$lines | Set-Content $report
$conn.Close()
Write-Output 'service-line-migration-finished'
