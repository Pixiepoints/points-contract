using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Points.Contracts.Point;

public partial class PointsContractTests : PointsContractTestBase
{
    [Fact]
    public async Task InitializeTests()
    {
        await Initialize();

        var admin = await PointsContractStub.GetAdmin.CallAsync(new Empty());
        admin.ShouldBe(DefaultAddress);

        // initialize twice
        var result = await PointsContractStub.Initialize.SendWithExceptionAsync(new InitializeInput
        {
            Admin = DefaultAddress
        });
        result.TransactionResult.Error.ShouldContain("Already initialized.");
    }

    [Fact]
    public async Task InitializeTests_Fail()
    {
        // empty address
        var result = await PointsContractStub.Initialize.SendWithExceptionAsync(new InitializeInput
        {
            Admin = new Address(),
        });
        result.TransactionResult.Error.ShouldContain("Invalid input admin.");

        // sender != author
        result = await PointsContractUserStub.Initialize.SendWithExceptionAsync(new InitializeInput
        {
            Admin = UserAddress,
        });
        result.TransactionResult.Error.ShouldContain("No permission.");
    }

    [Fact]
    public async Task SetAdminTests()
    {
        await Initialize();

        var output = await PointsContractStub.GetAdmin.CallAsync(new Empty());
        output.ShouldBe(DefaultAddress);

        var result = await PointsContractStub.SetAdmin.SendAsync(DefaultAddress);
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

        await PointsContractStub.SetAdmin.SendAsync(UserAddress);
        output = await PointsContractStub.GetAdmin.CallAsync(new Empty());
        output.ShouldBe(UserAddress);
    }

    [Fact]
    public async Task SetAdminTests_Fail()
    {
        await Initialize();

        var result = await PointsContractStub.SetAdmin.SendWithExceptionAsync(new Address());
        result.TransactionResult.Error.ShouldContain("Invalid input.");

        result = await PointsContractUserStub.SetAdmin.SendWithExceptionAsync(UserAddress);
        result.TransactionResult.Error.ShouldContain("No permission.");
    }

    [Fact]
    public async Task CreatePointTests()
    {
        await Initialize();
        var dappId = await AddDapp();
        var result = await PointsContractStub.CreatePoint.SendAsync(new CreatePointInput
        {
            DappId = dappId,
            PointsName = DefaultPointName,
            Decimals = 8
        });
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
    }

    [Fact]
    public async Task CreatePointTests_Fail()
    {
        var result = await PointsContractStub.CreatePoint.SendWithExceptionAsync(new CreatePointInput());
        result.TransactionResult.Error.ShouldContain("Not initialized.");

        await Initialize();

        result = await PointsContractUserStub.CreatePoint.SendWithExceptionAsync(new CreatePointInput());
        result.TransactionResult.Error.ShouldContain("No permission.");

        var dappId = await AddDapp();

        result = await PointsContractStub.CreatePoint.SendWithExceptionAsync(new CreatePointInput
        {
            DappId = dappId
        });
        result.TransactionResult.Error.ShouldContain("Invalid input.");

        result = await PointsContractStub.CreatePoint.SendWithExceptionAsync(new CreatePointInput
        {
            DappId = dappId,
            PointsName = ""
        });
        result.TransactionResult.Error.ShouldContain("Invalid input.");

        result = await PointsContractStub.CreatePoint.SendWithExceptionAsync(new CreatePointInput
        {
            DappId = dappId,
            Decimals = 8
        });
        result.TransactionResult.Error.ShouldContain("Invalid input.");

        result = await PointsContractStub.CreatePoint.SendWithExceptionAsync(new CreatePointInput
        {
            DappId = dappId,
            PointsName = DefaultPointName,
            Decimals = -1
        });
        result.TransactionResult.Error.ShouldContain("Invalid input.");

        result = await PointsContractStub.CreatePoint.SendWithExceptionAsync(new CreatePointInput
        {
            DappId = dappId,
            PointsName = string.Join("-", Enumerable.Repeat("abcdefghijklmnopqrstuvwxyz", 4))
        });
        result.TransactionResult.Error.ShouldContain("Invalid input.");

        result = await PointsContractStub.CreatePoint.SendWithExceptionAsync(new CreatePointInput
        {
            DappId = dappId,
            PointsName = DefaultPointName,
            Decimals = 19
        });
        result.TransactionResult.Error.ShouldContain("Invalid input.");

        await CreatePoint(dappId);
        result = await PointsContractStub.CreatePoint.SendWithExceptionAsync(new CreatePointInput
        {
            DappId = dappId,
            PointsName = DefaultPointName,
            Decimals = 8
        });
        result.TransactionResult.Error.ShouldContain("Point token already exists.");
    }

