using System.IO;

namespace LiquidPromptWin
{
    public class StandardInputWrap : Stream, IStoppableStream
    {
        private readonly Stream _internalStream;
        private bool _isStopped = false;
        public StandardInputWrap(Stream internalStream)
        {
            _internalStream = internalStream;
        }
        public override void Flush()
        {
            throw new System.NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new System.NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new System.NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_isStopped)
            {
                return 0;
            }

            return _internalStream.Read(buffer, offset, count);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new System.NotImplementedException();
        }

        public override bool CanRead => !_isStopped;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _isStopped ? 0 : _internalStream.Length;

        public override long Position
        {
            get => _internalStream.Position;
            set => _internalStream.Position = value;
        }
        public void Stop()
        {
            _isStopped = true;
        }
    }
}