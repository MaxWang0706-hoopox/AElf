using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AElf.Kernel;
using AElf.Kernel.Blockchain;
using AElf.Kernel.Blockchain.Application;
using AElf.Kernel.Blockchain.Domain;
using AElf.Types;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.ObjectMapping;


namespace AElf.WebApp.MessageQueue.Helpers;

public class BlockChainDataEtoGenerator : IBlockChainDataEtoGenerator
{
    
    private readonly IBlockchainService _blockchainService;
    private readonly ITransactionResultQueryService _transactionResultQueryService;
    private readonly ITransactionManager _transactionManager;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<TransactionListEtoGenerator> _logger;
    
    public BlockChainDataEtoGenerator(IBlockchainService blockchainService,
        ITransactionResultQueryService transactionResultQueryService, ITransactionManager transactionManager,
        IObjectMapper objectMapper, ILogger<TransactionListEtoGenerator> logger)
    {
        _blockchainService = blockchainService;
        _transactionResultQueryService = transactionResultQueryService;
        _transactionManager = transactionManager;
        _objectMapper = objectMapper;
        _logger = logger;
    }
    
   
    public async Task<BlockEto> GetBlockMessageEtoByHeightAsync(long height, CancellationToken cts)
    {
        var block = await GetBlockByHeightAsync(height);
        return await GetBlockMessageEtoByBlockAsync(block,cts.IsCancellationRequested);
    }

    public async Task<BlockEto> GetBlockMessageEtoByHashAsync(Hash blockId)
    {
        Block block = await _blockchainService.GetBlockByHashAsync(blockId);
        
        return await GetBlockMessageEtoByBlockAsync(block,false);
    }

    private async Task<BlockEto> GetBlockMessageEtoByBlockAsync(Block  block, bool isCancellationRequested )
    {
       
        if (block == null)
        {
            _logger.LogWarning($"Failed to find block information, height: {block.Height + 1}");
            return null;
        }
        var blockHash = block.Header.GetHash();
        var blockHashStr = blockHash.ToHex();
        var blockHeight = block.Height;
        var blockTime = block.Header.Time.ToDateTime();
        
        BlockEto blockEto = new BlockEto()
        {
            BlockHash=blockHashStr,
            BlockNumber= blockHeight,
            PreviousBlockId=block.Header.PreviousBlockHash,
            PreviousBlockHash= block.Header.PreviousBlockHash.ToHex(),
            BlockTime=blockTime,
            SignerPubkey=block.Header.SignerPubkey.ToByteArray().ToHex(false),
            Signature=block.Header.Signature.ToHex(),
        };
        //blockEto's extra properties
        
        Dictionary<string, string> blockExtraProperties = new Dictionary<string, string>();
        blockExtraProperties.Add("Version",block.Header.Version.ToString());
        blockExtraProperties.Add("Bloom",block.Header.Bloom.ToBase64());
        blockExtraProperties.Add("ExtraData",block.Header.ToString());
        blockExtraProperties.Add("MerkleTreeRootOfTransactions",block.Header.MerkleTreeRootOfTransactions.ToHex());
        blockExtraProperties.Add("MerkleTreeRootOfTransactions",block.Header.MerkleTreeRootOfWorldState.ToHex());
        blockEto.ExtraProperties = blockExtraProperties;
        //blockEto.SetVersion();
        List<TransactionEto> transactions = new List<TransactionEto>();
        
        foreach (var txId in block.TransactionIds)
        {
            if (isCancellationRequested)
            {
                return null;
            }

            var transactionResult = await _transactionResultQueryService.GetTransactionResultAsync(txId, blockHash);
            if (transactionResult == null)
            {
                _logger.LogWarning(
                    $"Failed to find transactionResult, block hash: {blockHash},  transaction ID: {txId}");
                continue;
            }

            var transaction = await _transactionManager.GetTransactionAsync(txId);
            if (transaction == null)
            {
                _logger.LogWarning($"Failed to find transaction, block hash: {blockHash},  transaction ID: {txId}");
                continue;
            }
            TransactionEto transactionEto = new TransactionEto()
            {
                TransactionId = txId.ToHex(),
                From = transaction.From.ToBase58(),
                To = transaction.To.ToBase58(),
                MethodName= transaction.MethodName,
                Params=transaction.Params.ToBase64(),
                Signature=transaction.Signature.ToBase64(),
                Status=(int)transactionResult.Status,

            };
            //TransactionEto's  extra properties
            Dictionary<string, string> transactionExtraProperties = new Dictionary<string, string>();
            transactionExtraProperties.Add("Version",block.Header.Version.ToString());
            transactionExtraProperties.Add("RefBlockNumber",transaction.RefBlockNumber.ToString());
            transactionExtraProperties.Add("RefBlockPrefix",transaction.RefBlockPrefix.ToHex());
            transactionExtraProperties.Add("Bloom",transactionResult.Bloom.ToBase64());
            transactionExtraProperties.Add("ReturnValue",transactionResult.ReturnValue.ToHex());
            transactionExtraProperties.Add("Error",transactionResult.Error);

            transactionEto.ExtraProperties = transactionExtraProperties;  

            List<LogEventEto> logEvents = new List<LogEventEto>();
            int index = 0;
            foreach (var logEvent in transactionResult.Logs)
            {
     
                LogEventEto logEventEto = new LogEventEto()
                {
                    ContractAddress=logEvent.Address.ToBase58(),
                    EventName=logEvent.Name,
                    Index =index
                    
                };
                //logEventEto's  extra properties
                Dictionary<string, string> logEventEtoExtraProperties = new Dictionary<string, string>();
                logEventEtoExtraProperties.Add("Indexed",logEvent.Indexed.ToString());
                logEventEtoExtraProperties.Add("NonIndexed",logEvent.NonIndexed.ToHex());
                logEventEto.ExtraProperties = logEventEtoExtraProperties;
               
                logEvents.Add(logEventEto);
                index = index + 1;
            }
            transactionEto.LogEvents = logEvents;
            transactions.Add(transactionEto);
        }
        blockEto.Transactions = transactions;
        return blockEto;
    }

    public BlockEto GetBlockMessageEto(BlockExecutedSet blockExecutedSet)
    {

       return _objectMapper.Map<BlockExecutedSet, BlockEto>(blockExecutedSet);
      
        
    }

    
    private async Task<Block> GetBlockByHeightAsync(long height)
    {
        var chain = await _blockchainService.GetChainAsync();
        var hash = await _blockchainService.GetBlockHashByHeightAsync(chain, height, chain.LongestChainHash);
        var blocks = await _blockchainService.GetBlocksInLongestChainBranchAsync(hash, 1);
        return blocks.Any() ? blocks.First(): null;
    }

   
}