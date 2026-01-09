using FluentValidation;
using HotelBooking.Models.DTO;

namespace HotelBooking.Validators;

public class CreateApiKeyDtoValidator : AbstractValidator<CreateApiKeyDto>
{
    public CreateApiKeyDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("API key name is required")
            .MaximumLength(100).WithMessage("API key name must not exceed 100 characters");

        RuleFor(x => x.ExpiresAt)
            .NotEmpty().WithMessage("Expiration date is required")
            .Must(BeInFuture).WithMessage("Expiration date must be in the future");
    }

    private bool BeInFuture(DateTime date)
    {
        return date > DateTime.UtcNow;
    }
}
