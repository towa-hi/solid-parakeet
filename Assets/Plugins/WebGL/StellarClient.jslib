mergeInto(LibraryManager.library, {
    JSSignString: async function(message) {
        const {rpc, xdr, nativeToScVal, TransactionBuilder, Transaction, Networks, Contract, Address, scValToNative} = StellarSdk;
        const FreighterApi = window.freighterApi;
        // Sign a new transaction that calls the contract function "hello" with one argument, a string.
        const { TransactionBuilder, Operation, Networks } = StellarSdk;
        const publicKey = await freighter.getPublicKey();
        const server = new StellarSdk.Server('https://horizon-testnet.stellar.org');
        const account = await server.loadAccount(publicKey);

        const transaction = new TransactionBuilder(account, {
                fee: '100',
                networkPassphrase: Networks.TESTNET
            })
            .addOperation(Operation.invokeHostFunction({
                contractId: contractId,
                function: 'hello',
                parameters: [StellarSdk.xdr.ScVal.scvString(message)]
            }))
            .setTimeout(30)
            .build();

        const signedXDR = await FreighterApi.signTransaction(
                transaction.toXDR(),
                { networkPassphrase: Networks.TESTNET }
        );

        if (signedXDR.error) {
            Module.SendUnityMessage(-1, `JSSignString() signedXDR error: ${signedXDR.error}`);
            return;
        }

        // Convert the string to a Unity-compatible format
        var bufferSize = lengthBytesUTF8(signedXDR.signedTxXdr) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(signedXDR.signedTxXdr, buffer, bufferSize);

        Module.SendUnityMessage(1, buffer);
    },
    
    
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
    
    JSGetFreighterAddress: async function()
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

    JSGetData: async function(contractAddressPtr, keyTypePtr, keyValuePtr)
    {
        const {rpc, xdr, Address, scValToNative} = window.StellarSdk;
        const server = new rpc.Server("https://soroban-testnet.stellar.org");
        // params
        const contractAddress = Address.fromString(UTF8ToString(contractAddressPtr));
        const keyTypeString = UTF8ToString(keyTypePtr);
        const keyValueString = UTF8ToString(keyValuePtr);
        console.log("KeyType and KeyValue:", keyTypeString, keyValueString);
        let keyParts = [new xdr.ScVal.scvSymbol(keyTypeString)];
        if (["User", "UserLobbyId"].includes(keyTypeString)) {
            keyParts.push(Address.fromString(keyValueString).toScVal());
        }
        else if (["Lobby", "SetupCommitments"].includes(keyTypeString)) {
            keyParts.push(new xdr.ScVal.scvU32(parseInt(keyValueString)));
        }
        else if (["AllUserIds", "AllLobbyIds"].includes(keyTypeString)) {
            // Global keys have no additional parameters
        }
        const dataKey = new xdr.ScVal.scvVec(keyParts);
        console.log("Generated datakey:", dataKey);
        const ledgerKey = xdr.LedgerKey.contractData(
            new xdr.LedgerKeyContractData({
                contract: contractAddress.toScAddress(),
                key: dataKey,
                durability: xdr.ContractDataDurability.persistent(), // TODO: change this to be configurable later
            })
        );
        console.log(`ledgerKey.toXDR: ${ledgerKey.toXDR('base64')}`);
        const getLedgerEntriesRes = await server.getLedgerEntries(ledgerKey);
        
        const entries = getLedgerEntriesRes.entries;
        console.log(entries);
        let valJsonArray = [];
        for (let entry of entries) {
            
            const entryData = entry.val._value._attributes.val;
            console.log(entryData);
            const native = scValToNative(entryData);
            console.log(native);
            const entryDataJson = JSON.stringify(native);
            console.log(entryDataJson)
            valJsonArray.push(entryDataJson);
        }
        const result = {entries: valJsonArray};
        console.log(`result: `, result);
        const resultString = JSON.stringify(result);
        Module.SendUnityMessage(1, resultString);
    },

    JSInvokeContractFunction: async function(addressPtr, contractAddressPtr, contractFunctionPtr, dataPtr)
    {
        try {
            // actual constants
            const {rpc, xdr, nativeToScVal, TransactionBuilder, Transaction, Networks, Contract, Address, scValToNative} = StellarSdk;
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
            // get account object with sequence number from rpc server (need this to make transaction)
            const account = await server.getAccount(address);
            // waiting...
            console.log(`JSInvokeContractFunction() account: ${account}`);
            // convert data to xdr

            const dataObject = JSON.parse(data);
            const dataScVal = nativeToScVal(dataObject, {
                type: "FlatTestReq",
                FlatTestReq: {
                    number: {type: "u32"},
                    word: {type: "string"}
                }
            });
            const fakeScVal = xdr.ScVal.scvMap([
                new xdr.ScMapEntry({
                    key: xdr.ScVal.scvSymbol("number"),
                    val: xdr.ScVal.scvU32(dataObject.number),
                }),
                new xdr.ScMapEntry({
                    key: xdr.ScVal.scvSymbol("word"),
                    val: xdr.ScVal.scvString(dataObject.word),
                })
            ]);
            const fakeScValString = fakeScVal.toXDR('base64');
            const dataScValString = dataScVal.toXDR('base64');
            let addressScVal = new Address(address).toScVal();

            console.log(`JSInvokeContractFunction() started. 
                address: ${address}, 
                contractAddress: ${contractAddress}, 
                contractFunction: ${contractFunction}, 
                data: ${data}, 
                fakeScValString: ${fakeScValString},
                currentNetwork: ${currentNetwork},
                transactionTimeoutSec: ${transactionTimeoutSec},
            `);
            
            // make contract object and call the contractFunction with address and inputScVal
            const contract = new Contract(contractAddress);
            const spec = await contract.spec();
            
            let contractCallOperation = contract.call(
                contractFunction,
                addressScVal,
                fakeScVal);
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
            console.log(getTransactionRes);
            if (getTransactionRes.status !== "SUCCESS") {
                Module.SendUnityMessage(-4, `JSInvokeContractFunction() getTransactionRes error: ${getTransactionRes}`);
                return;
            }
            
            
            let result = scValToNative(getTransactionRes.returnValue);
            console.log(`JSInvokeContractFunction() completed: result:`, result);
            const resultString = JSON.stringify(result);
            Module.SendUnityMessage(1, resultString);
            return;
        }
        catch (e)
        {
            console.error("JSInvokeContractFunction() unspecified error: ", e);
            Module.SendUnityMessage(-666, e);
            return;
        }
    },
    
    JSGetEvents: async function(filterPtr, contractAddressPtr, topicPtr) {
        const {rpc, nativeToScVal, TransactionBuilder, Transaction, Networks, Contract, Address, scValToNative} = StellarSdk;
        const filterString = UTF8ToString(filterPtr);
        const contractAddressString = UTF8ToString(contractAddressPtr);
        const topicString = UTF8ToString(topicPtr);
        
        const server = new rpc.Server("https://soroban-testnet.stellar.org");
        
        const includeDiagnosticEvents = false;
        
        // get startLedger
        const currentLedger = await server.getLatestLedger();
        const startLedgerSequence = Math.max(1, currentLedger.sequence - "9999");
        console.log(startLedgerSequence);
        console.log(contractAddressString);
        let params;
        if (filterString === "")
        {
            params = {
                startLedger: startLedgerSequence,
                filters: [{
                    contractIds: [contractAddressString],
                }]
            };
        }
        else
        {
            params = {
                startLedger: startLedgerSequence,
                filters: [{
                    contractIds: [contractAddressString],
                    // TODO: get topics
                }]
            };
        }
        console.log(params);
        const events = await server.getEvents(params);
        console.log(events);
        let eventList = [];
        for (let event of events.events)
        {
            if (!includeDiagnosticEvents && event.type === "diagnostic") {
                continue;
            }
            const eventTopicNative = event.topic.map(scVal => scValToNative(scVal));
            console.log(eventTopicNative);
            let eventValueNative = scValToNative(event.value);
            console.log(eventValueNative);
            const eventEntry = {
                contractId: event.contractId.toString(),
                id: event.id,
                type: event.type,
                pagingToken: event.pagingToken,
                topic: eventTopicNative,
                value: eventValueNative,
                txHash: event.txHash,
            }
            console.log(eventEntry);
            eventList.push(eventEntry);
        }
        const result = {events: eventList};
        console.log(result);
        const resultString = JSON.stringify(result);
        Module.SendUnityMessage(1, resultString);
    }
});