public interface IMenuCommand { }

public class ConnectToNetworkCommand : IMenuCommand
{
    public bool isTestnet;
    public string contract;
    public string sneed;
    public bool isWallet;

    public ConnectToNetworkCommand(bool isTestnet, string contract, string sneed, bool isWallet)
    {
        this.isTestnet = isTestnet;
        this.contract = contract;
        this.sneed = sneed;
        this.isWallet = isWallet;
    }
}


