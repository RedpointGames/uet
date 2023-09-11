namespace Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories
{
    internal record class PotentiallyQuotedPath
    {
        private bool _quoted;
        private string _path;

        public PotentiallyQuotedPath(string potentiallyQuotedPath)
        {
            _quoted = potentiallyQuotedPath.StartsWith('"');
            _path = potentiallyQuotedPath.Trim('"');
        }

        public void MakeAbsolutePath(string workingDirectory)
        {
            var oldPath = _path;
            _path = System.IO.Path.IsPathRooted(_path) ? _path : System.IO.Path.Combine(workingDirectory, _path);
            if (!oldPath.Contains(' ', StringComparison.Ordinal) && _path.Contains(' ', StringComparison.Ordinal))
            {
                // We just added a space into this path, so it probably needs to be quoted to get the right effect.
                _quoted = true;
            }
        }

        public string Path
        {
            get
            {
                return _path;
            }
            set
            {
                _path = value;
            }
        }

        public override string ToString()
        {
            if (_quoted)
            {
                return '"' + _path + '"';
            }
            return _path;
        }
    }
}
