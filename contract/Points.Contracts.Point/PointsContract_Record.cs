using System;
using System.Linq;
using AElf;
using AElf.CSharp.Core;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Points.Contracts.Point;

public partial class PointsContract
{
    public override Empty Join(JoinInput input)
    {
        AssertInitialized();
        var dappId = input.DappId;
        AssertDappContractAddress(dappId);

        var registrant = input.Registrant;
        Assert(registrant != null, "Invalid registrant address.");

        var domain = input.Domain;
        AssertDomainFormat(domain);

        // The user registered using an unofficial domain link.

        var relationship = State.DomainsMap[domain];
        Assert(domain == State.DappInfos[dappId].OfficialDomain || relationship != null, "Not exist domain.");

        Register(dappId, registrant, domain, nameof(Join));

        if (domain != State.DappInfos[dappId].OfficialDomain)
        {
            // The number of user will only be calculated during the registration process
            var invitee = relationship.Invitee;
            State.InvitationCount[dappId][invitee][domain] = State.InvitationCount[dappId][invitee][domain].Add(1);
            var inviter = relationship.Inviter;
            if (inviter != null)
            {
                State.TierTwoInvitationCount[dappId][inviter][domain] =
                    State.TierTwoInvitationCount[dappId][inviter][domain].Add(1);
            }
        }

        Context.Fire(new Joined
        {
            DappId = dappId,
            Domain = domain,
            Registrant = registrant
        });

        return new Empty();
    }

    public override Empty ApplyToBeAdvocate(ApplyToBeAdvocateInput input)
    {
        Assert(input.Inviter == null || !input.Inviter.Value.IsNullOrEmpty(), "Invalid inviter.");

        var invitee = input.Invitee;
        var inviter = input.Inviter ?? Context.Sender;
        Assert(invitee != null, "Invalid invitee.");

        var dappId = input.DappId;
        Assert(dappId != null && State.DappInfos[dappId] != null, "Invalid dapp id.");
        Assert(State.ApplyDomainCount[inviter]?[dappId] < State.MaxApplyCount.Value, "Apply count exceed the limit.");

        var domain = input.Domain;
        AssertDomainFormat(domain);
        Assert(State.DomainsMap[domain] == null, "Domain has Exist.");
        Assert(!State.ReservedDomainsMap[domain], "This domain name is an officially reserved domain name");

        State.DomainsMap[domain] = new DomainRelationshipInfo
        {
            Domain = domain,
            Invitee = invitee,
            Inviter = inviter != invitee ? inviter : null
        };

        const string actionName = nameof(ApplyToBeAdvocate);
        var rule = State.DappInfos[dappId].DappsPointRules?.PointsRules
            .FirstOrDefault(t => t.ActionName == actionName);

        if (rule != null)
        {
            var pointName = rule.PointName;
            var kolPoints = GetKolPoints(rule);

            var pointsChangeDetails = new PointsChangedDetails();
            pointsChangeDetails = UpdatePointsBalance(invitee, domain, IncomeSourceType.Kol, pointName, kolPoints, dappId,
                actionName, pointsChangeDetails);

            if (inviter != invitee)
            {
                var inviterPoints = GetInviterPoints(rule);
                pointsChangeDetails = UpdatePointsBalance(inviter, domain, IncomeSourceType.Inviter, rule.PointName,
                    inviterPoints, dappId, actionName, pointsChangeDetails);
            }

            State.ApplyDomainCount[inviter][input.DappId] = State.ApplyDomainCount[inviter][input.DappId].Add(1);
            Context.Fire(new PointsChanged { PointsChangedDetails = pointsChangeDetails });
        }
        
        Context.Fire(new InviterApplied
        {
            Domain = input.Domain,
            DappId = input.DappId,
            Invitee = input.Invitee,
            Inviter = inviter
        });
        return new Empty();
    }

