using Hearth.Domain.Entities;

namespace Hearth.Application.Auth;

public sealed record UserDto(Guid Id, string Name, string Email)
{
    public static UserDto FromEntity(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new UserDto(user.Id, user.Name, user.Email);
    }
}
