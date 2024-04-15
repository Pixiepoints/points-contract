using System.Collections.Generic;
using System.Linq;
using AElf;
using AElf.Types;

namespace Points.Contracts.Point;

public partial class PointsContract
{
    private void AssertAdmin() => Assert(Context.Sender == State.Admin.Value, "No permission.");
    private void AssertInitialized() => Assert(State.Initialized.Value, "Not initialized.");

    private void AssertDappAdmin(Hash dappId)
    {
        Assert(dappId != null && State.DappInfos[dappId] != null && Context.Sender == State.DappInfos[dappId].DappAdmin,
            "No permission.");
    }

    private void AssertDappContractAddress(Hash dappId)
    {
        Assert(dappId != null && !dappId.Value.IsNullOrEmpty() && State.DappInfos[dappId] != null &&
               Context.Sender == State.DappInfos[dappId].DappContractAddress, "No permission.");
    }

    private void AssertDomainFormat(string domain)
    {
        var invalidChars = new List<char> { '<', '>', ':', '"', '/', '|', '?', '*' };
        var alias = domain.Split('.');
        Assert(domain.Length is > 0 and <= PointsContractConstants.DomainNameLength &&
               !(domain.StartsWith(".") || domain.EndsWith(".")) &&
               !domain.Any(c => invalidChars.Contains(c) || c > 126 || char.IsUpper(c)) &&
               alias.Length is 3 or 2 && alias.All(t => !string.IsNullOrEmpty(t)), "Invalid domain.");
    }

    private bool IsStringValid(string input)
    {
        return !string.IsNullOrWhiteSpace(input);
    }
    
    private bool IsAddressValid(Address input)
    {
        return input != null && !input.Value.IsNullOrEmpty();
    }
    
    private bool IsHashValid(Hash input)
    {
        return input != null && !input.Value.IsNullOrEmpty();
    }
}