using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TypeSafe.AI.Tests.Support;

/// <summary>
/// A response body that never produces data and ignores cancellation tokens; only disposing it ends a pending read.
/// </summary>
internal sealed class StallingStream : Stream
{
    private volatile bool _disposed;

    public bool IsDisposed => _disposed;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        while (!_disposed)
        {
            await Task.Delay(5);
        }

        throw new ObjectDisposedException(nameof(StallingStream));
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        _disposed = true;
        base.Dispose(disposing);
    }
}

/// <summary>A response body that fails after delivering a few bytes.</summary>
internal sealed class FailingStream : Stream
{
    private bool _delivered;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (_delivered)
        {
            throw new IOException("connection reset");
        }

        _delivered = true;
        buffer[offset] = (byte)'{';
        return Task.FromResult(1);
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

/// <summary>Enums used to exercise typed choice questions and answers.</summary>
internal enum Tone
{
    Calm,
    [System.Runtime.Serialization.EnumMember(Value = "frustrated")]
    Frustrated,
    Angry,
}

[Flags]
internal enum Permissions
{
    None = 0,
    Read = 1,
    Write = 2,
}

internal enum NoMembers
{
}

internal enum DuplicateLabels
{
    First,
    [System.Runtime.Serialization.EnumMember(Value = "First")]
    Second,
}
