namespace GitHub.JPMikkers.Dhcp;

public struct OptionItem
{
    public OptionMode Mode;
    public IDhcpOption Option;

    public OptionItem(OptionMode mode, IDhcpOption option)
    {
        this.Mode = mode;
        this.Option = option;
    }
}
