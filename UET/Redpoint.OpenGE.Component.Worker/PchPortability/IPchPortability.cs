namespace Redpoint.OpenGE.Component.Worker.PchPortability
{
    using Redpoint.OpenGE.Protocol;
    using System.Threading.Tasks;

    internal interface IPchPortability
    {
        Task<PchFileReplacementLocations> ScanPchForReplacementLocationsAsync(
            string pchPath,
            string buildLayoutPath,
            CancellationToken cancellationToken);

        Task ConvertPchToPortablePch(
            string pchPath,
            string buildLayoutPath,
            CancellationToken cancellationToken);

        Task ConvertPotentialPortablePchToPch(
            string pchPath,
            string buildLayoutPath,
            CancellationToken cancellationToken);
    }
}
