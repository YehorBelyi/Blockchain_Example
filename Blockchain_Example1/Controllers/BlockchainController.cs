using System.Diagnostics;
using System.Net.WebSockets;
using Blockchain_Example1.Models;
using Blockchain_Example1.Services;
using Blockchain_Example1.Services.Repository;
using Blockchain_Example1.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NuGet.Protocol;

namespace Blockchain_Example1.Controllers
{
    public class BlockchainController : Controller
    {
        private readonly ILogger<BlockchainController> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IServiceScopeFactory _scopeFactory;

        private readonly IRepository<Block> _blockRepository;
        private readonly IRepository<Wallet> _walletRepository;
        private readonly IRepository<Transaction> _transactiopnRepository;
        private static readonly Dictionary<string, BlockchainService> _nodeKeys = new Dictionary<string, BlockchainService>()
        {
            ["A"] = null,
            ["B"] = null,
            ["C"] = null
        };
        private static readonly RSAService _staticRsaService = new RSAService();

        public BlockchainController(IServiceProvider serviceProvider, ILogger<BlockchainController> logger, ILoggerFactory loggerFactory, IServiceScopeFactory scopeFactory, BlockchainService blockchainService, IRepository<Block> blockRepository, IRepository<Wallet> walletRepository, IRepository<Transaction> transactionRepository)
        {
            _logger = logger;
            _loggerFactory = loggerFactory;
            _scopeFactory = scopeFactory;
            //_blockchainService = blockchainService;
            _blockRepository = blockRepository;
            _walletRepository = walletRepository;
            _transactiopnRepository = transactionRepository;

            if (_nodeKeys["A"] == null)
            {
                _nodeKeys["A"] = serviceProvider.GetService<BlockchainService>();

                _nodeKeys["B"] = serviceProvider.GetService<BlockchainService>();
                _nodeKeys["C"] = serviceProvider.GetService<BlockchainService>();
            }
        }

        private BlockchainService GetNodeScope(string nodeId, out BlockchainService service)
        {
            if (string.IsNullOrEmpty(nodeId)) nodeId = "A";

            service = _nodeKeys[nodeId];

            return service;
        }

        public async Task<IActionResult> Index(string nodeId = "A")
        {
            BlockchainService service;
            GetNodeScope(nodeId, out service);


            var chain = await _blockRepository.GetChain();
            var validation = ValidateChain(chain, nodeId);
            var averageMiningTime = (service.AverageMiningTime / 1000).ToString();  // given - in ms, in view - in s
            var amtFormatted = averageMiningTime.Length > 4 ? averageMiningTime.Substring(0, 4) : averageMiningTime; 

            ViewBag.ValidBlocks = validation.ValidBlocks;
            ViewBag.SignatureValidity = validation.SignatureValidity;
            ViewBag.IsValid = validation.IsChainValid;
            ViewBag.Difficulty = BlockchainService.Difficulty;
            ViewBag.AverageMiningTime = amtFormatted;

            ViewBag.PrivateKey = service.PrivateKey;
            ViewBag.PublicKey = service.PublicKeyXml;

            ViewBag.Mempool = await _transactiopnRepository.GetMempoolAsync();
            ViewBag.Wallets = await _walletRepository.GetListDataAsync();
            ViewBag.Balances = await service.GetBalances(true);

            ViewBag.Nodes = _nodeKeys.Keys.ToList();
            ViewBag.CurrentNodeId = nodeId;

            ViewBag.Contracts = BlockchainService.Contracts;

            ViewBag.PublicKeyWalletContract = service.PublicKeyXmlContractWallet;
            ViewBag.PrivateKeyWalletContract = service.PrivateKeyXmlContractWallet;

            return View(chain);

        }


