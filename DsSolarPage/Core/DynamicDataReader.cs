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

        // ✅ 핵심: 30057 ~ 30099 한번에 읽기 (총 43개)
        int startAddress = 30057 + offset;
        int length = 43;

        int[] regs = client.ReadInputRegisters(ToInputOffset(startAddress), length);

        // 👉 index 계산 함수
        int idx(int addr) => addr - startAddress;

        data.FaultBits0_15 = (ushort)regs[idx(30057 + offset)];
        data.FaultBits16_31 = (ushort)regs[idx(30058 + offset)];
        data.FaultBits32_47 = (ushort)regs[idx(30059 + offset)];
        data.FaultBits48_63 = (ushort)regs[idx(30060 + offset)];

        data.PvVoltage = (ushort)regs[idx(30061 + offset)];
        data.PvCurrent = (ushort)regs[idx(30062 + offset)];
        data.PvPower = ToS32(regs, idx(30063 + offset));

        data.DcnVoltage = (ushort)regs[idx(30065 + offset)];
        data.InvRsVoltage = (ushort)regs[idx(30066 + offset)];
        data.InvStVoltage = (ushort)regs[idx(30067 + offset)];
        data.InvTrVoltage = (ushort)regs[idx(30068 + offset)];

        data.L1Voltage = (ushort)regs[idx(30069 + offset)];
        data.L2Voltage = (ushort)regs[idx(30070 + offset)];
        data.L3Voltage = (ushort)regs[idx(30071 + offset)];

        data.L1Current = (ushort)regs[idx(30072 + offset)];
        data.L2Current = (ushort)regs[idx(30073 + offset)];
        data.L3Current = (ushort)regs[idx(30074 + offset)];

        data.ActivePowerTotal = ToS32(regs, idx(30075 + offset));
        data.ReactivePowerTotal = ToS32(regs, idx(30077 + offset));
        data.PowerFactorTotal = (ushort)regs[idx(30079 + offset)];
        data.Frequency = (ushort)regs[idx(30080 + offset)];
        data.StackTemp = ToS16(regs[idx(30081 + offset)]);

        data.Ad0 = ToS16(regs[idx(30082 + offset)]);
        data.Ad1 = ToS16(regs[idx(30083 + offset)]);
        data.Ad2 = ToS16(regs[idx(30084 + offset)]);
        data.Ad3 = ToS16(regs[idx(30085 + offset)]);

        data.RestartTime = ToS16(regs[idx(30086 + offset)]);
        data.RunMode = (ushort)regs[idx(30087 + offset)];
        data.AntiPidState = (ushort)regs[idx(30088 + offset)];

        data.AccumWh = (ushort)regs[idx(30090 + offset)];
        data.AccumKwh = ToU32(regs, idx(30091 + offset));
        data.TodayWh = ToU32(regs, idx(30093 + offset));

        data.PeakPowerInstall = ToU32(regs, idx(30095 + offset));
        data.PeakPowerToday = ToU32(regs, idx(30097 + offset));
        data.MaxActivePower = (ushort)regs[idx(30099 + offset)];

        return data;
    }

    // ===============================
    // 변환 함수 (기존 Read 대신 사용)
    // ===============================

    private int ToInputOffset(int address)
        => address - INPUT_BASE;

    private short ToS16(int value)
        => unchecked((short)(ushort)value);

    private int ToS32(int[] regs, int index)
    {
        int low = regs[index];
        int high = regs[index + 1];
        return (high << 16) | (low & 0xFFFF);
    }

    private uint ToU32(int[] regs, int index)
    {
        uint low = (uint)regs[index];
        uint high = (uint)regs[index + 1];
        return (high << 16) | (low & 0xFFFF);
    }
}