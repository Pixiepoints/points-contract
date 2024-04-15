using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Kernel.Blockchain.Application;
using AElf.Types;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Points.Contracts.Point;

public partial class PointsContractTests
{
    [Fact]
    public async Task AddDappTests()
    {
        await Initialize();
        var dappId = await AddDapp();

        var getResult = await PointsContractStub.GetDappInformation.CallAsync(new GetDappInformationInput
        {
            DappId = dappId
        });
        getResult.DappInfo.DappAdmin.ShouldBe(DefaultAddress);
        getResult.DappInfo.OfficialDomain.ShouldBe(DefaultOfficialDomain);
    }

    [Fact]
    public async Task AddDappTests_Fail()
    {
        var result = await PointsContractStub.AddDapp.SendWithExceptionAsync(new AddDappInput());
        result.TransactionResult.Error.ShouldContain("Not initialized.");

        await Initialize();

        result = await PointsContractUserStub.AddDapp.SendWithExceptionAsync(new AddDappInput());
        result.TransactionResult.Error.ShouldContain("No permission.");

        var input = new AddDappInput
        {
            DappAdmin = DefaultAddress,
            OfficialDomain = DefaultOfficialDomain,
        };

        input.OfficialDomain = "";
        result = await PointsContractStub.AddDapp.SendWithExceptionAsync(input);
        result.TransactionResult.Error.ShouldContain("Invalid domain.");

        input.OfficialDomain = string.Join(".", Enumerable.Repeat("abcdefghijklmnopqrstuvwxyz", 10));
        result = await PointsContractStub.AddDapp.SendWithExceptionAsync(input);
        result.TransactionResult.Error.ShouldContain("Invalid domain.");
    }

    [Fact]
    public async Task SetSelfIncreasingPointsRulesTests()
    {
        await Initialize();
        var dappId = await AddDapp();
        await CreatePoint(dappId);
        await SetSelfIncreasingPointsRules(dappId);

        var getResult = await PointsContractStub.GetSelfIncreasingPointsRule.CallAsync(
            new GetSelfIncreasingPointsRuleInput
            {
                DappId = dappId
            });
        getResult.Rule.PointName.ShouldBe(SelfIncreasingPointName);
        getResult.Rule.UserPoints.ShouldBe(10000000);
        getResult.Rule.KolPointsPercent.ShouldBe(1000000);
        getResult.Rule.InviterPointsPercent.ShouldBe(100000);
    }

    [Fact]
    public async Task SetSelfIncreasingPointsRulesTests_Fail()
    {
        var result = await PointsContractStub.SetSelfIncreasingPointsRules.SendWithExceptionAsync(
            new SetSelfIncreasingPointsRulesInput());
        result.TransactionResult.Error.ShouldContain("Not initialized.");

        await Initialize();
        var dappId = await AddDapp();

        result = await PointsContractUserStub.SetSelfIncreasingPointsRules.SendWithExceptionAsync(
            new SetSelfIncreasingPointsRulesInput());
        result.TransactionResult.Error.ShouldContain("No permission.");

        result = await PointsContractStub.SetSelfIncreasingPointsRules.SendWithExceptionAsync(
            new SetSelfIncreasingPointsRulesInput { DappId = dappId });
        result.TransactionResult.Error.ShouldContain("Invalid self-increasing points rules.");

        result = await PointsContractStub.SetSelfIncreasingPointsRules.SendWithExceptionAsync(
            new SetSelfIncreasingPointsRulesInput
            {
                DappId = dappId,
                SelfIncreasingPointsRule = new PointsRule
                {
                    PointName = DefaultPointName,
                    UserPoints = 10000000,
                    KolPointsPercent = 1000000,
                    InviterPointsPercent = 100000
                }
            });
        result.TransactionResult.Error.ShouldContain("Wrong points name input.");

        result = await PointsContractStub.SetSelfIncreasingPointsRules.SendWithExceptionAsync(
            new SetSelfIncreasingPointsRulesInput
            {
                DappId = dappId,
                SelfIncreasingPointsRule = new PointsRule
                {
                    PointName = "",
                    UserPoints = 10000000,
                    KolPointsPercent = 1000000,
                    InviterPointsPercent = 100000
                }
            });
        result.TransactionResult.Error.ShouldContain("Wrong points name input.");

        await CreatePoint(dappId);

        result = await PointsContractStub.SetSelfIncreasingPointsRules.SendWithExceptionAsync(
            new SetSelfIncreasingPointsRulesInput
            {
                DappId = dappId,
                SelfIncreasingPointsRule = new PointsRule
                {
                    PointName = DefaultPointName,
                    UserPoints = -1,
                    KolPointsPercent = 1000000,
                    InviterPointsPercent = 100000
                }
            });
        result.TransactionResult.Error.ShouldContain("Points must be greater than 0.");

        result = await PointsContractStub.SetSelfIncreasingPointsRules.SendWithExceptionAsync(
            new SetSelfIncreasingPointsRulesInput
            {
                DappId = dappId,
                SelfIncreasingPointsRule = new PointsRule
                {
                    PointName = DefaultPointName,
                    UserPoints = 10000000,
                    KolPointsPercent = -1,
                    InviterPointsPercent = 100000
                }
            });
        result.TransactionResult.Error.ShouldContain("Points must be greater than 0.");

        result = await PointsContractStub.SetSelfIncreasingPointsRules.SendWithExceptionAsync(
            new SetSelfIncreasingPointsRulesInput
            {
                DappId = dappId,
                SelfIncreasingPointsRule = new PointsRule
                {
                    PointName = DefaultPointName,
                    UserPoints = 10000000,
                    KolPointsPercent = 1000000,
                    InviterPointsPercent = -1
                }
            });
        result.TransactionResult.Error.ShouldContain("Points must be greater than 0.");

        result = await PointsContractStub.SetSelfIncreasingPointsRules.SendWithExceptionAsync(
            new SetSelfIncreasingPointsRulesInput
            {
                DappId = dappId,
                SelfIncreasingPointsRule = new PointsRule
                {
                    PointName = DefaultPointName,
                    UserPoints = 10000000,
                    KolPointsPercent = 1000000,
                }
            });
        result.TransactionResult.Error.ShouldContain("Points must be greater than 0.");
    }

