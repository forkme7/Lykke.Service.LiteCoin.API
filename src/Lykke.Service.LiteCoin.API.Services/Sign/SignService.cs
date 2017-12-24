﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Common.Log;
using Lykke.Service.LiteCoin.API.Core.Exceptions;
using Lykke.Service.LiteCoin.API.Core.Sign;
using Lykke.Service.LiteCoin.API.Core.Transactions;
using Lykke.Service.LiteCoin.API.Core.Wallet;
using NBitcoin;

namespace Lykke.Service.LiteCoin.API.Services.Sign
{
    public class SignService: ISignService
    {
        private readonly IBlockchainSignServiceApiProvider _serviceApiProvider;
        private readonly IWalletService _walletService;
        private readonly ITransactionBlobStorage _transactionBlobStorage;
        private readonly ILog _log;

        public SignService(IBlockchainSignServiceApiProvider serviceApiProvider,
            IWalletService walletService, 
            ILog log, 
            ITransactionBlobStorage transactionBlobStorage)
        {
            _serviceApiProvider = serviceApiProvider;
            _walletService = walletService;
            _log = log;
            _transactionBlobStorage = transactionBlobStorage;
        }

        public async Task<Transaction> SignTransaction(Transaction unsignedTransaction, params BitcoinAddress[] publicAddress)
        {
            await _transactionBlobStorage.AddOrReplaceTransaction(unsignedTransaction.GetHash().ToString(), TransactionBlobType.Initial, unsignedTransaction.ToHex());

            foreach (var bitcoinAddress in publicAddress)
            {
                var wallet = await _walletService.GetByPublicAddress(bitcoinAddress.ToString());
                if (wallet == null)
                {
                    throw new BusinessException($"Wallet {bitcoinAddress} not found", ErrorCode.WalletNotFound);
                }
            }

            Transaction signedTx;

            try
            {
                signedTx =  await _serviceApiProvider.SignTransaction(unsignedTransaction, publicAddress.Select(p => p.ToString()).ToArray());

                await _transactionBlobStorage.AddOrReplaceTransaction(signedTx.GetHash().ToString(), TransactionBlobType.Signed, signedTx.ToHex());

            }
            catch (Exception e)
            {
                await _log.WriteErrorAsync(nameof(SignService), nameof(SignTransaction),
                    unsignedTransaction.GetHash().ToString(), e);

                throw new BusinessException("Sign error", ErrorCode.SignError);
            }

            return signedTx;

        }
    }
}
