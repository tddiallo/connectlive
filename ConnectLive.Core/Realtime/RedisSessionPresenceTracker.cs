using StackExchange.Redis;

namespace ConnectLive.Core.Api.Realtime;

/// <summary>
/// Présence partagée entre toutes les instances de l'API, stockée dans Redis (voir docs/SIGNALR-BACKPLANE-REDIS.md).
/// </summary>
/// <remarks>
/// Deux clés par participant :
/// <list type="bullet">
/// <item><c>connectlive:presence:conn:{connectionId}</c> — chaîne <c>sessionId:userId</c> (retrouver où est une connexion) ;</item>
/// <item><c>connectlive:presence:session:{sessionId}</c> — hash <c>connectionId → userId</c> (lister une session).</item>
/// </list>
/// Les identifiants sont limités à <c>[A-Za-z0-9_-]</c> par le hub, donc <c>:</c> est un séparateur sûr.
/// Toutes les clés expirent (<see cref="PresenceTtl"/>) : si une instance meurt sans passer par <c>OnDisconnectedAsync</c>,
/// ses connexions « fantômes » disparaissent d'elles-mêmes. Le délai est rafraîchi à chaque <c>JoinSession</c>.
/// </remarks>
public sealed class RedisSessionPresenceTracker : ISessionPresenceTracker
{
    /// <summary>Durée de vie des entrées de présence (au-delà d'une session de quiz raisonnable).</summary>
    public static readonly TimeSpan PresenceTtl = TimeSpan.FromHours(12);

    private const string Prefix = "connectlive:presence:";
    private const char Separator = ':';

    private readonly IConnectionMultiplexer _redis;

    public RedisSessionPresenceTracker(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    private static RedisKey ConnectionKey(string connectionId) => $"{Prefix}conn:{connectionId}";
    private static RedisKey SessionKey(string sessionId) => $"{Prefix}session:{sessionId}";

    public async Task JoinAsync(string connectionId, string sessionId, string userId)
    {
        var db = _redis.GetDatabase();

        // SET ... GET : écrit la nouvelle valeur et renvoie l'ancienne, en une opération atomique.
        var previous = await db.StringSetAndGetAsync(ConnectionKey(connectionId), $"{sessionId}{Separator}{userId}", PresenceTtl);
        if (Parse(previous) is { } old && old.SessionId != sessionId)
            await db.HashDeleteAsync(SessionKey(old.SessionId), connectionId);

        await db.HashSetAsync(SessionKey(sessionId), connectionId, userId);
        await db.KeyExpireAsync(SessionKey(sessionId), PresenceTtl);
    }

    public async Task<SessionPresence?> LeaveAsync(string connectionId)
    {
        var db = _redis.GetDatabase();

        // GETDEL : lit et supprime atomiquement, donc une seule instance « gagne » si deux appels se croisent.
        var presence = Parse(await db.StringGetDeleteAsync(ConnectionKey(connectionId)));
        if (presence is null)
            return null;

        await db.HashDeleteAsync(SessionKey(presence.SessionId), connectionId);
        return presence;
    }

    public async Task<IReadOnlyCollection<string>> GetParticipantsAsync(string sessionId)
    {
        var users = await _redis.GetDatabase().HashValuesAsync(SessionKey(sessionId));
        return users.Select(u => (string)u!).Distinct().ToArray();
    }

    private static SessionPresence? Parse(RedisValue value)
    {
        if (value.IsNullOrEmpty)
            return null;

        var text = (string)value!;
        var index = text.IndexOf(Separator);
        return index <= 0 ? null : new SessionPresence(text[..index], text[(index + 1)..]);
    }
}
