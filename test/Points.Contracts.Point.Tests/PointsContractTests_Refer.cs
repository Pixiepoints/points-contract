using System.Linq;
using System.Threading.Tasks;
using AElf.CSharp.Core;
using AElf.CSharp.Core.Extension;
using AElf.Types;
using Google.Protobuf;
using Shouldly;
using Xunit;

namespace Points.Contracts.Point;

public partial class PointsContractTests
{
    private const long UserPoints = 6180000;
    private const long Period = 5;
    private const long KolPointsPercent = 1600;
    private const long InviterPointsPercent = 800;

    [Fact]
    public async Task AcceptReferralTests()
    {
        // Inviter -> null
        // Referrer -> DefaultAddress
        // Invitee -> UserAddress
        var dappId = await InitializeForAcceptReferralTests();

        await PointsContractStub.Join.SendAsync(new JoinInput
        {
            DappId = dappId,
            Domain = DefaultOfficialDomain,
            Registrant = DefaultAddress
        });

        var result = await PointsContractStub.AcceptReferral.SendAsync(new AcceptReferralInput
        {
            DappId = dappId,
            Referrer = DefaultAddress,
            Invitee = UserAddress
        });

        var log = GetLogEvent<ReferralAccepted>(result.TransactionResult);
        log.DappId.ShouldBe(dappId);
        log.Domain.ShouldBe(DefaultOfficialDomain);
        log.Referrer.ShouldBe(DefaultAddress);
        log.Invitee.ShouldBe(UserAddress);
        log.Inviter.ShouldBeNull();

        var acceptLog = GetLogEvent<PointsChanged>(result.TransactionResult);

        {
            var output = await PointsContractStub.GetReferralRelationInfo.CallAsync(new GetReferralRelationInfoInput
            {
                DappId = dappId,
                Invitee = UserAddress
            });

            output.DappId.ShouldBe(dappId);
            output.Invitee.ShouldBe(UserAddress);
            output.Referrer.ShouldBe(DefaultAddress);
            output.Inviter.ShouldBeNull();
        }

        BlockTimeProvider.SetBlockTime(BlockTimeProvider.GetBlockTime().AddSeconds(Period));

        result = await PointsContractStub.Settle.SendAsync(new SettleInput
        {
            DappId = dappId,
            ActionName = SettleActionName,
            UserAddress = UserAddress,
            UserPointsValue = UserPoints
        });

        var settleLog = GetLogEvent<PointsChanged>(result.TransactionResult);

        // referrer
        {
            // accept referral
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, DefaultAddress, JoinPointName).Result
                .ShouldBe(UserPoints + UserPoints * KolPointsPercent / 10000);
            CalculatePoints(acceptLog, DefaultOfficialDomain, IncomeSourceType.User, DefaultAddress, JoinPointName)
                .ShouldBe(UserPoints * KolPointsPercent / 10000);

            // settle
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, DefaultAddress, SettlePointName)
                .Result.ShouldBe(UserPoints * KolPointsPercent / 10000);
            CalculatePoints(settleLog, DefaultOfficialDomain, IncomeSourceType.User, DefaultAddress, SettlePointName)
                .ShouldBe(UserPoints * KolPointsPercent / 10000);