    private void SettlingPoints(Hash dappId, Address user, string actionName, BigIntValue sourceUserPoints = null)
    {
        var pointsRules = State.DappInfos[dappId].DappsPointRules;
        var rule = pointsRules?.PointsRules.FirstOrDefault(t => t.ActionName == actionName);
        var pointsChangeDetails = new PointsChangedDetails();

        if (rule != null)
        {
            var pointName = rule.PointName;
            var domain = State.RegistrationMap[dappId][user];

            var userPoints = GetPoints(rule, sourceUserPoints, out var kolPoints, out var inviterPoints);
            pointsChangeDetails = UpdatePointsBalance(user, domain, IncomeSourceType.User, pointName, userPoints,
                dappId,
                actionName, pointsChangeDetails);

            // kol related
            if (domain != State.DappInfos[dappId].OfficialDomain)
            {
                var domainRelationship = State.DomainsMap[domain];
                var invitee = domainRelationship.Invitee;

                pointsChangeDetails = UpdatePointsBalance(invitee, domain, IncomeSourceType.Kol, pointName, kolPoints,
                    dappId, actionName, pointsChangeDetails);

                var inviter = domainRelationship.Inviter;
                if (inviter != null)
                {
                    pointsChangeDetails = UpdatePointsBalance(inviter, domain, IncomeSourceType.Inviter, pointName,
                        inviterPoints, dappId, actionName, pointsChangeDetails);
                }
            }
            // referral
            else
            {
                var relationInfo = State.ReferralRelationInfoMap[dappId][user];
                if (relationInfo != null)
                {
                    pointsChangeDetails = UpdatePointsBalance(relationInfo.Referrer, domain, IncomeSourceType.User,
                        pointName, kolPoints, dappId, actionName, pointsChangeDetails);

                    // if referrer has referrer
                    if (relationInfo.Inviter != null)
                    {
                        pointsChangeDetails = UpdatePointsBalance(relationInfo.Inviter, domain, IncomeSourceType.User,
                            pointName, inviterPoints, dappId, actionName, pointsChangeDetails);
                    }

                    var referrerDomain = State.RegistrationMap[dappId][relationInfo.Referrer];

                    // if referrer belongs a kol
                    if (referrerDomain != domain)
                    {
                        var domainRelationship = State.DomainsMap[referrerDomain];
                        var invitee = domainRelationship.Invitee;
                        var points = kolPoints.Mul(rule.KolPointsPercent).Div(PointsContractConstants.Denominator);

                        pointsChangeDetails = UpdatePointsBalance(invitee, referrerDomain, IncomeSourceType.Kol,
                            pointName,
                            points, dappId, actionName, pointsChangeDetails);
                    }
                }
            }
        }

        // All points actions will be settled by self-increasing points.
        var details = SettlingSelfIncreasingPoints(dappId, user);
        if (details.PointsDetails.Count > 0)
            pointsChangeDetails.PointsDetails.AddRange(details.PointsDetails);
        // Points details
        if (pointsChangeDetails.PointsDetails.Count > 0)
        {
            Context.Fire(new PointsChanged { PointsChangedDetails = pointsChangeDetails });
        }
    }

    private BigIntValue GetPoints(PointsRule rule, BigIntValue sourceUserPoints, out BigIntValue kolPoints,
        out BigIntValue inviterPoints)
    {
        sourceUserPoints ??= new BigIntValue(rule.UserPoints);

        var userPoints = rule.EnableProportionalCalculation ? sourceUserPoints : rule.UserPoints;
        kolPoints = GetKolPoints(rule, sourceUserPoints);
        inviterPoints = GetInviterPoints(rule, sourceUserPoints);
        return userPoints;
    }

