mergeInto(LibraryManager.library, {
    CheckFreighter: async function() {
        if (!window.freighterApi) {
            console.log("Freighter API not detected.");
            SendMessage("StellarManager", "OnFreighterCheckComplete", -1);
            return;
        }
        else {
            console.log("Freighter API detected.");
        }
        try {
            const res = await window.freighterApi.isConnected();
            console.log("res: ", res);
            const isConnected = (res && res.isConnected) || false;
            SendMessage("StellarManager", "OnFreighterCheckComplete", isConnected ? 1 : 0);
        } catch (error) {
            console.error("Error checking Freighter:", error);
            SendMessage("StellarManager", "OnFreighterCheckComplete", -2);
        }
    }
    
    
});