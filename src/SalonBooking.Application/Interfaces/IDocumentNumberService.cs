namespace SalonBooking.Application.Interfaces;

public interface IDocumentNumberService
{
    /// <summary>
    /// Locks the series row, adds one, and returns the code. Call this inside <see cref="ExecuteAsync{T}"/>.
    /// </summary>
    Task<string> NextCodeAsync(long tenantId, string docType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the save in one transaction so a failed save gives the number back.
    /// </summary>
    Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default);
}