    /**
      * there are two types of relationship
      * under non-official domain:
      * inviter(may not exists) -> kol(invitee) -> user;
      * under official domain:
      * inviter(may not exists) -> referrer -> user(invitee);
      * the two relationships may intersect:
      * inviter(may not exists, non-official) -> kol(non-official) -> user(inviter, non-official)
      * -> user(referrer, official) -> user(invitee, official);
    */
    private PointsChangedDetails SettlingSelfIncreasingPoints(Hash dappId, Address user)
    {
        var pointsRule = State.SelfIncreasingPointsRules[dappId];
        if (pointsRule == null) return new PointsChangedDetails();
        var pointName = pointsRule.PointName;
        var actionName = pointsRule.ActionName;
        var domain = State.RegistrationMap[dappId][user];
        var officialDomain = State.DappInfos[dappId].OfficialDomain;

        var pointsDetails = new PointsChangedDetails();

        // settle user self-increasing points
        // Only registered users can calculate self-increasing points, and only registered users have settlement time.
        pointsDetails = UpdateSelfIncreasingPoint(dappId, user, IncomeSourceType.User, pointName, pointsRule.UserPoints,
            domain, actionName, pointsDetails);

        var domainRelationship = State.DomainsMap[domain];
        // user's domain is not official domain
        if (domainRelationship != null)
        {
            // settle kol self-increasing points
            var invitee = domainRelationship.Invitee;
            var kolPoints = GetKolPoints(pointsRule);
            pointsDetails = UpdateSelfIncreasingPoint(dappId, invitee, IncomeSourceType.Kol, pointName, kolPoints,
                domain, actionName, pointsDetails);

            // settle inviter(if exists) self-increasing points
            // kol registered a domain for himself but there was no inviter
            var inviter = domainRelationship.Inviter;
            if (inviter != null)
            {
                var inviterPoints = GetInviterPoints(pointsRule);
                pointsDetails = UpdateSelfIncreasingPoint(dappId, inviter, IncomeSourceType.Inviter, pointName,
                    inviterPoints, domain, actionName, pointsDetails);
            }
        }

        // settle user referral points
        pointsDetails = UpdateReferralSelfIncreasingPoint(dappId, user, IncomeSourceType.User, pointName, pointsRule,
            officialDomain, actionName, pointsDetails);

        var relationInfo = State.ReferralRelationInfoMap[dappId][user];
        if (relationInfo == null) return pointsDetails;

        // referrerDomain can be non-official and official
        var referrerDomain = State.RegistrationMap[dappId][relationInfo.Referrer];
        // settle referrer self-increasing points
        pointsDetails = UpdateSelfIncreasingPoint(dappId, relationInfo.Referrer, IncomeSourceType.User, pointName,
            pointsRule.UserPoints, referrerDomain, actionName, pointsDetails);
        // settle referrer referral points
        pointsDetails = UpdateReferralSelfIncreasingPoint(dappId, relationInfo.Referrer, IncomeSourceType.User,
            pointName, pointsRule, officialDomain, actionName, pointsDetails);


        if (relationInfo.Inviter != null)
        {
            // settle inviter(if exists) self-increasing points
            var inviterDomain = State.RegistrationMap[dappId][relationInfo.Inviter];
            pointsDetails = UpdateSelfIncreasingPoint(dappId, relationInfo.Inviter, IncomeSourceType.User, pointName,
                pointsRule.UserPoints, inviterDomain, actionName, pointsDetails);
            // settle inviter(if exists) referral points
            pointsDetails = UpdateReferralSelfIncreasingPoint(dappId, relationInfo.Inviter, IncomeSourceType.User,
                pointName, pointsRule, officialDomain, actionName, pointsDetails);
        }

        var referrerDomainRelationship = State.DomainsMap[referrerDomain];

        if (referrerDomainRelationship == null) return pointsDetails;

        // refererDomain is non-official, settle points for kol
        var kol = referrerDomainRelationship.Invitee;

        var kolDomain = State.RegistrationMap[dappId][kol];
        if (IsStringValid(kolDomain))
        {
            // if kol joined, settle kol self-increasing points as user
            pointsDetails = UpdateSelfIncreasingPoint(dappId, kol, IncomeSourceType.User, pointName,
                pointsRule.UserPoints, kolDomain, actionName, pointsDetails);
        }

        // settle kol self-increasing points as kol
        pointsDetails = UpdateSelfIncreasingPoint(dappId, kol, IncomeSourceType.Kol, pointName,
            GetKolPoints(pointsRule), referrerDomain, actionName, pointsDetails);
        // settle kol referral points
        pointsDetails = UpdateReferralSelfIncreasingPoint(dappId, kol, IncomeSourceType.Kol, pointName, pointsRule,
            referrerDomain, actionName, pointsDetails);

        return pointsDetails;
    }

