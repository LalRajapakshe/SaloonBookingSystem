using Microsoft.EntityFrameworkCore;
using SalonBooking.Application.Common;
using SalonBooking.Application.Features.Qualifications;
using SalonBooking.Application.Interfaces;
using SalonBooking.Domain.Entities;
using SalonBooking.Domain.Enums;
using SalonBooking.Infrastructure.Security;
using SalonBooking.Infrastructure.Storage;
using SalonBooking.Persistence.Context;

namespace SalonBooking.Infrastructure.Services;

public sealed class QualificationAdminService : IQualificationAdminService
{
    private readonly SalonBookingDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly CatalogueFileStore _files;

    public QualificationAdminService(
        SalonBookingDbContext context,
        ICurrentUserService currentUser,
        CatalogueFileStore files)
    {
        _context = context;
        _currentUser = currentUser;
        _files = files;
    }

    public async Task<IReadOnlyList<QualificationResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        _ = _currentUser.RequireTenantId();
        var rows = await _context.Qualifications
            .AsNoTracking()
            .OrderBy(item => item.QualificationName)
            .ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<QualificationResponse> CreateAsync(SaveQualificationRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.RequireTenantId();
        var name = RequiredName(request.QualificationName);
        var taken = await _context.Qualifications.AnyAsync(
            item => item.TenantId == tenantId && item.QualificationName == name,
            cancellationToken);
        if (taken)
        {
            throw new IdentityException("That qualification already exists.");
        }

        var qualification = new Qualification
        {
            TenantId = tenantId,
            QualificationName = name,
            QualificationLevel = Clean(request.QualificationLevel, 80),
            Description = Clean(request.Description, 500)
        };
        _context.Qualifications.Add(qualification);
        await _context.SaveChangesAsync(cancellationToken);
        if (!request.IsActive)
        {
            qualification.IsActive = false;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Map(qualification);
    }

    public async Task<QualificationResponse> UpdateAsync(
        long qualificationId,
        SaveQualificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.RequireTenantId();
        var qualification = await _context.Qualifications
            .FirstOrDefaultAsync(item => item.QualificationId == qualificationId && item.TenantId == tenantId, cancellationToken);
        if (qualification == null)
        {
            throw new KeyNotFoundException();
        }

        var name = RequiredName(request.QualificationName);
        var taken = await _context.Qualifications.AnyAsync(
            item => item.TenantId == tenantId && item.QualificationName == name && item.QualificationId != qualificationId,
            cancellationToken);
        if (taken)
        {
            throw new IdentityException("That qualification already exists.");
        }

        qualification.QualificationName = name;
        qualification.QualificationLevel = Clean(request.QualificationLevel, 80);
        qualification.Description = Clean(request.Description, 500);
        qualification.IsActive = request.IsActive;
        await _context.SaveChangesAsync(cancellationToken);
        return Map(qualification);
    }

    public async Task<IReadOnlyList<EmployeeQualificationResponse>> ListForEmployeeAsync(
        long employeeId,
        CancellationToken cancellationToken = default)
    {
        await RequireEmployeeAsync(employeeId, cancellationToken);
        var rows = await _context.EmployeeQualifications
            .AsNoTracking()
            .Include(item => item.Qualification)
            .Where(item => item.EmployeeId == employeeId)
            .OrderBy(item => item.Qualification.QualificationName)
            .ToListAsync(cancellationToken);
        return rows.Select(MapLink).ToList();
    }

    public async Task<EmployeeQualificationResponse> SaveForEmployeeAsync(
        SaveEmployeeQualificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var employee = await RequireEmployeeAsync(request.EmployeeId, cancellationToken);
        var qualification = await RequireQualificationAsync(request.QualificationId, employee.TenantId, cancellationToken);
        var existing = await _context.EmployeeQualifications
            .FirstOrDefaultAsync(
                item => item.EmployeeId == employee.EmployeeId && item.QualificationId == qualification.QualificationId,
                cancellationToken);
        if (existing != null)
        {
            return await UpdateForEmployeeAsync(existing.EmployeeQualificationId, request, cancellationToken);
        }

        var link = new EmployeeQualification
        {
            TenantId = employee.TenantId,
            EmployeeId = employee.EmployeeId,
            QualificationId = qualification.QualificationId,
            AwardingInstitution = Clean(request.AwardingInstitution, 200),
            CompletionYear = Year(request.CompletionYear),
            VerificationStatus = ParseStatus(request.VerificationStatus),
            IsCustomerVisible = request.IsCustomerVisible
        };
        _context.EmployeeQualifications.Add(link);
        await _context.SaveChangesAsync(cancellationToken);
        link.Qualification = qualification;
        return MapLink(link);
    }

    public async Task<EmployeeQualificationResponse> UpdateForEmployeeAsync(
        long employeeQualificationId,
        SaveEmployeeQualificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var link = await RequireLinkAsync(employeeQualificationId, cancellationToken);
        link.AwardingInstitution = Clean(request.AwardingInstitution, 200);
        link.CompletionYear = Year(request.CompletionYear);
        link.VerificationStatus = ParseStatus(request.VerificationStatus);
        link.IsCustomerVisible = request.IsCustomerVisible;
        await _context.SaveChangesAsync(cancellationToken);
        return MapLink(link);
    }

    public async Task DeleteForEmployeeAsync(long employeeQualificationId, CancellationToken cancellationToken = default)
    {
        var link = await RequireLinkAsync(employeeQualificationId, cancellationToken);
        var certificate = link.CertificatePath;
        link.IsActive = false;
        link.IsDeleted = true;
        link.CertificatePath = null;
        await _context.SaveChangesAsync(cancellationToken);
        _files.Delete(CatalogueImageKind.Certificates, certificate);
    }

    public async Task<StoredFileContent?> OpenCertificateAsync(long employeeQualificationId, CancellationToken cancellationToken = default)
    {
        var link = await RequireLinkAsync(employeeQualificationId, cancellationToken);
        return _files.Open(CatalogueImageKind.Certificates, link.CertificatePath);
    }

    public async Task SaveCertificateAsync(long employeeQualificationId, byte[] content, CancellationToken cancellationToken = default)
    {
        var link = await RequireLinkAsync(employeeQualificationId, cancellationToken);
        var previous = link.CertificatePath;
        var stored = await _files.SaveAsync(CatalogueImageKind.Certificates, content, cancellationToken);
        link.CertificatePath = stored;
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            _files.Delete(CatalogueImageKind.Certificates, stored);
            throw;
        }

        _files.Delete(CatalogueImageKind.Certificates, previous);
    }

