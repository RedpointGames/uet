namespace UET.Services
{
    internal interface ISelfLocation
    {
        string GetUetLocalLocation(bool versionIndependent = false);
    }
}
