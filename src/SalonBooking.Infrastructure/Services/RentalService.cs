using System.Data;
using Microsoft.EntityFrameworkCore;
using SalonBooking.Application.Common;
using SalonBooking.Application.Features.Rentals;
using SalonBooking.Application.Interfaces;
using SalonBooking.Domain.Billing;
using SalonBooking.Domain.Entities;
using SalonBooking.Domain.Enums;
using SalonBooking.Infrastructure.Security;
using SalonBooking.Persistence.Context;

namespace SalonBooking.Infrastructure.Services;

public sealed class RentalService : IRentalService
{
    private readonly SalonBookingDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public RentalService(SalonBookingDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<RentalAssetResponse>> ListAssetsAsync(bool includeInactive, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.RequireTenantId();
        var assets = _context.RentalAssets.AsNoTracking().Where(item => item.TenantId == tenantId);
        if (!includeInactive)
        {
            assets = assets.Where(item => item.IsActive);
        }

        var rows = await assets.OrderBy(item => item.AssetName).ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var busy = await BusyAssetIdsAsync(rows.Select(item => item.RentalAssetId).ToArray(), now, cancellationToken);
        return rows.Select(item => MapAsset(item, busy.Contains(item.RentalAssetId))).ToList();
    }

    public async Task<RentalAssetResponse> SaveAssetAsync(SaveRentalAssetRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.RequireTenantId();
        var code = Required(request.AssetCode, 40, "Enter an asset code of up to 40 characters.");
        var name = Required(request.AssetName, 100, "Enter an asset name of up to 100 characters.");
        var category = Required(request.Category, 80, "Enter a category of up to 80 characters.");
        if (request.CleaningBufferMinutes < 0 || request.CleaningBufferMinutes > 10080)
        {
            throw new BillingException("The cleaning buffer must be between 0 and 10080 minutes.");
        }

        var charge = Money.Round(request.ChargePerUse);
        if (charge < 0)
        {
            throw new BillingException("The rental charge cannot be negative.");
        }

        if (request.RentalAssetId is not > 0)
        {
            if (!request.IsActive)
            {
                throw new BillingException("Create the asset as active, then deactivate it.");
            }

            var branchId = _currentUser.ResolveBranchId(request.BranchId);
            await EnsureBranchAsync(tenantId, branchId, cancellationToken);
            if (await CodeTakenAsync(tenantId, branchId, code, null, cancellationToken))
            {
                throw new BillingException("That asset code is already used in this branch.");
            }

            var created = new RentalAsset
            {
                TenantId = tenantId,
                BranchId = branchId,
                AssetCode = code,
                AssetName = name,
                Description = Clean(request.Description),
                Category = category,
                ChargePerUse = charge,
                CleaningBufferMinutes = request.CleaningBufferMinutes,
                IsUnderMaintenance = request.IsUnderMaintenance,
                ConditionNotes = Clean(request.ConditionNotes),
                PhotoPath = Clean(request.PhotoPath)
            };
            _context.RentalAssets.Add(created);
            await _context.SaveChangesAsync(cancellationToken);
            return MapAsset(created, false);
        }

        var asset = await _context.RentalAssets.FirstOrDefaultAsync(item => item.RentalAssetId == request.RentalAssetId, cancellationToken)
            ?? throw new KeyNotFoundException();
        _currentUser.EnsureCanAccessBranch(asset.BranchId);
        if (await CodeTakenAsync(asset.TenantId, asset.BranchId, code, asset.RentalAssetId, cancellationToken))
        {
            throw new BillingException("That asset code is already used in this branch.");
        }

        asset.AssetCode = code;
        asset.AssetName = name;
        asset.Description = Clean(request.Description);
        asset.Category = category;
        asset.ChargePerUse = charge;
        asset.CleaningBufferMinutes = request.CleaningBufferMinutes;
        asset.IsUnderMaintenance = request.IsUnderMaintenance;
        asset.ConditionNotes = Clean(request.ConditionNotes);
        asset.PhotoPath = Clean(request.PhotoPath);
        asset.IsActive = request.IsActive;
        await _context.SaveChangesAsync(cancellationToken);
        return MapAsset(asset, false);
    }

    public async Task<IReadOnlyList<RentalReservationResponse>> ListReservationsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _context.RentalReservations.AsNoTracking()
            .Include(item => item.Asset)
            .Include(item => item.Customer)
            .OrderByDescending(item => item.PlannedIssueAt)
            .Take(200)
            .ToListAsync(cancellationToken);
        var claimed = await _context.ActiveRentalCharges.AsNoTracking()
            .Select(item => item.RentalReservationId)
            .ToListAsync(cancellationToken);
        return rows.Select(item => MapReservation(item, !claimed.Contains(item.RentalReservationId))).ToList();
    }

