using FluentValidation;
using HotelBooking.Models.DTO;

namespace HotelBooking.Validators;

public class CreateRoomDtoValidator : AbstractValidator<CreateRoomDto>
{
    public CreateRoomDtoValidator()
    {
        RuleFor(x => x.HotelId)
            .GreaterThan(0).WithMessage("Hotel ID must be greater than 0");

        RuleFor(x => x.RoomNumber)
            .NotEmpty().WithMessage("Room number is required")
            .MaximumLength(50).WithMessage("Room number must not exceed 50 characters");

        RuleFor(x => x.RoomType)
            .NotEmpty().WithMessage("Room type is required")
            .MaximumLength(50).WithMessage("Room type must not exceed 50 characters");

        RuleFor(x => x.PricePerNight)
            .GreaterThan(0).WithMessage("Price per night must be greater than 0");

        RuleFor(x => x.MaxOccupancy)
            .GreaterThan(0).WithMessage("Max occupancy must be greater than 0");
    }
}
