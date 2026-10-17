using Microsoft.EntityFrameworkCore;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Domain.Common;
using WatchParty.Domain.Users;

namespace WatchParty.Application.Features.Auth;

public sealed record GetUserQuery(Guid UserId) : IQuery<Result<UserDto>>;

internal sealed class GetUserQueryHandler(IAppDbContext db) : IQueryHandler<GetUserQuery, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(GetUserQuery query, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == query.UserId)
            .Select(u => new UserDto(u.Id, u.UserName, u.DisplayName, u.AvatarColor))
            .FirstOrDefaultAsync(cancellationToken);

        return user is null ? UserErrors.NotFound : user;
    }
}
