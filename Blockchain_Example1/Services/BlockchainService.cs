﻿using Blockchain_Example1.Models;
using Blockchain_Example1.Services.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace Blockchain_Example1.Services
{
    public class BlockchainService
    {
        private readonly IRepository<Block> _blockRepository;
        private readonly IRepository<Wallet> _walletRepository;
        private readonly IRepository<Transaction> _transactionRepository;
        // For RSA
        public string PrivateKey { get; }
        public string PublicKeyXml { get; }

        // [27.10.25] Mining block
        public static int Difficulty { get; set; } = 3;
        private static readonly SemaphoreSlim _chainLock = new(1, 1);

        // [31.10.25] Transactions, mempool
        public Dictionary<string, Wallet> Wallets { get; set; } = new Dictionary<string, Wallet>();
        //public List<Transaction> Mempool { get; set; } = new List<Transaction>();
        public const decimal MinerReward = 1.0m;

        public BlockchainService(IRepository<Block> blockRepository, IRepository<Wallet> walletRepository, IRepository<Transaction> transactionRepository) {
            var rsa = RSA.Create();
            PrivateKey = rsa.ToXmlString(true);
            PublicKeyXml = rsa.ToXmlString(false); 
                        
            _blockRepository = blockRepository;
            _blockRepository.CreateGenesisBlock(PrivateKey, PublicKeyXml);

            _walletRepository = walletRepository;
            _transactionRepository = transactionRepository;

            //// [MOVED TO Repository.cs]
            //// creating genesis block when initializing the service
            //if (!_context.Blocks.Any())
            //{
            //    var block = new Block("0") { Index = 0, IsMined = true };

            //    block.Sign(PrivateKey, PublicKeyXml);
            //    _context.Blocks.Add(block);
            //    _context.SaveChanges();
            //}
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
            var rsa = RSA.Create();
            var minerPublicKeyXml = rsa.ToXmlString(false);
            var minerAddress = (await _walletRepository.GetListDataAsync())
        .FirstOrDefault(w => w.PublicKeyXml == minerPublicKeyXml)?.Address;

            // Getting mempool straight from database
            var mempool = await _transactionRepository.GetMempoolAsync();


            // Fee from all transactions for mining
            decimal totalFee = mempool.Sum(t => t.Fee);
            // Generate a block from system that notifies about getting reward for mining and adding all transactions
            var bath = new List<Transaction>() {
                new Transaction
                {
                    FromAddress = "COINBASE",
                    ToAddress = minerAddress,
                    Amount = MinerReward + totalFee
                },
            };

            // Add all transactions from mempool
            bath.AddRange(mempool);

            var previousBlock = await _blockRepository.GetLastBlock();
            var newBlock = new Block(previousBlock.Hash);

            // Add all transactions to the block
            newBlock.SetTransaction(bath);

            // Mine this block to add it to the chain
            await newBlock.MineAsync(Difficulty);

            newBlock.Sign(privateKey, minerPublicKeyXml);
            await _blockRepository.AddDataAsync(newBlock);

            await _transactionRepository.ClearMempoolAsync();
            //Mempool.Clear();

            return newBlock;
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
    }
}
