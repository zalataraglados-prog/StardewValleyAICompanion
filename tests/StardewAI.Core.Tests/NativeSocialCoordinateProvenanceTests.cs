using System.Text.Json;

namespace StardewAI.Core.Tests;

public sealed class NativeSocialCoordinateProvenanceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void GetParameterInt_ParsesValidInteger()
    {
        var psScript = ExtractHelperPs1("Get-ParameterInt");
        var testPs = psScript + @"
$params = @(
    [PSCustomObject]@{ name = 'test_int'; value = '42' }
)
$result = Get-ParameterInt -Parameters $params -Name 'test_int'
if ($result -ne 42) { throw ""Expected 42, got $result"" }
Write-Output 'OK'
";
        var result = InvokePs1(testPs);
        Assert.Contains("OK", result, StringComparison.Ordinal);
    }

    [Fact]
    public void GetParameterInt_AcceptsTileZero()
    {
        var psScript = ExtractHelperPs1("Get-ParameterInt");
        var testPs = psScript + @"
$params = @(
    [PSCustomObject]@{ name = 'tile'; value = '0' }
)
$result = Get-ParameterInt -Parameters $params -Name 'tile'
if ($result -ne 0) { throw ""Expected 0, got $result"" }
Write-Output 'OK'
";
        var result = InvokePs1(testPs);
        Assert.Contains("OK", result, StringComparison.Ordinal);
    }

    [Fact]
    public void GetParameterInt_RejectsNonInteger()
    {
        var psScript = ExtractHelperPs1("Get-ParameterInt");
        var testPs = psScript + @"
$params = @(
    [PSCustomObject]@{ name = 'bad'; value = 'not_an_int' }
)
try {
    $null = Get-ParameterInt -Parameters $params -Name 'bad'
    Write-Output 'FAILED_SHOULD_HAVE_THROWN'
} catch {
    if ($_.Exception.Message -like '*must be a valid integer*') {
        Write-Output 'OK'
    } else {
        Write-Output ""UNEXPECTED: $($_.Exception.Message)""
    }
}
";
        var result = InvokePs1(testPs);
        Assert.Contains("OK", result, StringComparison.Ordinal);
    }

    [Fact]
    public void GetParameterInt_RejectsMissingParameter()
    {
        var psScript = ExtractHelperPs1("Get-ParameterInt");
        var testPs = psScript + @"
$params = @(
    [PSCustomObject]@{ name = 'other'; value = '1' }
)
try {
    $null = Get-ParameterInt -Parameters $params -Name 'missing'
    Write-Output 'FAILED_SHOULD_HAVE_THROWN'
} catch {
    if ($_.Exception.Message -like '*not found*') {
        Write-Output 'OK'
    } else {
        Write-Output ""UNEXPECTED: $($_.Exception.Message)""
    }
}
";
        var result = InvokePs1(testPs);
        Assert.Contains("OK", result, StringComparison.Ordinal);
    }

    [Fact]
    public void GetCandidateIdFromPreconditions_ExtractsCorrectId()
    {
        var psScript = ExtractHelperPs1("Get-CandidateIdFromPreconditions");
        var testPs = psScript + @"
$preconditions = @('candidate_id:abc-123', 'other:foo')
$id = Get-CandidateIdFromPreconditions -Preconditions $preconditions
if ($id -ne 'abc-123') { throw ""Expected 'abc-123', got '$id'"" }
Write-Output 'OK'
";
        var result = InvokePs1(testPs);
        Assert.Contains("OK", result, StringComparison.Ordinal);
    }

    [Fact]
    public void GetCandidateIdFromPreconditions_RejectsZeroMatches()
    {
        var psScript = ExtractHelperPs1("Get-CandidateIdFromPreconditions");
        var testPs = psScript + @"
$preconditions = @('other:foo', 'bar:baz')
try {
    $null = Get-CandidateIdFromPreconditions -Preconditions $preconditions
    Write-Output 'FAILED_SHOULD_HAVE_THROWN'
} catch {
    if ($_.Exception.Message -like '*No candidate_id precondition*') {
        Write-Output 'OK'
    } else {
        Write-Output ""UNEXPECTED: $($_.Exception.Message)""
    }
}
";
        var result = InvokePs1(testPs);
        Assert.Contains("OK", result, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptCandidateSelectionIsByPlanCandidateId()
    {
        var source = File.ReadAllText(FindRepositoryFile("scripts", "Invoke-RuntimeNativeSocialSmoke.ps1"));

        Assert.Contains("Get-CandidateIdFromPreconditions -Preconditions $socialStep.preconditions", source, StringComparison.Ordinal);
        Assert.Contains("matchingRanked", source, StringComparison.Ordinal);
        Assert.Contains("candidate_id -eq", source, StringComparison.Ordinal);
        Assert.Contains("Expected exactly 1 ranked candidate matching plan candidate_id", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptHasGetParameterIntHelper()
    {
        var source = File.ReadAllText(FindRepositoryFile("scripts", "Invoke-RuntimeNativeSocialSmoke.ps1"));

        Assert.Contains("function Get-ParameterInt", source, StringComparison.Ordinal);
        Assert.Contains("must be a valid integer", source, StringComparison.Ordinal);
        Assert.Contains("[int]::TryParse", source, StringComparison.Ordinal);
        Assert.Contains("Get-ParameterInt -Parameters", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptCoordinateCrossCheckBetweenCandidateAndPlan()
    {
        var source = File.ReadAllText(FindRepositoryFile("scripts", "Invoke-RuntimeNativeSocialSmoke.ps1"));

        Assert.Contains("Candidate location", source, StringComparison.Ordinal);
        Assert.Contains("does not match move plan target_location", source, StringComparison.Ordinal);
        Assert.Contains("does not match social plan target_location", source, StringComparison.Ordinal);
        Assert.Contains("Move target_tile_x", source, StringComparison.Ordinal);
        Assert.Contains("does not match candidate stand_tile_x", source, StringComparison.Ordinal);
        Assert.Contains("does not match candidate stand_tile_y", source, StringComparison.Ordinal);
        Assert.Contains("does not match candidate npc_tile_x", source, StringComparison.Ordinal);
        Assert.Contains("does not match candidate npc_tile_y", source, StringComparison.Ordinal);
        Assert.Contains("Candidate top-level tile_x", source, StringComparison.Ordinal);
        Assert.Contains("does not match stand_tile_x", source, StringComparison.Ordinal);
        Assert.Contains("does not match stand_tile_y", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptQueueNormalizedParamsIncludeCoordinates()
    {
        var source = File.ReadAllText(FindRepositoryFile("scripts", "Invoke-RuntimeNativeSocialSmoke.ps1"));

        Assert.Contains("Queue social item target_location mismatch with candidate", source, StringComparison.Ordinal);
        Assert.Contains("Queue social item npc_tile_x mismatch with candidate", source, StringComparison.Ordinal);
        Assert.Contains("Queue social item npc_tile_y mismatch with candidate", source, StringComparison.Ordinal);
        Assert.Contains("Queue social item stand_tile_x mismatch with candidate", source, StringComparison.Ordinal);
        Assert.Contains("Queue social item stand_tile_y mismatch with candidate", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptQueueItemIdCrossCheck()
    {
        var source = File.ReadAllText(FindRepositoryFile("scripts", "Invoke-RuntimeNativeSocialSmoke.ps1"));

        Assert.Contains("Execution result queue_item_id", source, StringComparison.Ordinal);
        Assert.Contains("does not match queue social item queue_item_id", source, StringComparison.Ordinal);
        Assert.Contains("$step.queue_item_id", source, StringComparison.Ordinal);
        Assert.Contains("$qSocialItem.queue_item_id", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptTalkTileFacingRequiredNotNull()
    {
        var source = File.ReadAllText(FindRepositoryFile("scripts", "Invoke-RuntimeNativeSocialSmoke.ps1"));

        Assert.Contains("Talk execution missing npc_tile_x_before", source, StringComparison.Ordinal);
        Assert.Contains("Talk execution missing npc_tile_y_before", source, StringComparison.Ordinal);
        Assert.Contains("Talk execution missing player_tile_x_before", source, StringComparison.Ordinal);
        Assert.Contains("Talk execution missing player_tile_y_before", source, StringComparison.Ordinal);
        Assert.Contains("Talk execution missing player_facing_before", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptGiftHasTileFacingAdjacencyChecks()
    {
        var source = File.ReadAllText(FindRepositoryFile("scripts", "Invoke-RuntimeNativeSocialSmoke.ps1"));

        Assert.Contains("Gift execution missing npc_tile_x_before", source, StringComparison.Ordinal);
        Assert.Contains("Gift execution missing npc_tile_y_before", source, StringComparison.Ordinal);
        Assert.Contains("Gift execution missing player_tile_x_before", source, StringComparison.Ordinal);
        Assert.Contains("Gift execution missing player_tile_y_before", source, StringComparison.Ordinal);
        Assert.Contains("Gift execution missing player_facing_before", source, StringComparison.Ordinal);
        Assert.Contains("Gift player not Manhattan-adjacent to NPC", source, StringComparison.Ordinal);
        Assert.Contains("Gift player facing", source, StringComparison.Ordinal);
        Assert.Contains("does not point toward NPC", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptExecutionTileCrossCheckAgainstCandidate()
    {
        var source = File.ReadAllText(FindRepositoryFile("scripts", "Invoke-RuntimeNativeSocialSmoke.ps1"));

        Assert.Contains("Execution npc_tile_x_before", source, StringComparison.Ordinal);
        Assert.Contains("mismatch with candidate npc_tile_x", source, StringComparison.Ordinal);
        Assert.Contains("Execution npc_tile_y_before", source, StringComparison.Ordinal);
        Assert.Contains("mismatch with candidate npc_tile_y", source, StringComparison.Ordinal);
        Assert.Contains("Execution player_tile_x_before", source, StringComparison.Ordinal);
        Assert.Contains("mismatch with candidate stand tile", source, StringComparison.Ordinal);
        Assert.Contains("Execution player_tile_y_before", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptGiftQueueParamsIncludeSlotItemStack()
    {
        var source = File.ReadAllText(FindRepositoryFile("scripts", "Invoke-RuntimeNativeSocialSmoke.ps1"));

        Assert.Contains("Gift queue slot_index mismatch with candidate", source, StringComparison.Ordinal);
        Assert.Contains("Gift queue qualified_item_id mismatch with candidate", source, StringComparison.Ordinal);
        Assert.Contains("Gift queue item_stack_before mismatch with candidate", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptGiftHasCoordinateCrossChecks()
    {
        var source = File.ReadAllText(FindRepositoryFile("scripts", "Invoke-RuntimeNativeSocialSmoke.ps1"));

        Assert.Contains("Gift candidate location", source, StringComparison.Ordinal);
        Assert.Contains("Gift move target_tile_x", source, StringComparison.Ordinal);
        Assert.Contains("Gift social target_tile_x", source, StringComparison.Ordinal);
        Assert.Contains("Gift candidate top-level tile_x", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptGiftCandidateSelectionIsByPlanId()
    {
        var source = File.ReadAllText(FindRepositoryFile("scripts", "Invoke-RuntimeNativeSocialSmoke.ps1"));

        Assert.Contains("Gift: expected exactly 1 ranked candidate matching plan candidate_id", source, StringComparison.Ordinal);
        Assert.Contains("Gift ranked candidate", source, StringComparison.Ordinal);
        Assert.Contains("is not available", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptGiftQueueItemIdCrossCheck()
    {
        var source = File.ReadAllText(FindRepositoryFile("scripts", "Invoke-RuntimeNativeSocialSmoke.ps1"));

        Assert.Contains("Gift execution result queue_item_id", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptCandidateTileXYEqualsStandTile()
    {
        var source = File.ReadAllText(FindRepositoryFile("scripts", "Invoke-RuntimeNativeSocialSmoke.ps1"));

        Assert.Contains("Candidate top-level tile_x", source, StringComparison.Ordinal);
        Assert.Contains("does not match stand_tile_x", source, StringComparison.Ordinal);
        Assert.Contains("does not match stand_tile_y", source, StringComparison.Ordinal);
    }

    private static string ExtractHelperPs1(string functionName)
    {
        var scriptPath = FindRepositoryFile("scripts", "Invoke-RuntimeNativeSocialSmoke.ps1");
        var source = File.ReadAllText(scriptPath);

        var result = new System.Text.StringBuilder();

        if (functionName == "Get-ParameterInt")
        {
            var paramValueStart = source.IndexOf("function Get-ParameterValue", StringComparison.Ordinal);
            Assert.True(paramValueStart >= 0, "Get-ParameterValue function not found");
            var nextAfterParamValue = source.IndexOf("function ", paramValueStart + 1, StringComparison.Ordinal);
            var paramValueBody = nextAfterParamValue >= 0 ? source.Substring(paramValueStart, nextAfterParamValue - paramValueStart) : source.Substring(paramValueStart);
            result.AppendLine(paramValueBody);
        }

        var fnStart = source.IndexOf("function " + functionName, StringComparison.Ordinal);
        Assert.True(fnStart >= 0, $"Function '{functionName}' not found in smoke script");

        var nextFn = source.IndexOf("function ", fnStart + 1, StringComparison.Ordinal);
        var fnBody = nextFn >= 0 ? source.Substring(fnStart, nextFn - fnStart) : source.Substring(fnStart);

        Assert.NotEmpty(fnBody);
        result.Append(fnBody);
        return result.ToString();
    }

    private static string InvokePs1(string psScript)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "stardewai_coord_test_" + Guid.NewGuid().ToString("N") + ".ps1");
        try
        {
            File.WriteAllText(tempPath, psScript, System.Text.Encoding.UTF8);

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -File \"" + tempPath + "\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            Assert.NotNull(process);

            var output = process!.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(30000);

            if (process.ExitCode != 0)
            {
                return "EXITCODE:" + process.ExitCode + " STDERR:" + error;
            }

            return output.Trim();
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate repository file", Path.Combine(parts));
    }
}
