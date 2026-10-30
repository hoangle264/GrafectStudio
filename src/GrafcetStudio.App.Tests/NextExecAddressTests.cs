using GrafcetStudio.Domain.Models;
using Xunit;

namespace GrafcetStudio.App.Tests;

public class NextExecAddressTests
{
    [Theory]
    [InlineData("MR300", "MR301")]
    [InlineData("@MR300", "@MR301")]
    [InlineData("MR099", "MR100")]
    [InlineData("DM0.2", "DM0.3")]
    [InlineData("DM10.9", "DM10.10")]
    [InlineData("ActiveWord.3", "ActiveWord.4")]
    [InlineData("Auto_Active_1.0", "Auto_Active_1.1")]
    [InlineData("\"Auto_Active\".%X3", "\"Auto_Active\".%X4")]
    [InlineData("Db.Member.%X7", "Db.Member.%X8")]
    [InlineData("Auto_Step09", "Auto_Step10")]
    [InlineData("ST_Auto.Step3", "ST_Auto.Step4")]
    [InlineData("\"Step_10\"", "\"Step_11\"")]
    public void IncrementExecAddress_HandlesAddressesAndLabels(string address, string expected)
    {
        Assert.Equal(expected, ResolvedStep.IncrementExecAddress(address));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("AutoEnd")]
    public void IncrementExecAddress_ReturnsNullWithoutNumber(string? address)
    {
        Assert.Null(ResolvedStep.IncrementExecAddress(address));
    }
}
