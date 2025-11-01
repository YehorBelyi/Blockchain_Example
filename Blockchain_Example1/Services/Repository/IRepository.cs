using Blockchain_Example1.Models;
using Microsoft.EntityFrameworkCore;

namespace Blockchain_Example1.Services.Repository
{
    public interface IRepository<T>
    {
        void CreateGenesisBlock(string privateKey, string publicKey);
        Task<IEnumerable<T>> GetListDataAsync();

        Task<T?> GetDataAsync(int id);

        Task<bool> DeleteDataAsync(int id);

        Task<bool> AddDataAsync(T data);

        Task<bool> UpdateDataAsync(T data);

        Task<int> SaveDataAsync();

        Task<List<Block>> GetChain();

        Task<Block> GetLastBlock();

        Task<Wallet> GetWalletByAddress(string fromAddress);

        Task<List<Transaction>> GetMempoolAsync();

        Task AddToMempoolAsync(Transaction transaction);

        Task ClearMempoolAsync();
    }
}
