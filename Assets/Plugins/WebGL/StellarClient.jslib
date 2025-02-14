mergeInto(LibraryManager.library, {
    SendUnityMessage: function(code, data)
    {
        // Get caller function name from the stack trace
        const functionName = new Error().stack.split("\n")[2].trim().split(" ")[1] || "UnknownFunction";
        const response = { function: functionName, code: code, data: data };
        console.log(response);
        SendMessage("StellarManager", "StellarResponse", JSON.stringify(response));
    },
    
    JSCheckWallet: async function()
    {
        const FreighterApi = window.freighterApi;
        // check if web template html has freighter
        if (!FreighterApi) {
            _SendUnityMessage(-1, `JSCheckWallet() failed because Freighter API not detected.`);
            return;
        }
        // check if clients freighter browser extension exists
        const isConnectedRes = await FreighterApi.isConnected();
        if (isConnectedRes.error)
        {
            _SendUnityMessage(-2, `JSCheckWallet() isConnectedRes error: ${isConnectedRes}`);
            return;
        }
        console.log("isConnected res: ", isConnectedRes);
        const isConnected = (isConnectedRes && isConnectedRes.isConnected) || false;
        if (!isConnected) {
            _SendUnityMessage(-3, `JSCheckWallet() failed because isConnected false`);
            return;
        }
        _SendUnityMessage(1, `JSCheckWallet() success`);
        return;
    },

    JSGetAddress: async function()
    {
        const currentNetwork = StellarSdk.Networks.TESTNET;
        const FreighterApi = window.freighterApi;
        // check if extension is set to the right network
        const getNetworkRes = await FreighterApi.getNetwork();
        if (getNetworkRes.error) {
            _SendUnityMessage(-1, `JSGetAddress() getNetworkRes error: ${getNetworkRes}`);
            return;
        }
        if (getNetworkRes.networkPassphrase !== currentNetwork) {
            _SendUnityMessage(-2, `JSGetAddress() is on wrong network ${getNetworkRes}`);
            return;
        }
        // ask extension for permission to use app
        const requestAccessRes = await FreighterApi.requestAccess();
        if (requestAccessRes.error) {
            console.error("requestAccessRes error: ", requestAccessRes.error);
            _SendUnityMessage(-3, `JSGetAddress() requestAccessRes error: ${requestAccessRes}`);
            return;
        }
        _SendUnityMessage(1, requestAccessRes.address);
    },

    JSInvokeContractFunction: async function(addressPtr, contractAddressPtr, contractFunctionPtr, dataPtr, transactionTimeoutSec, pingFrequencyMS)
    {
        try {
            // actual constants
            const {rpc, nativeToScVal, TransactionBuilder, Transaction, Networks, Contract, Address} = StellarSdk;
            const FreighterApi = window.freighterApi;
            const server = new rpc.Server("https://soroban-testnet.stellar.org");
            const currentNetwork = Networks.TESTNET;
            const fee = StellarSdk.BASE_FEE;
            const maxTries = 10;
            const data = UTF8ToString(dataPtr);
            const address = UTF8ToString(addressPtr);
            const contractAddress = UTF8ToString(contractAddressPtr);
            const contractFunction = UTF8ToString(contractFunctionPtr);

            const account = await server.getAccount(address);
            console.log("account: ", account);

            // convert data to xdr
            const addressScVal = new Address(address).toScVal();
            const inputScVal = nativeToScVal(data, {type: "string"});
            console.log("inputScVal: ", inputScVal);

            // make contract object and call the contractFunction with address and inputScVal
            const contract = new Contract(contractAddress);
            console.log("contract: ", contract);
            const contractCallOperation = contract.call(
                contractFunction,
                addressScVal,
                inputScVal);
            console.log("contractCallOperation: ", contractCallOperation);

            // build the transaction and then sim it with prepareTransaction
            const transaction = new TransactionBuilder(account, {fee: fee, networkPassphrase: currentNetwork})
                .addOperation(contractCallOperation)
                .setTimeout(transactionTimeoutSec)
                .build();
            const prepareTransactionRes = await server.prepareTransaction(transaction);
            if (prepareTransactionRes.error) {
                // NOTE: errors from prepareTransactionRes are somewhat relevant
                _SendUnityMessage(-1, `JSInvokeContractFunction() prepareTransactionRes error: ${prepareTransactionRes}`);
                return;
            }
            console.log("prepareTransactionRes: ", prepareTransactionRes);

            // convert response to xdr string and sign with Freighter
            const transactionXdrString = prepareTransactionRes.toEnvelope().toXDR().toString('base64');
            console.log("transactionXdrString: ", transactionXdrString);
            const signTransactionRes = await FreighterApi.signTransaction(transactionXdrString, {networkPassphrase: currentNetwork});
            if (signTransactionRes.error) {
                _SendUnityMessage(-2, `JSInvokeContractFunction() signTransactionRes error: ${signTransactionRes}`);
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
            if (sendTransactionRes.status) {
                _SendUnityMessage(-3, `JSInvokeContractFunction() sendTransactionRes error: ${sendTransactionRes}`);
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
                _SendUnityMessage(-4, `JSInvokeContractFunction() getTransactionRes error: ${getTransactionRes}`);
                return;
            }

            // TODO: convert to json and pass back to Unity
            const returnValueXdrString = getTransactionRes.returnValue.toXDR('base64');
            console.log("getTransactionRes SUCCESS. returnValueXdr as string: ", returnValueXdrString);
            _SendUnityMessage(1, returnValueXdrString);
        }
        catch (e)
        {
            console.error("unspecified error: ", e);
            _SendUnityMessage(-666, e);
        }
    }
});