using Blockchain_Example1.Models;
using Microsoft.EntityFrameworkCore;

namespace Blockchain_Example1.Services
{
    public static class ServiceProviderExtensions
    {
        public static void AddBlockchainSerivce(this IServiceCollection services, string connectionString)
        {
            services.AddDbContext<BlockchainContext>(options =>
                options.UseSqlServer(connectionString));
            services.AddScoped<BlockchainService>();
        }
    }
}
