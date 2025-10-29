using Blockchain_Example1.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace Blockchain_Example1.Services
{
    public class BlockchainService
    {
        private readonly BlockchainContext _context;
        // For RSA
        public string PrivateKey { get; }

        // [27.10.25] Mining block
        public static int Difficulty { get; set; } = 3;
        private static readonly SemaphoreSlim _chainLock = new(1, 1);

        public BlockchainService(BlockchainContext context) {
            var rsa = RSA.Create();
            PrivateKey = rsa.ToXmlString(true);
            var privateKey = rsa.ExportParameters(true);
            var publicKeyXml = rsa.ToXmlString(false); 
                        
            _context = context;
            // creating genesis block when initializing the service
            if (!_context.Blocks.Any())
            {
                var block = new Block("Genesis block", "0") { Index = 0, IsMined = true };

                block.Sign(privateKey, publicKeyXml);
                _context.Blocks.Add(block);
                _context.SaveChanges();
            }
        }

        public List<Block> GetChain() => _context.Blocks.OrderBy(b => b.Index).ToList();

        public int GetNextIndex() => _context.Blocks.Any() ? _context.Blocks.Max(b => b.Index) + 1 : 1;

        public long AddBlock(string data, string signature)
        {
            // getting last block in the list
            var previousBlock = _context.Blocks.OrderByDescending(b => b.Index).First();
            // creating new block using the info from previous block
            var newBlock = new Block(data, previousBlock.Hash);

            // [27.10.25] Mining before signing the block
            newBlock.Mine(Difficulty);

            using (var rsa = RSA.Create())
            {
                rsa.FromXmlString(signature);
                RSAParameters importedPrivateKey = rsa.ExportParameters(true);
                // here, public key appears based on given imported private key
                string importedPublicKeyXml = rsa.ToXmlString(false);
                newBlock.Sign(importedPrivateKey, importedPublicKeyXml);
            }
                
            _context.Blocks.Add(newBlock);
            _context.SaveChanges();
            return newBlock.MiningDurationMs;
        }

        public async Task AddBlockAsync(string data, string signature)
        {
            await _chainLock.WaitAsync();

            try
            {
                var previousBlock = await _context.Blocks.OrderByDescending(b => b.Index).FirstAsync();
                var newBlock = new Block(data, previousBlock.Hash) { MiningDurationMs = 0, IsMined = false }; // When is not mined

                _context.Blocks.Add(newBlock);
                await _context.SaveChangesAsync();

                // Start mining in background
                await Task.Run(async () =>
                {
                    await newBlock.MineAsync(Difficulty);

                    using (var rsa = RSA.Create())
                    {
                        rsa.FromXmlString(signature);
                        RSAParameters importedPrivateKey = rsa.ExportParameters(true);
                        string importedPublicKeyXml = rsa.ToXmlString(false);
                        newBlock.Sign(importedPrivateKey, importedPublicKeyXml);
                    }

                    newBlock.IsMined = true;

                    _context.Blocks.Update(newBlock);
                    await _context.SaveChangesAsync();
                });
            } finally
            {
                _chainLock.Release();
            }
        }

        public bool IsBlockValid(Block currentBlock, Block previousBlock)
        {
            if (currentBlock.PreviousHash != previousBlock.Hash) return false;
            if (currentBlock.Hash != currentBlock.ComputeHash()) return false;
            if (!currentBlock.Verify()) return false;
            // [27.10.25] 
            if (!currentBlock.HashValidProof()) return false;
            return true;
        }

        public bool IsChainValid()
        {
            var chain = GetChain();
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


        public Block GetBlock(int? id)
        {
            if (id == null)
            {
                throw new ArgumentNullException("Got null argument when requesting block!");
            }

            Block block = _context.Blocks.FirstOrDefault(b => b.Index == id);
            if (block == null)
            {
                throw new Exception("Got null value when looking for specific block!");
            }
            return block;
        }

        public void EditBlock(int? id, string? data, string? signature)
        {
            Block block = GetBlock(id);

            if (data != null)
            {
                block.Data = data;
                block.Hash = block.ComputeHash();
            }

            if (signature != null)
            {
                block.SetSignature(signature);
            }

            _context.SaveChanges();
        }
    }
}
