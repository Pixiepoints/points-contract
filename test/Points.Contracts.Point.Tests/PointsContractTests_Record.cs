using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.CSharp.Core.Extension;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Points.Contracts.Point;

public partial class PointsContractTests
{
    [Fact]
    public async Task ApplyToBeAdvocateTests()
    {
        await Initialize();
        var dappId = await AddDapp();
        await CreatePoint(dappId);
        await SetDappPointsRules(dappId);
        await SetSelfIncreasingPointsRules(dappId);
        await SetMaxApplyCount();

        const string domain = "user.com";
        var result = await PointsContractStub.ApplyToBeAdvocate.SendAsync(new ApplyToBeAdvocateInput
        {
            Domain = domain,
            DappId = dappId,
            Invitee = UserAddress
        });
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

        var getResult = await PointsContractStub.GetDomainApplyInfo.CallAsync(new StringValue { Value = domain });
        getResult.Domain.ShouldBe(domain);
        getResult.Invitee.ShouldBe(UserAddress);
        getResult.Inviter.ShouldBe(DefaultAddress);

        const string domain1 = "user1.com";
        result = await PointsContractStub.ApplyToBeAdvocate.SendAsync(new ApplyToBeAdvocateInput
        {
            Domain = domain1,
            DappId = dappId,
            Invitee = DefaultAddress
        });
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

        getResult = await PointsContractStub.GetDomainApplyInfo.CallAsync(new StringValue { Value = domain1 });
        getResult.Domain.ShouldBe(domain1);
        getResult.Invitee.ShouldBe(DefaultAddress);
        getResult.Inviter.ShouldBeNull();

        var getBalanceResult = await PointsContractStub.GetPointsBalance.CallAsync(new GetPointsBalanceInput
        {
            DappId = dappId,
            Address = DefaultAddress,
            Domain = domain,
            IncomeSourceType = IncomeSourceType.Inviter,
            PointName = DefaultPointName
        });
        getBalanceResult.PointName.ShouldBe(DefaultPointName);
        getBalanceResult.BalanceValue.ShouldBe(100000);

        getBalanceResult = await PointsContractStub.GetPointsBalance.CallAsync(new GetPointsBalanceInput
        {
            DappId = dappId,
            Address = DefaultAddress,
            Domain = domain1,
            IncomeSourceType = IncomeSourceType.Kol,
            PointName = DefaultPointName
        });
        getBalanceResult.PointName.ShouldBe(DefaultPointName);
        getBalanceResult.BalanceValue.ShouldBe(1000000);
    }

