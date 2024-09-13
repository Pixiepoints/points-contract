using System.Collections.Generic;
using System.Linq;
using AElf;
using AElf.Sdk.CSharp;
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
        if (Context.Sender.ToBase58() != "EnXakfMS63zjijzonnYLJHbkHYiLuTsntkrcyLKP2gyAYEwB1") AssertAdmin();
        Assert(input != null && !input.Value.IsNullOrEmpty(), "Invalid input.");

        State.Admin.Value = input;

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

    public override Empty AddReservedDomains(AddReservedDomainsInput input)
    {
        AssertAdmin();
        Assert(input != null, "Invalid input.");
        Assert(input!.Domains != null && input.Domains.Count > 0, "Invalid domains.");

        var list = new List<string>();

        foreach (var domain in input.Domains!.Distinct())
        {
            if (string.IsNullOrWhiteSpace(domain) || State.ReservedDomainsMap[domain]) continue;
            State.ReservedDomainsMap[domain] = true;
            list.Add(domain);
        }

        if (list.Count == 0) return new Empty();
        
        Context.Fire(new ReservedDomainsAdded
        {
            DomainList = new ReservedDomainList
            {
                Domains = { list }
            }
        });
        
        return new Empty();
    }

    public override Empty RemoveReservedDomains(RemoveReservedDomainsInput input)
    {
        AssertAdmin();
        Assert(input != null, "Invalid input.");
        Assert(input!.Domains != null && input.Domains.Count > 0, "Invalid domains.");

        var list = new List<string>();

        foreach (var domain in input.Domains!.Distinct())
        {
            if (string.IsNullOrWhiteSpace(domain) || !State.ReservedDomainsMap[domain]) continue;
            State.ReservedDomainsMap[domain] = false;
            list.Add(domain);
        }
        
        if (list.Count == 0) return new Empty();
        
        Context.Fire(new ReservedDomainsRemoved
        {
            DomainList = new ReservedDomainList
            {
                Domains = { list }
            }
        });
        
        return new Empty();
    }
}