using FinFlow.Api.Endpoints;
using FinFlow.Api.Entities;
using Xunit;

namespace FinFlow.Tests;

public class RuleEndpointsTests
{
    private static RuleEndpoints.RuleRequest Req(
        string pattern = "REWE", int? categoryId = 1,
        ClassificationRuleStatus status = ClassificationRuleStatus.Auto) =>
        new(pattern, categoryId, status, Priority: 100, IsActive: true);

    [Fact]
    public void Validate_NonInternalTransfer_NoCategory_ReturnsError()
    {
        var error = RuleEndpoints.Validate(Req(categoryId: null, status: ClassificationRuleStatus.Auto));

        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_InternalTransfer_WithCategory_ReturnsError()
    {
        var error = RuleEndpoints.Validate(Req(categoryId: 1, status: ClassificationRuleStatus.InternalTransfer));

        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_InternalTransfer_NoCategory_ReturnsNull()
    {
        var error = RuleEndpoints.Validate(Req(categoryId: null, status: ClassificationRuleStatus.InternalTransfer));

        Assert.Null(error);
    }

    [Fact]
    public void Validate_Auto_WithCategory_ReturnsNull()
    {
        var error = RuleEndpoints.Validate(Req(categoryId: 1, status: ClassificationRuleStatus.Auto));

        Assert.Null(error);
    }

    [Fact]
    public void Validate_EmptyPattern_ReturnsError()
    {
        var error = RuleEndpoints.Validate(Req(pattern: "  "));

        Assert.NotNull(error);
    }
}
