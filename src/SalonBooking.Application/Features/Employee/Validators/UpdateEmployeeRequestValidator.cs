using FluentValidation;
using SalonBooking.Application.Features.Employee.DTOs;

namespace SalonBooking.Application.Features.Employee.Validators
{
    public class UpdateEmployeeRequestValidator : AbstractValidator<UpdateEmployeeRequest>
    {
        public UpdateEmployeeRequestValidator()
        {
            RuleFor(x => x.FirstName)
                .NotEmpty().WithMessage("First name is required.")
                .MaximumLength(100);

            RuleFor(x => x.LastName)
                .NotEmpty().WithMessage("Last name is required.")
                .MaximumLength(100);

            RuleFor(x => x.MobileNo)
                .NotEmpty().WithMessage("Mobile number is required.")
                .Matches(@"^0\d{9}$")
                .WithMessage("Mobile number must be a valid 10-digit Sri Lankan number.");

            RuleFor(x => x.Email)
                .EmailAddress()
                .When(x => !string.IsNullOrWhiteSpace(x.Email))
                .WithMessage("Invalid email address.");

            RuleFor(x => x.Gender)
                .MaximumLength(20);

            RuleFor(x => x.Address)
                .MaximumLength(250);

            RuleFor(x => x.Designation)
                .NotEmpty().WithMessage("Designation is required.")
                .MaximumLength(100);

            RuleFor(x => x.HireDate)
                .NotEmpty().WithMessage("Hire date is required.");

            RuleFor(x => x.Salary)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Salary cannot be negative.");

           // RuleFor(x => x.BranchId)
            //    .GreaterThan(0)
            //    .WithMessage("Valid Branch is required.");
        }
    }
}