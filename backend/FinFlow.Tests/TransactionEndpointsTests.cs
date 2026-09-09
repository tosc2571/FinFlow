using System.Text.RegularExpressions;
using FinFlow.Api.Endpoints;
using FinFlow.Api.Entities;
using FinFlow.Api.Services;
using Xunit;

namespace FinFlow.Tests;

public class TransactionEndpointsTests
{
    private static TransactionEndpoints.TransactionDto Dto(
        ClassificationStatus status, string? counterpartyName = "REWE SAGT DANKE", string? purpose = "Einkauf") =>
        new(
            Id: 1, SourceBank: "dkb", BookingDate: null, ValueDate: null, Amount: -10m, Currency: "EUR",
            CounterpartyName: counterpartyName, CounterpartyIban: null, CounterpartyBic: null, Purpose: purpose,
            BookingType: null, Balance: null, CategoryId: 1, CategoryName: "Lebensmittel",
            ClassificationStatus: status, ImportBatchId: 1, ContractId: null);

    private static RuleSet MatchingRuleSet() => new([
        (new Regex("REWE", RegexOptions.IgnoreCase), new RuleSet.Match(RuleId: 5, Pattern: "REWE", CategoryId: 1, ClassificationStatus.Auto)),
    ]);

    [Theory]
    [InlineData(ClassificationStatus.Auto)]
    [InlineData(ClassificationStatus.Ignored)]
    public void WithMatchedRule_RuleDrivenStatus_AttachesMatchedRule(ClassificationStatus status)
    {
        TransactionEndpoints.TransactionDto result = TransactionEndpoints.WithMatchedRule(Dto(status), MatchingRuleSet());

        Assert.Equal(5, result.MatchedRuleId);
        Assert.Equal("REWE", result.MatchedRulePattern);
    }

    [Theory]
    [InlineData(ClassificationStatus.ManualOverride)]
    [InlineData(ClassificationStatus.InternalTransfer)]
    [InlineData(ClassificationStatus.NeedsReview)]
    public void WithMatchedRule_NotRuleDrivenStatus_LeavesMatchedRuleNull(ClassificationStatus status)
    {
        // The pattern would technically match this transaction's text, but since the status
        // wasn't produced by that rule (manually set, or no rule matched at all for NeedsReview
        // here), showing it as "the" matched rule would be misleading.
        TransactionEndpoints.TransactionDto result = TransactionEndpoints.WithMatchedRule(Dto(status), MatchingRuleSet());

        Assert.Null(result.MatchedRuleId);
        Assert.Null(result.MatchedRulePattern);
    }

    [Fact]
    public void WithMatchedRule_AutoStatusButNoRuleMatchesAnymore_LeavesMatchedRuleNull()
    {
        RuleSet empty = new([]);

        TransactionEndpoints.TransactionDto result = TransactionEndpoints.WithMatchedRule(Dto(ClassificationStatus.Auto), empty);

        Assert.Null(result.MatchedRuleId);
    }
}
