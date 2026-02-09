using FluentValidation;

namespace MultiLayeredCache.Application.Features.CreateData;

/// <summary>
/// Validator for CreateDataCommand
/// </summary>
public class CreateDataCommandValidator : AbstractValidator<CreateDataCommand>
{
    public CreateDataCommandValidator()
    {
        RuleFor(x => x.Value)
            .NotEmpty()
            .WithMessage("Value cannot be empty")
            .MaximumLength(1000)
            .WithMessage("Value cannot exceed 1000 characters");
    }
}