    [Fact]
    public async Task MaxApplyCountTest()
    {
        await Initialize();

        await SetMaxApplyCount();
        var getResult = await PointsContractStub.GetMaxApplyCount.CallAsync(new Empty());
        getResult.ShouldBe(DefaultMaxApply);
    }

    [Fact]
    public async Task MaxApplyCountTest_Fail()
    {
        var result = await PointsContractStub.SetMaxApplyDomainCount.SendWithExceptionAsync(DefaultMaxApply);
        result.TransactionResult.Error.ShouldContain("Not initialized.");

        await Initialize();

        result = await PointsContractUserStub.SetMaxApplyDomainCount.SendWithExceptionAsync(DefaultMaxApply);
        result.TransactionResult.Error.ShouldContain("No permission.");

        var errorMaxApply = new Int32Value { Value = -1 };
        result = await PointsContractStub.SetMaxApplyDomainCount.SendWithExceptionAsync(errorMaxApply);
        result.TransactionResult.Error.ShouldContain("Invalid input.");

        errorMaxApply = new Int32Value();
        result = await PointsContractStub.SetMaxApplyDomainCount.SendWithExceptionAsync(errorMaxApply);
        result.TransactionResult.Error.ShouldContain("Invalid input.");
    }

    [Fact]
    public async Task AddReservedDomainsTests()
    {
        await Initialize();

        var list = new List<string>
        {
            "domain.com"
        };

        var output = await PointsContractStub.CheckDomainReserved.CallAsync(new StringValue
        {
            Value = "domain.com"
        });
        output.Value.ShouldBeFalse();
        
        var result = await PointsContractStub.AddReservedDomains.SendAsync(new AddReservedDomainsInput
        {
            Domains = { list }
        });
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

        var log = GetLogEvent<ReservedDomainsAdded>(result.TransactionResult);
        log.DomainList.Domains.ShouldBe(list);
        
        output = await PointsContractStub.CheckDomainReserved.CallAsync(new StringValue
        {
            Value = "domain.com"
        });
        output.Value.ShouldBeTrue();
        
        var dappId = await AddDapp();
        await CreatePoint(dappId);
        await SetDappPointsRules(dappId);
        await SetSelfIncreasingPointsRules(dappId);
        await SetMaxApplyCount();

        result = await PointsContractStub.ApplyToBeAdvocate.SendWithExceptionAsync(new ApplyToBeAdvocateInput
        {
            DappId = dappId,
            Domain = "domain.com",
            Invitee = DefaultAddress
        });
        result.TransactionResult.Error.ShouldContain("This domain name is an officially reserved domain name");
        
        result = await PointsContractStub.AddReservedDomains.SendAsync(new AddReservedDomainsInput
        {
            Domains = { list }
        });
        
        result.TransactionResult.Logs.Any(l => l.Name == nameof(ReservedDomainsAdded)).ShouldBeFalse();
        
        result = await PointsContractStub.AddReservedDomains.SendAsync(new AddReservedDomainsInput
        {
            Domains = { "" }
        });
        
        result.TransactionResult.Logs.Any(l => l.Name == nameof(ReservedDomainsAdded)).ShouldBeFalse();
        
        result = await PointsContractStub.AddReservedDomains.SendAsync(new AddReservedDomainsInput
        {
            Domains = { "test.com", "test.com", "" }
        });
        GetLogEvent<ReservedDomainsAdded>(result.TransactionResult).DomainList.Domains.Count.ShouldBe(1);
    }

    [Fact]
    public async Task AddReservedDomainsTests_Fail()
    {
        await Initialize();

        var result = await PointsContractUserStub.AddReservedDomains.SendWithExceptionAsync(new AddReservedDomainsInput());
        result.TransactionResult.Error.ShouldContain("No permission");
        
        result = await PointsContractStub.AddReservedDomains.SendWithExceptionAsync(new AddReservedDomainsInput());
        result.TransactionResult.Error.ShouldContain("Invalid domains.");
        
        result = await PointsContractStub.AddReservedDomains.SendWithExceptionAsync(new AddReservedDomainsInput
        {
            Domains = {  }
        });
        result.TransactionResult.Error.ShouldContain("Invalid domains.");
    }

