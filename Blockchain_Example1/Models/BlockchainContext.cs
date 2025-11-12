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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Block>()
                .HasMany(b => b.Transactions)
                .WithOne(t => t.Block)
                .HasForeignKey(t => t.BlockId)
                .OnDelete(DeleteBehavior.Cascade);

            base.OnModelCreating(modelBuilder);
        }
    }
}
