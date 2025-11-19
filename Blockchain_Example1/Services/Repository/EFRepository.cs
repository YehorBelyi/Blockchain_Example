using Blockchain_Example1.Models;
using Microsoft.EntityFrameworkCore;

namespace Blockchain_Example1.Services.Repository
{
    public class EFRepository<T> : IRepository<T> where T : class
    {
        private readonly IDbContextFactory<BlockchainContext> _factory;
        public EFRepository(IDbContextFactory<BlockchainContext> factory)
        {
            _factory = factory;
        }
        // creating genesis block when initializing the service
        public void CreateGenesisBlock(string privateKey, string publicKey)
        {
            using var _context = _factory.CreateDbContext();
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
                await using var _context = _factory.CreateDbContext();
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
                await using var _context = _factory.CreateDbContext();
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
                await using var _context = _factory.CreateDbContext();
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
            await using var _context = _factory.CreateDbContext();
            return await _context.Set<T>().FindAsync(id);
        }

        public virtual async Task<List<T>> GetListDataAsync()
        {
            await using var _context = _factory.CreateDbContext();
            return await _context.Set<T>().ToListAsync();
        }

        public async Task<int> SaveDataAsync()
        {
            await using var _context = _factory.CreateDbContext();
            return await _context.SaveChangesAsync();
        }

        // Methods for [Block] entity
        public async Task<List<Block>> GetChain()
        {
            await using var _context = _factory.CreateDbContext();
            return await _context.Blocks
                .Include(b => b.Transactions)
                .OrderBy(b => b.Index)
                .ToListAsync();
        }

        public async Task<Block> GetLastBlock()
        {
            await using var _context = _factory.CreateDbContext();
            return await _context.Blocks
                .OrderByDescending(b => b.Index)
                .FirstOrDefaultAsync();
        }

        public async Task<Block?> GetBlockWithTransactions(int id)
        {
            await using var _context = _factory.CreateDbContext();
            return await _context.Blocks.Include(b => b.Transactions).FirstOrDefaultAsync(b => b.Index == id);
        }

        public async Task<Wallet?> GetWallet(int id)
        {
            await using var _context = _factory.CreateDbContext();
            return await _context.Wallets.FirstOrDefaultAsync(w => w.Id == id);
        }

        public async Task<List<Block>> GetLastNBlocksWithoutGenesis(int skip, int takeLast)
        {
            await using var _context = _factory.CreateDbContext();
            return await _context.Blocks
                .Where(b => b.Index > skip)
                .OrderByDescending(b => b.Index)
                .Take(takeLast)
                .OrderBy(b => b.Index)
                .ToListAsync();
        }

        public async Task<int> GetCountOfBlocks()
        {
            await using var _context = _factory.CreateDbContext();
            return await _context.Blocks.CountAsync();
        }

        // Methods for [Wallet] entity
        public async Task<Wallet> GetWalletByAddress(string fromAddress)
        {
            await using var _context = _factory.CreateDbContext();
            return await _context.Wallets.FirstOrDefaultAsync(w => w.Address == fromAddress);
        }


        public async Task<decimal> GetWalletBalanceAsync(string address)
        {
            await using var _context = _factory.CreateDbContext();
            var received = await _context.Transactions
                .Where(t => t.ToAddress == address)
                .SumAsync(t => (decimal?)t.Amount) ?? 0;

            var sent = await _context.Transactions
                .Where(t => t.FromAddress == address && t.FromAddress != "COINBASE")
                .SumAsync(t => (decimal?)(t.Amount + t.Fee)) ?? 0;

            return received - sent;
        }

        public async Task<List<Transaction>> GetWalletTransactionsAsync(string address)
        {
            await using var _context = _factory.CreateDbContext();
            return await _context.Transactions
                .Where(t => t.FromAddress == address || t.ToAddress == address)
                .OrderByDescending(t => t.Id) 
                .ToListAsync();
        }

        // Methods for [Mempool] entity
        public async Task<List<Transaction>> GetMempoolAsync()
        {
            await using var _context = _factory.CreateDbContext();
            return await _context.Transactions
                .Where(t => t.BlockId == null) 
                .ToListAsync();
        }

        public async Task AddToMempoolAsync(Transaction transaction)
        {
            await using var _context = _factory.CreateDbContext();
            transaction.BlockId = null;
            await _context.Transactions.AddAsync(transaction);
            await _context.SaveChangesAsync();
        }

        public async Task ClearMempoolAsync()
        {
            await using var _context = _factory.CreateDbContext();
            var pending = _context.Transactions.Where(t => t.BlockId == null);
            _context.Transactions.RemoveRange(pending);
            await _context.SaveChangesAsync();
        }

        public async Task ClearMempoolAsync(IEnumerable<Transaction> mempool)
        {
            await using var _context = _factory.CreateDbContext();
            _context.Transactions.RemoveRange(mempool);
            await _context.SaveChangesAsync();
        }

        public async Task RemoveTransactionsByIds(IEnumerable<int> transactionIds)
        {
            await using var _context = _factory.CreateDbContext();

            var ids = transactionIds.Where(id => id > 0).ToList();

            if (ids.Any())
            {
                await _context.Transactions
                    .Where(t => ids.Contains(t.Id))
                    .ExecuteDeleteAsync();
            }
        }

        public async Task<List<Transaction>> GetMostValuableTranscations(int maxTransactionsPerBlock)
        {
            await using var _context = _factory.CreateDbContext();
            return await _context.Transactions.Where(t => t.BlockId == null)
                .OrderByDescending(t => t.Fee)
                .Take(maxTransactionsPerBlock)
                .ToListAsync();
        }

        public async Task<bool> UpdateBlockWithTransactions(Block block)
        {
            try
            {
                await using var _context = _factory.CreateDbContext();

                _context.Blocks.Update(block);

                foreach (var tx in block.Transactions)
                {
                    if (tx.Id == 0)
                    {
                        _context.Transactions.Add(tx);
                    }
                    else
                    {
                        _context.Transactions.Update(tx);
                    }
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateBlockWithTransactions Error: {ex.Message}");
                return false;
            }
        }
    }
}
