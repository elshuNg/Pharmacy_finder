using PharmacyFinder.API.DTOs;
using PharmacyFinder.API.Models;

namespace PharmacyFinder.API.Services;

public static class MedicineMatching
{
    public const double MinReviewConfidence = 0.80;
    private const double FuzzyHighThreshold = 0.90;

    public static string NormalizeOcrLine(string line)
    {
        var text = line.Trim();
        while (text.Length > 0 && (text[0] == '[' || text[0] == '('))
            text = text[1..].Trim();
        while (text.Length > 0 && (text[^1] == ']' || text[^1] == ')'))
            text = text[..^1].Trim();

        var separators = new[] { ',', ';' };
        var cut = text.Length;
        foreach (var sep in separators)
        {
            var idx = text.IndexOf(sep, StringComparison.Ordinal);
            if (idx > 0)
                cut = Math.Min(cut, idx);
        }

        var mgIdx = text.IndexOf(" mg", StringComparison.OrdinalIgnoreCase);
        if (mgIdx > 0)
            cut = Math.Min(cut, mgIdx);

        text = text[..cut].Trim();
        return text;
    }

    public static string NormalizeForCompare(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var chars = text.Trim().ToLowerInvariant().ToCharArray();
        if (chars.Length > 0)
        {
            chars[0] = FixOcrChar(chars[0]);
            for (var i = 1; i < chars.Length; i++)
            {
                if (chars[i - 1] == ' ')
                    chars[i] = FixOcrChar(chars[i]);
            }
        }

        return new string(chars);
    }

    private static char FixOcrChar(char c) => c switch
    {
        '1' => 'i',
        '0' => 'o',
        '5' => 's',
        _ => c
    };

    public static double ComputeSimilarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return 0;

        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
            return 1;

        var distance = LevenshteinDistance(a, b);
        var maxLen = Math.Max(a.Length, b.Length);
        return maxLen == 0 ? 1 : 1.0 - (double)distance / maxLen;
    }

    public static MedicineMatchResult MatchMedicine(string ocrLine, IReadOnlyList<Medicine> medicines)
    {
        var ocrText = NormalizeOcrLine(ocrLine);
        if (string.IsNullOrWhiteSpace(ocrText) || ocrText.Length < 3)
            return MedicineMatchResult.Unmatched(ocrLine);

        var normalizedLine = NormalizeForCompare(ocrText);

        Medicine? exactMatch = null;
        foreach (var medicine in medicines)
        {
            var name = medicine.Name.ToLowerInvariant();
            if (normalizedLine.Contains(name) ||
                normalizedLine.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                if (exactMatch is null || medicine.Name.Length > exactMatch.Name.Length)
                    exactMatch = medicine;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(medicine.GenericName))
            {
                var generic = medicine.GenericName.ToLowerInvariant();
                if (normalizedLine.Contains(generic))
                {
                    if (exactMatch is null || generic.Length > exactMatch.Name.Length)
                        exactMatch = medicine;
                }
            }
        }

        if (exactMatch is not null)
        {
            return new MedicineMatchResult
            {
                OcrText = ocrText,
                Medicine = exactMatch,
                MatchConfidence = 1.0,
                MatchType = MedicineMatchType.Exact,
                RequiresConfirmation = false
            };
        }

        Medicine? best = null;
        var bestScore = 0.0;
        foreach (var medicine in medicines)
        {
            var candidates = new List<string> { medicine.Name };
            if (!string.IsNullOrWhiteSpace(medicine.GenericName))
                candidates.Add(medicine.GenericName);

            foreach (var candidate in candidates)
            {
                var normalizedCandidate = NormalizeForCompare(candidate);
                var score = ComputeSimilarity(normalizedLine, normalizedCandidate);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = medicine;
                }
            }
        }

        if (best is null || bestScore < MinReviewConfidence)
        {
            return new MedicineMatchResult
            {
                OcrText = ocrText,
                Medicine = null,
                MatchConfidence = bestScore,
                MatchType = MedicineMatchType.None,
                RequiresConfirmation = true
            };
        }

        var matchType = bestScore >= FuzzyHighThreshold ? MedicineMatchType.FuzzyHigh : MedicineMatchType.Fuzzy;
        return new MedicineMatchResult
        {
            OcrText = ocrText,
            Medicine = best,
            MatchConfidence = bestScore,
            MatchType = matchType,
            RequiresConfirmation = matchType == MedicineMatchType.Fuzzy
        };
    }

    private static int LevenshteinDistance(string a, string b)
    {
        var m = a.Length;
        var n = b.Length;
        var dp = new int[m + 1, n + 1];

        for (var i = 0; i <= m; i++) dp[i, 0] = i;
        for (var j = 0; j <= n; j++) dp[0, j] = j;

        for (var i = 1; i <= m; i++)
        {
            for (var j = 1; j <= n; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost);
            }
        }

        return dp[m, n];
    }
}

public class MedicineMatchResult
{
    public string OcrText { get; init; } = string.Empty;
    public Medicine? Medicine { get; init; }
    public double MatchConfidence { get; init; }
    public MedicineMatchType MatchType { get; init; }
    public bool RequiresConfirmation { get; init; }

    public static MedicineMatchResult Unmatched(string raw) => new()
    {
        OcrText = raw,
        MatchType = MedicineMatchType.None,
        RequiresConfirmation = true
    };
}

public class ParsedPrescriptionItem
{
    public Guid? MedicineId { get; set; }
    public string OcrText { get; set; } = string.Empty;
    public string? SuggestedMedicineName { get; set; }
    public int? Quantity { get; set; }
    public double MatchConfidence { get; set; }
    public MedicineMatchType MatchType { get; set; }
    public bool RequiresConfirmation { get; set; }
}
