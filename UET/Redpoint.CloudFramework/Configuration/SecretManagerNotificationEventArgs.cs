namespace Redpoint.CloudFramework.Configuration
{
    using Google.Cloud.SecretManager.V1;
    using System;

    internal class SecretManagerNotificationEventArgs : EventArgs
    {
        public required Secret Secret { get; init; }
    }
}
