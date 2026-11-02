using FluentAssertions;
using SGSFramework.Core.Abstractions.Entities.ReportCategories;
using SGSFramework.Core.Abstractions.ReportAccess;
using Xunit;

namespace SGSFramework.Core.Tests.ReportAccess;

public class ReportGrantGovernanceRulesTests
{
    [Theory]
    [InlineData(ReportActions.View | ReportActions.Edit, ReportDataScope.Assigned, ConfidentialityLevel.Normal, false)]
    [InlineData(ReportActions.View | ReportActions.Review, ReportDataScope.Lab, ConfidentialityLevel.Confidential, false)]
    [InlineData(ReportActions.View | ReportActions.Download, ReportDataScope.Lab, ConfidentialityLevel.Normal, false)]
    [InlineData(ReportActions.View | ReportActions.Sign, ReportDataScope.Lab, ConfidentialityLevel.Normal, true)]
    [InlineData(ReportActions.Void, ReportDataScope.Lab, ConfidentialityLevel.Normal, true)]
    [InlineData(ReportActions.ViewBlindIdentity, ReportDataScope.Assigned, ConfidentialityLevel.Normal, true)]
    [InlineData(ReportActions.View, ReportDataScope.Global, ConfidentialityLevel.Normal, true)]
    [InlineData(ReportActions.View, ReportDataScope.Lab, ConfidentialityLevel.Restricted, true)]
    public void Sensitive_grants_require_approval(ReportActions actions, ReportDataScope scope, ConfidentialityLevel max, bool expected)
    {
        ReportGrantGovernanceRules.RequiresApproval(actions, scope, max).Should().Be(expected);
    }
}
