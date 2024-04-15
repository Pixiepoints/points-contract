using AElf.Standards.ACS0;

namespace Points.Contracts.Point;

public partial class PointsContractState
{
    internal ACS0Container.ACS0ReferenceState GenesisContract { get; set; }
}