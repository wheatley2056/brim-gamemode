namespace Brim.GameMode.Tests;

public sealed class SensorMathTests
{
    [Fact]
    public void Busiest_adapter_sums_processes_per_engine_and_takes_the_max()
    {
        var engines = new (string, double)[]
        {
            // dGPU: 3D-Engine von zwei Prozessen zusammen 90 %, Copy 10 %
            ("pid_100_luid_0x00000000_0x0000C3B1_phys_0_eng_0_engtype_3D", 70),
            ("pid_200_luid_0x00000000_0x0000C3B1_phys_0_eng_0_engtype_3D", 20),
            ("pid_100_luid_0x00000000_0x0000C3B1_phys_0_eng_1_engtype_Copy", 10),
            // iGPU: 30 %
            ("pid_300_luid_0x00000000_0x0000D111_phys_0_eng_0_engtype_3D", 30),
        };

        var busiest = SensorMath.BusiestAdapter(engines);

        Assert.Equal(("luid_0x00000000_0x0000C3B1", 90.0), busiest);
        Assert.Null(SensorMath.BusiestAdapter([]));
    }

    [Fact]
    public void Luids_parse_from_instance_names()
    {
        Assert.Equal((1, 0xC3B1u), SensorMath.ParseLuid("luid_0x00000001_0x0000C3B1"));
        Assert.Null(SensorMath.ParseLuid("nonsense"));
    }

    [Fact]
    public void Vram_of_one_process_over_all_adapters()
    {
        var memory = new (string, double)[]
        {
            ("pid_100_luid_0x00000000_0x0000C3B1_phys_0", 4e9),
            ("pid_100_luid_0x00000000_0x0000D111_phys_0", 1e8),
            ("pid_1000_luid_0x00000000_0x0000C3B1_phys_0", 9e9),
        };

        Assert.Equal((ulong)4.1e9, SensorMath.ProcessVram(memory, 100));
        Assert.Null(SensorMath.ProcessVram(memory, 7));
    }

    [Fact]
    public void Driver_temperature_is_in_tenths_and_zero_means_unknown()
    {
        Assert.Equal(67.5, SensorMath.Temperature(675));
        Assert.Null(SensorMath.Temperature(0));
    }
}
