using FastEndpoints;
using FluentValidation;

namespace Exemplar.Customers.Application.Validators;

public sealed class CreateCustomerValidator : Validator<CreateCustomerRequest>
{
    public CreateCustomerValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email is not a valid address.")
            .MaximumLength(320).WithMessage("Email must not exceed 320 characters.");
    }
}
