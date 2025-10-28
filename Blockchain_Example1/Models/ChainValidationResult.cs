namespace Blockchain_Example1.Models
{
    public class ChainValidationResult
    {
        public Dictionary<int, bool> ValidBlocks { get; set; } = new();
        public Dictionary<int, bool> SignatureValidity { get; set; } = new();
        public bool IsChainValid { get; set; } = true;
    }
}