    [Fact]
    public async Task ApplyToBeAdvocateTests_Fail()
    {
        await Initialize();
        var dappId = await AddDapp();
        await SetMaxApplyCount();

        const string domain = "example.com";
        var result = await PointsContractStub.ApplyToBeAdvocate.SendWithExceptionAsync(new ApplyToBeAdvocateInput
        {
            Domain = string.Join(".", Enumerable.Repeat("abcdefghijklmnopqrstuvwxyz", 10)),
            DappId = dappId,
            Invitee = UserAddress
        });
        result.TransactionResult.Error.ShouldContain("Invalid domain.");

        result = await PointsContractStub.ApplyToBeAdvocate.SendWithExceptionAsync(new ApplyToBeAdvocateInput
        {
            Domain = "ABC.com",
            DappId = dappId,
            Invitee = UserAddress
        });
        result.TransactionResult.Error.ShouldContain("Invalid domain.");

        result = await PointsContractStub.ApplyToBeAdvocate.SendWithExceptionAsync(new ApplyToBeAdvocateInput
        {
            Domain = ".abc.com",
            DappId = dappId,
            Invitee = UserAddress
        });
        result.TransactionResult.Error.ShouldContain("Invalid domain.");

        result = await PointsContractStub.ApplyToBeAdvocate.SendWithExceptionAsync(new ApplyToBeAdvocateInput
        {
            Domain = "abc.com.",
            DappId = dappId,
            Invitee = UserAddress
        });
        result.TransactionResult.Error.ShouldContain("Invalid domain.");

        result = await PointsContractStub.ApplyToBeAdvocate.SendWithExceptionAsync(new ApplyToBeAdvocateInput
        {
            Domain = "€.com",
            DappId = dappId,
            Invitee = UserAddress
        });
        result.TransactionResult.Error.ShouldContain("Invalid domain.");

        result = await PointsContractStub.ApplyToBeAdvocate.SendWithExceptionAsync(new ApplyToBeAdvocateInput
        {
            Domain = "a.b.c.com",
            DappId = dappId,
            Invitee = UserAddress
        });
        result.TransactionResult.Error.ShouldContain("Invalid domain.");

        result = await PointsContractStub.ApplyToBeAdvocate.SendWithExceptionAsync(new ApplyToBeAdvocateInput
        {
            Domain = "*.com",
            DappId = dappId,
            Invitee = UserAddress
        });
        result.TransactionResult.Error.ShouldContain("Invalid domain.");

        result = await PointsContractStub.ApplyToBeAdvocate.SendWithExceptionAsync(new ApplyToBeAdvocateInput
        {
            DappId = dappId,
            Invitee = UserAddress
        });
        result.TransactionResult.Error.ShouldContain("Invalid domain.");

        result = await PointsContractStub.ApplyToBeAdvocate.SendWithExceptionAsync(new ApplyToBeAdvocateInput
        {
            Domain = domain,
            DappId = HashHelper.ComputeFrom("NotExistDappName"),
            Invitee = UserAddress
        });
        result.TransactionResult.Error.ShouldContain("Invalid dapp id.");

        result = await PointsContractStub.ApplyToBeAdvocate.SendWithExceptionAsync(new ApplyToBeAdvocateInput
        {
            Domain = domain,
            DappId = dappId,
        });
        result.TransactionResult.Error.ShouldContain("Invalid invitee.");

        await CreatePoint(dappId);
        await SetDappPointsRules(dappId);
        await SetSelfIncreasingPointsRules(dappId);
        await PointsContractStub.ApplyToBeAdvocate.SendAsync(new ApplyToBeAdvocateInput
        {
            Domain = domain,
            DappId = dappId,
            Invitee = UserAddress
        });

        result = await PointsContractStub.ApplyToBeAdvocate.SendWithExceptionAsync(new ApplyToBeAdvocateInput
        {
            Domain = domain,
            DappId = dappId,
            Invitee = UserAddress
        });
        result.TransactionResult.Error.ShouldContain("Domain has Exist.");

        for (var i = 0; i < DefaultMaxApply.Value; i++)
        {
            await PointsContractUser2Stub.ApplyToBeAdvocate.SendAsync(new ApplyToBeAdvocateInput
            {
                Domain = $"user{i}.com",
                DappId = dappId,
                Invitee = UserAddress
            });
        }

        result = await PointsContractUser2Stub.ApplyToBeAdvocate.SendWithExceptionAsync(new ApplyToBeAdvocateInput
        {
            Domain = "exceedMaxApply.com",
            DappId = dappId,
            Invitee = UserAddress
        });
        result.TransactionResult.Error.ShouldContain("Apply count exceed the limit.");
    }

