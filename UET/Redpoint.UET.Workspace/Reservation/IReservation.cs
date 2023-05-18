namespace Redpoint.UET.Workspace.Reservation
{
    using System;

    public interface IReservation : IAsyncDisposable
    {
        string ReservedPath { get; }
    }
}
