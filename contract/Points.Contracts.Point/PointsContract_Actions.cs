using System.Linq;
using AElf;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Points.Contracts.Point;

public partial class PointsContract : PointsContractContainer.PointsContractBase
{
    public override Empty Initialize(InitializeInput input)
    {
        Assert(!State.Initialized.Value, "Already initialized.");
        State.GenesisContract.Value = Context.GetZeroSmartContractAddress();
        Assert(State.GenesisContract.GetContractAuthor.Call(Context.Self) == Context.Sender, "No permission.");
        Assert(input.Admin == null || !input.Admin.Value.IsNullOrEmpty(), "Invalid input admin.");
        State.Admin.Value = input.Admin ?? Context.Sender;
        State.Initialized.Value = true;

        return new Empty();
    }

    public override Empty SetAdmin(Address input)
    {
        AssertInitialized();
        AssertAdmin();
        Assert(input != null && !input.Value.IsNullOrEmpty(), "Invalid input.");

        State.Admin.Value = input;

        return new Empty();
    }

    public override Empty SetReservedDomainList(SetReservedDomainListInput input)
    {
        AssertInitialized();
        AssertAdmin();
        Assert(input.ReservedDomainList?.Domains?.Count > 0, "Invalid reserved domain list count.");

        var domains = input.ReservedDomainList.Domains.Distinct();
        State.ReservedDomains.Value = new ReservedDomainList { Domains = { domains } };

        return new Empty();
    }

    public override Empty SetMaxApplyDomainCount(Int32Value input)
    {
        AssertInitialized();
        AssertAdmin();
        Assert(input is { Value: > 0 }, "Invalid input.");

        State.MaxApplyCount.Value = input.Value;
        return new Empty();
    }
}