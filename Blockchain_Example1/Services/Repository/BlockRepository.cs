using Blockchain_Example1.Models;
using Blockchain_Example1.Services.Repository;
using Microsoft.EntityFrameworkCore;

namespace Blockchain_Example1.Services
{
    public class BlockRepository : EFRepository<Block>
    {
        private readonly BlockchainContext _context;
        public BlockRepository(IDbContextFactory<BlockchainContext> factory) :base(factory)
        {
            //_context = context;
        }
    }
}
