namespace Redpoint.Uet.Database.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    [UetKind("VerifiedDllFile")]
    public class VerifiedDllFileModel : UetModel<VerifiedDllFileModel>
    {
        [UetField]
        public long? LastWriteTime { get; set; }
    }
}
