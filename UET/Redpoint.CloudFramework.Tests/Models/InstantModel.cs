namespace Redpoint.CloudFramework.Tests.Models
{
    using NodaTime;
    using Redpoint.CloudFramework.Models;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    [Kind("instantModel")]
    internal sealed class InstantModel : Model<InstantModel>
    {
        [Type(FieldType.Timestamp), Indexed]
        public Instant? dateEndedUtc { get; set; }
    }
}
