using FluentValidation;
using SalonBooking.Application.Features.Customer.DTOs;

namespace SalonBooking.Application.Features.Customer.Validators;

public class UpdateCustomerRequestValidator
    : AbstractValidator<UpdateCustomerRequest>
{
    public UpdateCustomerRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.MobileNo)
            .NotEmpty()
            .Matches(@"^[0-9]{10}$");

        RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.DateOfBirth)
            .LessThan(DateTime.Today);

       // RuleFor(x => x.Address)
       //     .MaximumLength(300);
    }
}