    private BigIntValue GetKolPoints(PointsRule pointsRule, BigIntValue sourceUserPoints = null)
    {
        var userPoints = sourceUserPoints ?? pointsRule.UserPoints;
        return pointsRule.EnableProportionalCalculation
            ? userPoints.Mul(pointsRule.KolPointsPercent).Div(PointsContractConstants.Denominator)
            : new BigIntValue(pointsRule.KolPointsPercent);
    }

    private BigIntValue GetInviterPoints(PointsRule pointsRule, BigIntValue sourceUserPoints = null)
    {
        var userPoints = sourceUserPoints ?? pointsRule.UserPoints;
        return pointsRule.EnableProportionalCalculation
            ? userPoints.Mul(pointsRule.InviterPointsPercent).Div(PointsContractConstants.Denominator)
            : new BigIntValue(pointsRule.InviterPointsPercent);
    }

    private PointsChangedDetails UpdateSelfIncreasingPoint(Hash dappId, Address address, IncomeSourceType type,
        string pointName, BigIntValue points, string domain, string actionName,
        PointsChangedDetails pointsChangedDetails)
    {
        var waitingSettledPoints = new BigIntValue(0);
        var lastBlockTimestamp = State.LastPointsUpdateTimes[dappId][address][domain][type];
        if (lastBlockTimestamp != null)
        {
            var lastBlockTime = lastBlockTimestamp.Seconds;
            waitingSettledPoints = CalculateWaitingSettledSelfIncreasingPoints(dappId, address, type,
                Context.CurrentBlockTime.Seconds, lastBlockTime, domain, points);
        }

        pointsChangedDetails = UpdatePointsBalance(address, domain, type, pointName, waitingSettledPoints, dappId,
            actionName, pointsChangedDetails);

        State.LastPointsUpdateTimes[dappId][address][domain][type] = Context.CurrentBlockTime;

        return pointsChangedDetails;
    }

