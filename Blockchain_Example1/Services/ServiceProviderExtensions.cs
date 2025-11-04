using Blockchain_Example1.Models;
using Blockchain_Example1.Services.Repository;
using Microsoft.EntityFrameworkCore;

namespace Blockchain_Example1.Services
{
    public static class ServiceProviderExtensions
    {
        public static void AddBlockchainSerivce(this IServiceCollection services, string connectionString)
        {
            services.AddSingleton<RSAService>();
            services.AddDbContext<BlockchainContext>(options =>
                options.UseSqlServer(connectionString));
            services.AddTransient<IRepository<Block>, BlockRepository>();
            services.AddTransient<IRepository<Wallet>, WalletRepository>();
            services.AddTransient<IRepository<Transaction>, TransactionRepository>();
            services.AddScoped<BlockchainService>();
        }
    }
}
