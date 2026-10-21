using System.Threading;

namespace FHZIPA.Installer.Security;

/// <summary>
/// Impede que dois instaladores rodem ao mesmo tempo. A instalacao mexe
/// no sistema, duas ao mesmo tempo atrapalham uma a outra.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;
    private bool _owned;

    public SingleInstanceGuard(string name = "FHZIPA.Installer.SingleInstance")
    {
        _mutex = new Mutex(initiallyOwned: false, name);
    }

    /// <summary>
    /// Tenta ser o unico instalador em execucao.
    /// </summary>
    public bool TryAcquire()
    {
        try
        {
            _owned = _mutex.WaitOne(TimeSpan.Zero, exitContext: false);
            return _owned;
        }
        catch (AbandonedMutexException)
        {
            // A instancia anterior caiu sem liberar. A.Lock ainda e nosso.
            _owned = true;
            return true;
        }
    }

    public void Dispose()
    {
        if (_owned)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }

            _owned = false;
        }

        _mutex.Dispose();
    }
}
