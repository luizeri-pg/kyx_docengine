// Gera hash BCrypt para colocar em tb_usuario.senha
// Uso: dotnet run --project backend/tools/HashPassword -- "SuaSenhaSegura"

if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.Error.WriteLine("Uso: dotnet run -- <senha_em_texto_plano>");
    Console.Error.WriteLine("Exemplo: dotnet run -- \"MinhaSenha123\"");
    return 1;
}

var hash = BCrypt.Net.BCrypt.HashPassword(args[0]);
Console.WriteLine(hash);
return 0;
