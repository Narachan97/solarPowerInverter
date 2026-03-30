using EasyModbus;
using System;

public class DynamicDataReader
{
    private readonly ModbusClient client;

    // ✅ Input Register 기준 주소
    private const int INPUT_BASE = 30001;

    public DynamicDataReader(ModbusClient sharedClient)
    {
        client = sharedClient ?? throw new ArgumentNullException(nameof(sharedClient));
    }

    public DynamicData ReadForStack(string deviceId, int stackNo)
    {
        int offset = stackNo * 100;

        var data = new DynamicData
        {
            DeviceId = deviceId,
            StackNo = stackNo,
            MeasuredAt = DateTime.Now
        };

        data.FaultBits0_15  = ReadU16(30057 + offset);
        data.FaultBits16_31 = ReadU16(30058 + offset);
        data.FaultBits32_47 = ReadU16(30059 + offset);
        data.FaultBits48_63 = ReadU16(30060 + offset);

        data.PvVoltage = ReadU16(30061 + offset);
        data.PvCurrent = ReadU16(30062 + offset);
        data.PvPower   = ReadS32(30063 + offset);

        data.DcnVoltage   = ReadU16(30065 + offset);
        data.InvRsVoltage = ReadU16(30066 + offset);
        data.InvStVoltage = ReadU16(30067 + offset);
        data.InvTrVoltage = ReadU16(30068 + offset);

        data.L1Voltage = ReadU16(30069 + offset);
        data.L2Voltage = ReadU16(30070 + offset);
        data.L3Voltage = ReadU16(30071 + offset);

        data.L1Current = ReadU16(30072 + offset);
        data.L2Current = ReadU16(30073 + offset);
        data.L3Current = ReadU16(30074 + offset);

        data.ActivePowerTotal   = ReadS32(30075 + offset);
        data.ReactivePowerTotal = ReadS32(30077 + offset);
        data.PowerFactorTotal   = ReadU16(30079 + offset);
        data.Frequency          = ReadU16(30080 + offset);
        data.StackTemp          = ReadS16(30081 + offset);

        data.Ad0 = ReadS16(30082 + offset);
        data.Ad1 = ReadS16(30083 + offset);
        data.Ad2 = ReadS16(30084 + offset);
        data.Ad3 = ReadS16(30085 + offset);

        data.RestartTime  = ReadS16(30086 + offset);
        data.RunMode      = ReadU16(30087 + offset);
        data.AntiPidState = ReadU16(30088 + offset);

        data.AccumWh  = ReadU16(30090 + offset);
        data.AccumKwh = ReadU32(30091 + offset);
        data.TodayWh  = ReadU32(30093 + offset);

        data.PeakPowerInstall = ReadU32(30095 + offset);
        data.PeakPowerToday   = ReadU32(30097 + offset);
        data.MaxActivePower   = ReadU16(30099 + offset);

        return data;
    }

    // ===============================
    // Input Register Read Functions
    // ===============================

    private int ToInputOffset(int address)
        => address - INPUT_BASE;

    private ushort ReadU16(int address)
        => (ushort)client.ReadInputRegisters(ToInputOffset(address), 1)[0];

    private short ReadS16(int address)
        => unchecked((short)(ushort)client.ReadInputRegisters(ToInputOffset(address), 1)[0]);

    private int ReadS32(int address)
    {
        int[] regs = client.ReadInputRegisters(ToInputOffset(address), 2);

        int low  = regs[0];
        int high = regs[1];

        return (high << 16) | (low & 0xFFFF);
    }

    private uint ReadU32(int address)
    {
        int[] regs = client.ReadInputRegisters(ToInputOffset(address), 2);

        uint low  = (uint)regs[0];
        uint high = (uint)regs[1];

        return (high << 16) | (low & 0xFFFF);
    }
}