    [Fact]
    public async Task<Hash> JoinTests()
    {
        await Initialize();
        var dappId = await AddDapp();
        await CreatePoint(dappId);
        await SetDappPointsRules(dappId);
        await SetSelfIncreasingPointsRules(dappId);
        await SetMaxApplyCount();

        var currentBlockTime = BlockTimeProvider.GetBlockTime();
        // official domain
        var result = await PointsContractStub.Join.SendAsync(new JoinInput
        {
            DappId = dappId,
            Domain = DefaultOfficialDomain,
            Registrant = DefaultAddress
        });
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

        BlockTimeProvider.SetBlockTime(currentBlockTime.AddSeconds(5));
        var getBalanceResult = await PointsContractStub.GetPointsBalance.CallAsync(new GetPointsBalanceInput
        {
            DappId = dappId,
            Address = DefaultAddress,
            Domain = DefaultOfficialDomain,
            IncomeSourceType = IncomeSourceType.User,
            PointName = JoinPointName
        });
        getBalanceResult.PointName.ShouldBe(JoinPointName);
        getBalanceResult.BalanceValue.ShouldBe(20000000); //
        getBalanceResult = await PointsContractStub.GetPointsBalance.CallAsync(new GetPointsBalanceInput
        {
            DappId = dappId,
            Address = DefaultAddress,
            Domain = DefaultOfficialDomain,
            IncomeSourceType = IncomeSourceType.User,
            PointName = SelfIncreasingPointName
        });
        getBalanceResult.PointName.ShouldBe(SelfIncreasingPointName);
        getBalanceResult.BalanceValue.ShouldBe(5 * 10000000); // join + 20000000, increasing + 10000000*5

        // create domain
        const string domain = "abc.com";
        await PointsContractStub.ApplyToBeAdvocate.SendAsync(new ApplyToBeAdvocateInput
        {
            Domain = domain,
            DappId = dappId,
            Invitee = UserAddress
        });
        result = await PointsContractStub.Join.SendAsync(new JoinInput
        {
            DappId = dappId,
            Domain = domain,
            Registrant = User2Address
        });
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

        BlockTimeProvider.SetBlockTime(currentBlockTime.AddSeconds(10));

        // user balance
        getBalanceResult = await PointsContractStub.GetPointsBalance.CallAsync(new GetPointsBalanceInput
        {
            DappId = dappId,
            Address = User2Address,
            Domain = domain,
            IncomeSourceType = IncomeSourceType.User,
            PointName = JoinPointName
        });
        getBalanceResult.PointName.ShouldBe(JoinPointName);
        getBalanceResult.BalanceValue.ShouldBe(20000000); // join + 20000000, increasing + 10000000*5
        getBalanceResult = await PointsContractStub.GetPointsBalance.CallAsync(new GetPointsBalanceInput
        {
            DappId = dappId,
            Address = User2Address,
            Domain = domain,
            IncomeSourceType = IncomeSourceType.User,
            PointName = SelfIncreasingPointName
        });
        getBalanceResult.PointName.ShouldBe(SelfIncreasingPointName);
        getBalanceResult.BalanceValue.ShouldBe(5 * 10000000); // join + 20000000, increasing + 10000000*5

        // invitee balance
        getBalanceResult = await PointsContractStub.GetPointsBalance.CallAsync(new GetPointsBalanceInput
        {
            DappId = dappId,
            Address = UserAddress,
            Domain = domain,
            IncomeSourceType = IncomeSourceType.Kol,
            PointName = JoinPointName
        });
        getBalanceResult.PointName.ShouldBe(JoinPointName);
        getBalanceResult.BalanceValue.ShouldBe(2000000); // join + 20000000, increasing + 10000000*5
        getBalanceResult = await PointsContractStub.GetPointsBalance.CallAsync(new GetPointsBalanceInput
        {
            DappId = dappId,
            Address = UserAddress,
            Domain = domain,
            IncomeSourceType = IncomeSourceType.Kol,
            PointName = SelfIncreasingPointName
        });
        getBalanceResult.PointName.ShouldBe(SelfIncreasingPointName);
        getBalanceResult.BalanceValue.ShouldBe(5 * 1000000); // join + 20000000, increasing + 10000000*5

        // inviter balance
        getBalanceResult = await PointsContractStub.GetPointsBalance.CallAsync(new GetPointsBalanceInput
        {
            DappId = dappId,
            Address = DefaultAddress,
            Domain = domain,
            IncomeSourceType = IncomeSourceType.Inviter,
            PointName = JoinPointName
        });
        getBalanceResult.PointName.ShouldBe(JoinPointName);
        getBalanceResult.BalanceValue.ShouldBe(200000); // join + 20000000, increasing + 10000000*5
        getBalanceResult = await PointsContractStub.GetPointsBalance.CallAsync(new GetPointsBalanceInput
        {
            DappId = dappId,
            Address = DefaultAddress,
            Domain = domain,
            IncomeSourceType = IncomeSourceType.Inviter,
            PointName = SelfIncreasingPointName
        });
        getBalanceResult.PointName.ShouldBe(SelfIncreasingPointName);
        getBalanceResult.BalanceValue.ShouldBe(5 * 100000); // join + 20000000, increasing + 10000000*5

        return dappId;
    }

    [Fact]
    public async Task JoinTests_Fail()
    {
        await Initialize();
        var dappId = await AddDapp();
        await CreatePoint(dappId);
        await SetMaxApplyCount();

        // not dapp admin
        var result = await PointsContractUserStub.Join.SendWithExceptionAsync(new JoinInput
        {
            DappId = dappId,
            Domain = DefaultOfficialDomain,
            Registrant = DefaultAddress
        });
        result.TransactionResult.Error.ShouldContain("No permission.");

        // register is null
        result = await PointsContractStub.Join.SendWithExceptionAsync(new JoinInput
        {
            DappId = dappId,
            Domain = DefaultOfficialDomain,
        });
        result.TransactionResult.Error.ShouldContain("Invalid registrant address.");

        // invalid domain 
        result = await PointsContractStub.Join.SendWithExceptionAsync(new JoinInput
        {
            DappId = dappId,
            Domain = "",
            Registrant = DefaultAddress
        });
        result.TransactionResult.Error.ShouldContain("Invalid domain.");
        result = await PointsContractStub.Join.SendWithExceptionAsync(new JoinInput
        {
            DappId = dappId,
            Domain = string.Join(".", Enumerable.Repeat("abcdefghijklmnopqrstuvwxyz", 10)),
            Registrant = DefaultAddress
        });
        result.TransactionResult.Error.ShouldContain("Invalid domain.");

        // join not exist domain
        result = await PointsContractStub.Join.SendWithExceptionAsync(new JoinInput
        {
            DappId = dappId,
            Domain = "not-exist.com",
            Registrant = DefaultAddress
        });
        result.TransactionResult.Error.ShouldContain("Not exist domain.");

        // register twice
        await SetDappPointsRules(dappId);
        await SetSelfIncreasingPointsRules(dappId);
        await PointsContractStub.Join.SendAsync(new JoinInput
        {
            DappId = dappId,
            Domain = DefaultOfficialDomain,
            Registrant = DefaultAddress
        });
        result = await PointsContractStub.Join.SendWithExceptionAsync(new JoinInput
        {
            DappId = dappId,
            Domain = DefaultOfficialDomain,
            Registrant = DefaultAddress
        });
        result.TransactionResult.Error.ShouldContain("A dapp can only be registered once.");
    }

