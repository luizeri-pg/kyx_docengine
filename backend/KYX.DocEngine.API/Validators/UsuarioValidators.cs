using FluentValidation;
using KYX.NotifyHUB.API.Models.DTOs.Usuario;

namespace KYX.NotifyHUB.API.Validators;

public class CreateUsuarioRequestValidator : AbstractValidator<CreateUsuarioRequest>
{
    public CreateUsuarioRequestValidator()
    {
        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("Nome é obrigatório");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email é obrigatório")
            .EmailAddress().WithMessage("Email inválido");

        RuleFor(x => x.Senha)
            .NotEmpty().WithMessage("Senha é obrigatória")
            .MinimumLength(6).WithMessage("Senha deve ter no mínimo 6 caracteres");

        RuleFor(x => x.PerfilId)
            .NotEmpty().WithMessage("Perfil é obrigatório");
    }
}

public class UpdateUsuarioRequestValidator : AbstractValidator<UpdateUsuarioRequest>
{
    public UpdateUsuarioRequestValidator()
    {
        When(x => !string.IsNullOrEmpty(x.Nome), () =>
        {
            RuleFor(x => x.Nome)
                .MinimumLength(1).WithMessage("Nome inválido");
        });

        When(x => !string.IsNullOrEmpty(x.Email), () =>
        {
            RuleFor(x => x.Email)
                .EmailAddress().WithMessage("Email inválido");
        });

        When(x => !string.IsNullOrEmpty(x.Senha), () =>
        {
            RuleFor(x => x.Senha)
                .MinimumLength(6).WithMessage("Senha deve ter no mínimo 6 caracteres");
        });
    }
}

