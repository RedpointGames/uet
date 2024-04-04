namespace Redpoint.Uet.Workspace
{
    using System.Collections.Generic;

    internal interface IWorkspaceReservationParameterGenerator
    {
        string[] ConstructReservationParameters(params string[] parameters);

        string[] ConstructReservationParameters(IEnumerable<string> parameters);
    }
}
