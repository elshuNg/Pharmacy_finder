namespace PharmacyFinder.API.Helpers;

public static class PostgresSearchPatterns
{
    public static string ContainsPattern(string value) => $"%{EscapeLike(value)}%";

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
