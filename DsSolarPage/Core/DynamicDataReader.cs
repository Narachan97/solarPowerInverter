using EasyModbus;
using System;

public class DynamicDataReader
{
    private readonly ModbusClient client;

    public DynamicDataReader(string ip, int port)
    {
        client = new ModbusClient(ip, port);
        client.Connect();
    }

    public DynamicData ReadForStack(string deviceId, int stackNo)
    {
        int offset = stackNo * 100;   // 0=Main, 1=+100, 2=+200 ...

        var data = new DynamicData
        {
            DeviceId = deviceId,
            StackNo = stackNo,
            MeasuredAt = DateTime.Now
        };

        // Fault bits (30057~30060)
        data.FaultBits0_15 = ReadU16(30057 + offset);
        data.FaultBits16_31 = ReadU16(30058 + offset);
        data.FaultBits32_47 = ReadU16(30059 + offset);
        data.FaultBits48_63 = ReadU16(30060 + offset);

        // PV / DC / AC
        data.PvVoltage = ReadU16(30061 + offset);
        data.PvCurrent = ReadU16(30062 + offset);
        data.PvPower = ReadS32(30063 + offset);   // int32 (30063~30064)

        data.DcnVoltage = ReadU16(30065 + offset);

        // 인버터 선간/상전압
        data.InvRsVoltage = ReadU16(30066 + offset);
        data.InvStVoltage = ReadU16(30067 + offset);
        data.InvTrVoltage = ReadU16(30068 + offset);

        data.L1Voltage = ReadU16(30069 + offset);
        data.L2Voltage = ReadU16(30070 + offset);
        data.L3Voltage = ReadU16(30071 + offset);

        // 3상 전류
        data.L1Current = ReadU16(30072 + offset);
        data.L2Current = ReadU16(30073 + offset);
        data.L3Current = ReadU16(30074 + offset);

        // 전력/역률/주파수/온도
        data.ActivePowerTotal = ReadS32(30075 + offset);      // (30075~30076)
        data.ReactivePowerTotal = ReadS32(30077 + offset);    // (30077~30078)

        data.PowerFactorTotal = ReadS16(30079 + offset) / 1000.0f; // 스케일 가정
        data.Frequency = ReadU16(30080 + offset) / 100.0f;   // 스케일 가정
        data.StackTemp = ReadS16(30081 + offset) / 10.0f;    // 스케일 가정

        // AD0~AD3 : 30082~30085
        data.Ad0 = ReadS16(30082 + offset);
        data.Ad1 = ReadS16(30083 + offset);
        data.Ad2 = ReadS16(30084 + offset);
        data.Ad3 = ReadS16(30085 + offset);

        // Restart Time : 30086
        data.RestartTime = ReadU16(30086 + offset);

        // RunMode : 30087
        data.RunMode = ReadU16(30087 + offset);

        // Anti-PID State : 30088
        data.AntiPidState = ReadU16(30088 + offset);

        // 누적/오늘 발전량 (중복 제거 / 주소 쌍 유지)
        data.AccumKwh = ReadU32(30090 + offset);  // 30090~30091
        data.TodayWh = ReadU32(30092 + offset);  // 30092~30093

        // Peak / Max : 30094~30097
        data.PeakPowerInstall = ReadU32(30094 + offset); // 30094~30095 (2 regs)일 수도 있음 (PDF 확인 필요)
        data.PeakPowerToday = ReadU32(30095 + offset); // 여기 주소는 겹칠 수 있음 → PDF 꼭 확인 필요
        data.MaxActivePower = ReadU32(30096 + offset); // 30096~30097

        return data;
    }

    /* =========================
       Register Read Helpers
       (주소 기반 Input/Holding 자동 분기)
       ========================= */

    private int[] ReadRegisters(int address, int quantity)
    {
        // 30001 ~ 39999 : Input Register
        if (address >= 30001 && address <= 39999)
        {
            int start = address - 30001;
            return client.ReadInputRegisters(start, quantity);
        }

        // 40001 ~ 49999 : Holding Register
        if (address >= 40001 && address <= 49999)
        {
            int start = address - 40001;
            return client.ReadHoldingRegisters(start, quantity);
        }

        throw new ArgumentException($"지원하지 않는 Modbus 주소: {address}");
    }

    private ushort ReadU16(int address)
    {
        return (ushort)ReadRegisters(address, 1)[0];
    }

    private short ReadS16(int address)
    {
        return (short)ReadRegisters(address, 1)[0];
    }

    private int ReadS32(int address)
    {
        // int32 → 레지스터 2개 (High, Low)
        int[] regs = ReadRegisters(address, 2);
        int high = regs[0];
        int low = regs[1];
        return (high << 16) | (low & 0xFFFF);
    }

    private uint ReadU32(int address)
    {
        int[] regs = ReadRegisters(address, 2);
        uint high = (uint)regs[0];
        uint low = (uint)regs[1];
        return (high << 16) | (low & 0xFFFF);
    }
}
