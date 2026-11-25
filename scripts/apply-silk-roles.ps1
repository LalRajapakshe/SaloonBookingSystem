$ErrorActionPreference = 'Stop'
$cs = (Get-Content -Raw (Join-Path $PSScriptRoot 'ConnectionStr.txt')).Trim()
$conn = New-Object System.Data.SqlClient.SqlConnection $cs
$conn.Open()

function Invoke-Table([string]$sql) {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    $cmd.CommandTimeout = 120
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
    throw 'Server or database check failed.'
}

function RowsToList($table, [string[]]$columns) {
    $list = New-Object System.Collections.Generic.List[object]
    foreach ($row in $table.Rows) {
        $item = [ordered]@{}
        foreach ($column in $columns) { $item[$column] = $row[$column] }
        $list.Add($item)
    }
    return $list
}

$snapshot = [ordered]@{
    capturedAt = (Get-Date).ToUniversalTime().ToString('o')
    server = 'NAGSERVER'
    database = 'saloonDB'
    permissions = (RowsToList (Invoke-Table "SELECT PermissionId, PermissionCode, IsActive, IsDeleted FROM Permissions ORDER BY PermissionId") @('PermissionId','PermissionCode','IsActive','IsDeleted'))
    roles = (RowsToList (Invoke-Table "SELECT RoleId, TenantId, RoleCode, IsActive, IsDeleted FROM Roles ORDER BY RoleId") @('RoleId','TenantId','RoleCode','IsActive','IsDeleted'))
    rolePermissions = (RowsToList (Invoke-Table "SELECT RolePermissionId, RoleId, PermissionId FROM RolePermissions ORDER BY RolePermissionId") @('RolePermissionId','RoleId','PermissionId'))
    userRoles = (RowsToList (Invoke-Table "SELECT UserRoleId, UserId, RoleId FROM UserRoles ORDER BY UserRoleId") @('UserRoleId','UserId','RoleId'))
    userFlags = (RowsToList (Invoke-Table "SELECT UserId, IsPlatformAdministrator, IsActive, IsDeleted, UserCategory, CASE WHEN BranchId IS NULL THEN 0 ELSE 1 END AS HasBranch FROM Users ORDER BY UserId") @('UserId','IsPlatformAdministrator','IsActive','IsDeleted','UserCategory','HasBranch'))
}
$snapshot | ConvertTo-Json -Depth 6 | Set-Content -Path (Join-Path $PSScriptRoot 'access-control-snapshot-20261122.json') -Encoding utf8

$sql = Get-Content -Raw (Join-Path $PSScriptRoot 'SilkTenantRoles.sql')
$cmd = $conn.CreateCommand()
$cmd.CommandText = $sql
$cmd.CommandTimeout = 120
try {
    [void]$cmd.ExecuteNonQuery()
} catch {
    $conn.Close()
    $_.Exception.Message | Set-Content (Join-Path $PSScriptRoot 'access-control-apply-result.txt')
    throw
}

$summary = Invoke-Table @"
SELECT r.TenantId, r.RoleCode, r.IsActive, COUNT(rp.RolePermissionId) AS PermissionLinks,
       COUNT(DISTINCT ur.UserRoleId) AS UserLinks
FROM Roles r
LEFT JOIN RolePermissions rp ON rp.RoleId = r.RoleId
LEFT JOIN UserRoles ur ON ur.RoleId = r.RoleId
WHERE r.IsDeleted = 0
GROUP BY r.TenantId, r.RoleId, r.RoleCode, r.IsActive
ORDER BY r.TenantId, r.RoleCode
"@
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('committed')
$lines.Add('permissionCount=' + (Invoke-Table "SELECT COUNT(*) AS C FROM Permissions WHERE IsDeleted = 0").Rows[0]['C'])
$lines.Add('userRoleCount=' + (Invoke-Table "SELECT COUNT(*) AS C FROM UserRoles").Rows[0]['C'])
$lines.Add('platformAdmins=' + (Invoke-Table "SELECT COUNT(*) AS C FROM Users WHERE IsPlatformAdministrator = 1 AND IsDeleted = 0").Rows[0]['C'])
foreach ($row in $summary.Rows) {
    $lines.Add("$($row.TenantId) $($row.RoleCode) active=$($row.IsActive) links=$($row.PermissionLinks) users=$($row.UserLinks)")
}
$tadmin = Invoke-Table "SELECT r.RoleCode FROM Users u JOIN UserRoles ur ON ur.UserId = u.UserId JOIN Roles r ON r.RoleId = ur.RoleId WHERE u.Username = N'Tadmin'"
$lines.Add('tadminRoles=' + (($tadmin.Rows | ForEach-Object { $_.RoleCode }) -join ','))
$lines | Set-Content (Join-Path $PSScriptRoot 'access-control-apply-result.txt')
$conn.Close()
Write-Output 'apply-finished'
