namespace Redpoint.Uet.Database.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    [UetKind("LastEnginePath")]
    public class LastEnginePathModel : UetModel<LastEnginePathModel>
    {
        [UetField]
        public string? LastEnginePath { get; set; }
    }
}
