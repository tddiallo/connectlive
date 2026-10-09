using System.Security.Cryptography;

namespace ConnectLive.Core.Api.Sessions;

/// <summary>Génère les codes courts que les joueurs saisissent pour rejoindre une session.</summary>
public static class SessionCodeGenerator
{
    /// <summary>Majuscules et chiffres sans caractères ambigus à l'oral ou à l'écran (<c>I O 0 1</c>).</summary>
    public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public const int CodeLength = 6;

    /// <summary>Tire un code avec un générateur cryptographique (codes non devinables par énumération simple).</summary>
    public static string Generate() =>
        new(RandomNumberGenerator.GetItems<char>(Alphabet, CodeLength));
}
