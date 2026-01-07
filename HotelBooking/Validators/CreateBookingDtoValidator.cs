using FluentValidation;
using HotelBooking.Models.DTO;

namespace HotelBooking.Validators;

public class CreateBookingDtoValidator : AbstractValidator<CreateBookingDto>
{
    public CreateBookingDtoValidator()
    {
        RuleFor(x => x.RoomId)
            .GreaterThan(0).WithMessage("Room ID must be greater than 0");

        RuleFor(x => x.GuestName)
            .NotEmpty().WithMessage("Guest name is required")
            .MaximumLength(200).WithMessage("Guest name must not exceed 200 characters");

        RuleFor(x => x.GuestEmail)
            .NotEmpty().WithMessage("Guest email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(200).WithMessage("Guest email must not exceed 200 characters");

        RuleFor(x => x.CheckInDate)
            .NotEmpty().WithMessage("Check-in date is required")
            .Must(BeInFuture).WithMessage("Check-in date cannot be in the past");

        RuleFor(x => x.CheckOutDate)
            .NotEmpty().WithMessage("Check-out date is required")
            .GreaterThan(x => x.CheckInDate).WithMessage("Check-out date must be after check-in date");

        RuleFor(x => x.NumberOfGuests)
            .GreaterThan(0).WithMessage("Number of guests must be greater than 0");
    }

    private bool BeInFuture(DateTime date)
    {
        return date >= DateTime.Today;
    }
}
