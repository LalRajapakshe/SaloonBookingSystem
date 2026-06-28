using FluentValidation;
using SalonBooking.Application.Features.Customer.DTOs;

namespace SalonBooking.Application.Features.Customer.Validators;

public class CreateCustomerRequestValidator
    : AbstractValidator<CreateCustomerRequest>
{
    public CreateCustomerRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.MobileNo)
            .NotEmpty()
            .Matches(@"^[0-9]{10}$")
            .WithMessage("Mobile number must contain exactly 10 digits.");

        RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.DateOfBirth)
            .LessThan(DateTime.Today);

        RuleFor(x => x.Gender)
            .Must(x => x == "Male" ||
                       x == "Female" ||
                       x == "Other")
            .When(x => !string.IsNullOrWhiteSpace(x.Gender));

      //  RuleFor(x => x.Address)
      //      .MaximumLength(300);
    }
}