using Blockchain_Example1.Models;
using Microsoft.EntityFrameworkCore;

namespace Blockchain_Example1.Services.Repository
{
    public class EFRepository<T> : IRepository<T> where T : class
    {
        private readonly BlockchainContext _context;
        public EFRepository(IDbContextFactory<BlockchainContext> factory)
        {
            _context = factory.CreateDbContext();
        }
        // creating genesis block when initializing the service
        public void CreateGenesisBlock(string privateKey, string publicKey)
        {
            if (!_context.Blocks.Any())
            {
                var block = new Block("0", new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc).ToString("f")) { Index = 0, IsMined = true };

                //block.Sign(privateKey, publicKey);
                _context.Blocks.Add(block);
                _context.SaveChanges();
            }
        }

        public async Task<bool> AddDataAsync(T data)
        {
            try
            {
                _context.Set<T>().Add(data);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UpdateDataAsync(T data)
        {
            try
            {
                _context.Entry(data).State = EntityState.Modified;
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DeleteDataAsync(int id)
        {
            try
            {
                var D = await GetDataAsync(id);
                _context.Set<T>().Remove(D);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public virtual async Task<T?> GetDataAsync(int id)
        {
            return await _context.Set<T>().FindAsync(id);
        }

        public virtual async Task<List<T>> GetListDataAsync()
        {
            return await _context.Set<T>().ToListAsync();
        }

        public async Task<int> SaveDataAsync()
        {
            return await _context.SaveChangesAsync();
        }

        // Methods for [Block] entity
        public async Task<List<Block>> GetChain()
        {
            return await _context.Blocks
                .Include(b => b.Transactions)
                .OrderBy(b => b.Index)
                .ToListAsync();
        }

        public async Task<Block?> GetLastBlock()
        {
            return await _context.Blocks
                .OrderByDescending(b => b.Index)
                .FirstOrDefaultAsync();
        }

        public async Task<Block?> GetBlockWithTransactions(int id)
        {
            return await _context.Blocks.Include(b => b.Transactions).FirstOrDefaultAsync(b => b.Index == id);
        }

        public async Task<List<Block>> GetLastNBlocksWithoutGenesis(int skip, int takeLast)
        {
            return await _context.Blocks
                .Where(b => b.Index > skip)
                .OrderByDescending(b => b.Index)
                .Take(takeLast)
                .OrderBy(b => b.Index)
                .ToListAsync();
        }

        public async Task<int> GetCountOfBlocks()
        {
            return await _context.Blocks.CountAsync();
        }

        // Methods for [Wallet] entity
        public async Task<Wallet> GetWalletByAddress(string fromAddress)
        {
            return await _context.Wallets.FirstOrDefaultAsync(w => w.Address == fromAddress);
        }


        public async Task<decimal> GetWalletBalanceAsync(string address)
        {
            var received = await _context.Transactions
                .Where(t => t.ToAddress == address)
                .SumAsync(t => (decimal?)t.Amount) ?? 0;

            var sent = await _context.Transactions
                .Where(t => t.FromAddress == address && t.FromAddress != "COINBASE")
                .SumAsync(t => (decimal?)(t.Amount + t.Fee)) ?? 0;

            return received - sent;
        }

        // Methods for [Mempool] entity
        public async Task<List<Transaction>> GetMempoolAsync()
        {
            return await _context.Transactions
                .Where(t => t.BlockId == null) 
                .ToListAsync();
        }

        public async Task AddToMempoolAsync(Transaction transaction)
        {
            transaction.BlockId = null;
            await _context.Transactions.AddAsync(transaction);
            await _context.SaveChangesAsync();
        }

        public async Task ClearMempoolAsync()
        {
            var pending = _context.Transactions.Where(t => t.BlockId == null);
            _context.Transactions.RemoveRange(pending);
            await _context.SaveChangesAsync();
        }

    }
}
