using AElf.Kernel.Infrastructure;
using AElf.Types;

namespace AElf.Boilerplate.TestBase.SmartContractNameProviders;

public class PointsSmartContractAddressNameProvider
{
    public static readonly Hash Name = HashHelper.ComputeFrom("Points.Contracts.Point");

    public static readonly string StringName = Name.ToStorageKey();
    public Hash ContractName => Name;
    public string ContractStringName => StringName;
}