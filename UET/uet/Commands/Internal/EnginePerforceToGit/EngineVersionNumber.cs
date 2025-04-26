using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UET.Commands.Internal.EnginePerforceToGit
{
    public class EngineVersionNumber
    {
        public int Major { get; set; }

        public int Minor { get; set; }

        public EngineVersionNumber(string versionNumber)
        {
            ArgumentNullException.ThrowIfNull(versionNumber);

            var components = versionNumber.Split('.', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Major = int.Parse(components[0], CultureInfo.InvariantCulture);
            Minor = int.Parse(components[1], CultureInfo.InvariantCulture);
        }

        public void Minus(int minor)
        {
            Minor -= minor;
            if (Minor < 0)
            {
                Major -= 1;
                Minor += 28;
            }
        }

        public override string ToString()
        {
            return $"{Major}.{Minor}";
        }
    }
}