    private async Task SetSelfIncreasingPointsRules(Hash dappId)
    {
        await PointsContractStub.SetSelfIncreasingPointsRules.SendAsync(new SetSelfIncreasingPointsRulesInput
        {
            DappId = dappId,
            SelfIncreasingPointsRule = new PointsRule
            {
                ActionName = SelfIncreaseActionName,
                PointName = SelfIncreasingPointName,
                UserPoints = 10000000,
                KolPointsPercent = 1000000,
                InviterPointsPercent = 100000
            }
        });
    }

    private async Task SetDappPointsRules(Hash dappId)
    {
        await PointsContractStub.SetDappPointsRules.SendAsync(new SetDappPointsRulesInput
        {
            DappId = dappId,
            DappPointsRules = new PointsRuleList
            {
                PointsRules =
                {
                    new PointsRule
                    {
                        ActionName = DefaultActionName,
                        PointName = DefaultPointName,
                        UserPoints = 10000000,
                        KolPointsPercent = 1000,
                        InviterPointsPercent = 100,
                        EnableProportionalCalculation = true
                    },
                    new PointsRule
                    {
                        ActionName = JoinActionName,
                        PointName = JoinPointName,
                        UserPoints = 20000000,
                        KolPointsPercent = 2000000,
                        InviterPointsPercent = 200000
                    }
                }
            }
        });
    }

    private async Task<Hash> AddDapp()
    {
        var input = new AddDappInput
        {
            DappAdmin = DefaultAddress,
            OfficialDomain = DefaultOfficialDomain,
            DappContractAddress = DefaultAddress
        };
        var result = await PointsContractStub.AddDapp.SendAsync(input);
        var blockchainService = Application.ServiceProvider.GetRequiredService<IBlockchainService>();
        var previousBlockHash = (await blockchainService.GetBlockByHashAsync(result.TransactionResult.BlockHash)).Header
            .PreviousBlockHash;
        return HashHelper.ConcatAndCompute(previousBlockHash, result.TransactionResult.TransactionId,
            HashHelper.ComputeFrom(input));
    }

    [Fact]
    public async Task CreatePointListTest()
    {
        await Initialize();
        var dappId = await AddDapp();
        var pointList = new List<PointInfo>();
        pointList.Add(new PointInfo
        {
            TokenName = DefaultPointName,
            Decimals = 8
        });
        pointList.Add(new PointInfo
        {
            TokenName = JoinPointName,
            Decimals = 8
        });
        pointList.Add(new PointInfo
        {
            TokenName = SelfIncreasingPointName,
            Decimals = 8
        });
        await PointsContractStub.CreatePointList.SendAsync(new CreatePointListInput
        {
            DappId = dappId,
            PointList = { pointList }
        });
        var point = await PointsContractStub.GetPoint.CallAsync(new GetPointInput
        {
            DappId = dappId,
            PointsName = SelfIncreasingPointName
        });
        point.Decimals.ShouldBe(8);
        point.TokenName.ShouldBe(SelfIncreasingPointName);
    }
}