    public async Task<RentalReservationResponse> CreateReservationAsync(CreateRentalReservationRequest request, CancellationToken cancellationToken = default)
    {
        if (request.PlannedReturnAt <= request.PlannedIssueAt)
        {
            throw new BillingException("The return time must be after the issue time.");
        }

        var tenantId = _currentUser.RequireTenantId();
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var asset = await _context.RentalAssets.FirstOrDefaultAsync(item => item.RentalAssetId == request.RentalAssetId, cancellationToken)
            ?? throw new KeyNotFoundException();
        _currentUser.EnsureCanAccessBranch(asset.BranchId);
        if (!asset.IsActive || asset.IsUnderMaintenance)
        {
            throw new BillingException("That asset is not available to reserve.");
        }

        var customer = await _context.Customers.IgnoreQueryFilters().FirstOrDefaultAsync(
            item => item.CustomerId == request.CustomerId && item.TenantId == tenantId && item.BranchId == asset.BranchId && !item.IsDeleted && item.IsActive,
            cancellationToken) ?? throw new BillingException("Choose an active customer in this branch.");
        if (request.AppointmentId is > 0)
        {
            var appointment = await _context.Appointments.IgnoreQueryFilters().FirstOrDefaultAsync(
                item => item.AppointmentId == request.AppointmentId && item.TenantId == tenantId && item.BranchId == asset.BranchId && item.CustomerId == customer.CustomerId && !item.IsDeleted,
                cancellationToken) ?? throw new BillingException("That appointment is not for this customer and branch.");
            _ = appointment;
        }

        var conflict = await ConflictsAsync(asset, request.PlannedIssueAt, request.PlannedReturnAt, null, cancellationToken);
        if (conflict)
        {
            throw new BillingConflictException("That asset is already reserved for this time, including its cleaning buffer.");
        }

        var reservation = new RentalReservation
        {
            TenantId = tenantId,
            BranchId = asset.BranchId,
            CustomerId = customer.CustomerId,
            RentalAssetId = asset.RentalAssetId,
            AppointmentId = request.AppointmentId is > 0 ? request.AppointmentId : null,
            Status = RentalReservationStatus.Reserved,
            PlannedIssueAt = request.PlannedIssueAt,
            PlannedReturnAt = request.PlannedReturnAt,
            ChargePerUse = asset.ChargePerUse,
            Notes = Clean(request.Notes)
        };
        _context.RentalReservations.Add(reservation);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (IsDeadlock(exception))
        {
            throw new BillingConflictException("That asset is already reserved for this time, including its cleaning buffer.");
        }
        reservation.Asset = asset;
        reservation.Customer = customer;
        return MapReservation(reservation, false);
    }

    public async Task<RentalReservationResponse> CancelReservationAsync(long id, CancellationToken cancellationToken = default)
    {
        var reservation = await RequireAsync(id, cancellationToken);
        if (reservation.Status != RentalReservationStatus.Reserved)
        {
            throw new BillingException("Only a reservation that has not been issued can be cancelled.");
        }

        reservation.Status = RentalReservationStatus.Cancelled;
        await _context.SaveChangesAsync(cancellationToken);
        return MapReservation(reservation, false);
    }

    public async Task<RentalReservationResponse> IssueAsync(long id, RentalMovementRequest request, CancellationToken cancellationToken = default)
    {
        var reservation = await RequireAsync(id, cancellationToken);
        if (reservation.Status != RentalReservationStatus.Reserved)
        {
            throw new BillingException("Only a reserved asset can be issued.");
        }

        if (reservation.Asset.IsUnderMaintenance)
        {
            throw new BillingException("That asset is under maintenance.");
        }

        reservation.Status = RentalReservationStatus.Issued;
        reservation.ActualIssuedAt = DateTime.UtcNow;
        reservation.IssuedByUserId = _currentUser.UserId;
        reservation.IssueCondition = Required(request.Condition, 200, "Record the condition at issue.");
        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            reservation.Notes = Clean(request.Notes);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return MapReservation(reservation, false);
    }

    public async Task<RentalReservationResponse> ReturnAsync(long id, RentalMovementRequest request, CancellationToken cancellationToken = default)
    {
        var reservation = await RequireAsync(id, cancellationToken);
        if (reservation.Status != RentalReservationStatus.Issued)
        {
            throw new BillingException("Only an issued asset can be returned.");
        }

        var condition = Required(request.Condition, 200, "Record the condition at return.");
        reservation.Status = RentalReservationStatus.Returned;
        reservation.ActualReturnedAt = DateTime.UtcNow;
        reservation.ReturnedByUserId = _currentUser.UserId;
        reservation.ReturnCondition = condition;
        reservation.ConditionDiscrepancy = !string.Equals(reservation.IssueCondition, condition, StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            reservation.Notes = Clean(request.Notes);
        }

        await _context.SaveChangesAsync(cancellationToken);
        var claimed = await _context.ActiveRentalCharges.AnyAsync(item => item.RentalReservationId == reservation.RentalReservationId, cancellationToken);
        return MapReservation(reservation, !claimed);
    }

