namespace Redpoint.KubernetesManager.Tests
{
    using Redpoint.KubernetesManager.PxeBoot.Bootmgr;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class BootManagerTests
    {
        [Fact]
        public void TestEfibootmgrParse()
        {
            var tab = '\t';
            var output =
                $"""
                BootCurrent: 0000
                Timeout: 0 seconds
                BootOrder: 0000,0004,0003,0005
                Boot0000* EFI Network{tab}AcpiEx(VMBus,,)/VenHw(9b17e5a2-0891-42dd-b653-80b5c22809ba,635161f83edfc546913ff2d2f965ed0e7c43d7a29f048f469cfd765e69eec77b)/MAC(000000000000,0)/IPv4(0.0.0.0,0,DHCP,0.0.0.0,0.0.0.0,0.0.0.0)
                Boot0001* FrontPage{tab}MemoryMapped(11,0x100000,0x5dffff)/FvFile(4042708a-0f2d-4823-ac60-0d77b3111889)
                Boot0002  FrontPage{tab}MemoryMapped(11,0x100000,0x5dffff)/FvFile(4042708a-0f2d-4823-ac60-0d77b3111889)
                Boot0003* Windows Boot Manager{tab}HD(1,GPT,65604577-96f4-4851-872f-df5886722450,0x800,0x82000)/\EFI\Microsoft\Boot\bootmgfw.efi57494e444f5753000100000088000000780000004200430044004f0042004a004500430054003d007b00390064006500610038003600320063002d0035006300640064002d0034006500370030002d0061006300630031002d006600330032006200330034003400640034003700390035007d00000000000100000010000000040000007fff0400
                Boot0004* EFI Network{tab}AcpiEx(VMBus,,)/VenHw(9b17e5a2-0891-42dd-b653-80b5c22809ba,635161f83edfc546913ff2d2f965ed0e1ed81a093682d44286cce8d7aa339ee8)/MAC(000000000000,0)/IPv4(0.0.0.0,0,DHCP,0.0.0.0,0.0.0.0,0.0.0.0)
                Boot0005* EFI SCSI Device{tab}AcpiEx(VMBus,,)/VenHw(9b17e5a2-0891-42dd-b653-80b5c22809ba,d96361baa104294db60572e2ffb1dc7f7a8a3fdfec51db4b8203351a459be4d5)/SCSI(0,0)
                """;

            var bootManager = new DefaultEfiBootManagerParser();
            var configuration = bootManager.ParseBootManagerConfiguration(output);

            Assert.Equal(0, configuration.BootCurrentId);
            Assert.Equal(0, configuration.Timeout);
            Assert.Equal(
                new int[] { 0, 4, 3, 5 },
                configuration.BootOrder);
            Assert.Equal(
                new EfiBootManagerEntry
                {
                    BootId = 0,
                    Name = "EFI Network",
                    Path = "AcpiEx(VMBus,,)/VenHw(9b17e5a2-0891-42dd-b653-80b5c22809ba,635161f83edfc546913ff2d2f965ed0e7c43d7a29f048f469cfd765e69eec77b)/MAC(000000000000,0)/IPv4(0.0.0.0,0,DHCP,0.0.0.0,0.0.0.0,0.0.0.0)",
                    Active = true,
                },
                Assert.Contains(0, configuration.BootEntries));
            Assert.Equal(
                new EfiBootManagerEntry
                {
                    BootId = 1,
                    Name = "FrontPage",
                    Path = "MemoryMapped(11,0x100000,0x5dffff)/FvFile(4042708a-0f2d-4823-ac60-0d77b3111889)",
                    Active = true,
                },
                Assert.Contains(1, configuration.BootEntries));
            Assert.Equal(
                new EfiBootManagerEntry
                {
                    BootId = 2,
                    Name = "FrontPage",
                    Path = "MemoryMapped(11,0x100000,0x5dffff)/FvFile(4042708a-0f2d-4823-ac60-0d77b3111889)",
                    Active = false,
                },
                Assert.Contains(2, configuration.BootEntries));
            Assert.Equal(
                new EfiBootManagerEntry
                {
                    BootId = 3,
                    Name = "Windows Boot Manager",
                    Path = "HD(1,GPT,65604577-96f4-4851-872f-df5886722450,0x800,0x82000)/\\EFI\\Microsoft\\Boot\\bootmgfw.efi57494e444f5753000100000088000000780000004200430044004f0042004a004500430054003d007b00390064006500610038003600320063002d0035006300640064002d0034006500370030002d0061006300630031002d006600330032006200330034003400640034003700390035007d00000000000100000010000000040000007fff0400",
                    Active = true,
                },
                Assert.Contains(3, configuration.BootEntries));
            Assert.Equal(
                new EfiBootManagerEntry
                {
                    BootId = 4,
                    Name = "EFI Network",
                    Path = "AcpiEx(VMBus,,)/VenHw(9b17e5a2-0891-42dd-b653-80b5c22809ba,635161f83edfc546913ff2d2f965ed0e1ed81a093682d44286cce8d7aa339ee8)/MAC(000000000000,0)/IPv4(0.0.0.0,0,DHCP,0.0.0.0,0.0.0.0,0.0.0.0)",
                    Active = true,
                },
                Assert.Contains(4, configuration.BootEntries));
            Assert.Equal(
                new EfiBootManagerEntry
                {
                    BootId = 5,
                    Name = "EFI SCSI Device",
                    Path = "AcpiEx(VMBus,,)/VenHw(9b17e5a2-0891-42dd-b653-80b5c22809ba,d96361baa104294db60572e2ffb1dc7f7a8a3fdfec51db4b8203351a459be4d5)/SCSI(0,0)",
                    Active = true,
                },
                Assert.Contains(5, configuration.BootEntries));


            // Remove entry 0:
            // efibootmgr -b 0 -B

            // create
            // efibootmgr -c -d /dev/sda -L ipxe-recovery -l '\EFI\ipxe-recovery\ipxe.efi'
        }
    }
}
