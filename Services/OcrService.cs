using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Options;
using PharmacyFinder.API.Helpers;
using Tesseract;

namespace PharmacyFinder.API.Services;

public interface IOcrService
{
    Task<string> ExtractTextAsync(string imageAbsolutePath, CancellationToken cancellationToken = default);
}

public class TesseractOcrService(
    IOptions<TesseractSettings> tesseractOptions,
    IWebHostEnvironment env) : IOcrService
{
    public async Task<string> ExtractTextAsync(string imageAbsolutePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(imageAbsolutePath))
            throw ApiException.BadRequest(ApiErrorCodes.PrescriptionImageInvalid, "Prescription image file was not found.");

        var dataPath = ResolveDataPath();

        var language = tesseractOptions.Value.Language;

        try
        {
            var text = await Task.Run(() => ExtractWithNuGet(dataPath, language, imageAbsolutePath), cancellationToken);
            return ValidateExtractedText(text);
        }
        catch (ApiException)
        {
            throw;
        }
        catch (AggregateException ex) when (ex.InnerException is ApiException apiEx)
        {
            throw apiEx;
        }
        catch (Exception ex) when (ShouldFallbackToCli(ex))
        {
            var text = await ExtractWithCliAsync(dataPath, imageAbsolutePath, cancellationToken);
            return ValidateExtractedText(text);
        }
    }

    private string ResolveDataPath()
    {
        var dataPath = Path.IsPathRooted(tesseractOptions.Value.DataPath)
            ? tesseractOptions.Value.DataPath
            : Path.Combine(env.ContentRootPath, tesseractOptions.Value.DataPath);

        if (!Directory.Exists(dataPath))
            throw ApiException.BadRequest(ApiErrorCodes.PrescriptionOcrSetup,
                $"Tesseract data path not found: {dataPath}. Download eng.traineddata into tessdata/.");

        var trainedDataFile = Path.Combine(dataPath, $"{tesseractOptions.Value.Language}.traineddata");
        if (!File.Exists(trainedDataFile))
            throw ApiException.BadRequest(ApiErrorCodes.PrescriptionOcrSetup,
                $"Missing {trainedDataFile}. See tessdata/README.md for setup.");

        return dataPath;
    }

    private static string ExtractWithNuGet(string dataPath, string language, string imageAbsolutePath)
    {
        using var engine = new TesseractEngine(dataPath, language, EngineMode.Default);
        using var img = Pix.LoadFromFile(imageAbsolutePath);
        using var page = engine.Process(img);
        return page.GetText()?.Trim() ?? string.Empty;
    }

    private async Task<string> ExtractWithCliAsync(
        string dataPath,
        string imageAbsolutePath,
        CancellationToken cancellationToken)
    {
        var language = tesseractOptions.Value.Language;
        var psi = new ProcessStartInfo
        {
            FileName = "tesseract",
            Arguments = $"\"{imageAbsolutePath}\" stdout -l {language}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.Environment["TESSDATA_PREFIX"] = dataPath;

        using var process = Process.Start(psi)
            ?? throw ApiException.BadRequest(ApiErrorCodes.PrescriptionOcrSetup,
                "Tesseract CLI is not installed or could not be started.");

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var output = (await outputTask).Trim();
        var error = (await errorTask).Trim();

        if (process.ExitCode != 0)
            throw ApiException.BadRequest(ApiErrorCodes.PrescriptionOcrFailed,
                string.IsNullOrWhiteSpace(error)
                    ? $"Tesseract CLI failed with exit code {process.ExitCode}."
                    : error);

        return output;
    }

    private static string ValidateExtractedText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw ApiException.BadRequest(ApiErrorCodes.PrescriptionOcrFailed,
                "OCR could not extract any text from the prescription image.");
        return text;
    }

    private static bool ShouldFallbackToCli(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is DllNotFoundException or TesseractException or TypeInitializationException
                or TargetInvocationException or BadImageFormatException)
                return true;
        }

        return false;
    }

    internal static string GetRootCauseMessage(Exception ex)
    {
        var current = ex;
        while (current.InnerException is not null)
            current = current.InnerException;
        return current.Message;
    }
}
