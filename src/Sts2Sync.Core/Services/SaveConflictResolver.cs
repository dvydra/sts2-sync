using System.Text;
using Sts2Sync.Core.Models;

namespace Sts2Sync.Core.Services;

public static class SaveConflictResolver
{
    public static ConflictResult Resolve(string saveFileName, byte[]? localBytes, byte[]? cloudBytes)
    {
        // 1. Both null — nothing to sync
        if (localBytes is null && cloudBytes is null)
            return new ConflictResult(saveFileName, ResolutionOutcome.Identical, WinReason.ShaMatch,
                null, null, null, null);

        // 2. One side missing
        if (localBytes is null)
            return new ConflictResult(saveFileName, ResolutionOutcome.CloudWins, WinReason.LocalMissing,
                null, null, null, null);

        if (cloudBytes is null)
            return new ConflictResult(saveFileName, ResolutionOutcome.LocalWins, WinReason.CloudMissing,
                null, null, null, null);

        // 3. SHA-1 match — identical content
        var localSha1 = SaveFileHasher.ComputeSha1(localBytes);
        var cloudSha1 = SaveFileHasher.ComputeSha1(cloudBytes);
        if (string.Equals(localSha1, cloudSha1, StringComparison.OrdinalIgnoreCase))
            return new ConflictResult(saveFileName, ResolutionOutcome.Identical, WinReason.ShaMatch,
                null, null, null, null);

        // 4. Corruption check
        var localText = Encoding.UTF8.GetString(localBytes);
        var cloudText = Encoding.UTF8.GetString(cloudBytes);
        var localCorrupt = !IsValidJson(localText);
        var cloudCorrupt = !IsValidJson(cloudText);

        if (localCorrupt && cloudCorrupt)
            return new ConflictResult(saveFileName, ResolutionOutcome.CloudWins, WinReason.CloudCorrupt,
                null, null, null, null);
        if (localCorrupt)
            return new ConflictResult(saveFileName, ResolutionOutcome.CloudWins, WinReason.LocalCorrupt,
                null, null, null, null);
        if (cloudCorrupt)
            return new ConflictResult(saveFileName, ResolutionOutcome.LocalWins, WinReason.CloudCorrupt,
                null, null, null, null);

        // 5. Dispatch by file type
        if (saveFileName.Contains("progress.save"))
            return ResolveProgress(saveFileName, localText, cloudText);

        return ResolveCurrentRun(saveFileName, localText, cloudText);
    }

    private static ConflictResult ResolveProgress(string saveFileName, string localJson, string cloudJson)
    {
        var localParse = SaveFileParser.ParseProgressSave(localJson);
        var cloudParse = SaveFileParser.ParseProgressSave(cloudJson);

        if (!localParse.IsSuccess && !cloudParse.IsSuccess)
            return new ConflictResult(saveFileName, ResolutionOutcome.CloudWins, WinReason.CloudCorrupt,
                null, null, null, null);
        if (!localParse.IsSuccess)
            return new ConflictResult(saveFileName, ResolutionOutcome.CloudWins, WinReason.LocalCorrupt,
                null, null, null, null);
        if (!cloudParse.IsSuccess)
            return new ConflictResult(saveFileName, ResolutionOutcome.LocalWins, WinReason.CloudCorrupt,
                null, null, null, null);

        var localAnalysis = SaveFileAnalyzer.AnalyzeProgress(localParse.Value!);
        var cloudAnalysis = SaveFileAnalyzer.AnalyzeProgress(cloudParse.Value!);
        var cmp = SaveFileAnalyzer.CompareProgress(localAnalysis, cloudAnalysis);

        var (outcome, reason) = cmp switch
        {
            CompareResult.LeftWins => (ResolutionOutcome.LocalWins, WinReason.HeuristicWin),
            CompareResult.RightWins => (ResolutionOutcome.CloudWins, WinReason.HeuristicWin),
            _ => (ResolutionOutcome.CloudWins, WinReason.TieBreakCloudWins)
        };

        return new ConflictResult(saveFileName, outcome, reason,
            localAnalysis, cloudAnalysis, null, null);
    }

    private static ConflictResult ResolveCurrentRun(string saveFileName, string localJson, string cloudJson)
    {
        var localParse = SaveFileParser.ParseCurrentRun(localJson);
        var cloudParse = SaveFileParser.ParseCurrentRun(cloudJson);

        if (!localParse.IsSuccess && !cloudParse.IsSuccess)
            return new ConflictResult(saveFileName, ResolutionOutcome.CloudWins, WinReason.CloudCorrupt,
                null, null, null, null);
        if (!localParse.IsSuccess)
            return new ConflictResult(saveFileName, ResolutionOutcome.CloudWins, WinReason.LocalCorrupt,
                null, null, null, null);
        if (!cloudParse.IsSuccess)
            return new ConflictResult(saveFileName, ResolutionOutcome.LocalWins, WinReason.CloudCorrupt,
                null, null, null, null);

        var localAnalysis = SaveFileAnalyzer.AnalyzeCurrentRun(localParse.Value!);
        var cloudAnalysis = SaveFileAnalyzer.AnalyzeCurrentRun(cloudParse.Value!);
        var cmp = SaveFileAnalyzer.CompareCurrentRun(localAnalysis, cloudAnalysis);

        var (outcome, reason) = cmp switch
        {
            CompareResult.LeftWins => (ResolutionOutcome.LocalWins, WinReason.HeuristicWin),
            CompareResult.RightWins => (ResolutionOutcome.CloudWins, WinReason.HeuristicWin),
            _ => (ResolutionOutcome.CloudWins, WinReason.TieBreakCloudWins)
        };

        return new ConflictResult(saveFileName, outcome, reason,
            null, null, localAnalysis, cloudAnalysis);
    }

    private static bool IsValidJson(string text)
    {
        var trimmed = text.TrimStart();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[');
    }
}