    public async Task RemoveCertificateAsync(long employeeQualificationId, CancellationToken cancellationToken = default)
    {
        var link = await RequireLinkAsync(employeeQualificationId, cancellationToken);
        var previous = link.CertificatePath;
        link.CertificatePath = null;
        await _context.SaveChangesAsync(cancellationToken);
        _files.Delete(CatalogueImageKind.Certificates, previous);
    }

    private async Task<Employee> RequireEmployeeAsync(long employeeId, CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.RequireTenantId();
        return await _context.Employees.RequireOwnedAsync(
            _currentUser,
            item => item.EmployeeId == employeeId && item.TenantId == tenantId && !item.IsDeleted,
            item => item.BranchId,
            cancellationToken);
    }

    private async Task<Qualification> RequireQualificationAsync(long qualificationId, long tenantId, CancellationToken cancellationToken)
    {
        var qualification = await _context.Qualifications
            .FirstOrDefaultAsync(
                item => item.QualificationId == qualificationId && item.TenantId == tenantId && item.IsActive,
                cancellationToken);
        if (qualification == null)
        {
            throw new KeyNotFoundException();
        }

        return qualification;
    }

    private async Task<EmployeeQualification> RequireLinkAsync(long employeeQualificationId, CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.RequireTenantId();
        var link = await _context.EmployeeQualifications
            .Include(item => item.Qualification)
            .Include(item => item.Employee)
            .FirstOrDefaultAsync(
                item => item.EmployeeQualificationId == employeeQualificationId && item.TenantId == tenantId,
                cancellationToken);
        if (link == null)
        {
            throw new KeyNotFoundException();
        }

        _currentUser.EnsureCanAccessBranch(link.Employee.BranchId);
        return link;
    }

    private static QualificationResponse Map(Qualification qualification)
    {
        return new QualificationResponse
        {
            QualificationId = qualification.QualificationId,
            QualificationName = qualification.QualificationName,
            QualificationLevel = qualification.QualificationLevel,
            Description = qualification.Description,
            IsActive = qualification.IsActive
        };
    }

    private static EmployeeQualificationResponse MapLink(EmployeeQualification link)
    {
        return new EmployeeQualificationResponse
        {
            EmployeeQualificationId = link.EmployeeQualificationId,
            EmployeeId = link.EmployeeId,
            QualificationId = link.QualificationId,
            QualificationName = link.Qualification.QualificationName,
            QualificationLevel = link.Qualification.QualificationLevel,
            AwardingInstitution = link.AwardingInstitution,
            CompletionYear = link.CompletionYear,
            VerificationStatus = link.VerificationStatus.ToString(),
            IsCustomerVisible = link.IsCustomerVisible,
            HasCertificate = !string.IsNullOrWhiteSpace(link.CertificatePath)
        };
    }

    private static string RequiredName(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length is < 1 or > 150)
        {
            throw new IdentityException("Qualification name is required and must be 150 characters or fewer.");
        }

        return trimmed;
    }

    private static string? Clean(string? value, int max)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > max)
        {
            throw new IdentityException($"The value must be {max} characters or fewer.");
        }

        return trimmed;
    }

    private static int? Year(int? year)
    {
        if (year is null)
        {
            return null;
        }

        if (year < 1950 || year > DateTime.UtcNow.Year + 1)
        {
            throw new IdentityException("Completion year is not valid.");
        }

        return year;
    }

    private static QualificationVerificationStatus ParseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return QualificationVerificationStatus.Pending;
        }

        if (!Enum.TryParse<QualificationVerificationStatus>(value, ignoreCase: true, out var status) ||
            !Enum.IsDefined(status))
        {
            throw new IdentityException("Verification status must be Pending, Verified, or Rejected.");
        }

        return status;
    }
}
