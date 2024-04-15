using AElf.Sdk.CSharp.State;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Points.Contracts.Point;

public partial class PointsContractState : ContractState
{
    public SingletonState<bool> Initialized { get; set; }
    public SingletonState<Address> Admin { get; set; }
    public SingletonState<ReservedDomainList> ReservedDomains { get; set; }
    public SingletonState<int> MaxApplyCount { get; set; }
    public MappedState<Hash, Address, string> RegistrationMap { get; set; }
    public MappedState<string, DomainRelationshipInfo> DomainsMap { get; set; }
    public MappedState<Hash, DappInfo> DappInfos { get; set; }
    public MappedState<Address, Hash, int> ApplyDomainCount { get; set; }
    public MappedState<Hash, PointsRule> SelfIncreasingPointsRules { get; set; }
    public MappedState<Hash, string, PointInfo> PointInfos { get; set; }
    public MappedState<Hash, Address, string, int> InvitationCount { get; set; }
    public MappedState<Hash, Address, string, int> TierTwoInvitationCount { get; set; }

    public MappedState<Hash, Address, string, IncomeSourceType, Timestamp> LastPointsUpdateTimes { get; set; }

    public MappedState<Address, string, IncomeSourceType, string, long> PointsBalance { get; set; }
    public MappedState<Address, string, IncomeSourceType, string, BigIntValue> PointsBalanceValue { get; set; }

    // <dapp id -> address -> referral relation>
    public MappedState<Hash, Address, ReferralRelationInfo> ReferralRelationInfoMap { get; set; }
    // <dapp id -> address -> follower and sub follower count>
    public MappedState<Hash, Address, ReferralFollowerCountInfo> ReferralFollowerCountInfoMap { get; set; }
    // <dapp id -> kol address -> sub followers count>
    public MappedState<Hash, Address, long> KolReferralSubFollowerCountMap { get; set; }
    // <dapp id -> address -> domain -> type -> latest update time>
    public MappedState<Hash, Address, string, IncomeSourceType, Timestamp> ReferralPointsUpdateTimes { get; set; }
}