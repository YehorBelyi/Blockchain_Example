using Blockchain_Example1.Models;
using Blockchain_Example1.Services;
using Blockchain_Example1.Services.Repository;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Net;
using System.Runtime.InteropServices.Marshalling;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Blockchain_Example1.Services
{
    public class BlockchainService
    {
        private readonly RSAService _rsaService;
        private readonly IRepository<Block> _blockRepository;
        private readonly IRepository<Wallet> _walletRepository;
        private readonly IRepository<Transaction> _transactionRepository;
        private readonly ILogger<BlockchainService> _logger;
        // For RSA
        public string privateKey;
        public string publicKey;
        public string PrivateKey { get => privateKey; set => privateKey = value; }
        public string PublicKeyXml { get => publicKey; set => publicKey = value; }

        // [27.10.25] Mining block
        public static int Difficulty { get; set; } = 1;
        private static readonly SemaphoreSlim _chainLock = new(1, 1);

        // [31.10.25] Transactions, mempool
        //public Dictionary<string, Wallet> Wallets { get; set; } = new Dictionary<string, Wallet>();
        //public List<Transaction> Mempool { get; set; } = new List<Transaction>();
        //public const decimal MinerReward = 1.0m;

        // [10.11.2025] Halving
        private const decimal BaseMinerReward = 1.0m;
        private int HalvingBlockInterval = 100;

        // Dynamic change of difficulty while meaning to balance chain load
        private const int TargetBlockTimeSeconds = 10; // time to mine one block
        private const int AdjustEveryBlocks = 10; // 
        private const double Tolerance = 0.2; // +- 20%

        public double AverageMiningTime { get; set; }

        public BlockchainService(IRepository<Block> blockRepository, IRepository<Wallet> walletRepository, IRepository<Transaction> transactionRepository, RSAService rsaService, ILogger<BlockchainService> logger)
        {
            _rsaService = rsaService ?? new RSAService();
            (privateKey, publicKey) = _rsaService.GetRSAKeys();

            _blockRepository = blockRepository;
            _walletRepository = walletRepository;
            _transactionRepository = transactionRepository;
            _logger = logger;

            if (_blockRepository != null)
            {
                _blockRepository.CreateGenesisBlock(privateKey, publicKey);
            }
        }

        private async Task AdjustDifficultyIfNeeded()
        {
            var chainCount = await _blockRepository.GetCountOfBlocks();
            if (chainCount % AdjustEveryBlocks != 0 || chainCount < AdjustEveryBlocks)
            {
                return;
            }

            var recent = await _blockRepository.GetLastNBlocksWithoutGenesis(1, AdjustEveryBlocks);

            var avgMs = recent.Average(b => b.MiningDurationMs);
            AverageMiningTime = avgMs;

            if (recent.Count < AdjustEveryBlocks)
            {
                return;
            }

            // in milliseconds
            var targetMs = TargetBlockTimeSeconds * 1000;

            var lowerBound = targetMs * (1 - Tolerance);
            var upperBound = targetMs * (1 + Tolerance);

            if (avgMs < lowerBound)
            {
                Difficulty++;
            } else if (avgMs > upperBound && Difficulty > 1)
            {
                Difficulty--;
            }

            if (Difficulty < 1) Difficulty = 1;
            if (Difficulty > 10) Difficulty = 10;
        }

        public async Task<Wallet> RegisterWallet(string publicKeyXml, string displayName)
        {
            var wallet = new Wallet
            {
                PublicKeyXml = publicKeyXml,
                Address = Wallet.DereveAddressFromPublicKeyXml(publicKeyXml),
                DisplayName = displayName
            };
            await _walletRepository.AddDataAsync(wallet);
            //Wallets[wallet.Address] = wallet;
            return wallet;
        }

        public async Task CreateTransaction(Transaction transaction)
        {
            var rsa = RSA.Create();
            //var wallet = Wallets[transaction.FromAddress];
            var wallet = await _walletRepository.GetWalletByAddress(transaction.FromAddress);

            var currentWalletBalance = await _walletRepository.GetWalletBalanceAsync(wallet.Address);

            if (currentWalletBalance < (transaction.Amount + transaction.Fee))
            {
                throw new Exception("Not enough coins on the balance!");
            }

            rsa.FromXmlString(wallet.PublicKeyXml);
            var payload = Encoding.UTF8.GetBytes(transaction.CanonicalPayload());
            var sig = Convert.FromBase64String(transaction.Signature);
            // Check if the signature is created by the user who owns it
            if (!rsa.VerifyData(payload, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
            {
                throw new Exception("Invalid Transaction Signature");
            }

            //Mempool.Add(transaction);
            await _transactionRepository.AddToMempoolAsync(transaction);
        }

        public async Task<Block> MinePending(string privateKey)
        {
            await _chainLock.WaitAsync();

            try
            {
                var rsa = RSA.Create();
                rsa.FromXmlString(privateKey);
                var minerPublicKeyXml = rsa.ToXmlString(false);
                var minerAddress = (await _walletRepository.GetListDataAsync())
                    .FirstOrDefault(w => w.PublicKeyXml == minerPublicKeyXml)?.Address;

                // Getting mempool straight from database
                var mempool = await _transactionRepository.GetMempoolAsync();

                // Fee from all transactions for mining
                decimal totalFee = mempool.Sum(t => t.Fee);

                var previousBlock = await _blockRepository.GetLastBlock();
                var newBlock = new Block(previousBlock.Hash);

                await _blockRepository.AddDataAsync(newBlock);

                var coinbaseTransaction = new Transaction
                {
                    FromAddress = "COINBASE",
                    ToAddress = minerAddress,
                    Amount = GetCurrentBlockReward(newBlock.Index) + totalFee,
                    BlockId = newBlock.Index 
                };

                var allTransactions = new List<Transaction> { coinbaseTransaction };
                allTransactions.AddRange(mempool);

                foreach (var tx in mempool)
                {
                    tx.BlockId = newBlock.Index;
                }

                
                newBlock.SetTransaction(allTransactions);

                await newBlock.MineAsync(Difficulty);
                await AdjustDifficultyIfNeeded();
                newBlock.Sign(privateKey, minerPublicKeyXml);
                newBlock.IsMined = true;
                await _blockRepository.UpdateBlockWithTransactions(newBlock);

                await _transactionRepository.ClearMempoolAsync();

                return newBlock;
            }
            finally
            {
                _chainLock.Release();
            }
        }

        //// obsolete function
        //public long AddBlock(string data, string signature)
        //{
        //    // getting last block in the list
        //    var previousBlock = _context.Blocks.OrderByDescending(b => b.Index).First();
        //    // creating new block using the info from previous block
        //    var newBlock = new Block(previousBlock.Hash);

        //    // [27.10.25] Mining before signing the block
        //    newBlock.Mine(Difficulty);

        //    using (var rsa = RSA.Create())
        //    {
        //        rsa.FromXmlString(signature);
        //        RSAParameters importedPrivateKey = rsa.ExportParameters(true);
        //        // here, public key appears based on given imported private key
        //        string importedPublicKeyXml = rsa.ToXmlString(false);
        //        newBlock.Sign(importedPrivateKey, importedPublicKeyXml);
        //    }

        //    _context.Blocks.Add(newBlock);
        //    _context.SaveChanges();
        //    return newBlock.MiningDurationMs;
        //}

        //// current modern async function
        //public async Task AddBlockAsync(string data, string signature)
        //{
        //    await _chainLock.WaitAsync();

        //    try
        //    {
        //        var previousBlock = await _context.Blocks.OrderByDescending(b => b.Index).FirstAsync();
        //        var newBlock = new Block(previousBlock.Hash) { MiningDurationMs = 0, IsMined = false }; // When is not mined

        //        _context.Blocks.Add(newBlock);
        //        await _context.SaveChangesAsync();

        //        // Start mining in background
        //        await Task.Run(async () =>
        //        {
        //            await newBlock.MineAsync(Difficulty);

        //            using (var rsa = RSA.Create())
        //            {
        //                rsa.FromXmlString(signature);
        //                RSAParameters importedPrivateKey = rsa.ExportParameters(true);
        //                string importedPublicKeyXml = rsa.ToXmlString(false);
        //                newBlock.Sign(importedPrivateKey, importedPublicKeyXml);
        //            }

        //            newBlock.IsMined = true;

        //            _context.Blocks.Update(newBlock);
        //            await _context.SaveChangesAsync();
        //        });
        //    } finally
        //    {
        //        _chainLock.Release();
        //    }
        //}

        public bool IsBlockValid(Block currentBlock, Block previousBlock)
        {
            if (currentBlock.PreviousHash != previousBlock.Hash) return false;
            if (currentBlock.Hash != currentBlock.ComputeHash()) return false;
            var tempHash = currentBlock.ComputeHash();
            if (!currentBlock.Verify()) return false;
            // [27.10.25] 
            if (!currentBlock.HashValidProof()) return false;
            return true;
        }

        public async Task<bool> IsChainValid()
        {
            var chain = await _blockRepository.GetChain();
            bool chainStillValid = true;

            for (int i = 1; i < chain.Count; i++)
            {
                if (!chainStillValid)
                    return false;

                var current = chain[i];
                var previous = chain[i - 1];

                bool isValid = IsBlockValid(current, previous);

                if (!isValid)
                {
                    chainStillValid = false;
                }
            }

            return chainStillValid;
        }

        // Overloaded function to chain validity of the incoming chain
        public async Task<bool> IsChainValid(List<Block> chain)
        {
            if (chain.Count == 0) return true;

            if (!chain[0].Verify()) return false;
            if (chain[0].Hash != chain[0].ComputeHash()) return false;

            for (int i = 1; i < chain.Count; i++)
            {
                var current = chain[i];
                var previous = chain[i - 1];

                if (current.PreviousHash != previous.Hash) return false;
                if (!current.HashValidProof()) return false;
                if (!current.Verify()) return false;
                if (current.Hash != current.ComputeHash()) return false;
            }
            return true;
        }

        // Proccess balances for all wallets
        public async Task<Dictionary<string, decimal>> GetBalances(bool includeToMempool = false)
        {
            var balances = new Dictionary<string, decimal>();
            foreach (var block in await _blockRepository.GetChain())
            {
                foreach (var item in block.Transactions)
                {
                    ApplyTranscationToBalances(balances, item);
                }
            }

            if (includeToMempool)
            {
                foreach (var tran in await _transactionRepository.GetMempoolAsync())
                {
                    ApplyTranscationToBalances(balances, tran);
                }
            }
            foreach (var (key, value) in balances) {
                _logger.LogInformation($"{key}: {value}");
            }
            return balances;
        }

        // Proccess wallets balances
        private static void ApplyTranscationToBalances(Dictionary<string, decimal> balances, Transaction tx)
        {
            if (!tx.FromAddress.Equals("COINBASE", StringComparison.OrdinalIgnoreCase))
            {
                if (!balances.TryGetValue(tx.FromAddress, out var fromBalance))
                {
                    fromBalance = 0;
                }

                balances[tx.FromAddress] = fromBalance - (tx.Amount + tx.Fee);
            }

            if (!balances.TryGetValue(tx.ToAddress, out var toBalance))
            {
                toBalance = 0;
            }
            balances[tx.ToAddress] = toBalance + tx.Amount;
        }



        public async Task<decimal> GetWalletsBalancesAsync(string address)
        {
            return await _walletRepository.GetWalletBalanceAsync(address);
        }

        public async Task<Block> GetBlock(int id)
        {
            if (id == null)
            {
                throw new ArgumentNullException("Got null argument when requesting block!");
            }

            Block block = await _blockRepository.GetDataAsync(id);
            if (block == null)
            {
                throw new Exception("Got null value when looking for specific block!");
            }
            return block;
        }

        public async Task EditBlock(int id, string data, string signature)
        {
            Block block = await GetBlock(id);


            if (data != null)
            {
                //block.Data = data;
                block.Hash = block.ComputeHash();
            }

            if (signature != null)
            {
                block.SetSignature(signature);
            }

            await _blockRepository.SaveDataAsync();
        }

        // Demo method for generating a few wallets for testing
        public async Task<(Wallet wallet, string privateKeyXml)> DemoCreateWallet(string displayName)
        {
            var rsa = RSA.Create();
            var privateKeyXml = rsa.ToXmlString(true);
            var publicKeyXml = rsa.ToXmlString(false);
            var wallet = await RegisterWallet(publicKeyXml, displayName);
            return (wallet, privateKeyXml);
        }

        public static string SignPayload(string payload, string privateKeyXml)
        {
            var rsa = RSA.Create();
            rsa.FromXmlString(privateKeyXml);
            var data = Encoding.UTF8.GetBytes(payload);
            var sig = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return Convert.ToBase64String(sig);
        }

        public async Task<bool> TryAddExternalChain(List<Block> externalChain)
        {
            if (_blockRepository == null || _walletRepository == null || _transactionRepository == null)
            {
                _logger.LogWarning("Node is not properly initialized. Database repositories are unavailable.");
                return false;
            }

            await _chainLock.WaitAsync();
            try
            {
                var currentChain = await _blockRepository.GetChain();

                // Check length of the incoming chain
                if (externalChain == null || externalChain.Count == 0)
                {
                    _logger.LogWarning("Received empty external chain.");
                    return false;
                }

                if (externalChain.Count <= currentChain.Count)
                {
                    _logger.LogInformation("External chain is not longer than current one.");
                    return false;
                }

                // Check validity of the first block
                if (!externalChain.First().Verify())
                {
                    _logger.LogWarning("Genesis block signature invalid.");
                    return false;
                }

                for (int i = 1; i < externalChain.Count; i++)
                {
                    var prevBlock = externalChain[i - 1];
                    var currBlock = externalChain[i];

                    // PreviousHash must match Hash from previous block
                    if (currBlock.PreviousHash != prevBlock.Hash)
                    {
                        _logger.LogWarning($"Invalid chain link at block {currBlock.Index}: PrevHash mismatch.");
                        return false;
                    }

                    if (!currBlock.Verify())
                    {
                        _logger.LogWarning($"Invalid signature at block {currBlock.Index}.");
                        return false;
                    }

                    if (!currBlock.HashValidProof())
                    {
                        _logger.LogWarning($"Invalid proof of work at block {currBlock.Index}.");
                        return false;
                    }

                    if (currBlock.Hash != currBlock.ComputeHash())
                    {
                        _logger.LogWarning($"Hash mismatch at block {currBlock.Index}.");
                        return false;
                    }
                }

                // If external chain is valid, update local chain that is in database
                foreach (var block in currentChain)
                {
                    await _blockRepository.DeleteDataAsync(block.Index);
                }

                await _transactionRepository.ClearMempoolAsync();

                // Adding block to the local chain from externail chain
                foreach (var block in externalChain)
                {
                    await _blockRepository.AddDataAsync(block);

                    foreach (var tx in block.Transactions)
                    {
                        tx.BlockId = block.Index;
                        await _transactionRepository.AddDataAsync(tx);
                    }
                }

                _logger.LogInformation("External chain successfully synchronized.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during external chain synchronization.");
                return false;
            }
            finally
            {
                _chainLock.Release();
            }
        }

        public async Task<Block?> GetBlockWithTransactions(int id)
        {
            return await _blockRepository.GetBlockWithTransactions(id);
        }

        public async Task<(Wallet? wallet, decimal balance, List<Transaction> transactions)> GetWallet(int id)
        {
            var wallet = await _walletRepository.GetWallet(id);

            var balance = await _walletRepository.GetWalletBalanceAsync(wallet.Address);
            var transactions = await _transactionRepository.GetWalletTransactionsAsync(wallet.Address);

            return (wallet, balance, transactions);
        }

        public async Task<Block?> GetLastBlock()
        {
            return await _blockRepository.GetLastBlock();
        }

        // [10.11.25] Halving
        public decimal GetCurrentBlockReward(int newBlockIndex)
        {
            if (newBlockIndex < 1) return 0;

            int halvings = (newBlockIndex / HalvingBlockInterval);

            decimal reward = BaseMinerReward;

            for (int i = 0; i < halvings; i++)
            {
                reward /= 2;
            }

            return reward;
        }
    }
}