    private static bool IsDeadlock(Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current.Message.Contains("deadlock", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<RentalReservation> RequireAsync(long id, CancellationToken cancellationToken)
    {
        var reservation = await _context.RentalReservations.Include(item => item.Asset).Include(item => item.Customer)
            .FirstOrDefaultAsync(item => item.RentalReservationId == id, cancellationToken) ?? throw new KeyNotFoundException();
        _currentUser.EnsureCanAccessBranch(reservation.BranchId);
        return reservation;
    }

    private async Task<bool> ConflictsAsync(RentalAsset asset, DateTime start, DateTime end, long? ignoreId, CancellationToken cancellationToken)
    {
        var rows = await _context.RentalReservations
            .Where(item => item.RentalAssetId == asset.RentalAssetId && item.Status != RentalReservationStatus.Cancelled)
            .ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            if (ignoreId == row.RentalReservationId)
            {
                continue;
            }

            var otherStart = row.ActualIssuedAt ?? row.PlannedIssueAt;
            var otherEnd = (row.ActualReturnedAt ?? row.PlannedReturnAt).AddMinutes(asset.CleaningBufferMinutes);
            if (start < otherEnd && otherStart < end.AddMinutes(asset.CleaningBufferMinutes))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<HashSet<long>> BusyAssetIdsAsync(IReadOnlyCollection<long> ids, DateTime now, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var rows = await _context.RentalReservations.AsNoTracking()
            .Include(item => item.Asset)
            .Where(item => ids.Contains(item.RentalAssetId) && item.Status != RentalReservationStatus.Cancelled)
            .ToListAsync(cancellationToken);
        var busy = new HashSet<long>();
        foreach (var row in rows)
        {
            var start = row.ActualIssuedAt ?? row.PlannedIssueAt;
            var end = (row.ActualReturnedAt ?? row.PlannedReturnAt).AddMinutes(row.Asset.CleaningBufferMinutes);
            if (start <= now && now < end)
            {
                busy.Add(row.RentalAssetId);
            }
        }

        return busy;
    }

    private async Task EnsureBranchAsync(long tenantId, long branchId, CancellationToken cancellationToken)
    {
        var exists = await _context.Branches.IgnoreQueryFilters().AnyAsync(
            item => item.BranchId == branchId && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
        if (!exists)
        {
            throw new KeyNotFoundException();
        }
    }

    private Task<bool> CodeTakenAsync(long tenantId, long branchId, string code, long? ignoreId, CancellationToken cancellationToken) =>
        _context.RentalAssets.IgnoreQueryFilters().AnyAsync(
            item => item.TenantId == tenantId && item.BranchId == branchId && item.AssetCode == code && !item.IsDeleted && item.RentalAssetId != ignoreId,
            cancellationToken);

    private static RentalAssetResponse MapAsset(RentalAsset asset, bool busy) => new()
    {
        RentalAssetId = asset.RentalAssetId,
        BranchId = asset.BranchId,
        AssetCode = asset.AssetCode,
        AssetName = asset.AssetName,
        Description = asset.Description,
        Category = asset.Category,
        ChargePerUse = asset.ChargePerUse,
        CleaningBufferMinutes = asset.CleaningBufferMinutes,
        IsUnderMaintenance = asset.IsUnderMaintenance,
        IsActive = asset.IsActive,
        ConditionNotes = asset.ConditionNotes,
        PhotoPath = asset.PhotoPath,
        Availability = !asset.IsActive ? "Inactive" : asset.IsUnderMaintenance ? "Maintenance" : busy ? "Out" : "Available"
    };

    private static RentalReservationResponse MapReservation(RentalReservation item, bool billable) => new()
    {
        RentalReservationId = item.RentalReservationId,
        BranchId = item.BranchId,
        CustomerId = item.CustomerId,
        CustomerName = item.Customer == null ? null : $"{item.Customer.FirstName} {item.Customer.LastName}".Trim(),
        RentalAssetId = item.RentalAssetId,
        AssetName = item.Asset?.AssetName ?? string.Empty,
        AssetCode = item.Asset?.AssetCode ?? string.Empty,
        AppointmentId = item.AppointmentId,
        Status = item.Status.ToString(),
        IsOverdue = item.Status == RentalReservationStatus.Issued && item.PlannedReturnAt < DateTime.UtcNow,
        PlannedIssueAt = item.PlannedIssueAt,
        PlannedReturnAt = item.PlannedReturnAt,
        ActualIssuedAt = item.ActualIssuedAt,
        ActualReturnedAt = item.ActualReturnedAt,
        IssueCondition = item.IssueCondition,
        ReturnCondition = item.ReturnCondition,
        ConditionDiscrepancy = item.ConditionDiscrepancy,
        ChargePerUse = item.ChargePerUse,
        Notes = item.Notes,
        IsBillable = billable && item.Status == RentalReservationStatus.Returned
    };

    private static string Required(string? value, int max, string message)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length == 0 || text.Length > max)
        {
            throw new BillingException(message);
        }

        return text;
    }

    private static string? Clean(string? value)
    {
        var text = value?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }
}
