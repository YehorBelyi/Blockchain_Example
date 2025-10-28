using System.Diagnostics;
using Blockchain_Example1.Models;
using Blockchain_Example1.Services;
using Microsoft.AspNetCore.Mvc;

namespace Blockchain_Example1.Controllers
{
    public class BlockchainController : Controller
    {
        private readonly ILogger<BlockchainController> _logger;
        private BlockchainService _blockchainService;

        public BlockchainController(ILogger<BlockchainController> logger, BlockchainService blockchainService)
        {
            _logger = logger;
            _blockchainService = blockchainService;
        }

        public IActionResult Index()
        {
            var chain = _blockchainService.GetChain();
            var validation = ValidateChain(chain);

            ViewBag.ValidBlocks = validation.ValidBlocks;
            ViewBag.SignatureValidity = validation.SignatureValidity;
            ViewBag.IsValid = validation.IsChainValid;
            ViewBag.Difficulty = BlockchainService.Difficulty;
            ViewBag.PrivateKey = _blockchainService.PrivateKey;

            return View(chain);
        }


        [HttpPost]
        public IActionResult Add(string data, string signature)
        {
            var ms = _blockchainService.AddBlock(data, signature);
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Edit(int? id)
        {
            Block block = _blockchainService.GetBlock(id);
            return View(block);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int? id, string? data, string? signature)
        {
            _blockchainService.EditBlock(id, data, signature);
            return RedirectToAction("Index");
        }

        // [27.10.25] Set mining difficulty
        [HttpPost]
        public IActionResult SetDifficulty(int difficulty)
        {
            if (difficulty < 1) difficulty = 1;
            if (difficulty > 10) difficulty = 10;

            BlockchainService.Difficulty = difficulty;
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult Search(string query)
        {
            var chain = _blockchainService.GetChain();
            var validation = ValidateChain(chain);

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

        private ChainValidationResult ValidateChain(List<Block> chain)
        {
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

    }
}
