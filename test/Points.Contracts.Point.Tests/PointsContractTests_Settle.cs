using System.Threading.Tasks;
using AElf.Types;
using Shouldly;
using Xunit;

namespace Points.Contracts.Point;

public partial class PointsContractTests
{
    [Fact]
    public async Task SettleTests()
    {
        var dappId = await JoinTests();
        await CreatePointForSettle(dappId);
        await SetDappPointsRulesForSettle(dappId);
        var domain = "abc.com";
        await PointsContractStub.Settle.SendAsync(new SettleInput
        {
            DappId = dappId,
            ActionName = "Adpot",
            UserAddress = User2Address,
            UserPointsValue = 131400
        });
        var getBalanceResult = await PointsContractStub.GetPointsBalance.CallAsync(new GetPointsBalanceInput
        {
            DappId = dappId,
            Address = User2Address,
            Domain = domain,
            IncomeSourceType = IncomeSourceType.User,
            PointName = "XPSGR-5"
        });
        getBalanceResult.PointName.ShouldBe("XPSGR-5");
        getBalanceResult.BalanceValue.ShouldBe(131400);
        getBalanceResult = await PointsContractStub.GetPointsBalance.CallAsync(new GetPointsBalanceInput
        {
            DappId = dappId,
            Address = UserAddress,
            Domain = domain,
            IncomeSourceType = IncomeSourceType.Kol,
            PointName = "XPSGR-5"
        });
        getBalanceResult.PointName.ShouldBe("XPSGR-5");
        getBalanceResult.BalanceValue.ShouldBe(131400*1600/10000);
        getBalanceResult = await PointsContractStub.GetPointsBalance.CallAsync(new GetPointsBalanceInput
        {
            DappId = dappId,
            Address = DefaultAddress,
            Domain = domain,
            IncomeSourceType = IncomeSourceType.Inviter,
            PointName = "XPSGR-5"
        });
        getBalanceResult.PointName.ShouldBe("XPSGR-5");
        getBalanceResult.BalanceValue.ShouldBe(131400*800/10000);
        await PointsContractStub.Settle.SendAsync(new SettleInput
        {
            DappId = dappId,
            ActionName = "Reroll",
            UserAddress = User2Address,
            UserPointsValue = 191900
        });
        getBalanceResult = await PointsContractStub.GetPointsBalance.CallAsync(new GetPointsBalanceInput
        {
            DappId = dappId,
            Address = User2Address,
            Domain = domain,
            IncomeSourceType = IncomeSourceType.User,
            PointName = "XPSGR-6"
        });
        getBalanceResult.PointName.ShouldBe("XPSGR-6");
        getBalanceResult.BalanceValue.ShouldBe(191900);
        getBalanceResult = await PointsContractStub.GetPointsBalance.CallAsync(new GetPointsBalanceInput
        {
            DappId = dappId,
            Address = UserAddress,
            Domain = domain,
            IncomeSourceType = IncomeSourceType.Kol,
            PointName = "XPSGR-6"
        });
        getBalanceResult.PointName.ShouldBe("XPSGR-6");
        getBalanceResult.BalanceValue.ShouldBe(191900*1600/10000);
        getBalanceResult = await PointsContractStub.GetPointsBalance.CallAsync(new GetPointsBalanceInput
        {
            DappId = dappId,
            Address = DefaultAddress,
            Domain = domain,
            IncomeSourceType = IncomeSourceType.Inviter,
            PointName = "XPSGR-6"
        });
        getBalanceResult.PointName.ShouldBe("XPSGR-6");
        getBalanceResult.BalanceValue.ShouldBe(191900*800/10000);
    }
    
    private async Task SetDappPointsRulesForSettle(Hash dappId)
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
                        ActionName = "Adpot",
                        PointName = "XPSGR-5",
                        UserPoints = 0,
                        KolPointsPercent = 1600,
                        InviterPointsPercent = 800,
                        EnableProportionalCalculation = true
                    },
                    new PointsRule
                    {
                        ActionName = "Reroll",
                        PointName = "XPSGR-6",
                        UserPoints = 0,
                        KolPointsPercent = 1600,
                        InviterPointsPercent = 800,
                        EnableProportionalCalculation = true
                    },
                    new PointsRule
                    {
                        ActionName = "Trade",
                        PointName = "XPSGR-7",
                        UserPoints = 0,
                        KolPointsPercent = 1600,
                        InviterPointsPercent = 800,
                        EnableProportionalCalculation = true
                    }
                }
            }
        });
        var points = await PointsContractStub.GetDappInformation.CallAsync(new GetDappInformationInput
        {
            DappId = dappId
        });
        points.DappInfo.DappsPointRules.PointsRules.Count.ShouldBe(3);
        points.DappInfo.DappsPointRules.PointsRules[0].ActionName.ShouldBe("Adpot");
        points.DappInfo.DappsPointRules.PointsRules[0].PointName.ShouldBe("XPSGR-5");
        points.DappInfo.DappsPointRules.PointsRules[0].UserPoints.ShouldBe(0);
        points.DappInfo.DappsPointRules.PointsRules[0].KolPointsPercent.ShouldBe(1600);
        points.DappInfo.DappsPointRules.PointsRules[0].InviterPointsPercent.ShouldBe(800);
        points.DappInfo.DappsPointRules.PointsRules[1].ActionName.ShouldBe("Reroll");
        points.DappInfo.DappsPointRules.PointsRules[1].PointName.ShouldBe("XPSGR-6");
        points.DappInfo.DappsPointRules.PointsRules[1].UserPoints.ShouldBe(0);
        points.DappInfo.DappsPointRules.PointsRules[1].KolPointsPercent.ShouldBe(1600);
        points.DappInfo.DappsPointRules.PointsRules[1].InviterPointsPercent.ShouldBe(800);
    }
}