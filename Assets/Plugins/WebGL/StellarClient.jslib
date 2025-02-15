mergeInto(LibraryManager.library, {
    JSCheckWallet: async function()
    {
        const FreighterApi = window.freighterApi;
        console.log(window.xdr.types());
        // check if web template html has freighter
        if (!FreighterApi) {
            Module.SendUnityMessage(-1, `JSCheckWallet() failed because Freighter API not detected.`);
            return;
        }
        // check if clients freighter browser extension exists
        const isConnectedRes = await FreighterApi.isConnected();
        if (isConnectedRes.error)
        {
            Module.SendUnityMessage(-2, `JSCheckWallet() isConnectedRes error: ${isConnectedRes}`);
            return;
        }
        console.log("isConnected res: ", isConnectedRes);
        const isConnected = (isConnectedRes && isConnectedRes.isConnected) || false;
        if (!isConnected) {
            Module.SendUnityMessage(-3, `JSCheckWallet() failed because isConnected false`);
            return;
        }
        Module.SendUnityMessage(1, `JSCheckWallet() success`);
        return;
    },
    
    JSGetAddress: async function()
    {
        const {Networks} = StellarSdk;
        const currentNetwork = Networks.TESTNET;
        const FreighterApi = window.freighterApi;
        // check if extension is set to the right network
        const getNetworkRes = await FreighterApi.getNetwork();
        if (getNetworkRes.error) {
            Module.SendUnityMessage(-1, `JSGetAddress() getNetworkRes error: ${getNetworkRes}`);
            return;
        }
        if (getNetworkRes.networkPassphrase !== currentNetwork) {
            Module.SendUnityMessage(-2, `JSGetAddress() is on wrong network ${getNetworkRes}`);
            return;
        }
        // ask extension for permission to use app
        const requestAccessRes = await FreighterApi.requestAccess();
        if (requestAccessRes.error) {
            console.error("requestAccessRes error: ", requestAccessRes.error);
            Module.SendUnityMessage(-3, `JSGetAddress() requestAccessRes error: ${requestAccessRes}`);
            return;
        }
        Module.SendUnityMessage(1, requestAccessRes.address);
    },

    JSGetUser: async function(addressPtr, contractAddressPtr)
    {
        const {rpc, xdr, StrKey, nativeToScVal, TransactionBuilder, Transaction, Networks, Contract, Address} = StellarSdk;
        const server = new rpc.Server("https://soroban-testnet.stellar.org");
        const xdrJson = window.xdr;
        
        const currentNetwork = Networks.TESTNET;
        // params
        const addressString = UTF8ToString(addressPtr);
        const contractAddressString = UTF8ToString(contractAddressPtr);
        console.log(`addressString: ${addressString}`);
        console.log(`contractAddressString: ${contractAddressString}`);

        const contract = Address.fromString(contractAddressString);
        
        const address = Address.fromString(addressString);
        const userKey = new xdr.ScVal.scvVec([
            new xdr.ScVal.scvSymbol("User"),
            address.toScVal()
        ]);
        console.log("userKey", userKey);
        
        const ledgerKey = xdr.LedgerKey.contractData(
            new xdr.LedgerKeyContractData({
                contract: contract.toScAddress(),
                key: userKey,
                durability: xdr.ContractDataDurability.persistent(),
            })
        );
        console.log(`ledgerKey.toXDR: ${ledgerKey.toXDR('base64')}`);
        const getLedgerEntriesRes = await server.getLedgerEntries(ledgerKey);
        const entry = getLedgerEntriesRes.entries[0];
        console.log("entry", entry);
        const valString = entry.val.toXDR('base64');
        console.log("valString", valString);
        const entryDataJson = xdrJson.decode("LedgerEntryData", valString);
        console.log("entryDataJson", entryDataJson);
        // TODO: finish this to return all entries
        // let entries = getLedgerEntriesRes.entries.map(entry => {
        //     return {
        //         key: xdrJson.decode("LedgerKey", entry.key),
        //         value: xdrJson.decode("LedgerEntryData", entry.value),
        //         lastModifiedLedgerSeq: entry.lastModifiedLedgerSeq,
        //         liveUntilLedgerSeq: entry.liveUntilLedgerSeq,
        //     };
        // });
        // const jsonEntries = JSON.stringify({
        //     entries,
        //     latestLedger: getLedgerEntriesRes.latestLedger,
        // });
        // console.log("getLedgerEntriesRes", jsonEntries);
        Module.SendUnityMessage(1, "done");
    },

    JSInvokeContractFunction: async function(addressPtr, contractAddressPtr, contractFunctionPtr, dataPtr)
    {
        try {
            // actual constants
            const {rpc, nativeToScVal, TransactionBuilder, Transaction, Networks, Contract, Address} = StellarSdk;
            const FreighterApi = window.freighterApi;
            const server = new rpc.Server("https://soroban-testnet.stellar.org");
            const currentNetwork = Networks.TESTNET;
            const fee = StellarSdk.BASE_FEE;
            const maxTries = 10;
            // parameters
            const data = UTF8ToString(dataPtr);
            const address = UTF8ToString(addressPtr);
            const contractAddress = UTF8ToString(contractAddressPtr);
            const contractFunction = UTF8ToString(contractFunctionPtr);
            const transactionTimeoutSec = 2000;
            console.log(`JSInvokeContractFunction() started. 
                address: ${address}, 
                contractAddress: ${contractAddress}, 
                contractFunction: ${contractFunction}, 
                data: ${data}, 
                currentNetwork: ${currentNetwork},
                transactionTimeoutSec: ${transactionTimeoutSec},
            `);
            // get account object with sequence number from rpc server (need this to make transaction)
            const account = await server.getAccount(address);
            // waiting...
            
            console.log(`JSInvokeContractFunction() account: ${account}`);
            // convert data to xdr
            const addressScVal = new Address(address).toScVal();
            const inputScVal = nativeToScVal(data, {type: "string"});
            // make contract object and call the contractFunction with address and inputScVal
            const contract = new Contract(contractAddress);
            const contractCallOperation = contract.call(
                contractFunction,
                addressScVal,
                inputScVal);
            // build the transaction and then sim it with prepareTransaction
            const transaction = new TransactionBuilder(account, {fee: fee, networkPassphrase: currentNetwork})
                .addOperation(contractCallOperation)
                .setTimeout(transactionTimeoutSec)
                .build();
            const prepareTransactionRes = await server.prepareTransaction(transaction);
            // waiting...
            
            if (prepareTransactionRes.error) {
                // NOTE: errors from prepareTransactionRes are somewhat relevant
                Module.SendUnityMessage(-1, `JSInvokeContractFunction() prepareTransactionRes error: ${prepareTransactionRes}`);
                return;
            }
            console.log(`JSInvokeContractFunction() prepareTransactionRes: ${prepareTransactionRes}`);
            if (prepareTransactionRes instanceof StellarSdk.FeeBumpTransaction) {
                console.log(`JSInvokeContractFunction() prepareTransactionRes returned a fee bump transaction with fee ${prepareTransactionRes.fee}`);
            }
            // convert response to xdr string and sign with Freighter
            const transactionXdrString = prepareTransactionRes.toEnvelope().toXDR().toString('base64');
            const signTransactionRes = await FreighterApi.signTransaction(transactionXdrString, {networkPassphrase: currentNetwork});
            // waiting for freighter...
            
            if (signTransactionRes.error) {
                Module.SendUnityMessage(-2, `JSInvokeContractFunction() signTransactionRes error: ${signTransactionRes}`);
                return;
            }
            console.log(`JSInvokeContractFunction() signTransactionRes: ${signTransactionRes}`);
            // convert Freighter xdr string output back to Transaction object and send to network
            const signedXdrString = signTransactionRes.signedTxXdr;
            const signedTransaction = new Transaction(signedXdrString, currentNetwork);
            const sendTransactionRes = await server.sendTransaction(signedTransaction);
            // waiting...
            
            if (sendTransactionRes.status === "ERROR") {
                Module.SendUnityMessage(-3, `JSInvokeContractFunction() sendTransactionRes error: ${sendTransactionRes.errorResultXdr}`);
                return;
            }
            console.log(`JSInvokeContractFunction() sendTransactionRes: ${sendTransactionRes}`);
            const transactionHash = sendTransactionRes.hash;
            // ping getTransactionRes every pingFrequencyMS until status isn't NOT_FOUND or PENDING
            console.log(`JSInvokeContractFunction() polling transaction hash ${transactionHash}, please wait warmly...`);
            let getTransactionRes = await server.pollTransaction(transactionHash, {attempts: maxTries, sleepStrategy: rpc.LinearSleepStrategy});
            // waiting...
            
            if (getTransactionRes.status !== "SUCCESS") {
                Module.SendUnityMessage(-4, `JSInvokeContractFunction() getTransactionRes error: ${getTransactionRes}`);
                return;
            }
            const returnValueXdrString = getTransactionRes.returnValue.toXDR('base64');
            console.log(`JSInvokeContractFunction() completed: returnValueXdrString: ${returnValueXdrString}`);
            Module.SendUnityMessage(1, returnValueXdrString);
            return;
        }
        catch (e)
        {
            console.error("JSInvokeContractFunction() unspecified error: ", e);
            Module.SendUnityMessage(-666, e);
            return;
        }
    }
});