    [Fact]
    public async Task RemoveReservedDomainsTests()
    {
        await Initialize();

        var list = new List<string>
        {
            "domain.com"
        };

        await PointsContractStub.AddReservedDomains.SendAsync(new AddReservedDomainsInput { Domains = { list } });
        
        var dappId = await AddDapp();
        await CreatePoint(dappId);
        await SetDappPointsRules(dappId);
        await SetSelfIncreasingPointsRules(dappId);
        await SetMaxApplyCount();

        var result = await PointsContractStub.ApplyToBeAdvocate.SendWithExceptionAsync(new ApplyToBeAdvocateInput
        {
            DappId = dappId,
            Domain = "domain.com",
            Invitee = DefaultAddress
        });
        result.TransactionResult.Error.ShouldContain("This domain name is an officially reserved domain name");

        var output = await PointsContractStub.CheckDomainReserved.CallAsync(new StringValue
        {
            Value = "domain.com"
        });
        output.Value.ShouldBeTrue();
        
        result = await PointsContractStub.RemoveReservedDomains.SendAsync(new RemoveReservedDomainsInput
        {
            Domains = { list }
        });
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

        var log = GetLogEvent<ReservedDomainsRemoved>(result.TransactionResult);
        log.DomainList.Domains.ShouldBe(list);
        
        output = await PointsContractStub.CheckDomainReserved.CallAsync(new StringValue
        {
            Value = "domain.com"
        });
        output.Value.ShouldBeFalse();

        result = await PointsContractStub.ApplyToBeAdvocate.SendAsync(new ApplyToBeAdvocateInput
        {
            DappId = dappId,
            Domain = "domain.com",
            Invitee = DefaultAddress
        });
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
    }
    
    [Fact]
    public async Task RemoveReservedDomainsTests_Fail()
    {
        await Initialize();

        var result = await PointsContractUserStub.RemoveReservedDomains.SendWithExceptionAsync(new RemoveReservedDomainsInput());
        result.TransactionResult.Error.ShouldContain("No permission");
        
        result = await PointsContractStub.RemoveReservedDomains.SendWithExceptionAsync(new RemoveReservedDomainsInput());
        result.TransactionResult.Error.ShouldContain("Invalid domains.");
        
        result = await PointsContractStub.RemoveReservedDomains.SendWithExceptionAsync(new RemoveReservedDomainsInput
        {
            Domains = {  }
        });
        result.TransactionResult.Error.ShouldContain("Invalid domains.");
    }

    private async Task SetMaxApplyCount() => await PointsContractStub.SetMaxApplyDomainCount.SendAsync(DefaultMaxApply);
    private async Task Initialize() => await PointsContractStub.Initialize.SendAsync(new InitializeInput());

    private async Task CreatePoint(Hash dappId)
    {
        await PointsContractStub.CreatePoint.SendAsync(new CreatePointInput
            { DappId = dappId, PointsName = DefaultPointName, Decimals = 8 });
        await PointsContractStub.CreatePoint.SendAsync(new CreatePointInput
            { DappId = dappId, PointsName = JoinPointName, Decimals = 8 });
        await PointsContractStub.CreatePoint.SendAsync(new CreatePointInput
            { DappId = dappId, PointsName = SelfIncreasingPointName, Decimals = 8 });
        await PointsContractStub.CreatePoint.SendAsync(new CreatePointInput
            { DappId = dappId, PointsName = SettlePointName, Decimals = 8 });
    }
    
    private async Task CreatePointForSettle(Hash dappId)
    {
        await PointsContractStub.CreatePoint.SendAsync(new CreatePointInput
            { DappId = dappId, PointsName = "XPSGR-5", Decimals = 8 });
        await PointsContractStub.CreatePoint.SendAsync(new CreatePointInput
            { DappId = dappId, PointsName = "XPSGR-6", Decimals = 8 });
        await PointsContractStub.CreatePoint.SendAsync(new CreatePointInput
            { DappId = dappId, PointsName = "XPSGR-7", Decimals = 8 });
    }
}