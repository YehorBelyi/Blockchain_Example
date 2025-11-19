namespace Blockchain_Example1.Models.Contracts
{
    public class StakeRecord
    {
        public decimal Principal { get; set; } = 0m;
        public int StartBlock { get; set; } = 0;
        public decimal EarnedReward { get; set; } = 0m;
    }
}
