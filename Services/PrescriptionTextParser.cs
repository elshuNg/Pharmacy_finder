using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PharmacyFinder.API.Data;
using PharmacyFinder.API.DTOs;

namespace PharmacyFinder.API.Services;

public interface IPrescriptionTextParser
{
    Task<List<ParsedPrescriptionItem>> ParseAsync(string extractedText, CancellationToken cancellationToken = default);
}

public class PrescriptionTextParser(AppDbContext db) : IPrescriptionTextParser
{
    private static readonly Regex BracketSegmentRegex = new(@"\[([^\]]+)\]", RegexOptions.Compiled);

    public async Task<List<ParsedPrescriptionItem>> ParseAsync(string extractedText, CancellationToken cancellationToken = default)
    {
        var medicines = await db.Medicines.AsNoTracking().ToListAsync(cancellationToken);
        var candidates = ExtractCandidates(extractedText);
        var seenMedicineIds = new HashSet<Guid>();
        var items = new List<ParsedPrescriptionItem>();

        foreach (var (line, sourceLine) in candidates)
        {
            if (IsNoiseLine(line) || IsPlaceholderLine(line))
                continue;

            var match = MedicineMatching.MatchMedicine(line, medicines);
            if (string.IsNullOrWhiteSpace(match.OcrText))
                continue;

            if (match.Medicine is null ||
                match.MatchType == MedicineMatchType.None ||
                match.MatchConfidence < MedicineMatching.MinReviewConfidence)
                continue;

            if (!seenMedicineIds.Add(match.Medicine.Id))
                continue;

            items.Add(new ParsedPrescriptionItem
            {
                MedicineId = match.Medicine.Id,
                OcrText = match.OcrText,
                SuggestedMedicineName = match.Medicine.Name,
                Quantity = TryParseQuantity(sourceLine),
                MatchConfidence = match.MatchConfidence,
                MatchType = match.MatchType,
                RequiresConfirmation = match.RequiresConfirmation
            });
        }

        return items;
    }

    private static IEnumerable<(string Line, string SourceLine)> ExtractCandidates(string extractedText)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in extractedText
                     .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (rawLine.Length < 3 || IsNoiseLine(rawLine))
                continue;

            var bracketMatches = BracketSegmentRegex.Matches(rawLine);
            if (bracketMatches.Count > 0)
            {
                foreach (Match bracketMatch in bracketMatches)
                {
                    var inner = bracketMatch.Groups[1].Value.Trim();
                    if (inner.Length >= 3 && seen.Add(inner))
                        yield return (inner, rawLine);
                }
                continue;
            }

            if (seen.Add(rawLine))
                yield return (rawLine, rawLine);
        }
    }

    private static bool IsPlaceholderLine(string line)
    {
        var lower = line.Trim().ToLowerInvariant();
        return lower.StartsWith("insert medicine") || lower.Contains("insert medicine name");
    }

    private static bool IsNoiseLine(string line)
    {
        var lower = line.Trim().ToLowerInvariant();
        if (lower.Length < 3)
            return true;

        string[] noisePrefixes =
        [
            "prescription", "doctor", "dr.", "patient", "date", "signature", "rx", "pharmacy",
            "tel", "phone", "address", "dose", "directions", "health choice", "clinic",
            "prescription no", "name:", "age:", "contact", "medical physician"
        ];
        if (noisePrefixes.Any(n => lower.StartsWith(n)))
            return true;

        string[] noiseContains = ["your email", "your website"];
        if (noiseContains.Any(n => lower.Contains(n)))
            return true;

        if (lower.StartsWith("mr.") || lower.StartsWith("mrs.") || lower.StartsWith("ms."))
            return true;

        return false;
    }

    private static int? TryParseQuantity(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (int.TryParse(part.TrimEnd('x', 'X'), out var qty) && qty > 0 && qty < 1000)
                return qty;
        }
        return null;
    }
}