        public async Task<IActionResult> Edit(int id, string nodeId)
        {

            GetNodeScope(nodeId, out var _blockchainService);


            Block block = await _blockchainService.GetBlock(id);
            return View(block);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, string data, string signature, string nodeId)
        {
            GetNodeScope(nodeId, out var _blockchainService);
            await _blockchainService.EditBlock(id, data, signature);
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> BlockDetails(int id, string nodeId)
        {
            GetNodeScope(nodeId, out var _blockchainService);
            var block = await _blockchainService.GetBlockWithTransactions(id);
            var lastBlock = await _blockchainService.GetLastBlock();
            ViewBag.LastBlockIndex = lastBlock.Index;
            ViewBag.NeededConfirmationCount = 3;
            return View(block);
        }

        public async Task<IActionResult> WalletDetails(int id, string nodeId)
        {
            GetNodeScope(nodeId, out var blockchainService);
            var (wallet, balance, transactions) = await blockchainService.GetWallet(id);

            if (wallet == null)
            {
                return NotFound();
            }

            ViewBag.ThisWalletBalance = balance;
            ViewBag.WalletTransactions = transactions;

            var lastBlock = await blockchainService.GetLastBlock();
            ViewBag.LastBlockIndex = lastBlock.Index;
            ViewBag.NeededConfirmationCount = 3;

            return View(wallet);
        }

        // [27.10.25] Set mining difficulty
        [HttpPost]
        public IActionResult SetDifficulty(int difficulty, string nodeId)
        {
            if (difficulty < 1) difficulty = 1;
            if (difficulty > 10) difficulty = 10;

            BlockchainService service;
            GetNodeScope(nodeId, out service);

            BlockchainService.Difficulty = difficulty;
            return RedirectToAction("Index", new { nodeId });

        }

        [HttpGet]
        public async Task<IActionResult> Search(string query, string nodeId)
        {
            var chain = await _blockRepository.GetChain();
            var validation = ValidateChain(chain, nodeId);

            ViewBag.ValidBlocks = validation.ValidBlocks;
            ViewBag.SignatureValidity = validation.SignatureValidity;
            ViewBag.CurrentFilter = query;

            if (!string.IsNullOrWhiteSpace(query))
            {
                if (int.TryParse(query, out int idx))
                {
                    chain = chain.Where(b => b.Index == idx).ToList();
                }
                else
                {
                    chain = chain.Where(b => b.Hash.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
                }
            }

            return PartialView("_BlockList", chain);
        }

        private ChainValidationResult ValidateChain(List<Block> chain, string nodeId)
        {
            GetNodeScope(nodeId, out var _blockchainService);
            var result = new ChainValidationResult();
            bool chainStillValid = true;

            if (chain.Count > 0)
            {
                result.ValidBlocks[chain[0].Index] = true;
                result.SignatureValidity[chain[0].Index] = chain[0].Verify();
            }

            for (int i = 1; i < chain.Count; i++)
            {
                // validate block
                result.SignatureValidity[chain[i].Index] = chain[i].Verify();

                // if chain is not valid then mark other blocks as invalid
                if (!chainStillValid)
                {
                    result.ValidBlocks[chain[i].Index] = false;
                    continue;
                }

                bool isValid = _blockchainService.IsBlockValid(chain[i], chain[i - 1]);
                result.ValidBlocks[chain[i].Index] = isValid;

                if (!isValid)
                    chainStillValid = false;
            }

            result.IsChainValid = chainStillValid;
            return result;
        }

        [HttpPost]
        public IActionResult RegisterWallet(string publicKeyXml, string displayName, string nodeId)
        {
            BlockchainService service;
            GetNodeScope(nodeId, out service);

            var wallet = service.RegisterWallet(publicKeyXml, displayName);
            return RedirectToAction("Index", new { nodeId });

        }

        [HttpPost]
        public async Task<IActionResult> DemoSetup(string nodeId)
        {
            BlockchainService service;
            GetNodeScope(nodeId, out service);

            var (Ivan, privateKey1) = await service.DemoCreateWallet("Ivan");
            var (Taras, privateKey2) = await service.DemoCreateWallet("Taras");

            decimal amount = 2.0m;
            decimal fee = 0.5m;



            // Getting some coins for test users so their balance won't be 0
            for (int i = 0; i < 30; i++)
            {
                await MinePending(privateKey1, nodeId);
                await MinePending(privateKey2, nodeId);
            }

            for (int i = 0; i < 12; i++)
            {
                var tx = new Transaction
                {
                    FromAddress = Ivan.Address,
                    ToAddress = Taras.Address,
                    Amount = amount,
                    Fee = fee,
                    Note = "Test payment service"
                };

                var sig = BlockchainService.SignPayload(tx.CanonicalPayload(), privateKey1);

                tx.Signature = sig;

                await service.CreateTransaction(tx);
            }

            return RedirectToAction("Index", new { nodeId });

        }

        [HttpPost]
        public async Task<IActionResult> CreateTransaction(string fromAddress, string toAddress, decimal amount, decimal fee, string privateKey, string note, string nodeId)
        {
            BlockchainService service;
            GetNodeScope(nodeId, out service);

            var tx = new Models.Transaction
            {
                FromAddress = fromAddress,
                ToAddress = toAddress,
                Amount = amount,
                Fee = fee,
                Note = note
            };

            tx.Signature = BlockchainService.SignPayload(tx.CanonicalPayload(), privateKey);
            Console.WriteLine($"1: {tx.Signature}");
            try
            {
                await service.CreateTransaction(tx);
            }
            catch (Exception ex)
            {
                TempData["TransactionError"] = ex.Message;
            }
            return RedirectToAction("Index", new { nodeId });

        }

        [HttpPost]
        public async Task<IActionResult> MinePending(string privateKey, string nodeId)
        {
            BlockchainService service;
            GetNodeScope(nodeId, out service);

            //try
            //{
                await service.MinePending(privateKey);
                await BroadcastLastBlock(nodeId);
            //}
            //catch (Exception ex)
            //{
                //TempData["Error"] = ex.Message;
            //}
            return RedirectToAction("Index", new { nodeId });

        }

        [HttpPost]
        public async Task<IActionResult> BroadcastLastBlock(string fromNodeId)
        {
            //await Task.Delay(new Random().Next(5000, 15000));

            BlockchainService fromNodeService;
            GetNodeScope(fromNodeId, out fromNodeService);


            var fullChain = await _blockRepository.GetChain();
            var countOfNodes = _nodeKeys.Keys.Count();

            foreach (var nodeId in _nodeKeys.Keys)
            {
                if (nodeId == fromNodeId) continue;

                BlockchainService toNodeService;
                GetNodeScope(nodeId, out toNodeService);
                //try
                //{
                    var success = await toNodeService.TryAddExternalChain(fullChain);
                    if (!success)
                    {
                        _logger.LogWarning($"Failed to sync chain from node {fromNodeId} to node {nodeId}.");
                    }
                TempData["BroadcastMessage"] = $"Block #{fullChain.LastOrDefault().Index} was broadcasted. Accepted: {countOfNodes - 1} of {countOfNodes} nodes";
                //}
                //catch (Exception ex)
                //{
                //    TempData["Error"] = $"Failed to sync chain to node {nodeId}: {ex.Message}";
                //    _logger.LogError(ex, $"Failed to add new block to the node {nodeId}!");

                //}
            }
            return RedirectToAction("Index", new { nodeId = fromNodeId });
        }


    }
}