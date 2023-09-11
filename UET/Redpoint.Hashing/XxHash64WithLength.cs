namespace Redpoint.Hashing
{
    /// <summary>
    /// Represents an xxHash64 hash and the byte length of the original input data.
    /// </summary>
    /// <param name="Hash">The xxHash64 value as a signed integer.</param>
    /// <param name="ByteLength">
    /// The length of the byte sequence used as the original input. This is provided so that 
    /// if you hash a file or stream, you can retrieve the length of the data without having 
    /// to compute it separately from the hash.
    /// </param>
    public record struct XxHash64WithLength(long Hash, long ByteLength);
}