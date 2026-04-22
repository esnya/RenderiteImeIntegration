using System.Threading;

namespace ResoniteImeIntegration.Core;

public sealed class ImeStateController(Action onEnable, Action onDisable)
{
    private int _depth;
    private readonly Action _onEnable = onEnable ?? throw new ArgumentNullException(nameof(onEnable));
    private readonly Action _onDisable =
        onDisable ?? throw new ArgumentNullException(nameof(onDisable));

    public int Depth => Volatile.Read(ref _depth);

    public void Enter()
    {
        if (Interlocked.Increment(ref _depth) == 1)
        {
            _onEnable();
        }
    }

    public void Exit()
    {
        if (Interlocked.Decrement(ref _depth) <= 0)
        {
            Interlocked.Exchange(ref _depth, 0);
            _onDisable();
        }
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _depth, 0);
        _onDisable();
    }
}
