using Microsoft.EntityFrameworkCore;
using SalonBooking.Application.Common;
using SalonBooking.Application.Features.Feedback;
using SalonBooking.Application.Interfaces;
using SalonBooking.Domain.Entities;
using SalonBooking.Domain.Enums;
using SalonBooking.Infrastructure.Security;
using SalonBooking.Persistence.Context;

namespace SalonBooking.Infrastructure.Services;

public sealed class FeedbackService : IFeedbackService
{
    private readonly SalonBookingDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public FeedbackService(SalonBookingDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<PendingFeedbackResponse>> ListPendingForCurrentCustomerAsync(CancellationToken cancellationToken = default)
    {
        var customer = await RequireCustomerAsync(cancellationToken);
        var lines = await EligibleLinesAsync(customer, cancellationToken);
        return lines.Select(MapPending).ToList();
    }

    public async Task<ServiceFeedbackResponse> SubmitAsync(SubmitFeedbackRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Rating is < 1 or > 5)
        {
            throw new BillingException("Choose a rating from 1 to 5.");
        }

        var comment = request.Comment?.Trim();
        if (comment?.Length > 1000)
        {
            throw new BillingException("The comment is too long.");
        }

        var customer = await RequireCustomerAsync(cancellationToken);
        var line = await _context.AppointmentServices.IgnoreQueryFilters()
            .Include(item => item.Appointment)
            .Include(item => item.Service)
            .Include(item => item.Employee)
            .FirstOrDefaultAsync(item => item.AppointmentServiceId == request.AppointmentServiceId && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException();
        if (line.TenantId != customer.TenantId || line.Appointment.CustomerId != customer.CustomerId)
        {
            throw new KeyNotFoundException();
        }

        if (!IsEligible(line))
        {
            throw new BillingException("Feedback is only accepted for a completed service with an assigned stylist.");
        }

        var exists = await _context.ServiceFeedback.IgnoreQueryFilters().AnyAsync(
            item => item.AppointmentServiceId == line.AppointmentServiceId && !item.IsDeleted, cancellationToken);
        if (exists)
        {
            throw new BillingConflictException("Feedback was already submitted for this service.");
        }

        var feedback = new ServiceFeedback
        {
            TenantId = line.TenantId,
            BranchId = line.BranchId,
            CustomerId = customer.CustomerId,
            AppointmentId = line.AppointmentId,
            AppointmentServiceId = line.AppointmentServiceId,
            EmployeeId = line.EmployeeId!.Value,
            Rating = request.Rating,
            Comment = string.IsNullOrEmpty(comment) ? null : comment,
            SubmittedAt = DateTime.UtcNow
        };
        _context.ServiceFeedback.Add(feedback);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true
            || exception.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new BillingConflictException("Feedback was already submitted for this service.");
        }

        return new ServiceFeedbackResponse
        {
            ServiceFeedbackId = feedback.ServiceFeedbackId,
            AppointmentServiceId = feedback.AppointmentServiceId,
            EmployeeId = feedback.EmployeeId,
            Rating = feedback.Rating,
            Comment = feedback.Comment,
            SubmittedAt = feedback.SubmittedAt
        };
    }

    public async Task EnsureCustomerMayContinueAsync(CancellationToken cancellationToken = default)
    {
        if (_currentUser.CustomerId is not > 0)
        {
            return;
        }

        var customer = await RequireCustomerAsync(cancellationToken);
        var pending = await EligibleLinesAsync(customer, cancellationToken);
        if (pending.Count > 0)
        {
            throw new FeedbackRequiredException();
        }
    }

    private async Task<List<AppointmentService>> EligibleLinesAsync(Customer customer, CancellationToken cancellationToken)
    {
        var lines = await _context.AppointmentServices.IgnoreQueryFilters()
            .Include(item => item.Appointment)
            .Include(item => item.Service)
            .Include(item => item.Employee)
            .Where(item => item.TenantId == customer.TenantId && item.Appointment.CustomerId == customer.CustomerId && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var submitted = await _context.ServiceFeedback.IgnoreQueryFilters()
            .Where(item => item.CustomerId == customer.CustomerId && !item.IsDeleted)
            .Select(item => item.AppointmentServiceId)
            .ToListAsync(cancellationToken);
        return lines.Where(line => IsEligible(line) && !submitted.Contains(line.AppointmentServiceId)).ToList();
    }

    private static bool IsEligible(AppointmentService line) =>
        line.LineStatus == ServiceLineStatus.Completed &&
        !line.IsCancelled &&
        line.EmployeeId is > 0 &&
        line.Appointment.Status == AppointmentStatus.Completed;

    private async Task<Customer> RequireCustomerAsync(CancellationToken cancellationToken)
    {
        if (_currentUser.CustomerId is not > 0 || _currentUser.TenantId is not > 0)
        {
            throw new UnauthorizedAccessException("Customer claim is missing.");
        }

        var customer = await _context.Customers.IgnoreQueryFilters().FirstOrDefaultAsync(
            item => item.CustomerId == _currentUser.CustomerId && item.TenantId == _currentUser.TenantId && !item.IsDeleted,
            cancellationToken) ?? throw new KeyNotFoundException();
        return customer;
    }

    private static PendingFeedbackResponse MapPending(AppointmentService line) => new()
    {
        AppointmentServiceId = line.AppointmentServiceId,
        AppointmentId = line.AppointmentId,
        BranchId = line.BranchId,
        ServiceName = line.Service?.ServiceName ?? "Service",
        StylistName = line.Employee == null ? "Stylist" : $"{line.Employee.FirstName} {line.Employee.LastName}".Trim(),
        AppointmentDate = line.Appointment.AppointmentDate
    };
}
