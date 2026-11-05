using SalonBooking.Application.Common;
using SalonBooking.Application.Features.Qualifications;

namespace SalonBooking.Application.Interfaces;

public interface IQualificationAdminService
{
    Task<IReadOnlyList<QualificationResponse>> ListAsync(CancellationToken cancellationToken = default);

    Task<QualificationResponse> CreateAsync(SaveQualificationRequest request, CancellationToken cancellationToken = default);

    Task<QualificationResponse> UpdateAsync(long qualificationId, SaveQualificationRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmployeeQualificationResponse>> ListForEmployeeAsync(long employeeId, CancellationToken cancellationToken = default);

    Task<EmployeeQualificationResponse> SaveForEmployeeAsync(SaveEmployeeQualificationRequest request, CancellationToken cancellationToken = default);

    Task<EmployeeQualificationResponse> UpdateForEmployeeAsync(long employeeQualificationId, SaveEmployeeQualificationRequest request, CancellationToken cancellationToken = default);

    Task DeleteForEmployeeAsync(long employeeQualificationId, CancellationToken cancellationToken = default);

    Task<StoredFileContent?> OpenCertificateAsync(long employeeQualificationId, CancellationToken cancellationToken = default);

    Task SaveCertificateAsync(long employeeQualificationId, byte[] content, CancellationToken cancellationToken = default);

    Task RemoveCertificateAsync(long employeeQualificationId, CancellationToken cancellationToken = default);
}
