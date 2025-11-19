namespace Blockchain_Example1.ViewModels
{
    public class StakeUserStatus
    {
        public string UserAddress { get; set; }
        public decimal PrincipalAmount { get; set; }
        public int StartBlock { get; set; } 
        public int HeldBlocks { get; set; } 
        public decimal CurrentPayout { get; set; } 
        public bool IsLocked { get; set; } 
    }
}