    [Fact]
    public async Task ApplyToBeAdvocateTestsUpdated()
    {
        const string domain = "user.com";
        const string domain2 = "user2.com";
        const string domain3 = "user3.com";

        await Initialize();
        var dappId = await AddDapp();
        await CreatePoint(dappId);
        await SetDappPointsRules(dappId);
        await SetSelfIncreasingPointsRules(dappId);
        await SetMaxApplyCount();

        {
            var result = await PointsContractStub.ApplyToBeAdvocate.SendAsync(new ApplyToBeAdvocateInput
            {
                Domain = domain,
                DappId = dappId,
                Invitee = User2Address,
                Inviter = UserAddress
            });
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

            var log = GetLogEvent<InviterApplied>(result.TransactionResult);
            log.DappId.ShouldBe(dappId);
            log.Domain.ShouldBe(domain);
            log.Invitee.ShouldBe(User2Address);
            log.Inviter.ShouldBe(UserAddress);

            var output = await PointsContractStub.GetDomainApplyInfo.CallAsync(new StringValue { Value = domain });
            output.Domain.ShouldBe(domain);
            output.Invitee.ShouldBe(User2Address);
            output.Inviter.ShouldBe(UserAddress);
        }
        {
            var result = await PointsContractStub.ApplyToBeAdvocate.SendAsync(new ApplyToBeAdvocateInput
            {
                Domain = domain2,
                DappId = dappId,
                Invitee = UserAddress
            });
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

            var log = GetLogEvent<InviterApplied>(result.TransactionResult);
            log.DappId.ShouldBe(dappId);
            log.Domain.ShouldBe(domain2);
            log.Invitee.ShouldBe(UserAddress);
            log.Inviter.ShouldBe(DefaultAddress);

            var output = await PointsContractStub.GetDomainApplyInfo.CallAsync(new StringValue { Value = domain2 });
            output.Domain.ShouldBe(domain2);
            output.Invitee.ShouldBe(UserAddress);
            output.Inviter.ShouldBe(DefaultAddress);
        }
        {
            var result = await PointsContractStub.ApplyToBeAdvocate.SendAsync(new ApplyToBeAdvocateInput
            {
                Domain = domain3,
                DappId = dappId,
                Invitee = DefaultAddress,
                Inviter = DefaultAddress
            });
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

            var log = GetLogEvent<InviterApplied>(result.TransactionResult);
            log.DappId.ShouldBe(dappId);
            log.Domain.ShouldBe(domain3);
            log.Invitee.ShouldBe(DefaultAddress);
            log.Inviter.ShouldBe(DefaultAddress);

            var output = await PointsContractStub.GetDomainApplyInfo.CallAsync(new StringValue { Value = domain3 });
            output.Domain.ShouldBe(domain3);
            output.Invitee.ShouldBe(DefaultAddress);
            output.Inviter.ShouldBeNull();
        }
    }

    [Fact]
    public async Task ApplyToBeAdvocateTestsUpdated_Fail()
    {
        const string domain = "user.com";

        await Initialize();
        var dappId = await AddDapp();
        await CreatePoint(dappId);
        await SetDappPointsRules(dappId);
        await SetSelfIncreasingPointsRules(dappId);
        await SetMaxApplyCount();

        var result = await PointsContractStub.ApplyToBeAdvocate.SendWithExceptionAsync(new ApplyToBeAdvocateInput
        {
            Domain = domain,
            DappId = dappId,
            Invitee = User2Address,
            Inviter = new Address()
        });
        result.TransactionResult.Error.ShouldContain("Invalid inviter.");
    }
}