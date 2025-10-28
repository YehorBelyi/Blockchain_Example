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
            var validBlocks = new Dictionary<int, bool>();
            var signatureValidity = new Dictionary<int, bool>();

            bool chainStillValid = true;

            if (chain.Count > 0)
            {
                validBlocks[chain[0].Index] = true;
                signatureValidity[chain[0].Index] = chain[0].Verify();
            }

            for (int i = 1; i < chain.Count; i++)
            {
                // validate block
                signatureValidity[chain[i].Index] = chain[i].Verify();

                // if chain is not valid then mark other blocks as invalid
                if (!chainStillValid)
                {
                    validBlocks[chain[i].Index] = false;
                    continue;
                }

                bool isValid = _blockchainService.IsBlockValid(chain[i], chain[i - 1]);
                validBlocks[chain[i].Index] = isValid;

                if (!isValid)
                    chainStillValid = false;
            }

            ViewBag.ValidBlocks = validBlocks;
            ViewBag.SignatureValidity = signatureValidity;
            ViewBag.IsValid = chainStillValid;
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
    }
}
