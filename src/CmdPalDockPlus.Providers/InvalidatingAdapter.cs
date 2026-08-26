namespace CmdPalDockPlus.Providers;

public interface IInvalidatingWindowDataAdapter : IWindowDataAdapter
{
    event EventHandler? DataInvalidated;
}
