using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Redpoint.KubernetesManager.Tests")]

namespace Redpoint.KubernetesManager.PxeBoot.Bootmgr
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    internal class DefaultEfiBootManagerParser : IEfiBootManagerParser
    {
        private readonly Regex _bootCurrentRegex = new Regex("^BootCurrent: (?<bootid>[0-9A-F]+)$");
        private readonly Regex _timeoutRegex = new Regex("^Timeout: (?<seconds>[0-9]+) seconds$");
        private readonly Regex _bootOrderRegex = new Regex("^BootOrder: (?<order>[0-9A-F,]+)$");
        private readonly Regex _bootEntryRegex = new Regex("^Boot(?<bootid>[0-9A-F]+)(?<activeflag>( |\\*)) (?<name>[^\\t]+)\\t(?<path>.+)$");

        public EfiBootManagerConfiguration ParseBootManagerConfiguration(string efibootmgrOutput)
        {
            int bootCurrentId = 0;
            int timeout = 0;
            List<int> bootOrder = new();
            Dictionary<int, EfiBootManagerEntry> bootEntries = new();

            var lines = efibootmgrOutput.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var bootCurrentMatch = _bootCurrentRegex.Match(lines[i]);
                var timeoutMatch = _timeoutRegex.Match(lines[i]);
                var bootOrderMatch = _bootOrderRegex.Match(lines[i]);
                var bootEntryMatch = _bootEntryRegex.Match(lines[i]);

                if (bootCurrentMatch.Success)
                {
                    bootCurrentId = int.Parse(
                        bootCurrentMatch.Groups["bootid"].Value,
                        NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture);
                    continue;
                }

                if (timeoutMatch.Success)
                {
                    timeout = int.Parse(
                        timeoutMatch.Groups["seconds"].Value,
                        CultureInfo.InvariantCulture);
                    continue;
                }

                if (bootOrderMatch.Success)
                {
                    foreach (var entry in bootOrderMatch.Groups["order"].Value.Split(','))
                    {
                        bootOrder.Add(
                            int.Parse(
                                entry,
                                NumberStyles.HexNumber,
                                CultureInfo.InvariantCulture));
                    }
                    continue;
                }

                if (bootEntryMatch.Success)
                {
                    var bootId = int.Parse(
                        bootEntryMatch.Groups["bootid"].Value,
                        NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture);
                    var isActive = bootEntryMatch.Groups["activeflag"].Value == "*";
                    var name = bootEntryMatch.Groups["name"].Value;
                    var path = bootEntryMatch.Groups["path"].Value;
                    bootEntries.Add(
                        bootId,
                        new EfiBootManagerEntry
                        {
                            BootId = bootId,
                            Name = name,
                            Path = path,
                            Active = isActive,
                        });
                }
            }

            return new EfiBootManagerConfiguration
            {
                BootCurrentId = bootCurrentId,
                Timeout = timeout,
                BootOrder = bootOrder,
                BootEntries = bootEntries,
            };
        }
    }
}
