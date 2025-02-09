mergeInto(LibraryManager.library, {
    JSCheckFreighter: async function() 
    {
        // actually constant
        const StellarSdk = window.StellarSdk;
        const { rpc, xdr, Address, nativeToScVal, TransactionBuilder, Transaction, Networks, Operation, Contract } = StellarSdk;
        const server = new rpc.Server("https://soroban-testnet.stellar.org", {allowHttp: true});
        const currentNetwork = Networks.TESTNET;
        const fee = StellarSdk.BASE_FEE;
        
        // parameters
        const inputData = "hello";
        const contractAddress = "CDSEFFTMRY3F4Y2C5J3KV7G7VEHGWJIWWWYE4BG2VAY34FII3KHNQ4GT";
        const contractFunction = "hello";
        const transactionTimeoutSec = 120;
        const pingFrequencyMS = 2000;
        
        // check if web template html has freighter
        if (!window.freighterApi) 
        {
            console.log("Freighter API not detected.");
            SendMessage("StellarManager", "OnFreighterCheckComplete", -1);
            return;
        }
        console.log("Freighter API detected.", window.freighterApi);
        const FreighterApi = window.freighterApi;
        
        // check if clients freighter browser extension exists
        const isConnectedRes = await FreighterApi.isConnected();
        console.log("isConnected res: ", isConnectedRes);
        const isConnected = (isConnectedRes && isConnectedRes.isConnected) || false;
        if (!isConnected)
        {
            console.log("isConnected error: ", isConnected);
            SendMessage("StellarManager", "OnFreighterCheckComplete", -2);
            return;
        }
        
        // ask extension for permission to use app
        const requestAccessRes = await FreighterApi.requestAccess();
        if (requestAccessRes.error)
        {
            console.log("requestAccess error: ", requestAccessRes.error);
            SendMessage("StellarManager", "OnFreighterCheckComplete", -3);
            return;
        }
        const address = requestAccessRes.address;
        const account = await server.getAccount(address);
        console.log("account: ", account);
        
        // convert inputData to xdr
        const inputScVal = nativeToScVal(inputData, {type: "string"});
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
        if (prepareTransactionRes.error)
        {
            console.log("prepareTransactionRes error: ", prepareTransactionRes.error);
            // NOTE: errors from prepareTransactionRes are somewhat relevant
            SendMessage("StellarManager", "OnFreighterCheckComplete", -4);
            return;
        }
        console.log("prepareTransactionRes: ", prepareTransactionRes);
        
        // convert response to xdr string and sign with Freighter
        const transactionXdrString = prepareTransactionRes.toEnvelope().toXDR().toString('base64');
        console.log("transactionXdrString: ", transactionXdrString);
        const signTransactionRes = await FreighterApi.signTransaction(transactionXdrString, {networkPassphrase: currentNetwork});
        if (signTransactionRes.error)
        {
            console.log("signTransactionRes error: ", signTransactionRes.error);
            SendMessage("StellarManager", "OnFreighterCheckComplete", -5);
            return;
        }
        console.log("signTransactionRes: ", signTransactionRes);
        
        // convert Freighter xdr string output back to Transaction object and send to network
        const signedXdrString = signTransactionRes.signedTxXdr; 
        console.log("signedXdrString: ", signedXdrString);
        const signedTransaction = new Transaction(signedXdrString, currentNetwork);
        console.log("signedTransaction: ", signedTransaction);
        let sendTransactionRes = await server.sendTransaction(signedTransaction);
        console.log("sendTransactionRes: ", sendTransactionRes);
        if (sendTransactionRes.error)
        {
            SendMessage("StellarManager", "OnFreighterCheckComplete", -6);
            return;
        }
        const transactionHash = sendTransactionRes.hash;
        console.log("Transaction submitted, hash:", sendTransactionRes.hash);
        
        // ping getTransactionRes every pingFrequencyMS until status isn't NOT_FOUND or PENDING
        let getTransactionRes = await server.getTransaction(transactionHash);
        console.log("calling getTransactionRes until not NOT_FOUND or PENDING, please wait warmly...");
        while (getTransactionRes.status === "PENDING" || getTransactionRes.status === "NOT_FOUND")
        {
            console.log("checking getTransactionRes: ", getTransactionRes);
            await new Promise(resolve => setTimeout(resolve, pingFrequencyMS));
            getTransactionRes = await server.getTransaction(transactionHash);
        }
        console.log("got getTransactionRes: ", getTransactionRes);
        if (getTransactionRes.status === "FAILED")
        {
            console.log("getTransactionRes FAILED. resultXdr: ", getTransactionRes.resultXdr);
            SendMessage("StellarManager", "OnFreighterCheckComplete", -7);
            return;
        }
        
        // TODO: convert to json and pass back to Unity
        console.log("getTransactionRes SUCCESS. resultXdr: ", getTransactionRes.resultXdr);
        SendMessage("StellarManager", "OnFreighterCheckComplete", 1);
    },
    JSSetFreighterAllowed: async function() 
    {
        const isAllowedRes = await window.freighterApi.isAllowed();
        if (isAllowedRes.isAllowed)
        {
            alert("User has already allowed app");
            SendMessage("StellarManager", "OnSetFreighterAllowedComplete", 1)
        }
        else
        {
            const setAllowedRes = await window.freighterApi.setAllowed();
            if (setAllowedRes.isAllowed)
            {
                SendMessage("StellarManager", "OnSetFreighterAllowedComplete", 1)
            }
            else
            {
                alert("User has rejected app");
                SendMessage("StellarManager", "OnSetFreighterAllowedComplete", -1)
            }
        }
    }
    
});