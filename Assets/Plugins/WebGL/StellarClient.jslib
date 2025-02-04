mergeInto(LibraryManager.library, {
    JSCheckFreighter: async function() 
    {
        if (!window.freighterApi) 
        {
            console.log("Freighter API not detected.");
            SendMessage("StellarManager", "OnFreighterCheckComplete", -1);
            return;
        }
        else 
        {
            console.log("Freighter API detected.");
        }
        try 
        {
            const res = await window.freighterApi.isConnected();
            console.log("res: ", res);
            const isConnected = (res && res.isConnected) || false;
            SendMessage("StellarManager", "OnFreighterCheckComplete", isConnected ? 1 : 0);
        }
        catch (error) 
        {
            console.error("Error checking Freighter:", error);
            SendMessage("StellarManager", "OnFreighterCheckComplete", -2);
        }
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