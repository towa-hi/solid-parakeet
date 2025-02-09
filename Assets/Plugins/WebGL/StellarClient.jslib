

mergeInto(LibraryManager.library, {
    JSCheckFreighter: async function(contractAddressPtr, contractFunctionPtr, dataPtr, transactionTimeoutSec, pingFrequencyMS) 
    {
        function logAndAlert(...args)
        {
            console.log(...args);
            alert(args.join());
        }
        
        try {
            // actual constants
            const StellarSdk = window.StellarSdk;
            const {rpc, nativeToScVal, TransactionBuilder, Transaction, Networks, Contract} = StellarSdk;
            const server = new rpc.Server("https://soroban-testnet.stellar.org");
            const currentNetwork = Networks.TESTNET;
            const fee = StellarSdk.BASE_FEE;
            const maxTries = 10;
            const data = UTF8ToString(dataPtr);
            const contractAddress = UTF8ToString(contractAddressPtr);
            const contractFunction = UTF8ToString(contractFunctionPtr);

            // check if web template html has freighter
            if (!window.freighterApi) {
                logAndAlert("Freighter API not detected.");
                SendMessage("StellarManager", "OnFreighterCheckComplete", -1);
                return;
            }
            console.log("Freighter API detected.", window.freighterApi);
            const FreighterApi = window.freighterApi;

            // check if clients freighter browser extension exists
            const isConnectedRes = await FreighterApi.isConnected();
            console.log("isConnected res: ", isConnectedRes);
            const isConnected = (isConnectedRes && isConnectedRes.isConnected) || false;
            if (!isConnected) {
                logAndAlert("isConnected error: ", isConnected);
                SendMessage("StellarManager", "OnFreighterCheckComplete", -2);
                return;
            }

            // check if extension is set to the right network
            const getNetworkRes = await FreighterApi.getNetwork();
            if (getNetworkRes.error) {
                logAndAlert("getNetworkRes error: ", getNetworkRes.error);
                SendMessage("StellarManager", "OnFreighterCheckComplete", -3)
                return;
            }
            if (getNetworkRes.networkPassphrase !== currentNetwork) {
                logAndAlert("getNetworkRes network is not currentNetwork");
                SendMessage("StellarManager", "OnFreighterCheckComplete", -4);
                return;
            }

            // ask extension for permission to use app
            const requestAccessRes = await FreighterApi.requestAccess();
            if (requestAccessRes.error) {
                logAndAlert("requestAccessRes error: ", requestAccessRes.error);
                SendMessage("StellarManager", "OnFreighterCheckComplete", -5);
                return;
            }
            const address = requestAccessRes.address;
            const account = await server.getAccount(address);
            console.log("account: ", account);

            // convert data to xdr
            const inputScVal = nativeToScVal(data, {type: "string"});
            console.log("inputScVal: ", inputScVal);

            // make contract object and call the contractFunction with inputScVal
            const contract = new Contract(contractAddress);
            console.log("contract: ", contract);
            const contractCallOperation = contract.call(contractFunction, inputScVal);
            console.log("contractCallOperation: ", contractCallOperation);

            // build the transaction and then sim it with prepareTransaction
            const transaction = new TransactionBuilder(account, {fee: fee, networkPassphrase: currentNetwork})
                .addOperation(contractCallOperation)
                .setTimeout(transactionTimeoutSec)
                .build();
            const prepareTransactionRes = await server.prepareTransaction(transaction);
            if (prepareTransactionRes.error) {
                logAndAlert("prepareTransactionRes error: ", prepareTransactionRes.error);
                // NOTE: errors from prepareTransactionRes are somewhat relevant
                SendMessage("StellarManager", "OnFreighterCheckComplete", -6);
                return;
            }
            console.log("prepareTransactionRes: ", prepareTransactionRes);

            // convert response to xdr string and sign with Freighter
            const transactionXdrString = prepareTransactionRes.toEnvelope().toXDR().toString('base64');
            console.log("transactionXdrString: ", transactionXdrString);
            const signTransactionRes = await FreighterApi.signTransaction(transactionXdrString, {networkPassphrase: currentNetwork});
            if (signTransactionRes.error) {
                logAndAlert("signTransactionRes error: ", signTransactionRes.error);
                SendMessage("StellarManager", "OnFreighterCheckComplete", -7);
                return;
            }
            console.log("signTransactionRes: ", signTransactionRes);

            // convert Freighter xdr string output back to Transaction object and send to network
            const signedXdrString = signTransactionRes.signedTxXdr;
            console.log("signedXdrString: ", signedXdrString);
            const signedTransaction = new Transaction(signedXdrString, currentNetwork);
            console.log("signedTransaction: ", signedTransaction);
            const sendTransactionRes = await server.sendTransaction(signedTransaction);
            console.log("sendTransactionRes: ", sendTransactionRes);
            if (sendTransactionRes.error) {
                logAndAlert("sendTransactionRes error: ", sendTransactionRes.error);
                SendMessage("StellarManager", "OnFreighterCheckComplete", -8);
                return;
            }
            const transactionHash = sendTransactionRes.hash;
            console.log("Transaction submitted, hash:", sendTransactionRes.hash);

            // ping getTransactionRes every pingFrequencyMS until status isn't NOT_FOUND or PENDING
            let getTransactionRes = await server.getTransaction(transactionHash);
            console.log("calling getTransactionRes until not NOT_FOUND or PENDING, please wait warmly...");
            let tryCounter = 0;
            while (getTransactionRes.status === "PENDING" || getTransactionRes.status === "NOT_FOUND" && tryCounter < maxTries) {
                await new Promise(resolve => setTimeout(resolve, pingFrequencyMS));
                tryCounter += 1;
                getTransactionRes = await server.getTransaction(transactionHash);
                console.log("checked getTransactionRes: ", getTransactionRes, "tryCounter: ", tryCounter);
            }
            if (getTransactionRes.status !== "SUCCESS") {
                logAndAlert("getTransactionRes not SUCCESS and exhausted all tries");
                SendMessage("StellarManager", "OnFreighterCheckComplete", -9);
                return;
            }

            // TODO: convert to json and pass back to Unity
            const returnValueXdrString = getTransactionRes.returnValue.toXDR('base64');
            logAndAlert("getTransactionRes SUCCESS. returnValueXdr as string: ", returnValueXdrString);
            SendMessage("StellarManager", "OnFreighterCheckComplete", 1);
            
        }
        catch (e)
        {
            logAndAlert("unspecified error: ", e);
            SendMessage("StellarManager", "OnFreighterCheckComplete", -666);
        }
    }
});