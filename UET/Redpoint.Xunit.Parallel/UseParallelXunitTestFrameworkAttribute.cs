namespace Redpoint.Xunit.Parallel
{
    using global::Xunit.Sdk;

    internal enum ParallelXunitMode
    {
        FixedCpuCount,
        FixedParallelismCount,
        RatioedCpuCount,
    }

    [TestFrameworkDiscoverer("Redpoint.Xunit.Parallel.ParallelXunitTestFrameworkTypeDiscoverer", "Redpoint.Xunit.Parallel")]
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class UseParallelXunitTestFrameworkAttribute : Attribute, ITestFrameworkAttribute
    {
        private readonly ParallelXunitMode _mode;
        private readonly int _fixedParallelismCount;
        private readonly double _coreToParallelismRatio;
        private readonly int? _maxParallelismCount;

        public UseParallelXunitTestFrameworkAttribute()
        {
            _mode = ParallelXunitMode.FixedCpuCount;
        }

        public UseParallelXunitTestFrameworkAttribute(int fixedParallelismCount)
        {
            _mode = ParallelXunitMode.FixedParallelismCount;
            _fixedParallelismCount = fixedParallelismCount;
        }

        public UseParallelXunitTestFrameworkAttribute(double coreToParallelismRatio, bool constrainMaxParallelismCount, int maxParallelismCount)
        {
            _mode = ParallelXunitMode.RatioedCpuCount;
            _coreToParallelismRatio = coreToParallelismRatio;
            _maxParallelismCount = constrainMaxParallelismCount ? maxParallelismCount : null;
        }

        public int GetParallelismCount()
        {
            int count;
            switch (_mode)
            {
                case ParallelXunitMode.FixedParallelismCount:
                    count = _fixedParallelismCount;
                    break;
                case ParallelXunitMode.RatioedCpuCount:
                    count = (int)Math.Round(Environment.ProcessorCount * _coreToParallelismRatio);
                    if (_maxParallelismCount.HasValue)
                    {
                        count = Math.Min(count, _maxParallelismCount.Value);
                    }
                    break;
                case ParallelXunitMode.FixedCpuCount:
                default:
                    count = Environment.ProcessorCount;
                    break;
            }
            count = Math.Max(1, count);
            return count;
        }
    }
}