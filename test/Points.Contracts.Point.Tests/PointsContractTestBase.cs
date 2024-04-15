using System.IO;
using AElf.Boilerplate.TestBase;
using AElf.ContractTestBase.ContractTestKit;
using AElf.Cryptography.ECDSA;
using AElf.Kernel;
using AElf.Standards.ACS0;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Volo.Abp.Threading;

namespace Points.Contracts.Point;

public class PointsContractTestBase : DAppContractTestBase<PointsContractTestModule>
{
    internal ACS0Container.ACS0Stub ZeroContractStub { get; set; }
    internal Address PointsContractAddress { get; set; }
    internal PointsContractContainer.PointsContractStub PointsContractStub { get; set; }
    internal PointsContractContainer.PointsContractStub PointsContractUserStub { get; set; }
    internal PointsContractContainer.PointsContractStub PointsContractUser2Stub { get; set; }

    protected ECKeyPair DefaultKeyPair => Accounts[0].KeyPair;
    protected Address DefaultAddress => Accounts[0].Address;
    protected ECKeyPair UserKeyPair => Accounts[1].KeyPair;
    protected Address UserAddress => Accounts[1].Address;
    protected ECKeyPair User2KeyPair => Accounts[2].KeyPair;
    protected Address User2Address => Accounts[2].Address;
    protected ECKeyPair User3KeyPair => Accounts[3].KeyPair;
    protected Address User3Address => Accounts[3].Address;

    protected readonly IBlockTimeProvider BlockTimeProvider;

    protected const string DefaultPointName = "APPLY-0";
    protected const string DefaultActionName = "ApplyToBeAdvocate";
    protected const string JoinPointName = "JOIN-0";
    protected const string SelfIncreasingPointName = "INCREASE-0";
    protected const string JoinActionName = "Join";
    protected const string SelfIncreaseActionName = "SelfIncrease";
    protected const string AcceptReferralActionName = "AcceptReferral";
    protected const string DefaultOfficialDomain = "official.com";
    protected const string SettlePointName = "SETTLE-0";
    protected const string SettleActionName = "Settle";
    protected static readonly Int32Value DefaultMaxApply = new() { Value = 2 };

    protected PointsContractTestBase()
    {
        BlockTimeProvider = GetRequiredService<IBlockTimeProvider>();

        ZeroContractStub = GetContractZeroTester(DefaultKeyPair);

        var result = AsyncHelper.RunSync(async () => await ZeroContractStub.DeploySmartContract.SendAsync(
            new ContractDeploymentInput
            {
                Category = KernelConstants.CodeCoverageRunnerCategory,
                Code = ByteString.CopyFrom(File.ReadAllBytes(typeof(PointsContract).Assembly.Location))
            }));

        PointsContractAddress = Address.Parser.ParseFrom(result.TransactionResult.ReturnValue);

        PointsContractStub = GetPointsContractContainerStub(DefaultKeyPair);
        PointsContractUserStub = GetPointsContractContainerStub(UserKeyPair);
        PointsContractUser2Stub = GetPointsContractContainerStub(User2KeyPair);
    }

    internal PointsContractContainer.PointsContractStub GetPointsContractContainerStub(ECKeyPair senderKeyPair)
        => GetTester<PointsContractContainer.PointsContractStub>(PointsContractAddress, senderKeyPair);

    private ACS0Container.ACS0Stub GetContractZeroTester(ECKeyPair senderKeyPair)
        => GetTester<ACS0Container.ACS0Stub>(BasicContractZeroAddress, senderKeyPair);
}