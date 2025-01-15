namespace Redpoint.CloudFramework.Infrastructure
{
    public interface IRandomStringGenerator
    {
        string GetRandomString(int halfLength);
    }
}
