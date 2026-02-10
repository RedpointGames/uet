namespace Io.Database.Views
{
    using Microsoft.EntityFrameworkCore;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    [Keyless]
    public class DesiredCapacityCalculations
    {
        public string? RunnerTag { get; set; }

        public double Percentile { get; set; }

        public double DesiredCapacity { get; set; }
    }
}
