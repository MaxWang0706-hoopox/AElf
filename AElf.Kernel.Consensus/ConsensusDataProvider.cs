﻿using System;
using System.Threading.Tasks;
using AElf.Common;
using AElf.Configuration;
using AElf.Configuration.Config.Chain;
using AElf.Kernel.Storages;
using AElf.SmartContract;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using NLog;

namespace AElf.Kernel.Consensus
{
    // ReSharper disable InconsistentNaming
    public class ConsensusDataProvider
    {
        private readonly IStateStore _stateStore;

        private readonly ILogger _logger = LogManager.GetLogger(nameof(ConsensusDataProvider));

        public ConsensusDataProvider(IStateStore stateStore)
        {
            _stateStore = stateStore;
        }

        public Hash ChainId => Hash.LoadByteArray(ChainConfig.Instance.ChainId.DecodeBase58());

        public Address ContractAddress =>
            ContractHelpers.GetConsensusContractAddress(ChainId);
        
        private DataProvider DataProvider
        {
            get
            {
                var dp = DataProvider.GetRootDataProvider(ChainId, ContractAddress);
                dp.StateStore = _stateStore;
                return dp;
            }
        }
        
        /// <summary>
        /// Assert: Related value has surely exists in database.
        /// </summary>
        /// <param name="keyHash"></param>
        /// <param name="resourceStr"></param>
        /// <returns></returns>
        private async Task<byte[]> GetBytes<T>(Hash keyHash, string resourceStr = "") where T : IMessage, new()
        {
            return await (resourceStr != ""
                ? DataProvider.GetChild(resourceStr).GetAsync<T>(keyHash)
                : DataProvider.GetAsync<T>(keyHash));
        }
        
        public async Task<Miners> GetMiners()
        {
            try
            {
                var miners =
                    Miners.Parser.ParseFrom(
                        await GetBytes<Miners>(Hash.FromString(GlobalConfig.AElfDPoSOngoingMinersString)));
                return miners;
            }
            catch (Exception ex)
            {
                _logger?.Trace(ex, "Failed to get miners list.");
                return new Miners();
            }
        }

        public async Task<ulong> GetCurrentRoundNumber()
        {
            try
            {
                var number = UInt64Value.Parser.ParseFrom(
                    await GetBytes<UInt64Value>(Hash.FromString(GlobalConfig.AElfDPoSCurrentRoundNumber)));
                return number.Value;
            }
            catch (Exception ex)
            {
                _logger?.Trace(ex, "Failed to current round number.");
                return 0;
            }
        }

        public async Task<Round> GetCurrentRoundInfo()
        {
            var currentRoundNumber = await GetCurrentRoundNumber();
            try
            {
                var bytes = await GetBytes<Round>(Hash.FromMessage(new UInt64Value {Value = currentRoundNumber}),
                    GlobalConfig.AElfDPoSRoundsMapString);
                var round = Round.Parser.ParseFrom(bytes);
                return round;
            }
            catch (Exception e)
            {
                _logger.Error(e,
                    $"Failed to get Round information of provided round number. - {currentRoundNumber}\n");
                return null;
            }
        }

        public async Task<MinerInRound> GetMinerInfo(string addressToHex = null)
        {
            if (addressToHex == null)
            {
                addressToHex = NodeConfig.Instance.NodeAccount;
            }
            
            var round = await GetCurrentRoundInfo();
            return round.RealTimeMinersInfo[addressToHex.RemoveHexPrefix()];
        }

        public async Task<Timestamp> GetExpectMiningTime(string addressToHex = null)
        {
            if (addressToHex == null)
            {
                addressToHex = NodeConfig.Instance.NodeAccount;
            }

            var info = await GetMinerInfo(addressToHex);
            return info.ExpectedMiningTime;
        }

        public async Task<double> GetDistanceToTimeSlot(string addressToHex = null)
        {
            var now = DateTime.UtcNow.ToTimestamp();

            if (addressToHex == null)
            {
                addressToHex = NodeConfig.Instance.NodeAccount;
            }

            var timeSlot = await GetExpectMiningTime(addressToHex);
            var distance = timeSlot - now;
            return distance.ToTimeSpan().TotalMilliseconds;
        }
    }
}