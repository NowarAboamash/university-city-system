using HousingService.Domain.Enums;
using HousingService.DTOs;

namespace HousingService.Tests;

public class HousingRequestFilterTests
{
    private static readonly PaginationParams Page = new() { PageNumber = 1, PageSize = 50 };

    private static (TestContext ctx, int cycleId, int govId) Setup()
    {
        var ctx = new TestContext();
        var cycle = ctx.AddOpenCycle(1000);
        var gov = ctx.AddGovernorate(1000);
        return (ctx, cycle.Id, gov.Id);
    }

    [Fact]
    public async Task Filter_IsPaid_SplitsPaidAndUnpaid()
    {
        var (ctx, cycle, gov) = Setup();
        using var _ = ctx;
        ctx.AddRequest(1000, "s-paid", cycle, gov, Gender.Male, isPaid: true, decisionStatus: AdmissionDecisionStatus.Accepted);
        ctx.AddRequest(1001, "s-unpaid-1", cycle, gov, Gender.Male, decisionStatus: AdmissionDecisionStatus.Accepted);
        ctx.AddRequest(1002, "s-unpaid-2", cycle, gov, Gender.Male);

        var paid = await ctx.RequestService.GetAllAsync(new HousingRequestFilterParams { IsPaid = true }, Page);
        var unpaid = await ctx.RequestService.GetAllAsync(new HousingRequestFilterParams { IsPaid = false }, Page);

        Assert.Equal(1, paid.TotalCount);
        Assert.Equal("s-paid", Assert.Single(paid.Items).StudentId);
        Assert.Equal(2, unpaid.TotalCount);
        Assert.All(unpaid.Items, r => Assert.False(r.IsPaid));
    }

