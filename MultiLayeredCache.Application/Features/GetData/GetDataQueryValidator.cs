using FluentValidation;

namespace MultiLayeredCache.Application.Features.GetData;

/// <summary>
/// Validator for GetDataQuery
/// </summary>
public class GetDataQueryValidator : AbstractValidator<GetDataQuery>
{
    public GetDataQueryValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id cannot be empty");
    }
}
