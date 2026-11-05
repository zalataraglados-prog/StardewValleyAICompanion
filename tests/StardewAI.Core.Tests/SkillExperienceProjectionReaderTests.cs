using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class SkillExperienceProjectionReaderTests
{
    [Fact]
    public void MaterializesTheOriginalScalarBounds()
    {
        var candidate = Candidate(P("skill_experience_skill_id", "mining"),
            P("skill_experience_on_success_min", "4"), P("skill_experience_on_success_max", "8"));
        Assert.True(GrandpaDirectionDailyCandidateBinding.TryReadSkillExperienceProjection(candidate, out var rows, out var reason));
        var row = Assert.Single(rows);
        Assert.Equal("mining", row.SkillId);
        Assert.Equal(4, row.Minimum);
        Assert.Equal(8, row.Maximum);
        Assert.Empty(reason);
    }

    [Fact]
    public void MaterializesBothOriginalStructuredPropertyCasings()
    {
        var candidate = Candidate(P("expected_skill_experience_deltas_json",
            "[{\"skillId\":\"farming\",\"skillIndex\":0,\"delta\":8},{\"SkillId\":\"fishing\",\"SkillIndex\":1,\"Delta\":5}]"));
        Assert.True(GrandpaDirectionDailyCandidateBinding.TryReadSkillExperienceProjection(candidate, out var rows, out _));
        Assert.Equal(new[] { "farming", "fishing" }, rows.Select(row => row.SkillId));
        Assert.Equal(new[] { 8, 5 }, rows.Select(row => row.Minimum));
        Assert.All(rows, row => Assert.Equal(row.Minimum, row.Maximum));
    }

    [Fact]
    public void MaterializesLegacyMultiSkillBoundsWithoutDroppingZeroRows()
    {
        var candidate = Candidate(P("foraging_experience_on_success_min", "0"),
            P("foraging_experience_on_success_max", "0"), P("farming_experience_on_success_min", "8"),
            P("farming_experience_on_success_max", "8"));
        Assert.True(GrandpaDirectionDailyCandidateBinding.TryReadSkillExperienceProjection(candidate, out var rows, out _));
        Assert.Equal(2, rows.Length);
        Assert.Equal(0, rows[0].Maximum);
        Assert.Equal(8, rows[1].Minimum);
    }

    [Theory]
    [InlineData("not-json", "structured_skill_experience_json_invalid")]
    [InlineData("{}", "structured_skill_experience_not_array")]
    [InlineData("[]", "multi_skill_experience_not_positive")]
    [InlineData("[{\"skillId\":\"mining\",\"skillIndex\":0,\"delta\":5}]", "structured_skill_experience_row_invalid")]
    [InlineData("[{\"skillId\":\"farming\",\"skillIndex\":0,\"delta\":5},{\"skillId\":\"farming\",\"skillIndex\":0,\"delta\":5}]", "structured_skill_experience_row_invalid")]
    public void ReturnsTheUnchangedBindingRejection(string json, string expectedReason)
    {
        Assert.False(GrandpaDirectionDailyCandidateBinding.TryReadSkillExperienceProjection(
            Candidate(P("expected_skill_experience_deltas_json", json)), out var rows, out var reason));
        Assert.Empty(rows);
        Assert.Equal(expectedReason, reason);
    }

    [Fact]
    public void DoesNotWidenTheOldGuardForACollectionOnlyProjection()
    {
        var candidate = new PolicyEventCandidatePrediction
        {
            Parameters = new[] { P("expected_skill_id", "fishing"), P("expected_skill_experience_delta", "5") }
        };
        Assert.False(GrandpaDirectionDailyCandidateBinding.TryReadSkillExperienceProjection(candidate, out _, out var reason));
        Assert.Equal("skill_experience_projection_not_complete", reason);
    }

    private static PolicyEventCandidatePrediction Candidate(params SmallModelActionParameter[] projection) => new()
    {
        Parameters = new[] { P("skill_experience_projection_status", "exact"),
            P("skill_experience_condition", "native_success") }.Concat(projection).ToArray()
    };

    private static SmallModelActionParameter P(string name, string value) => new() { Name = name, Value = value };
}
