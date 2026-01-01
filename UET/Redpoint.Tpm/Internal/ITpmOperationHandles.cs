namespace Redpoint.Tpm.Internal
{
    using Tpm2Lib;

    internal interface ITpmOperationHandles : IDisposable
    {
        Tpm2Device TpmDevice { get; }

        Tpm2 Tpm { get; }

        TpmHandle EkHandle { get; }

        TpmPublic EkPublic { get; }

        TpmHandle AikHandle { get; }

        TpmPublic AikPublic { get; }
    }
}
