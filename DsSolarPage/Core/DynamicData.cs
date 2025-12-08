public class DynamicData
{
    public string DeviceId { get; set; }
    public int StackNo { get; set; }
    public DateTime MeasuredAt { get; set; }

    public float PvVoltage { get; set; }
    public float PvCurrent { get; set; }
    public float PvPower { get; set; }

    public float DcnVoltage { get; set; }

    public float InvRsVoltage { get; set; }
    public float InvStVoltage { get; set; }
    public float InvTrVoltage { get; set; }

    public float L1Voltage { get; set; }
    public float L2Voltage { get; set; }
    public float L3Voltage { get; set; }

    public float L1Current { get; set; }
    public float L2Current { get; set; }
    public float L3Current { get; set; }

    public float ActivePowerTotal { get; set; }
    public float ReactivePowerTotal { get; set; }
    public float PowerFactorTotal { get; set; }

    public float Frequency { get; set; }
    public float StackTemp { get; set; }

    public long TodayWh { get; set; }
    public long AccumKwh { get; set; }

    public int RunMode { get; set; }

    public int FaultBits0_15 { get; set; }
    public int FaultBits16_31 { get; set; }
    public int FaultBits32_47 { get; set; }
    public int FaultBits48_63 { get; set; }

    // 🔽 여기부터 새로 추가한 애들

    public int Ad0 { get; set; }
    public int Ad1 { get; set; }
    public int Ad2 { get; set; }
    public int Ad3 { get; set; }

    public int RestartTime { get; set; }
    public int AntiPidState { get; set; }

    public long PeakPowerInstall { get; set; }
    public long PeakPowerToday { get; set; }
    public long MaxActivePower { get; set; }
}
