namespace Redpoint.OpenGE.LexerParser.LineScanning
{
    using System;
    using System.IO.MemoryMappedFiles;

    internal class ScannerOpenFile : IDisposable
    {
        private readonly FileStream _stream;
        private readonly MemoryMappedFile _mapped;
        private readonly MemoryMappedViewAccessor _view;
        private bool _disposedValue;

        public ScannerOpenFile(string filename)
        {
            _stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (_stream.Length > int.MaxValue)
            {
                _stream.Close();
                throw new ArgumentException($"'{filename}' is too large to scan!", nameof(filename));
            }
            _mapped = MemoryMappedFile.CreateFromFile(_stream, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, true);
            _view = _mapped.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
        }

        public ReadOnlySpan<byte> AsSpan()
        {
            unsafe
            {
                return new ReadOnlySpan<byte>(
                    (byte*)_view.SafeMemoryMappedViewHandle.DangerousGetHandle(),
                    (int)_stream.Length);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _view.Dispose();
                    _mapped.Dispose();
                    _stream.Dispose();
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
