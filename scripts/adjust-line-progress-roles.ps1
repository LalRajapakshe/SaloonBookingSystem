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
    throw 'Server or database check failed. No permission change was applied.'
}

$sql = @'
SET XACT_ABORT ON;
BEGIN TRAN;

DELETE existing
FROM RolePermissions existing
INNER JOIN Roles r ON r.RoleId = existing.RoleId
INNER JOIN Permissions p ON p.PermissionId = existing.PermissionId
WHERE r.RoleCode = N'STYLIST'
  AND r.IsDeleted = 0
  AND p.PermissionCode = N'appointments.own.progress'
  AND p.IsDeleted = 0;

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId
FROM Roles r
INNER JOIN Permissions p
    ON p.PermissionCode = N'appointments.lines.progress' AND p.IsDeleted = 0 AND p.IsActive = 1
WHERE r.RoleCode = N'RECEPTIONIST' AND r.IsDeleted = 0 AND r.IsActive = 1
  AND NOT EXISTS (
      SELECT 1 FROM RolePermissions existing
      WHERE existing.RoleId = r.RoleId AND existing.PermissionId = p.PermissionId);

IF EXISTS (
    SELECT 1
    FROM Roles r
    INNER JOIN RolePermissions rp ON rp.RoleId = r.RoleId
    INNER JOIN Permissions p ON p.PermissionId = rp.PermissionId
    WHERE r.RoleCode = N'STYLIST' AND r.IsDeleted = 0
      AND p.PermissionCode = N'appointments.own.progress' AND p.IsDeleted = 0)
    THROW 51000, 'Stylist still has appointments.own.progress.', 1;

IF EXISTS (
    SELECT 1
    FROM Roles r
    INNER JOIN RolePermissions rp ON rp.RoleId = r.RoleId
    INNER JOIN Permissions p ON p.PermissionId = rp.PermissionId
    WHERE r.RoleCode = N'ADMIN' AND r.IsDeleted = 0
      AND p.PermissionCode IN (N'appointments.lines.progress', N'platform.manage')
      AND p.IsDeleted = 0)
    THROW 51000, 'Admin received a forbidden permission.', 1;

DECLARE @reception INT = (
    SELECT COUNT(*)
    FROM Roles r
    WHERE r.RoleCode = N'RECEPTIONIST' AND r.IsDeleted = 0 AND r.IsActive = 1
      AND NOT EXISTS (
          SELECT 1
          FROM RolePermissions rp
          INNER JOIN Permissions p ON p.PermissionId = rp.PermissionId
          WHERE rp.RoleId = r.RoleId AND p.PermissionCode = N'appointments.lines.progress' AND p.IsDeleted = 0));
IF @reception <> 0 THROW 51000, 'A receptionist role is missing appointments.lines.progress.', 1;

COMMIT;
'@

$cmd = $conn.CreateCommand()
$cmd.CommandText = $sql
$cmd.CommandTimeout = 60
[void]$cmd.ExecuteNonQuery()

$summary = Invoke-Table @"
SELECT r.TenantId, r.RoleCode, p.PermissionCode
FROM Roles r
JOIN RolePermissions rp ON rp.RoleId = r.RoleId
JOIN Permissions p ON p.PermissionId = rp.PermissionId
WHERE r.IsDeleted = 0 AND p.IsDeleted = 0
  AND r.RoleCode IN (N'STYLIST', N'RECEPTIONIST', N'SUPERVISOR', N'ADMIN')
  AND p.PermissionCode IN (
      N'appointments.own.read', N'appointments.own.progress', N'appointments.lines.progress',
      N'appointments.manage', N'appointments.read', N'schedules.read', N'platform.manage')
ORDER BY r.TenantId, r.RoleCode, p.PermissionCode
"@
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('target=NAGSERVER/saloonDB')
$lines.Add('committed')
foreach ($row in $summary.Rows) {
    $lines.Add("$($row.TenantId) $($row.RoleCode) $($row.PermissionCode)")
}
$lines | Set-Content (Join-Path $PSScriptRoot 'line-progress-role-result.txt')
$conn.Close()
Write-Output 'line-progress-roles-updated'
