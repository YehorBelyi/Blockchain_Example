using Blockchain_Example1.Services;

namespace Blockchain_Example1.Models.Contracts
{
    public interface ISmartContract
    {
        string Address { get; }

        bool ValidateTranscation(BlockchainService chain, Transaction tx, int currentBlock);
    }
}
