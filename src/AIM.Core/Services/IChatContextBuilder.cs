using AIM.Core.Chat;
using AIM.Core.Personalities;

namespace AIM.Core.Services;

public interface IChatContextBuilder
{
    Task<ChatContext> BuildAsync(
        Personality personality,
        Conversation? conversation = null,
        CancellationToken cancellationToken = default);
}
