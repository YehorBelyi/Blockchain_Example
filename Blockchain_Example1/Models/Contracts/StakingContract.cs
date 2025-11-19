using Blockchain_Example1.Services;

namespace Blockchain_Example1.Models.Contracts
{
    public class StakingContract : ISmartContract
    {
        public string Address { get; set; }
        private decimal _rewardPerBlockPerToken;
        private readonly int _lockPeriodInBlocks;

        private readonly Dictionary<string, decimal> _stakes = new Dictionary<string, decimal>();
        private readonly Dictionary<string, int> _stakeStartBlock = new Dictionary<string, int>();

        public StakingContract(string address, decimal rewardPerBlockPerToken, int lockPeriodInBlocks)
        {
            Address = address;
            _rewardPerBlockPerToken = rewardPerBlockPerToken;
            _lockPeriodInBlocks = lockPeriodInBlocks;
        }

        public bool ValidateTranscation(BlockchainService chain, Transaction tx, int currentBlock)
        {
            bool isDeposit = String.Equals(tx.ToAddress, Address, StringComparison.OrdinalIgnoreCase);

            bool isWithdraw = String.Equals(tx.FromAddress, Address, StringComparison.OrdinalIgnoreCase);

            if (isDeposit)
            {
                return HandleDeposit(tx, currentBlock);
            }
            else if (isWithdraw)
            {
                return HandleWithdraw(tx, currentBlock);
            }

            return false;
        }

        private bool HandleDeposit(Transaction tx, int currentBlock)
        {
            var user = tx.FromAddress;
            if (!_stakes.TryGetValue(user, out var currentStake))
            {
                currentStake = 0m;
            }

            _stakes[user] = currentStake + tx.Amount;

            if (!_stakeStartBlock.ContainsKey(user))
            {
                _stakeStartBlock[user] = currentBlock;
            }

            return true;
        }

        private bool HandleWithdraw(Transaction tx, int currentBlock)
        {
            var user = tx.ToAddress;
            if (!_stakes.TryGetValue(user, out var currentStake))
            {
                return false;
            }

            if (!_stakeStartBlock.TryGetValue(user, out var startBlock))
            {
                return false;
            }

            if (currentBlock < startBlock + _lockPeriodInBlocks)
            {
                return false;
            }

            decimal reward = (currentBlock - startBlock) * _rewardPerBlockPerToken * currentStake;
            decimal totalPayout = currentStake + reward;

            if (tx.Amount > totalPayout)
            {
                return false;
            }

            tx.Amount = totalPayout;
            _stakes[user] = 0m;
            _stakeStartBlock.Remove(user);

            return true;
        }

        public decimal GetStakeInfo(string userAddress, int currentBlock)
        {
            decimal currentStake = 0m;
            int startBlock = 0;

            if (_stakes.TryGetValue(userAddress, out var stake))
            {
                currentStake = stake;
            }

            if (_stakeStartBlock.TryGetValue(userAddress, out var sBlock))
            {
                startBlock = sBlock;
            }
            decimal reward = 0m;

            if (currentStake > 0 && startBlock > 0)
            {
                reward = (currentBlock - startBlock) * _rewardPerBlockPerToken * currentStake;
            }
            return reward;
        }
    }
}
