using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SalonBooking.Application.Documents;
using SalonBooking.Application.Interfaces;
using SalonBooking.Domain.Entities;
using SalonBooking.Persistence.Context;

namespace SalonBooking.Infrastructure.Services;

public sealed class DocumentNumberService : IDocumentNumberService
{
    private readonly SalonBookingDbContext _context;

    public DocumentNumberService(SalonBookingDbContext context)
    {
        _context = context;
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return await action();
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var result = await action();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<string> NextCodeAsync(
        long tenantId,
        string docType,
        CancellationToken cancellationToken = default)
    {
        if (_context.Database.CurrentTransaction == null)
        {
            throw new InvalidOperationException(
                "A transaction is required to issue a document number.");
        }

        await EnsureSeriesAsync(tenantId, cancellationToken);
        return await IncrementAsync(tenantId, docType, cancellationToken);
    }

    private async Task EnsureSeriesAsync(long tenantId, CancellationToken cancellationToken)
    {
        var existing = await _context.DocSeries
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted)
            .Select(item => item.DocType)
            .ToListAsync(cancellationToken);

        var missing = DocSeriesKind.All
            .Where(item => !existing.Contains(item.Kind))
            .ToList();
        if (missing.Count == 0)
        {
            return;
        }

        foreach (var series in missing)
        {
            _context.DocSeries.Add(new DocSeries
            {
                TenantId = tenantId,
                DocType = series.Kind,
                Prefix = series.Prefix,
                LastNo = await HighWaterMarkAsync(tenantId, series.Kind, cancellationToken),
                PadLength = 6
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        foreach (var entry in _context.ChangeTracker.Entries<DocSeries>().ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    private async Task<long> HighWaterMarkAsync(
        long tenantId,
        string docType,
        CancellationToken cancellationToken)
    {
        long? highest = docType switch
        {
            DocSeriesKind.Branch => await _context.Branches
                .IgnoreQueryFilters()
                .Where(item => item.TenantId == tenantId)
                .MaxAsync(item => (long?)item.BranchId, cancellationToken),
            DocSeriesKind.Category => await _context.ServiceCategories
                .IgnoreQueryFilters()
                .Where(item => item.TenantId == tenantId)
                .MaxAsync(item => (long?)item.ServiceCategoryId, cancellationToken),
            DocSeriesKind.Service => await _context.Services
                .IgnoreQueryFilters()
                .Where(item => item.TenantId == tenantId)
                .MaxAsync(item => (long?)item.ServiceId, cancellationToken),
            DocSeriesKind.Employee => await _context.Employees
                .IgnoreQueryFilters()
                .Where(item => item.TenantId == tenantId)
                .MaxAsync(item => (long?)item.EmployeeId, cancellationToken),
            DocSeriesKind.Customer => await _context.Customers
                .IgnoreQueryFilters()
                .Where(item => item.TenantId == tenantId)
                .MaxAsync(item => (long?)item.CustomerId, cancellationToken),
            _ => throw new InvalidOperationException($"Unknown document series '{docType}'.")
        };

        return highest ?? 0;
    }

    private async Task<string> IncrementAsync(
        long tenantId,
        string docType,
        CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = IncrementSql();

        var tenantParameter = command.CreateParameter();
        tenantParameter.ParameterName = "@tenantId";
        tenantParameter.Value = tenantId;
        command.Parameters.Add(tenantParameter);

        var typeParameter = command.CreateParameter();
        typeParameter.ParameterName = "@docType";
        typeParameter.Value = docType;
        command.Parameters.Add(typeParameter);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException($"Document series '{docType}' was not found.");
        }

        var prefix = reader.GetString(0);
        var lastNo = Convert.ToInt64(reader.GetValue(1));
        var padLength = Convert.ToInt32(reader.GetValue(2));
        return prefix + lastNo.ToString().PadLeft(padLength, '0');
    }

    private string IncrementSql()
    {
        var provider = _context.Database.ProviderName ?? string.Empty;
        if (provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            return """
                UPDATE Docs
                SET LastNo = LastNo + 1
                OUTPUT INSERTED.Prefix, INSERTED.LastNo, INSERTED.PadLength
                WHERE TenantId = @tenantId AND DocType = @docType AND IsDeleted = 0
                """;
        }

        if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            return """
                UPDATE Docs
                SET LastNo = LastNo + 1
                WHERE TenantId = @tenantId AND DocType = @docType AND IsDeleted = 0
                RETURNING Prefix, LastNo, PadLength
                """;
        }

        throw new NotSupportedException(
            "Document numbers support SQL Server and SQLite.");
    }
}
