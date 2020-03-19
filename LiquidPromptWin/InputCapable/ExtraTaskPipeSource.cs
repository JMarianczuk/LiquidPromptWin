using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;

namespace LiquidPromptWin.InputCapable
{
    public class ExtraTaskPipeSource : PipeSource
    {
        private bool _hasUnreadBytes;
        private byte[] _buffer;

        public byte[] UnreadBytes => _hasUnreadBytes ? _buffer : new byte[0];

        public override Task CopyToAsync(Stream destination, CancellationToken cancellationToken = new CancellationToken())
        {
            var stream = Console.OpenStandardInput();
            CopyToInternal(stream, destination, cancellationToken);
            return Task.CompletedTask;
        }
        private async Task CopyToInternal(Stream source, Stream destination, CancellationToken cancellationToken)
        {
            await Task.Run(async () =>
            {
                int bufferSize = 1;
                var _buffer = new byte[bufferSize];
                while (!cancellationToken.IsCancellationRequested)
                {
                    var readTask = source.ReadAsync(_buffer, 0, bufferSize, cancellationToken);
                    try
                    {
                        readTask.Wait(cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    var bytesRead = readTask.Result;
                    if (bytesRead > 0)
                    {
                        _hasUnreadBytes = true;
                        try
                        {
                            await destination.WriteAsync(_buffer, 0, bytesRead, cancellationToken);
                        }
                        catch (TaskCanceledException)
                        {
                            break;
                        }
                        catch (ObjectDisposedException)
                        {
                            break;
                        }
                        _hasUnreadBytes = false;
                    }
                }
            }, cancellationToken);
        }
    }
}