            // self increasing
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, DefaultAddress,
                    SelfIncreasingPointName).Result
                .ShouldBe(UserPoints * Period + UserPoints * KolPointsPercent / 10000 * Period);
            CalculatePoints(settleLog, DefaultOfficialDomain, IncomeSourceType.User, DefaultAddress,
                SelfIncreasingPointName).ShouldBe(UserPoints * Period + UserPoints * KolPointsPercent / 10000 * Period);
        }

        // invitee
        {
            // accept referral
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, UserAddress, JoinPointName).Result
                .ShouldBe(UserPoints);
            CalculatePoints(acceptLog, DefaultOfficialDomain, IncomeSourceType.User, UserAddress, JoinPointName)
                .ShouldBe(UserPoints);

            // settle
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, UserAddress, SettlePointName).Result
                .ShouldBe(UserPoints);
            CalculatePoints(settleLog, DefaultOfficialDomain, IncomeSourceType.User, UserAddress, SettlePointName)
                .ShouldBe(UserPoints);

            // self increasing
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, UserAddress, SelfIncreasingPointName)
                .Result.ShouldBe(UserPoints * Period);
            CalculatePoints(settleLog, DefaultOfficialDomain, IncomeSourceType.User, UserAddress,
                SelfIncreasingPointName).ShouldBe(UserPoints * Period);
        }
    }

    [Fact]
    public async Task AcceptReferralTests_Inviter()
    {
        // Inviter -> DefaultAddress
        // Referrer -> UserAddress
        // Invitee -> User2Address
        var dappId = await InitializeForAcceptReferralTests();

        await PointsContractStub.Join.SendAsync(new JoinInput
        {
            DappId = dappId,
            Domain = DefaultOfficialDomain,
            Registrant = DefaultAddress
        });

        var result = await PointsContractStub.AcceptReferral.SendAsync(new AcceptReferralInput
        {
            DappId = dappId,
            Referrer = DefaultAddress,
            Invitee = UserAddress
        });

        var log = GetLogEvent<ReferralAccepted>(result.TransactionResult);
        log.DappId.ShouldBe(dappId);
        log.Domain.ShouldBe(DefaultOfficialDomain);
        log.Referrer.ShouldBe(DefaultAddress);
        log.Invitee.ShouldBe(UserAddress);
        log.Inviter.ShouldBeNull();

        var output = await PointsContractStub.GetReferralRelationInfo.CallAsync(new GetReferralRelationInfoInput
        {
            DappId = dappId,
            Invitee = UserAddress
        });

        output.DappId.ShouldBe(dappId);
        output.Invitee.ShouldBe(UserAddress);
        output.Referrer.ShouldBe(DefaultAddress);
        output.Inviter.ShouldBeNull();

        result = await PointsContractStub.AcceptReferral.SendAsync(new AcceptReferralInput
        {
            DappId = dappId,
            Referrer = UserAddress,
            Invitee = User2Address
        });

        log = GetLogEvent<ReferralAccepted>(result.TransactionResult);
        log.DappId.ShouldBe(dappId);
        log.Domain.ShouldBe(DefaultOfficialDomain);
        log.Referrer.ShouldBe(UserAddress);
        log.Invitee.ShouldBe(User2Address);
        log.Inviter.ShouldBe(DefaultAddress);

        var acceptLog = GetLogEvent<PointsChanged>(result.TransactionResult);

        output = await PointsContractStub.GetReferralRelationInfo.CallAsync(new GetReferralRelationInfoInput
        {
            DappId = dappId,
            Invitee = User2Address
        });

        output.DappId.ShouldBe(dappId);
        output.Invitee.ShouldBe(User2Address);
        output.Referrer.ShouldBe(UserAddress);
        output.Inviter.ShouldBe(DefaultAddress);

        BlockTimeProvider.SetBlockTime(BlockTimeProvider.GetBlockTime().AddSeconds(Period));

        result = await PointsContractStub.Settle.SendAsync(new SettleInput
        {
            DappId = dappId,
            ActionName = SettleActionName,
            UserAddress = User2Address,
            UserPointsValue = UserPoints
        });

        var settleLog = GetLogEvent<PointsChanged>(result.TransactionResult);

        // inviter
        {
            // accept referral
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, DefaultAddress, JoinPointName).Result
                .ShouldBe(
                    UserPoints + UserPoints * KolPointsPercent / 10000 + UserPoints * InviterPointsPercent / 10000);
            CalculatePoints(acceptLog, DefaultOfficialDomain, IncomeSourceType.User, DefaultAddress, JoinPointName)
                .ShouldBe(UserPoints * InviterPointsPercent / 10000);

            // settle
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, DefaultAddress, SettlePointName)
                .Result.ShouldBe(UserPoints * InviterPointsPercent / 10000);
            CalculatePoints(settleLog, DefaultOfficialDomain, IncomeSourceType.User, DefaultAddress, SettlePointName)
                .ShouldBe(UserPoints * InviterPointsPercent / 10000);

            // self increasing
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, DefaultAddress,
                SelfIncreasingPointName).Result.ShouldBe(UserPoints * Period +
                                                         UserPoints * KolPointsPercent / 10000 * Period +
                                                         UserPoints * InviterPointsPercent / 10000 * Period);
            CalculatePoints(settleLog, DefaultOfficialDomain, IncomeSourceType.User, DefaultAddress,
                SelfIncreasingPointName).ShouldBe(UserPoints * Period + UserPoints * KolPointsPercent / 10000 * Period +
                                                  UserPoints * InviterPointsPercent / 10000 * Period);
        }

        // referrer
        {
            // accept referral
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, UserAddress, JoinPointName).Result
                .ShouldBe(UserPoints + UserPoints * KolPointsPercent / 10000);
            CalculatePoints(acceptLog, DefaultOfficialDomain, IncomeSourceType.User, UserAddress, JoinPointName)
                .ShouldBe(UserPoints * KolPointsPercent / 10000);

            // settle
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, UserAddress, SettlePointName).Result
                .ShouldBe(UserPoints * KolPointsPercent / 10000);
            CalculatePoints(settleLog, DefaultOfficialDomain, IncomeSourceType.User, UserAddress, SettlePointName)
                .ShouldBe(UserPoints * KolPointsPercent / 10000);

            // self increasing
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, UserAddress, SelfIncreasingPointName)
                .Result.ShouldBe(UserPoints * Period + UserPoints * KolPointsPercent / 10000 * Period);
            CalculatePoints(settleLog, DefaultOfficialDomain, IncomeSourceType.User, UserAddress,
                SelfIncreasingPointName).ShouldBe(UserPoints * Period + UserPoints * KolPointsPercent / 10000 * Period);
        }

        // invitee
        {
            // accept referral
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, User2Address, JoinPointName).Result
                .ShouldBe(UserPoints);
            CalculatePoints(acceptLog, DefaultOfficialDomain, IncomeSourceType.User, User2Address, JoinPointName)
                .ShouldBe(UserPoints);

            // settle
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, User2Address, SettlePointName).Result
                .ShouldBe(UserPoints);
            CalculatePoints(settleLog, DefaultOfficialDomain, IncomeSourceType.User, User2Address, SettlePointName)
                .ShouldBe(UserPoints);

            // self increasing
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, User2Address,
                SelfIncreasingPointName).Result.ShouldBe(UserPoints * Period);
            CalculatePoints(settleLog, DefaultOfficialDomain, IncomeSourceType.User, User2Address,
                SelfIncreasingPointName).ShouldBe(UserPoints * Period);
        }
    }

    [Fact]
    public async Task AcceptReferralTests_Kol()
    {
        const string domain = "user.com";

        // Inviter -> DefaultAddress, Kol
        // Referrer -> UserAddress
        // Invitee -> User2Address
        var dappId = await InitializeForAcceptReferralTests();

        await PointsContractStub.ApplyToBeAdvocate.SendAsync(new ApplyToBeAdvocateInput
        {
            Domain = domain,
            DappId = dappId,
            Invitee = DefaultAddress
        });

        await PointsContractStub.Join.SendAsync(new JoinInput
        {
            DappId = dappId,
            Domain = domain,
            Registrant = UserAddress
        });

        var result = await PointsContractStub.AcceptReferral.SendAsync(new AcceptReferralInput
        {
            DappId = dappId,
            Referrer = UserAddress,
            Invitee = User2Address
        });

        var log = GetLogEvent<ReferralAccepted>(result.TransactionResult);
        log.DappId.ShouldBe(dappId);
        log.Domain.ShouldBe(DefaultOfficialDomain);
        log.Referrer.ShouldBe(UserAddress);
        log.Invitee.ShouldBe(User2Address);
        log.Inviter.ShouldBeNull();

        var acceptLog = GetLogEvent<PointsChanged>(result.TransactionResult);

        var output = await PointsContractStub.GetReferralRelationInfo.CallAsync(new GetReferralRelationInfoInput
        {
            DappId = dappId,
            Invitee = User2Address
        });

        output.DappId.ShouldBe(dappId);
        output.Invitee.ShouldBe(User2Address);
        output.Referrer.ShouldBe(UserAddress);
        output.Inviter.ShouldBeNull();

        BlockTimeProvider.SetBlockTime(BlockTimeProvider.GetBlockTime().AddSeconds(Period));

        result = await PointsContractStub.Settle.SendAsync(new SettleInput
        {
            DappId = dappId,
            ActionName = SettleActionName,
            UserAddress = User2Address,
            UserPointsValue = UserPoints
        });

        var settleLog = GetLogEvent<PointsChanged>(result.TransactionResult);

        // kol
        {
            // accept referral
            GetPointsBalance(dappId, domain, IncomeSourceType.Kol, DefaultAddress, JoinPointName).Result.ShouldBe(
                UserPoints * KolPointsPercent / 10000 +
                UserPoints * KolPointsPercent / 10000 * KolPointsPercent / 10000);
            CalculatePoints(acceptLog, domain, IncomeSourceType.Kol, DefaultAddress, JoinPointName)
                .ShouldBe(UserPoints * KolPointsPercent / 10000 * KolPointsPercent / 10000);

            // settle
            GetPointsBalance(dappId, domain, IncomeSourceType.Kol, DefaultAddress, SettlePointName).Result
                .ShouldBe(UserPoints * KolPointsPercent / 10000 * KolPointsPercent / 10000);
            CalculatePoints(settleLog, domain, IncomeSourceType.Kol, DefaultAddress, SettlePointName)
                .ShouldBe(UserPoints * KolPointsPercent / 10000 * KolPointsPercent / 10000);

            // self increasing
            GetPointsBalance(dappId, domain, IncomeSourceType.Kol, DefaultAddress, SelfIncreasingPointName).Result
                .ShouldBe(UserPoints * KolPointsPercent / 10000 * Period +
                          UserPoints * KolPointsPercent / 10000 * KolPointsPercent / 10000 * Period);
            CalculatePoints(settleLog, domain, IncomeSourceType.Kol, DefaultAddress, SelfIncreasingPointName).ShouldBe(
                UserPoints * KolPointsPercent / 10000 * Period +
                UserPoints * KolPointsPercent / 10000 * KolPointsPercent / 10000 * Period);
        }

        // referrer
        {
            // accept referral
            GetPointsBalance(dappId, domain, IncomeSourceType.User, UserAddress, JoinPointName).Result
                .ShouldBe(UserPoints);
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, UserAddress, JoinPointName).Result
                .ShouldBe(UserPoints * KolPointsPercent / 10000);
            CalculatePoints(acceptLog, domain, IncomeSourceType.User, UserAddress, JoinPointName).ShouldBe(0);
            CalculatePoints(acceptLog, DefaultOfficialDomain, IncomeSourceType.User, UserAddress, JoinPointName)
                .ShouldBe(UserPoints * KolPointsPercent / 10000);

            // settle
            GetPointsBalance(dappId, domain, IncomeSourceType.User, UserAddress, SettlePointName).Result.ShouldBe(0);
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, UserAddress, SettlePointName).Result
                .ShouldBe(UserPoints * KolPointsPercent / 10000);
            CalculatePoints(settleLog, domain, IncomeSourceType.User, UserAddress, SettlePointName).ShouldBe(0);
            CalculatePoints(settleLog, DefaultOfficialDomain, IncomeSourceType.User, UserAddress, SettlePointName)
                .ShouldBe(UserPoints * KolPointsPercent / 10000);


            // self increasing
            GetPointsBalance(dappId, domain, IncomeSourceType.User, UserAddress, SelfIncreasingPointName).Result
                .ShouldBe(UserPoints * Period);
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, UserAddress, SelfIncreasingPointName)
                .Result.ShouldBe(UserPoints * KolPointsPercent / 10000 * Period);
            CalculatePoints(settleLog, domain, IncomeSourceType.User, UserAddress, SelfIncreasingPointName)
                .ShouldBe(UserPoints * Period);
            CalculatePoints(settleLog, DefaultOfficialDomain, IncomeSourceType.User, UserAddress,
                SelfIncreasingPointName).ShouldBe(UserPoints * KolPointsPercent / 10000 * Period);
        }

        // invitee
        {
            // accept referral
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, User2Address, JoinPointName).Result
                .ShouldBe(UserPoints);
            CalculatePoints(acceptLog, DefaultOfficialDomain, IncomeSourceType.User, User2Address, JoinPointName)
                .ShouldBe(UserPoints);

            // settle
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, User2Address, SettlePointName).Result
                .ShouldBe(UserPoints);
            CalculatePoints(settleLog, DefaultOfficialDomain, IncomeSourceType.User, User2Address, SettlePointName)
                .ShouldBe(UserPoints);

            // self increasing
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, User2Address,
                SelfIncreasingPointName).Result.ShouldBe(UserPoints * Period);
            CalculatePoints(settleLog, DefaultOfficialDomain, IncomeSourceType.User, User2Address,
                SelfIncreasingPointName).ShouldBe(UserPoints * Period);
        }
    }

    [Fact]
    public async Task AcceptReferralTests_KolJoinDomain()
    {
        const string domain = "user.com";

        // Inviter -> DefaultAddress, Kol
        // Referrer -> DefaultAddress
        // Invitee -> UserAddress
        var dappId = await InitializeForAcceptReferralTests();

        await PointsContractStub.ApplyToBeAdvocate.SendAsync(new ApplyToBeAdvocateInput
        {
            Domain = domain,
            DappId = dappId,
            Invitee = DefaultAddress
        });

        await PointsContractStub.Join.SendAsync(new JoinInput
        {
            DappId = dappId,
            Domain = domain,
            Registrant = DefaultAddress
        });

        var result = await PointsContractStub.AcceptReferral.SendAsync(new AcceptReferralInput
        {
            DappId = dappId,
            Referrer = DefaultAddress,
            Invitee = UserAddress
        });

        var log = GetLogEvent<ReferralAccepted>(result.TransactionResult);
        log.DappId.ShouldBe(dappId);
        log.Domain.ShouldBe(DefaultOfficialDomain);
        log.Referrer.ShouldBe(DefaultAddress);
        log.Invitee.ShouldBe(UserAddress);
        log.Inviter.ShouldBeNull();

        var acceptLog = GetLogEvent<PointsChanged>(result.TransactionResult);

        var output = await PointsContractStub.GetReferralRelationInfo.CallAsync(new GetReferralRelationInfoInput
        {
            DappId = dappId,
            Invitee = UserAddress
        });

        output.DappId.ShouldBe(dappId);
        output.Invitee.ShouldBe(UserAddress);
        output.Referrer.ShouldBe(DefaultAddress);
        output.Inviter.ShouldBeNull();

        BlockTimeProvider.SetBlockTime(BlockTimeProvider.GetBlockTime().AddSeconds(Period));

        result = await PointsContractStub.Settle.SendAsync(new SettleInput
        {
            DappId = dappId,
            ActionName = SettleActionName,
            UserAddress = UserAddress,
            UserPointsValue = UserPoints
        });

        var settleLog = GetLogEvent<PointsChanged>(result.TransactionResult);

        // kol
        {
            // accept referral
            GetPointsBalance(dappId, domain, IncomeSourceType.Kol, DefaultAddress, JoinPointName).Result.ShouldBe(
                UserPoints * KolPointsPercent / 10000 +
                UserPoints * KolPointsPercent / 10000 * KolPointsPercent / 10000);
            CalculatePoints(acceptLog, domain, IncomeSourceType.Kol, DefaultAddress, JoinPointName)
                .ShouldBe(UserPoints * KolPointsPercent / 10000 * KolPointsPercent / 10000);

            // settle
            GetPointsBalance(dappId, domain, IncomeSourceType.Kol, DefaultAddress, SettlePointName).Result
                .ShouldBe(UserPoints * KolPointsPercent / 10000 * KolPointsPercent / 10000);
            CalculatePoints(settleLog, domain, IncomeSourceType.Kol, DefaultAddress, SettlePointName)
                .ShouldBe(UserPoints * KolPointsPercent / 10000 * KolPointsPercent / 10000);

            // self increasing
            GetPointsBalance(dappId, domain, IncomeSourceType.Kol, DefaultAddress, SelfIncreasingPointName).Result
                .ShouldBe(UserPoints * KolPointsPercent / 10000 * Period +
                          UserPoints * KolPointsPercent / 10000 * KolPointsPercent / 10000 * Period);
            CalculatePoints(settleLog, domain, IncomeSourceType.Kol, DefaultAddress, SelfIncreasingPointName).ShouldBe(
                UserPoints * KolPointsPercent / 10000 * Period +
                UserPoints * KolPointsPercent / 10000 * KolPointsPercent / 10000 * Period);
        }

        // referrer
        {
            // accept referral
            GetPointsBalance(dappId, domain, IncomeSourceType.User, DefaultAddress, JoinPointName).Result
                .ShouldBe(UserPoints);
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, DefaultAddress, JoinPointName).Result
                .ShouldBe(UserPoints * KolPointsPercent / 10000);
            CalculatePoints(acceptLog, domain, IncomeSourceType.User, DefaultAddress, JoinPointName).ShouldBe(0);
            CalculatePoints(acceptLog, DefaultOfficialDomain, IncomeSourceType.User, DefaultAddress, JoinPointName)
                .ShouldBe(UserPoints * KolPointsPercent / 10000);

            // settle
            GetPointsBalance(dappId, domain, IncomeSourceType.User, DefaultAddress, SettlePointName).Result.ShouldBe(0);
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, DefaultAddress, SettlePointName)
                .Result.ShouldBe(UserPoints * KolPointsPercent / 10000);
            CalculatePoints(settleLog, domain, IncomeSourceType.User, DefaultAddress, SettlePointName).ShouldBe(0);
            CalculatePoints(settleLog, DefaultOfficialDomain, IncomeSourceType.User, DefaultAddress, SettlePointName)
                .ShouldBe(UserPoints * KolPointsPercent / 10000);

            // self increasing
            GetPointsBalance(dappId, domain, IncomeSourceType.User, DefaultAddress, SelfIncreasingPointName).Result
                .ShouldBe(UserPoints * Period);
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, DefaultAddress,
                SelfIncreasingPointName).Result.ShouldBe(UserPoints * KolPointsPercent / 10000 * Period);
            CalculatePoints(settleLog, domain, IncomeSourceType.User, DefaultAddress, SelfIncreasingPointName)
                .ShouldBe(UserPoints * Period);
            CalculatePoints(settleLog, DefaultOfficialDomain, IncomeSourceType.User, DefaultAddress,
                SelfIncreasingPointName).ShouldBe(UserPoints * KolPointsPercent / 10000 * Period);
        }

        // invitee
        {
            // accept referral
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, UserAddress, JoinPointName).Result
                .ShouldBe(UserPoints);
            CalculatePoints(acceptLog, DefaultOfficialDomain, IncomeSourceType.User, UserAddress, JoinPointName)
                .ShouldBe(UserPoints);

            // settle
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, UserAddress, SettlePointName).Result
                .ShouldBe(UserPoints);
            CalculatePoints(settleLog, DefaultOfficialDomain, IncomeSourceType.User, UserAddress, SettlePointName)
                .ShouldBe(UserPoints);

            // self increasing
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, UserAddress, SelfIncreasingPointName)
                .Result.ShouldBe(UserPoints * Period);
            CalculatePoints(settleLog, DefaultOfficialDomain, IncomeSourceType.User, UserAddress,
                SelfIncreasingPointName).ShouldBe(UserPoints * Period);
        }
    }

    [Fact]
    public async Task AcceptReferralTests_Fail()
    {
        const string domain = "user.com";

        var dappId = await InitializeForAcceptReferralTests();

        {
            var result = await PointsContractStub.AcceptReferral.SendWithExceptionAsync(new AcceptReferralInput());
            result.TransactionResult.Error.ShouldContain("No permission.");
        }
        {
            var result = await PointsContractStub.AcceptReferral.SendWithExceptionAsync(new AcceptReferralInput
            {
                DappId = new Hash()
            });
            result.TransactionResult.Error.ShouldContain("No permission.");
        }
        {
            var result = await PointsContractStub.AcceptReferral.SendWithExceptionAsync(new AcceptReferralInput
            {
                DappId = dappId
            });
            result.TransactionResult.Error.ShouldContain("Invalid referrer.");
        }
        {
            var result = await PointsContractStub.AcceptReferral.SendWithExceptionAsync(new AcceptReferralInput
            {
                DappId = dappId,
                Referrer = new Address()
            });
            result.TransactionResult.Error.ShouldContain("Invalid referrer.");
        }
        {
            var result = await PointsContractStub.AcceptReferral.SendWithExceptionAsync(new AcceptReferralInput
            {
                DappId = dappId,
                Referrer = UserAddress
            });
            result.TransactionResult.Error.ShouldContain("Referrer not joined.");
        }

        await PointsContractStub.Join.SendAsync(new JoinInput
        {
            DappId = dappId,
            Domain = DefaultOfficialDomain,
            Registrant = UserAddress
        });

        {
            var result = await PointsContractStub.AcceptReferral.SendWithExceptionAsync(new AcceptReferralInput
            {
                DappId = dappId,
                Referrer = UserAddress
            });
            result.TransactionResult.Error.ShouldContain("Invalid invitee.");
        }
        {
            var result = await PointsContractStub.AcceptReferral.SendWithExceptionAsync(new AcceptReferralInput
            {
                DappId = dappId,
                Referrer = UserAddress,
                Invitee = new Address()
            });
            result.TransactionResult.Error.ShouldContain("Invalid invitee.");
        }
        {
            var result = await PointsContractStub.AcceptReferral.SendWithExceptionAsync(new AcceptReferralInput
            {
                DappId = dappId,
                Referrer = User2Address,
                Invitee = User2Address
            });
            result.TransactionResult.Error.ShouldContain("Referrer not joined.");
        }
        {
            var result = await PointsContractStub.AcceptReferral.SendWithExceptionAsync(new AcceptReferralInput
            {
                DappId = dappId,
                Referrer = UserAddress,
                Invitee = UserAddress
            });
            result.TransactionResult.Error.ShouldContain("A dapp can only be registered once.");
        }

        await PointsContractStub.Join.SendAsync(new JoinInput
        {
            DappId = dappId,
            Domain = DefaultOfficialDomain,
            Registrant = User2Address
        });

        {
            var result = await PointsContractStub.AcceptReferral.SendWithExceptionAsync(new AcceptReferralInput
            {
                DappId = dappId,
                Referrer = UserAddress,
                Invitee = User2Address
            });
            result.TransactionResult.Error.ShouldContain("A dapp can only be registered once.");
        }

        await PointsContractStub.ApplyToBeAdvocate.SendAsync(new ApplyToBeAdvocateInput
        {
            DappId = dappId,
            Domain = domain,
            Invitee = DefaultAddress
        });

        await PointsContractStub.Join.SendAsync(new JoinInput
        {
            DappId = dappId,
            Domain = domain,
            Registrant = User3Address
        });

        {
            var result = await PointsContractStub.AcceptReferral.SendWithExceptionAsync(new AcceptReferralInput
            {
                DappId = dappId,
                Referrer = UserAddress,
                Invitee = User3Address
            });
            result.TransactionResult.Error.ShouldContain("A dapp can only be registered once.");
        }
    }

    [Fact]
    public async Task AcceptReferralTests_ReferKol_Fail()
    {
        const string domain = "user.com";

        var dappId = await InitializeForAcceptReferralTests();

        await PointsContractStub.ApplyToBeAdvocate.SendAsync(new ApplyToBeAdvocateInput
        {
            DappId = dappId,
            Domain = domain,
            Invitee = DefaultAddress
        });

        await PointsContractStub.Join.SendAsync(new JoinInput
        {
            DappId = dappId,
            Domain = domain,
            Registrant = UserAddress
        });

        var result = await PointsContractStub.AcceptReferral.SendWithExceptionAsync(new AcceptReferralInput
        {
            DappId = dappId,
            Referrer = UserAddress,
            Invitee = DefaultAddress
        });
        result.TransactionResult.Error.ShouldContain("Can not refer kol.");
    }

    [Fact]
    public async Task GetReferralRelationInfo()
    {
        var dappId = await InitializeForAcceptReferralTests();

        await PointsContractStub.Join.SendAsync(new JoinInput
        {
            DappId = dappId,
            Domain = DefaultOfficialDomain,
            Registrant = DefaultAddress
        });

        await PointsContractStub.AcceptReferral.SendAsync(new AcceptReferralInput
        {
            DappId = dappId,
            Referrer = DefaultAddress,
            Invitee = UserAddress
        });

        {
            var output = await PointsContractStub.GetReferralRelationInfo.CallAsync(new GetReferralRelationInfoInput
            {
                DappId = dappId,
                Invitee = UserAddress
            });
            output.DappId.ShouldBe(dappId);
            output.Invitee.ShouldBe(UserAddress);
            output.Referrer.ShouldBe(DefaultAddress);
            output.Inviter.ShouldBeNull();
        }
        {
            var output = await PointsContractStub.GetReferralRelationInfo.CallAsync(new GetReferralRelationInfoInput
            {
                DappId = dappId,
                Invitee = DefaultAddress
            });
            output.ShouldBe(new ReferralRelationInfo());
        }
        {
            var output = await PointsContractStub.GetReferralRelationInfo.CallAsync(new GetReferralRelationInfoInput
            {
                Invitee = UserAddress
            });
            output.ShouldBe(new ReferralRelationInfo());
        }
        {
            var output = await PointsContractStub.GetReferralRelationInfo.CallAsync(new GetReferralRelationInfoInput
            {
                DappId = new Hash(),
                Invitee = UserAddress
            });
            output.ShouldBe(new ReferralRelationInfo());
        }
        {
            var output = await PointsContractStub.GetReferralRelationInfo.CallAsync(new GetReferralRelationInfoInput
            {
                DappId = dappId
            });
            output.ShouldBe(new ReferralRelationInfo());
        }
        {
            var output = await PointsContractStub.GetReferralRelationInfo.CallAsync(new GetReferralRelationInfoInput
            {
                DappId = dappId,
                Invitee = new Address()
            });
            output.ShouldBe(new ReferralRelationInfo());
        }
    }

    private async Task<Hash> InitializeForAcceptReferralTests()
    {
        await Initialize();
        var dappId = await AddDapp();
        await CreatePoint(dappId);
        await SetMaxApplyCount();

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
                        KolPointsPercent = 1000000,
                        InviterPointsPercent = 100000
                    },
                    new PointsRule
                    {
                        ActionName = JoinActionName,
                        PointName = JoinPointName,
                        UserPoints = UserPoints,
                        KolPointsPercent = KolPointsPercent,
                        InviterPointsPercent = InviterPointsPercent,
                        EnableProportionalCalculation = true
                    },
                    new PointsRule
                    {
                        ActionName = AcceptReferralActionName,
                        PointName = JoinPointName,
                        UserPoints = UserPoints,
                        KolPointsPercent = KolPointsPercent,
                        InviterPointsPercent = InviterPointsPercent,
                        EnableProportionalCalculation = true
                    },
                    new PointsRule
                    {
                        ActionName = SettleActionName,
                        PointName = SettlePointName,
                        UserPoints = UserPoints,
                        KolPointsPercent = KolPointsPercent,
                        InviterPointsPercent = InviterPointsPercent,
                        EnableProportionalCalculation = true
                    }
                }
            }
        });

        await PointsContractStub.SetSelfIncreasingPointsRules.SendAsync(new SetSelfIncreasingPointsRulesInput
        {
            DappId = dappId,
            SelfIncreasingPointsRule = new PointsRule
            {
                ActionName = SelfIncreaseActionName,
                PointName = SelfIncreasingPointName,
                UserPoints = UserPoints,
                KolPointsPercent = KolPointsPercent,
                InviterPointsPercent = InviterPointsPercent,
                EnableProportionalCalculation = true
            }
        });

        return dappId;
    }

    private T GetLogEvent<T>(TransactionResult transactionResult) where T : IEvent<T>, new()
    {
        var log = transactionResult.Logs.FirstOrDefault(l => l.Name == typeof(T).Name);
        log.ShouldNotBeNull();

        var logEvent = new T();
        logEvent.MergeFrom(log.NonIndexed);

        return logEvent;
    }

    private async Task<BigIntValue> GetPointsBalance(Hash dappId, string domain, IncomeSourceType type, Address address,
        string pointName)
    {
        var output = await PointsContractStub.GetPointsBalance.CallAsync(new GetPointsBalanceInput
        {
            DappId = dappId,
            Domain = domain,
            IncomeSourceType = type,
            Address = address,
            PointName = pointName
        });

        return output.BalanceValue;
    }

    private BigIntValue CalculatePoints(PointsChanged log, string domain, IncomeSourceType type, Address address,
        string pointsName)
    {
        var result = new BigIntValue(0);

        foreach (var detail in log.PointsChangedDetails.PointsDetails)
        {
            if (detail.PointsName == pointsName && detail.PointsReceiver == address && detail.Domain == domain &&
                detail.IncomeSourceType == type)
            {
                result = result.Add(detail.IncreaseValue);
            }
        }

        return result;
    }
}