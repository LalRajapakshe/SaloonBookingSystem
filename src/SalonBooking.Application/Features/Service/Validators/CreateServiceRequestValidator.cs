using FluentValidation;
using SalonBooking.Application.Features.Service.DTOs;

namespace SalonBooking.Application.Features.Service.Validators;

public class CreateServiceRequestValidator
    : AbstractValidator<CreateServiceRequest>
{
    public CreateServiceRequestValidator()
    {
        RuleFor(x => x.BranchId)
            .GreaterThan(0);

        RuleFor(x => x.ServiceCategoryId)
            .GreaterThan(0);

       // RuleFor(x => x.ServiceCode)
       //     .NotEmpty()
       //     .MaximumLength(20);

        RuleFor(x => x.ServiceName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Description)
            .MaximumLength(250);

        RuleFor(x => x.DurationMinutes)
            .GreaterThan(0)
            .LessThanOrEqualTo(600);

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.Cost)
            .GreaterThanOrEqualTo(0);
          //  .When(x => x.Cost.HasValue);

        RuleFor(x => x.CommissionPercentage)
            .InclusiveBetween(0, 100);
        //    .When(x => x.CommissionPercentage.HasValue);
    }
}