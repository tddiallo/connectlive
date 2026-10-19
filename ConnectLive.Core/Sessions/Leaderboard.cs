using ConnectLive.Portal.Shared.Realtime;
using ConntectLive.DAL;
using Microsoft.EntityFrameworkCore;

namespace ConnectLive.Core.Api.Sessions;

/// <summary>Calcul du classement d'une session.</summary>
public static class Leaderboard
{
    /// <summary>Pseudo affiché si l'utilisateur n'existe plus.</summary>
    public const string UnknownNickname = "Unknown";

    /// <summary>
    /// Classe les joueurs par score décroissant. Classement « compétition » : les ex æquo partagent le rang
    /// (1, 1, 3). À égalité, l'ordre est alphabétique sur le pseudo pour rester stable d'un envoi à l'autre.
    /// </summary>
    public static IReadOnlyList<LeaderboardEntryDto> Rank(IReadOnlyDictionary<Guid, int> scores, IReadOnlyDictionary<Guid, string> nicknames)
    {
        var ordered = scores
            .Select(s => (UserId: s.Key, Score: s.Value, Nickname: nicknames.GetValueOrDefault(s.Key) ?? UnknownNickname))
            .OrderByDescending(p => p.Score)
            .ThenBy(p => p.Nickname, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return ordered
            .Select(p => new LeaderboardEntryDto(p.UserId, p.Nickname, 1 + ordered.Count(o => o.Score > p.Score), p.Score))
            .ToList();
    }

    /// <summary>Lit les réponses de la session et renvoie le classement à jour.</summary>
    public static async Task<IReadOnlyList<LeaderboardEntryDto>> LoadAsync(this ApplicationDbContext db, Guid sessionId, CancellationToken cancellationToken)
    {
        var scores = await db.SessionAnswers.AsNoTracking()
            .Where(a => a.SessionId == sessionId)
            .GroupBy(a => a.UserId)
            .Select(g => new { UserId = g.Key, Score = g.Sum(a => a.Points) })
            .ToDictionaryAsync(x => x.UserId, x => x.Score, cancellationToken);

        var userIds = scores.Keys.ToList();
        var nicknames = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Nickname, cancellationToken);

        return Rank(scores, nicknames);
    }
}
