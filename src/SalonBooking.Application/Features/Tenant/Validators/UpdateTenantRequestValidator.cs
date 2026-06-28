using FluentValidation;
using SalonBooking.Application.Features.Tenant.DTOs;

namespace SalonBooking.Application.Features.Tenant.Validators;

public class UpdateTenantRequestValidator : AbstractValidator<UpdateTenantRequest>
{
    public UpdateTenantRequestValidator()
    {
        RuleFor(x => x.TenantName)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(x => x.BusinessName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.PhoneNo)
            .MaximumLength(20);

        RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.MaxBranches)
            .GreaterThan(0);

        RuleFor(x => x.MaxUsers)
            .GreaterThan(0);
    }
}