using Blockchain_Example1.Services;
using NuGet.Common;

namespace Blockchain_Example1.Models.Contracts
{
    public class TimeLockContract : ISmartContract
    {
        public string Address { get; }
        public int UnlockBlockIndex { get; set; }

        public TimeLockContract(string address, int unlockBlockIndex)
        {
            Address = address;
            UnlockBlockIndex = unlockBlockIndex;
        }

        // This method will be called after new every transcation is added to be validated
        public bool ValidateTranscation(BlockchainService chain, Transaction tx, int currentBlockIndex)
        {
            if (String.Equals(Address, tx.FromAddress, StringComparison.OrdinalIgnoreCase))
            {
                if (currentBlockIndex < UnlockBlockIndex)
                {
                    return false;
                    //throw new Exception($"TimeLockContract: Transaction from {Address} is locked until Block {UnlockBlockIndex}. Current block is {currentBlockIndex}");
                }
                return true;
            }
            return false;
        }
    }
}
