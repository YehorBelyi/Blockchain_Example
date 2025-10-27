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
        private readonly RSAParameters _privateKey;
        private readonly string _publicKeyXml;

        // [27.10.25] Mining block
        public static int Difficulty { get; set; } = 3;


        public BlockchainService(BlockchainContext context) {
            var rsa = RSA.Create();
            _privateKey = rsa.ExportParameters(true);
            _publicKeyXml = rsa.ToXmlString(false);
                        
            _context = context;
            // creating genesis block when initializing the service
            if (!_context.Blocks.Any())
            {
                var block = new Block("Genesis block", "0") { Index = 0 };
                block.Sign(_privateKey, _publicKeyXml);
                _context.Blocks.Add(block);
                _context.SaveChanges();
            }
        }

        public List<Block> GetChain() => _context.Blocks.OrderBy(b => b.Index).ToList();

        public int GetNextIndex() => _context.Blocks.Any() ? _context.Blocks.Max(b => b.Index) + 1 : 1;

        public long AddBlock(string data)
        {
            // getting last block in the list
            var previousBlock = _context.Blocks.OrderByDescending(b => b.Index).First();
            // creating new block using the info from previous block
            var newBlock = new Block(data, previousBlock.Hash);

            // [27.10.25] Mining before signing the block
            newBlock.Mine(Difficulty);

            newBlock.Sign(_privateKey, _publicKeyXml);
            _context.Blocks.Add(newBlock);
            _context.SaveChanges();
            return newBlock.MiningDurationMs;
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
