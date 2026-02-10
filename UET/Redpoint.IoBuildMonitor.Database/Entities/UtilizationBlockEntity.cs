namespace Io.Database.Entities
{
    using NodaTime;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class UtilizationBlockEntity
    {
        public long Week { get; set; }

        // 0 indexed. 0 = Sunday.
        public long DayInWeek { get; set; }

        // 15 minute blocks over the day. 0 = midnight.
        public long HourQuarter { get; set;}

        public long RunnerId { get; set; }

        public bool InUse { get; set; }
    }
}
