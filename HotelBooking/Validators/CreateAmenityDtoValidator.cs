using FluentValidation;
using HotelBooking.Models.DTO;

namespace HotelBooking.Validators;

public class CreateAmenityDtoValidator : AbstractValidator<CreateAmenityDto>
{
    public CreateAmenityDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Amenity name is required")
            .MaximumLength(100).WithMessage("Amenity name must not exceed 100 characters");

        RuleFor(x => x.Icon)
            .NotEmpty().WithMessage("Icon is required")
            .MaximumLength(50).WithMessage("Icon must not exceed 50 characters");
    }
}
