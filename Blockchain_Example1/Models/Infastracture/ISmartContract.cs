using Blockchain_Example1.Services;

namespace Blockchain_Example1.Models.Infastracture
{
    public interface ISmartContract
    {
        string Address { get; }

        void ValidateTranscation(BlockchainService chain, Transaction tx, int currentBlock);
    }
}
