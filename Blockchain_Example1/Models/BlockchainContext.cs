using Microsoft.EntityFrameworkCore;

namespace Blockchain_Example1.Models
{
    public class BlockchainContext: DbContext
    {
        public BlockchainContext(DbContextOptions<BlockchainContext> options):base(options)
        {
            //Database.EnsureDeleted();
            Database.EnsureCreated();
        }

        public virtual DbSet<Block> Blocks { get; set; }
        public virtual DbSet<Wallet> Wallets { get; set; }
        public virtual DbSet<Transaction> Transactions { get; set; }
    }
}
