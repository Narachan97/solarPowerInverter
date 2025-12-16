public class DynamicData
{
    public string DeviceId { get; set; }
    public int StackNo { get; set; }
    public DateTime MeasuredAt { get; set; }

    public ushort PvVoltage { get; set; }
    public ushort PvCurrent { get; set; }
    public int PvPower { get; set; }

    public ushort DcnVoltage { get; set; }

    public ushort InvRsVoltage { get; set; }
    public ushort InvStVoltage { get; set; }
    public ushort InvTrVoltage { get; set; }

    public ushort L1Voltage { get; set; }
    public ushort L2Voltage { get; set; }
    public ushort L3Voltage { get; set; }

    public ushort L1Current { get; set; }
    public ushort L2Current { get; set; }
    public ushort L3Current { get; set; }

    public int ActivePowerTotal { get; set; }
    public int ReactivePowerTotal { get; set; }
    public ushort PowerFactorTotal { get; set; }

    // 일단 주파수 쪽 숫자 맞춰주기 소수점 찍어주기 이건 테스트해봐야함
    public ushort Frequency { get; set; }
    public short StackTemp { get; set; }

    public uint TodayWh { get; set; }
    public uint AccumKwh { get; set; }
    public ushort AccumWh { get; set; }

    public ushort RunMode { get; set; }

    public ushort FaultBits0_15 { get; set; }
    public ushort FaultBits16_31 { get; set; }
    public ushort FaultBits32_47 { get; set; }
    public ushort FaultBits48_63 { get; set; }

    // 🔽 여기부터 새로 추가한 애들

    public short Ad0 { get; set; }
    public short Ad1 { get; set; }
    public short Ad2 { get; set; }
    public short Ad3 { get; set; }

    public short RestartTime { get; set; }
    public ushort AntiPidState { get; set; }

    public uint PeakPowerInstall { get; set; }
    public uint PeakPowerToday { get; set; }
    public ushort MaxActivePower { get; set; }
}
