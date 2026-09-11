using Equine.Domain.JournalTemplates;
using Equine.Api.Features.TreatmentTypes;
using FluentValidation;

namespace Equine.Api.Validation;

public class TreatmentTypeCreateRequestValidator : AbstractValidator<TreatmentTypeEndpoints.TreatmentTypeCreateRequest>
{
    public TreatmentTypeCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ShortDescription).NotEmpty().MaximumLength(500);
        RuleFor(x => x.JournalTemplateJson)
            .Must(BeValidTemplate)
            .WithMessage(x => TemplateError(x.JournalTemplateJson));
    }

    private static bool BeValidTemplate(string? json) =>
        JournalTemplateValidator.TryValidate(json, out _);

    private static string TemplateError(string? json)
    {
        JournalTemplateValidator.TryValidate(json, out var error);
        return error ?? "Invalid journal template.";
    }
}

public class TreatmentTypeUpdateRequestValidator : AbstractValidator<TreatmentTypeEndpoints.TreatmentTypeUpdateRequest>
{
    public TreatmentTypeUpdateRequestValidator()
    {
        RuleFor(x => x.JournalTemplateJson)
            .Must(BeValidTemplate)
            .WithMessage(x => TemplateError(x.JournalTemplateJson));
    }

    private static bool BeValidTemplate(string? json) =>
        JournalTemplateValidator.TryValidate(json, out _);

    private static string TemplateError(string? json)
    {
        JournalTemplateValidator.TryValidate(json, out var error);
        return error ?? "Invalid journal template.";
    }
}
