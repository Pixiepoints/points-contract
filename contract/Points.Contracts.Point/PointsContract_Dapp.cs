using AElf;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Points.Contracts.Point;

public partial class PointsContract
{
    public override Empty AddDapp(AddDappInput input)
    {
        AssertInitialized();
        AssertAdmin();
        AssertDomainFormat(input.OfficialDomain);

        var dappId = HashHelper.ConcatAndCompute(Context.PreviousBlockHash, Context.TransactionId,
            HashHelper.ComputeFrom(input));
        var dappInfo = new DappInfo
        {
            DappAdmin = input.DappAdmin,
            OfficialDomain = input.OfficialDomain,
            DappContractAddress = input.DappContractAddress
        };

        State.DappInfos[dappId] = dappInfo;

        Context.Fire(new DappAdded
        {
            DappId = dappId,
            DappInfo = dappInfo
        });

        return new Empty();
    }

    public override Empty SetDappPointsRules(SetDappPointsRulesInput input)
    {
        AssertInitialized();
        var dappId = input.DappId;
        AssertDappAdmin(input.DappId);

        foreach (var rule in input.DappPointsRules.PointsRules)
        {
            Assert(!string.IsNullOrEmpty(rule.PointName) && State.PointInfos[dappId][rule.PointName] != null,
                "Wrong points name input.");
            Assert(!string.IsNullOrEmpty(rule.ActionName), "ActionName cannot be empty.");
            Assert(rule.UserPoints >= 0 && rule.KolPointsPercent > 0 && rule.InviterPointsPercent > 0,
                "Points must be greater than 0.");
        }

        var info = State.DappInfos[dappId];
        State.DappInfos[dappId] = new DappInfo
        {
            DappAdmin = info.DappAdmin,
            OfficialDomain = info.OfficialDomain,
            DappsPointRules = new PointsRuleList { PointsRules = { input.DappPointsRules.PointsRules } },
            DappContractAddress = info.DappContractAddress
        };
        
        Context.Fire(new DappPointsRulesSet
        {
            DappId = input.DappId,
            DappPointsRules = input.DappPointsRules
        });

        return new Empty();
    }


    public override Empty SetSelfIncreasingPointsRules(SetSelfIncreasingPointsRulesInput input)
    {
        AssertInitialized();
        AssertDappAdmin(input.DappId);
        // Assert(State.SelfIncreasingPointsRules[input.DappId] == null, "Self-increasing points rules already set.");

        var rule = input.SelfIncreasingPointsRule;

        // If input.SelfIncreasingPointsRule is null, remove the rule
        if (rule == null)
        {
            State.SelfIncreasingPointsRules.Remove(input.DappId);
            
            Context.Fire(new SelfIncreasingPointsRulesChanged
            {
                DappId = input.DappId,
                Frequency = input.Frequency
            });
        }
        else
        {
            Assert(!string.IsNullOrEmpty(rule.PointName) && State.PointInfos[input.DappId][rule.PointName] != null,
                "Wrong points name input.");
            Assert(rule.UserPoints > 0 && rule.KolPointsPercent > 0 && rule.InviterPointsPercent > 0,
                "Points must be greater than 0.");

            State.SelfIncreasingPointsRules[input.DappId] = rule;
            
            Context.Fire(new SelfIncreasingPointsRulesChanged
            {
                DappId = input.DappId,
                PointName = rule.PointName,
                UserPoints = rule.UserPoints,
                KolPointsPercent = rule.KolPointsPercent,
                InviterPointsPercent = rule.InviterPointsPercent,
                Frequency = input.Frequency
            });
        }
        
        return new Empty();
    }

    public override Empty CreatePoint(CreatePointInput input)
    {
        AssertInitialized();
        AssertDappAdmin(input.DappId);
        AssertValidCreateInput(input.PointsName,input.Decimals,input.DappId);
        SetPoint(input.DappId, input.PointsName, input.Decimals);

        return new Empty();
    }   

    public override Empty CreatePointList(CreatePointListInput input)
    {
        AssertInitialized();
        AssertDappAdmin(input.DappId);
        Assert(input.PointList != null && input.PointList.Count > 0, "Invalid input.");
        foreach (var point in input.PointList)
        {
            AssertValidCreateInput(point.TokenName,point.Decimals,input.DappId);
            SetPoint(input.DappId, point.TokenName, point.Decimals);
        }

        return new Empty();
    }

    private void SetPoint(Hash dappId, string pointsName, int decimals)
    {
        State.PointInfos[dappId][pointsName] = new PointInfo
        {
            TokenName = pointsName,
            Decimals = decimals
        };
        Context.Fire(new PointCreated
        {
            DappId = dappId,
            TokenName = pointsName,
            Decimals = decimals
        });
    }

    private void AssertValidCreateInput(string pointsName, int decimals, Hash dappId)
    {
        Assert(pointsName.Length is > 0 and <= PointsContractConstants.TokenNameLength
               && decimals is >= 0 and <= PointsContractConstants.MaxDecimals, "Invalid input.");

        var empty = new PointInfo();
        var existing = State.PointInfos[dappId][pointsName];
        Assert(existing == null || existing.Equals(empty), "Point token already exists.");
    }
}