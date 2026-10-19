namespace ConnectLive.Portal.Shared.Realtime;

/// <summary>Ligne du classement d'une session (diffusée par <c>LeaderboardUpdated</c> et renvoyée par l'API).</summary>
/// <param name="UserId">Joueur.</param>
/// <param name="Nickname">Pseudo affiché.</param>
/// <param name="Rank">Rang à partir de 1 ; les ex æquo partagent le même rang (1, 1, 3…).</param>
/// <param name="Score">Total de points sur la session.</param>
public sealed record LeaderboardEntryDto(Guid UserId, string Nickname, int Rank, int Score);