    private BigIntValue CalculateWaitingSettledSelfIncreasingPoints(Hash dappId, Address address, IncomeSourceType type,
        long currentBlockTime, long lastBlockTime, string domain, BigIntValue points)
    {
        var timeGap = currentBlockTime.Sub(lastBlockTime);
        return type switch
        {
            IncomeSourceType.Inviter => points.Mul(timeGap).Mul(State.TierTwoInvitationCount[dappId][address][domain]),
            IncomeSourceType.Kol => points.Mul(timeGap).Mul(State.InvitationCount[dappId][address][domain]),
            IncomeSourceType.User => points.Mul(timeGap),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "")
        };
    }

    private PointsChangedDetails UpdatePointsBalance(Address address, string domain, IncomeSourceType type,
        string pointName, BigIntValue amount, Hash dappId, string actionName, PointsChangedDetails pointsChangedDetails)
    {
        var balance = State.PointsBalance[address][domain][type][pointName];
        var pointsBalance = State.PointsBalanceValue[address][domain][type][pointName] ?? new BigIntValue(balance);
        pointsBalance = pointsBalance.Add(amount);
        State.PointsBalanceValue[address][domain][type][pointName] = pointsBalance;

        pointsChangedDetails.PointsDetails.Add(GeneratePointsDetail(address, domain, actionName, type, pointName,
            amount, dappId));

        return pointsChangedDetails;
    }

    private PointsChangedDetail GeneratePointsDetail(Address address, string domain, string actionName,
        IncomeSourceType type, string pointName, BigIntValue amount, Hash dappId)
    {
        // pointsChanged
        return new PointsChangedDetail
        {
            DappId = dappId,
            PointsReceiver = address,
            Domain = domain,
            IncomeSourceType = type,
            ActionName = actionName,
            PointsName = pointName,
            IncreaseValue = amount,
            BalanceValue = State.PointsBalanceValue[address][domain][type][pointName]
        };
    }

    public override Empty Settle(SettleInput input)
    {
        CheckSettleParam(input.DappId, input.ActionName);
        var dappId = input.DappId;
        var userAddress = input.UserAddress;
        CheckAndSettlePoints(dappId, userAddress, input.UserPointsValue, input.UserPoints, input.ActionName);
        return new Empty();
    }

    public override Empty BatchSettle(BatchSettleInput input)
    {
        CheckSettleParam(input.DappId, input.ActionName);
        var dappId = input.DappId;
        Assert(
            input.UserPointsList.Count > 0 &&
            input.UserPointsList.Count <= PointsContractConstants.MaxBatchSettleListCount, "Invalid user point list.");
        foreach (var userPoints in input.UserPointsList)
        {
            CheckAndSettlePoints(dappId, userPoints.UserAddress, userPoints.UserPointsValue, userPoints.UserPoints_,
                input.ActionName);
        }

        return new Empty();
    }

    private void CheckAndSettlePoints(Hash dappId, Address userAddress, BigIntValue userPointsValue, long userPoints,
        string actionName)
    {
        Assert(userAddress.Value != null, "User address cannot be null");
        Assert(!string.IsNullOrEmpty(State.RegistrationMap[dappId][userAddress]), "User has not registered yet");
        var userPoint = userPointsValue ?? new BigIntValue(userPoints);
        Assert(userPoint.Value != "0", "Invalid user points value.");
        SettlingPoints(dappId, userAddress, actionName, userPoint);
    }

    private void CheckSettleParam(Hash dappId, string actionName)
    {
        AssertInitialized();
        AssertDappContractAddress(dappId);
        Assert(IsStringValid(actionName), "Invalid action name.");
    }

    private void Register(Hash dappId, Address user, string domain, string actionName)
    {
        Assert(string.IsNullOrEmpty(State.RegistrationMap[dappId][user]), "A dapp can only be registered once.");
        State.RegistrationMap[dappId][user] = domain;

        SettlingPoints(dappId, user, actionName);
    }

    public override Empty AcceptReferral(AcceptReferralInput input)
    {
        Assert(input != null, "Invalid input.");

        var dappId = input.DappId;
        AssertDappContractAddress(dappId);

        var officialDomain = State.DappInfos[dappId].OfficialDomain;

        var referrer = input.Referrer;
        Assert(IsAddressValid(referrer), "Invalid referrer.");
        var referrerDomain = State.RegistrationMap[dappId][referrer];
        Assert(IsStringValid(referrerDomain), "Referrer not joined.");

        var invitee = input.Invitee;
        Assert(IsAddressValid(invitee), "Invalid invitee.");

        var inviter = State.ReferralRelationInfoMap[dappId][referrer]?.Referrer;
        State.ReferralRelationInfoMap[dappId][invitee] = new ReferralRelationInfo
        {
            DappId = dappId,
            Invitee = invitee,
            Referrer = referrer,
            Inviter = inviter
        };

        State.ReferralFollowerCountInfoMap[dappId][invitee] = new ReferralFollowerCountInfo();

        Register(dappId, invitee, officialDomain, nameof(AcceptReferral));

        AddFollowerCount(dappId, referrer, 1, 0);
        AddFollowerCount(dappId, inviter, 0, 1);

        var kol = State.DomainsMap[referrerDomain]?.Invitee;
        if (kol != null)
        {
            Assert(kol != input.Invitee, "Can not refer kol.");

            State.KolReferralSubFollowerCountMap[dappId][kol] =
                State.KolReferralSubFollowerCountMap[dappId][kol].Add(1);
        }

        Context.Fire(new ReferralAccepted
        {
            DappId = dappId,
            Domain = officialDomain,
            Referrer = referrer,
            Invitee = invitee,
            Inviter = inviter
        });

        return new Empty();
    }

    private void AddFollowerCount(Hash dappId, Address address, long followerCount, long subFollowerCount)
    {
        if (address == null) return;

        var referralFollowerCount = State.ReferralFollowerCountInfoMap[dappId][address];

        if (referralFollowerCount == null)
        {
            State.ReferralFollowerCountInfoMap[dappId][address] = new ReferralFollowerCountInfo
            {
                FollowerCount = followerCount,
                SubFollowerCount = subFollowerCount
            };
        }
        else
        {
            referralFollowerCount.FollowerCount = referralFollowerCount.FollowerCount.Add(followerCount);
            referralFollowerCount.SubFollowerCount = referralFollowerCount.SubFollowerCount.Add(subFollowerCount);
        }
    }

    private PointsChangedDetails UpdateReferralSelfIncreasingPoint(Hash dappId, Address address, IncomeSourceType type,
        string pointName, PointsRule pointsRule, string domain, string actionName,
        PointsChangedDetails pointsChangedDetails)
    {
        var waitingSettledPoints = new BigIntValue(0);

        var lastBlockTimestamp = State.ReferralPointsUpdateTimes[dappId][address][domain][type];
        if (lastBlockTimestamp != null)
        {
            var lastBlockTime = lastBlockTimestamp.Seconds;
            waitingSettledPoints = CalculateWaitingSettledSelfIncreasingPointsForReferral(dappId, address,
                Context.CurrentBlockTime.Seconds, lastBlockTime, pointsRule, domain, type);
        }

        pointsChangedDetails = UpdatePointsBalance(address, domain, type, pointName, waitingSettledPoints, dappId,
            actionName, pointsChangedDetails);

        State.ReferralPointsUpdateTimes[dappId][address][domain][type] = Context.CurrentBlockTime;

        return pointsChangedDetails;
    }

    private BigIntValue CalculateWaitingSettledSelfIncreasingPointsForReferral(Hash dappId, Address address,
        long currentBlockTime, long lastBlockTime, PointsRule pointsRule, string domain, IncomeSourceType type)
    {
        var totalPoints = new BigIntValue(0);

        var timeGap = currentBlockTime.Sub(lastBlockTime);

        var domainRelationshipInfo = State.DomainsMap[domain];

        // settle kol if referrer's domain is non-official
        if (domainRelationshipInfo != null && address == domainRelationshipInfo.Invitee && type == IncomeSourceType.Kol)
        {
            var subFollowerCount = State.KolReferralSubFollowerCountMap[dappId][address];
            totalPoints = totalPoints.Add(CalculatePoints(pointsRule.UserPoints, pointsRule.KolPointsPercent,
                    subFollowerCount, timeGap)
                .Mul(pointsRule.KolPointsPercent)
                .Div(PointsContractConstants.Denominator));
            return totalPoints;
        }

        var followerCount = State.ReferralFollowerCountInfoMap[dappId][address];
        if (followerCount == null) return totalPoints;

        // calculate follower points
        totalPoints = totalPoints.Add(CalculatePoints(pointsRule.UserPoints, pointsRule.KolPointsPercent,
            followerCount.FollowerCount, timeGap));

        // calculate subFollower points
        totalPoints = totalPoints.Add(CalculatePoints(pointsRule.UserPoints, pointsRule.InviterPointsPercent,
            followerCount.SubFollowerCount, timeGap));

        return totalPoints;
    }

    private BigIntValue CalculatePoints(BigIntValue points, long percent, long count, long timeGap)
    {
        return points.Mul(percent).Div(PointsContractConstants.Denominator).Mul(count).Mul(timeGap);
    }
}