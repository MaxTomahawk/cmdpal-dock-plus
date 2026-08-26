using System.IO.Pipes;
using System.Text;
using CmdPalDockPlus.Core.Compatibility;

namespace CmdPalDockPlus.Extension.Compatibility;

internal sealed class NamedPipeHoverEventSource : IHoverEventSource
{
    public const string PipeName = "CmdPalDockPlus.hover.v1";

    private readonly CancellationTokenSource _disposeCts = new();
    private Task? _loop;
    private bool _disposed;

    public event EventHandler<HoverEvent>? HoverChanged;

    public bool IsConnected { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        _loop ??= Task.Run(() => AcceptLoopAsync(_disposeCts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _disposeCts.Cancel();
        if (_loop is not null)
        {
            try { await _loop.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }

        _disposeCts.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly,
                    4096,
                    4096);

                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                IsConnected = true;
                try
                {
                    await ReadMessagesAsync(pipe, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    IsConnected = false;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (IOException)
            {
                await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ReadMessagesAsync(Stream stream, CancellationToken cancellationToken)
    {
        var message = new MemoryStream(HoverEventProtocol.MaxMessageBytes);
        var buffer = new byte[512];
        while (!cancellationToken.IsCancellationRequested)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0) return;
            for (var index = 0; index < read; index++)
            {
                var value = buffer[index];
                if (value == (byte)'\n')
                {
                    if (message.Length != 0)
                    {
                        var json = Encoding.UTF8.GetString(message.GetBuffer(), 0, checked((int)message.Length));
                        message.SetLength(0);
                        try
                        {
                            HoverChanged?.Invoke(this, HoverEventProtocol.Parse(json));
                        }
                        catch (HoverProtocolException)
                        {
                            // Invalid local messages are dropped; the pipe remains healthy.
                        }
                    }

                    continue;
                }

                if (message.Length >= HoverEventProtocol.MaxMessageBytes)
                {
                    throw new IOException("Hover message exceeds protocol limit.");
                }

                message.WriteByte(value);
            }
        }
    }
}
