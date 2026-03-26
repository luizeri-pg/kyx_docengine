using FluentValidation;
using KYX.NotifyHUB.API.Models.DTOs.Integracao;

namespace KYX.NotifyHUB.API.Validators;

public class CreateIntegracaoRequestValidator : AbstractValidator<CreateIntegracaoRequest>
{
    public CreateIntegracaoRequestValidator()
    {
        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("Nome é obrigatório");

        RuleFor(x => x.Canal)
            .NotEmpty().WithMessage("Canal é obrigatório")
            .Must(c => c == "email" || c == "sms" || c == "whatsapp")
            .WithMessage("Canal deve ser 'email', 'sms' ou 'whatsapp'");

        RuleFor(x => x.Provedor)
            .NotEmpty().WithMessage("Provedor é obrigatório");

        When(x => !string.IsNullOrEmpty(x.UrlBase), () =>
        {
            RuleFor(x => x.UrlBase)
                .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
                .WithMessage("URL Base inválida");
        });
    }
}

public class UpdateIntegracaoRequestValidator : AbstractValidator<UpdateIntegracaoRequest>
{
    public UpdateIntegracaoRequestValidator()
    {
        When(x => !string.IsNullOrEmpty(x.Canal), () =>
        {
            RuleFor(x => x.Canal)
                .Must(c => c == "email" || c == "sms" || c == "whatsapp")
                .WithMessage("Canal deve ser 'email', 'sms' ou 'whatsapp'");
        });

        When(x => !string.IsNullOrEmpty(x.UrlBase), () =>
        {
            RuleFor(x => x.UrlBase)
                .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
                .WithMessage("URL Base inválida");
        });
    }
}

