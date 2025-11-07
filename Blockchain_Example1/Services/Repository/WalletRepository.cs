using Blockchain_Example1.Models;
using Blockchain_Example1.Services.Repository;
using Microsoft.EntityFrameworkCore;

namespace Blockchain_Example1.Services
{
    public class WalletRepository : EFRepository<Wallet>
    {
        private readonly BlockchainContext _context;
        public WalletRepository(IDbContextFactory<BlockchainContext> factory) :base(factory)
        {
           // _context = context;
        }
    }
}
