using FluentValidation;
using SalonBooking.Application.Features.ServiceCategory.DTOs;

namespace SalonBooking.Application.Features.ServiceCategory.Validators;

public class CreateServiceCategoryRequestValidator
    : AbstractValidator<CreateServiceCategoryRequest>
{
    public CreateServiceCategoryRequestValidator()
    {
         //   RuleFor(x => x.BranchId)
        //    .GreaterThan(0);

        RuleFor(x => x.CategoryName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Description)
            .MaximumLength(500);

        RuleFor(x => x.DisplayOrder)
            .GreaterThanOrEqualTo(0);
    }
}