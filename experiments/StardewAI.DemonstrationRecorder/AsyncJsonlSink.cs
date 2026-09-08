using System.Threading.Channels;

namespace StardewAI.DemonstrationRecorder;

internal sealed class AsyncJsonlSink
{
    private readonly Channel<string> channel = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
    private readonly Task writerTask;

    public AsyncJsonlSink(string path)
    {
        writerTask = WriteLoopAsync(path);
    }

    public void Enqueue(string line)
    {
        if (!channel.Writer.TryWrite(line))
            throw new InvalidOperationException("Demonstration event channel is closed.");
    }

    public async Task CompleteAsync()
    {
        channel.Writer.TryComplete();
        await writerTask.ConfigureAwait(false);
    }

    private async Task WriteLoopAsync(string path)
    {
        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var writer = new StreamWriter(stream);
        var pending = 0;
        await foreach (var line in channel.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            await writer.WriteLineAsync(line).ConfigureAwait(false);
            pending++;
            if (pending >= 64)
            {
                await writer.FlushAsync().ConfigureAwait(false);
                pending = 0;
            }
        }
        await writer.FlushAsync().ConfigureAwait(false);
    }
}
