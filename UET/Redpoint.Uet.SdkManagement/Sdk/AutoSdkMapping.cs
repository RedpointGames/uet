namespace Redpoint.Uet.SdkManagement
{
    public record class AutoSdkMapping
    {
        public required string RelativePathInsideAutoSdkPath { get; set; }

        public required string RelativePathInsideSdkPackagePath { get; set; }
    }
}