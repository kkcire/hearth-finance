using System.Security.Claims;
using Hearth.Application.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Hearth.Web.Auth;

public static class HearthAuth
{
    public const string RateLimitPolicyName = "auth";
    public const string CookieName = "Hearth.Auth";
    public const string RateLimitExceededMessage = "Muitas tentativas. Aguarde um minuto e tente novamente.";

    public static ClaimsPrincipal CreatePrincipal(UserDto user)
    {
        ArgumentNullException.ThrowIfNull(user);

        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, user.Id.ToString("D")),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email),
        ];

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    public static AuthenticationProperties CreateCookieProperties() =>
        new()
        {
            IsPersistent = true,
            AllowRefresh = true,
        };

    public static bool TryReadUser(
        ClaimsPrincipal? principal,
        out Guid userId,
        out string? name,
        out string? email)
    {
        userId = Guid.Empty;
        name = null;
        email = null;

        if (principal?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var rawId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(rawId, out var parsedId) || parsedId == Guid.Empty)
        {
            return false;
        }

        userId = parsedId;
        name = principal.FindFirstValue(ClaimTypes.Name);
        email = principal.FindFirstValue(ClaimTypes.Email);
        return true;
    }

    public static bool IsSafeLocalReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return false;
        }

        if (!returnUrl.StartsWith('/') || returnUrl.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        if (returnUrl.Contains('\\') || returnUrl.Contains('\r') || returnUrl.Contains('\n'))
        {
            return false;
        }

        if (returnUrl.Length > 1 && char.IsControl(returnUrl[1]))
        {
            return false;
        }

        var pathOnly = returnUrl.Split('?', '#')[0].TrimEnd('/');
        if (string.Equals(pathOnly, "/login", StringComparison.OrdinalIgnoreCase)
            || string.Equals(pathOnly, "/cadastro", StringComparison.OrdinalIgnoreCase)
            || string.Equals(pathOnly, "/auth/logout", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    public static string ToPortugueseError(AuthErrorCode errorCode) =>
        errorCode switch
        {
            AuthErrorCode.InvalidName => "Informe um nome válido.",
            AuthErrorCode.InvalidEmail => "Informe um e-mail válido.",
            AuthErrorCode.PasswordTooShort => "A senha deve ter pelo menos 8 caracteres.",
            AuthErrorCode.PasswordTooLong => "A senha deve ter no máximo 128 caracteres.",
            AuthErrorCode.EmailAlreadyInUse => "Já existe uma conta com este e-mail.",
            AuthErrorCode.InvalidCredentials => "E-mail ou senha inválidos.",
            AuthErrorCode.RegistrationDisabled => "Novos cadastros estão desativados no momento.",
            _ => "Não foi possível concluir a autenticação. Tente novamente.",
        };
}
