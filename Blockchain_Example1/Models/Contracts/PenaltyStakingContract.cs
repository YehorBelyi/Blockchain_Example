using Blockchain_Example1.Models;
using Blockchain_Example1.Services;
using System.Collections.Generic;
using System.Linq;

namespace Blockchain_Example1.Models.Contracts
{
    public class PenaltyStakingContract : ISmartContract
    {
        public string Address { get; }

        private readonly Dictionary<string, StakeRecord> _stakes = new Dictionary<string, StakeRecord>(StringComparer.OrdinalIgnoreCase);

        private readonly decimal _rewardPerBlockPerToken;
        private readonly int _minLockBlocks;
        private readonly decimal _earlyPenaltyPercent;

        public decimal TotalPenaltiesCollected { get; private set; } = 0m;

        public PenaltyStakingContract(string address, decimal rewardPerBlockPerToken,
                                      int minLockBlocks, decimal earlyPenaltyPercent)
        {
            Address = address;
            _rewardPerBlockPerToken = rewardPerBlockPerToken;
            _minLockBlocks = minLockBlocks;
            _earlyPenaltyPercent = earlyPenaltyPercent;
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

            return true;
        }

        private bool HandleDeposit(Transaction tx, int currentBlock)
        {
            if (tx.Amount <= 0) return false;

            var userAddress = tx.FromAddress;

            if (!_stakes.TryGetValue(userAddress, out var record))
            {
                record = new StakeRecord();
                _stakes[userAddress] = record;
            }

            if (record.Principal == 0)
            {
                record.StartBlock = currentBlock;
            }

            record.Principal += tx.Amount;
            return true;
        }

        private bool HandleWithdraw(Transaction tx, int currentBlock)
        {
            var userAddress = tx.ToAddress;

            if (!_stakes.TryGetValue(userAddress, out var record) || record.Principal == 0)
            {
                return false;
            }

            if (tx.Amount <= 0) return false;

            int heldBlocks = currentBlock - record.StartBlock;
            if (heldBlocks < 0) heldBlocks = 0;

            decimal totalReward = record.Principal * _rewardPerBlockPerToken * heldBlocks;
            decimal totalPayable = record.Principal + totalReward;
            decimal finalPayout;

            if (heldBlocks < _minLockBlocks)
            {
                decimal penaltyAmount = record.Principal * _earlyPenaltyPercent;

                finalPayout = record.Principal - penaltyAmount + totalReward;
                TotalPenaltiesCollected += penaltyAmount;
            }
            else
            {
                finalPayout = totalPayable;
            }

            if (tx.Amount > finalPayout)
            {
                return false;
            }

            record.Principal = 0;
            record.StartBlock = 0;

            tx.Amount = finalPayout;

            return true;
        }

        public (decimal Principal, decimal Reward, int HeldBlocks, bool IsLocked) GetStakeInfo(string userAddress, int currentBlock)
        {
            if (!_stakes.TryGetValue(userAddress, out var record) || record.Principal == 0)
            {
                return (0m, 0m, 0, false);
            }

            int heldBlocks = currentBlock - record.StartBlock;
            decimal reward = record.Principal * _rewardPerBlockPerToken * (heldBlocks > 0 ? heldBlocks : 0);
            bool isLocked = heldBlocks < _minLockBlocks;

            return (record.Principal, reward, heldBlocks, isLocked);
        }

        public Dictionary<string, StakeRecord> GetStakeRecords()
        {
            return _stakes;
        }

        public decimal EarlyPenaltyPercent => _earlyPenaltyPercent;

    }
}