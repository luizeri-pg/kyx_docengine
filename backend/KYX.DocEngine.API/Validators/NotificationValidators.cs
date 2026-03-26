using FluentValidation;
using KYX.NotifyHUB.API.Models.DTOs.Notification;

namespace KYX.NotifyHUB.API.Validators;

public class NotificationSendRequestValidator : AbstractValidator<NotificationSendRequest>
{
    public NotificationSendRequestValidator()
    {
        RuleFor(x => x.Config)
            .NotNull().WithMessage("Config é obrigatório");

        When(x => x.Config != null, () =>
        {
            RuleFor(x => x.Config.Canal)
                .NotEmpty().WithMessage("Canal é obrigatório")
                .Must(c => c == "email" || c == "sms" || c == "whatsapp")
                .WithMessage("Canal deve ser 'email', 'sms' ou 'whatsapp'");
        });

        RuleFor(x => x.Dados)
            .NotNull().WithMessage("Dados é obrigatório");

        When(x => x.Dados != null, () =>
        {
            RuleFor(x => x.Dados.Destinatario)
                .NotNull().WithMessage("Destinatário é obrigatório");

            When(x => x.Dados.Destinatario != null, () =>
            {
                RuleFor(x => x)
                    .Must(x => !string.IsNullOrEmpty(x.Dados.Destinatario.Email) || 
                               !string.IsNullOrEmpty(x.Dados.Destinatario.Telefone))
                    .WithMessage("Email ou telefone do destinatário é obrigatório");

                When(x => !string.IsNullOrEmpty(x.Dados.Destinatario.Email), () =>
                {
                    RuleFor(x => x.Dados.Destinatario.Email)
                        .EmailAddress().WithMessage("Email do destinatário inválido");
                });
            });

            RuleFor(x => x.Dados.Mensagem)
                .NotNull().WithMessage("Mensagem é obrigatória");

            When(x => x.Dados.Mensagem != null, () =>
            {
                RuleFor(x => x.Dados.Mensagem.Tipo)
                    .NotEmpty().WithMessage("Tipo da mensagem é obrigatório")
                    .Must(t => t == "text" || t == "html" || t == "template")
                    .WithMessage("Tipo da mensagem deve ser 'text', 'html' ou 'template'");

                RuleFor(x => x.Dados.Mensagem.Corpo)
                    .NotEmpty().WithMessage("Corpo da mensagem é obrigatório");
            });
        });
    }
}

public class AnexoValidator : AbstractValidator<Anexo>
{
    public AnexoValidator()
    {
        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("Nome do anexo é obrigatório");

        RuleFor(x => x.Tipo)
            .NotEmpty().WithMessage("Tipo do anexo é obrigatório");

        RuleFor(x => x.Base64)
            .NotEmpty().WithMessage("Conteúdo base64 é obrigatório");
    }
}

