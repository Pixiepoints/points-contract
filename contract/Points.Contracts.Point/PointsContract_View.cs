using System;
using AElf.CSharp.Core;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Points.Contracts.Point;

public partial class PointsContract
{
    public override Address GetAdmin(Empty input) => State.Admin.Value;
    public override Int32Value GetMaxApplyCount(Empty input) => new() { Value = State.MaxApplyCount.Value };

    public override DomainRelationshipInfo GetDomainApplyInfo(StringValue domain) => State.DomainsMap[domain.Value];

    public override GetPointsBalanceOutput GetPointsBalance(GetPointsBalanceInput input)
    {
        var dappId = input.DappId;
        var address = input.Address;
        var type = input.IncomeSourceType;
        var domain = input.Domain;
        var pointName = input.PointName;
        Assert(
            address != null && !string.IsNullOrEmpty(domain) && !string.IsNullOrEmpty(pointName) &&
            State.PointInfos[dappId][pointName] != null, "Invalid input.");
        Assert(dappId != null && State.DappInfos[dappId]?.OfficialDomain == input.Domain ||
               State.DomainsMap[domain] != null, "Invalid domain.");

        var balance = State.PointsBalance[address][domain][type][pointName];
        var balanceValue = State.PointsBalanceValue[address][domain][type][pointName] ?? new BigIntValue(balance);
        var userLastBillingUpdateTimes = State.LastPointsUpdateTimes[dappId]?[address]?[domain]?[type];
        var rule = State.SelfIncreasingPointsRules[dappId];

        if (pointName != rule?.PointName || userLastBillingUpdateTimes == null)
        {
            return new GetPointsBalanceOutput
            {
                PointName = input.PointName,
                Owner = input.Address,
                LastUpdateTime = userLastBillingUpdateTimes,
                BalanceValue = balanceValue
            };
        }

        var points = type switch
        {
            IncomeSourceType.User => rule.UserPoints,
            IncomeSourceType.Kol => GetKolPoints(rule),
            IncomeSourceType.Inviter => GetInviterPoints(rule),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "")
        };

        balanceValue = balanceValue.Add(CalculateWaitingSettledSelfIncreasingPoints(dappId, address, type,
            Context.CurrentBlockTime.Seconds, userLastBillingUpdateTimes.Seconds, domain, points));

        // An account that has not been bound to a domain has no update time.
        var userLastBillingUpdateTimesForReferral = State.ReferralPointsUpdateTimes[dappId]?[address]?[domain]?[type];

        if (userLastBillingUpdateTimesForReferral != null)
        {
            balanceValue = balanceValue.Add(CalculateWaitingSettledSelfIncreasingPointsForReferral(dappId, address,
                Context.CurrentBlockTime.Seconds, userLastBillingUpdateTimesForReferral.Seconds, rule, domain, type));
        }

        return new GetPointsBalanceOutput
        {
            PointName = input.PointName,
            Owner = input.Address,
            LastUpdateTime = userLastBillingUpdateTimes,
            BalanceValue = balanceValue
        };
    }

    public override GetDappInformationOutput GetDappInformation(GetDappInformationInput input)
        => new() { DappInfo = State.DappInfos[input.DappId] };

    public override GetSelfIncreasingPointsRuleOutput GetSelfIncreasingPointsRule(
        GetSelfIncreasingPointsRuleInput input) => new() { Rule = State.SelfIncreasingPointsRules[input.DappId] };

    public override PointInfo GetPoint(GetPointInput input)
    {
        return State.PointInfos[input.DappId][input.PointsName];
    }

    public override ReferralRelationInfo GetReferralRelationInfo(GetReferralRelationInfoInput input)
    {
        if (!IsHashValid(input.DappId) || !IsAddressValid(input.Invitee)) return new ReferralRelationInfo();
        return State.ReferralRelationInfoMap[input.DappId]?[input.Invitee] ?? new ReferralRelationInfo();
    }

    public override BoolValue CheckDomainReserved(StringValue input)
    {
        return input != null && !string.IsNullOrWhiteSpace(input.Value)
            ? new BoolValue { Value = State.ReservedDomainsMap[input.Value] }
            : new BoolValue();
    }
}