    [Fact]
    public async Task Filter_AcademicLevel_ExactMatch()
    {
        var (ctx, cycle, gov) = Setup();
        using var _ = ctx;
        ctx.AddRequest(1000, "a", cycle, gov, Gender.Male, academicLevel: AcademicLevel.First);
        ctx.AddRequest(1001, "b", cycle, gov, Gender.Male, academicLevel: AcademicLevel.Third);
        ctx.AddRequest(1002, "c", cycle, gov, Gender.Male, academicLevel: AcademicLevel.Third);

        var result = await ctx.RequestService.GetAllAsync(new HousingRequestFilterParams { AcademicLevel = AcademicLevel.Third }, Page);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, r => Assert.Equal(AcademicLevel.Third, r.AcademicLevel));
    }

    [Fact]
    public async Task Filter_Gender_ExactMatch()
    {
        var (ctx, cycle, gov) = Setup();
        using var _ = ctx;
        ctx.AddRequest(1000, "m", cycle, gov, Gender.Male);
        ctx.AddRequest(1001, "f1", cycle, gov, Gender.Female);
        ctx.AddRequest(1002, "f2", cycle, gov, Gender.Female);

        var result = await ctx.RequestService.GetAllAsync(new HousingRequestFilterParams { Gender = Gender.Female }, Page);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, r => Assert.Equal(Gender.Female, r.Gender));
    }

    [Fact]
    public async Task Filter_StudentIds_KeepsOnlyListedStudents()
    {
        var (ctx, cycle, gov) = Setup();
        using var _ = ctx;
        ctx.AddRequest(1000, "s1", cycle, gov, Gender.Male);
        ctx.AddRequest(1001, "s2", cycle, gov, Gender.Male);
        ctx.AddRequest(1002, "s3", cycle, gov, Gender.Male);

        var result = await ctx.RequestService.GetAllAsync(
            new HousingRequestFilterParams { StudentIds = ["s1", "s3", "missing"] }, Page);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(new[] { "s1", "s3" }, result.Items.Select(r => r.StudentId).OrderBy(x => x));
    }

    [Fact]
    public async Task Filter_EmptyStudentIds_Ignored()
    {
        var (ctx, cycle, gov) = Setup();
        using var _ = ctx;
        ctx.AddRequest(1000, "s1", cycle, gov, Gender.Male);
        ctx.AddRequest(1001, "s2", cycle, gov, Gender.Male);

        var result = await ctx.RequestService.GetAllAsync(new HousingRequestFilterParams { StudentIds = [] }, Page);

        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task Filter_IsGrouped_SplitsGroupedAndIndividual()
    {
        var (ctx, cycle, gov) = Setup();
        using var _ = ctx;
        var group = ctx.AddGroup(1000, "leader", cycle);
        ctx.AddRequest(1000, "leader", cycle, gov, Gender.Male, housingGroupId: group.Id);
        ctx.AddRequest(1001, "solo-1", cycle, gov, Gender.Male);
        ctx.AddRequest(1002, "solo-2", cycle, gov, Gender.Male);

        var grouped = await ctx.RequestService.GetAllAsync(new HousingRequestFilterParams { IsGrouped = true }, Page);
        var individual = await ctx.RequestService.GetAllAsync(new HousingRequestFilterParams { IsGrouped = false }, Page);

        Assert.Equal("leader", Assert.Single(grouped.Items).StudentId);
        Assert.Equal(2, individual.TotalCount);
        Assert.All(individual.Items, r => Assert.Null(r.HousingGroupId));
    }

    [Fact]
    public async Task Filter_HasSpecialNeeds_Only()
    {
        var (ctx, cycle, gov) = Setup();
        using var _ = ctx;
        ctx.AddRequest(1000, "sn", cycle, gov, Gender.Male, hasSpecialNeeds: true);
        ctx.AddRequest(1001, "no", cycle, gov, Gender.Male);

        var result = await ctx.RequestService.GetAllAsync(new HousingRequestFilterParams { HasSpecialNeeds = true }, Page);

        Assert.Equal("sn", Assert.Single(result.Items).StudentId);
    }

    [Fact]
    public async Task Filter_SubmittedDateRange_Inclusive()
    {
        var (ctx, cycle, gov) = Setup();
        using var _ = ctx;
        var day0 = ctx.Clock.GetUtcNow().UtcDateTime;
        ctx.AddRequest(1000, "d0", cycle, gov, Gender.Male);
        ctx.Clock.Advance(TimeSpan.FromDays(2));
        ctx.AddRequest(1001, "d2", cycle, gov, Gender.Male);
        ctx.Clock.Advance(TimeSpan.FromDays(2));
        ctx.AddRequest(1002, "d4", cycle, gov, Gender.Male);

        var result = await ctx.RequestService.GetAllAsync(new HousingRequestFilterParams
        {
            SubmittedFrom = day0.AddDays(1),
            SubmittedTo = day0.AddDays(3)
        }, Page);

        Assert.Equal("d2", Assert.Single(result.Items).StudentId);
    }

    [Fact]
    public async Task Filter_Combined_AppliedAsAnd()
    {
        var (ctx, cycle, gov) = Setup();
        using var _ = ctx;
        ctx.AddRequest(1000, "match", cycle, gov, Gender.Female, isPaid: true, academicLevel: AcademicLevel.Second, decisionStatus: AdmissionDecisionStatus.Accepted);
        ctx.AddRequest(1001, "wrong-paid", cycle, gov, Gender.Female, academicLevel: AcademicLevel.Second);
        ctx.AddRequest(1002, "wrong-level", cycle, gov, Gender.Female, isPaid: true, academicLevel: AcademicLevel.Fifth, decisionStatus: AdmissionDecisionStatus.Accepted);
        ctx.AddRequest(1003, "wrong-gender", cycle, gov, Gender.Male, isPaid: true, academicLevel: AcademicLevel.Second, decisionStatus: AdmissionDecisionStatus.Accepted);

        var result = await ctx.RequestService.GetAllAsync(new HousingRequestFilterParams
        {
            IsPaid = true,
            AcademicLevel = AcademicLevel.Second,
            Gender = Gender.Female
        }, Page);

        Assert.Equal("match", Assert.Single(result.Items).StudentId);
    }

    [Fact]
    public async Task Filter_None_ReturnsAllNewestFirst()
    {
        var (ctx, cycle, gov) = Setup();
        using var _ = ctx;
        ctx.AddRequest(1000, "old", cycle, gov, Gender.Male);
        ctx.Clock.Advance(TimeSpan.FromHours(1));
        ctx.AddRequest(1001, "new", cycle, gov, Gender.Male);

        var result = await ctx.RequestService.GetAllAsync(new HousingRequestFilterParams(), Page);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal("new", result.Items[0].StudentId);
    }
}
