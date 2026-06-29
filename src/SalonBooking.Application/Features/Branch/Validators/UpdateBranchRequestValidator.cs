using FluentValidation;
using SalonBooking.Application.Features.Branch.DTOs;

namespace SalonBooking.Application.Features.Branch.Validators;

public class UpdateBranchRequestValidator : AbstractValidator<UpdateBranchRequest>
{
    public UpdateBranchRequestValidator()
    {
        RuleFor(x => x.TenantId)
            .GreaterThan(0);

        RuleFor(x => x.BranchName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.AddressLine1)
            .MaximumLength(200);

        RuleFor(x => x.AddressLine2)
            .MaximumLength(200);

        RuleFor(x => x.City)
            .MaximumLength(100);

        RuleFor(x => x.PhoneNo)
            .Matches(@"^\d{10}$")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNo))
            .WithMessage("Phone number must contain exactly 10 digits.");

        RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.ManagerName)
            .MaximumLength(100);
    }
}