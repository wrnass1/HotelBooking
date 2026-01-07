using FluentValidation;
using HotelBooking.Models.DTO;

namespace HotelBooking.Validators;

public class CreateHotelDtoValidator : AbstractValidator<CreateHotelDto>
{
    public CreateHotelDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Hotel name is required")
            .MaximumLength(200).WithMessage("Hotel name must not exceed 200 characters");

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("Address is required")
            .MaximumLength(500).WithMessage("Address must not exceed 500 characters");

        RuleFor(x => x.City)
            .NotEmpty().WithMessage("City is required")
            .MaximumLength(100).WithMessage("City must not exceed 100 characters");

        RuleFor(x => x.Country)
            .NotEmpty().WithMessage("Country is required")
            .MaximumLength(100).WithMessage("Country must not exceed 100 characters");

        RuleFor(x => x.StarRating)
            .InclusiveBetween(1, 5).WithMessage("Star rating must be between 1 and 5");
    }
}
