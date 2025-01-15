namespace Redpoint.CloudFramework.Repository.Geographic
{
    using System.Collections.Generic;

    public interface IGeoModel
    {
        Dictionary<string, ushort> GetHashKeyLengthsForGeopointFields();
    }
}
