using EasyModbus;
using System;

public class DynamicDataReader : IDisposable
{
    private readonly ModbusClient client;
    private bool _disposed;

    // ✅ PDF 30001을 0번으로 맞추는 Input Register 기준
    private const int INPUT_BASE = 30001;

    public DynamicDataReader(string ip, int port)
    {
        //// ============================
        //// ② RTU(Modbus RTU, RS-485)로 사용할 때
        ////    - 위 TCP용 부분을 전부 주석 처리하고
        ////      아래 RTU용 부분 주석을 풀어서 사용
        //// ============================
        //// client = new ModbusClient("COM3");      // 실제 연결된 포트 이름으로 변경
        //// client.Baudrate = 9600;                 // 인버터 화면의 Baud Rate
        //// client.DataBits = 8;                    // Data Bit
        //// client.Parity   = System.IO.Ports.Parity.None; // Parity = 0 (None)
        //// client.StopBits = System.IO.Ports.StopBits.One; // Stop Bit = 1
        //// client.UnitIdentifier = 1;              // 인버터 ID (설정 값 확인해서 변경)

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
        data.PvPower = ReadS32(30063 + offset);   // int32 (2 regs)

        data.DcnVoltage = ReadU16(30065 + offset);
        data.InvRsVoltage = ReadU16(30066 + offset);
        data.InvStVoltage = ReadU16(30067 + offset);
        data.InvTrVoltage = ReadU16(30068 + offset);

        data.L1Voltage = ReadU16(30069 + offset);
        data.L2Voltage = ReadU16(30070 + offset);
        data.L3Voltage = ReadU16(30071 + offset);

        data.L1Current = ReadU16(30072 + offset);
        data.L2Current = ReadU16(30073 + offset);
        data.L3Current = ReadU16(30074 + offset);

        // 전력/역률/주파수/온도
        data.ActivePowerTotal = ReadS32(30075 + offset);
        data.ReactivePowerTotal = ReadS32(30077 + offset);
        data.PowerFactorTotal = ReadU16(30079 + offset);
        data.Frequency = ReadU16(30080 + offset);
        data.StackTemp = ReadS16(30081 + offset);

        // AD0~AD3 : 30082~30085
        data.Ad0 = ReadS16(30082 + offset);
        data.Ad1 = ReadS16(30083 + offset);
        data.Ad2 = ReadS16(30084 + offset);
        data.Ad3 = ReadS16(30085 + offset);

        // Restart Time : 30086
        data.RestartTime = ReadS16(30086 + offset);

        // 운전 모드
        data.RunMode = ReadU16(30087 + offset);

        // Anti-PID State : 30088
        data.AntiPidState = ReadU16(30088 + offset);

        // 누적/오늘 발전량 (PDF: 30090~30094)
        data.AccumWh = ReadU16(30090 + offset);       // Uint16
        data.AccumKwh = ReadU32(30091 + offset);      // Uint32 (2 regs)
        data.TodayWh = ReadU32(30093 + offset);       // Uint32 (2 regs)

        // Peak / Max (PDF: 30095~30099)
        data.PeakPowerInstall = ReadU32(30095 + offset); // Uint32 (2 regs)
        data.PeakPowerToday = ReadU32(30097 + offset);   // Uint32 (2 regs)
        data.MaxActivePower = ReadU16(30099 + offset);   // Uint16

        return data;
    }

    // =========================
    // ✅ 핵심 변경: FC04 + 30001 기준 오프셋
    // =========================

    private int ToInputOffset(int address)
    {
        int off = address - INPUT_BASE; // 30001 -> 0
        if (off < 0) throw new ArgumentOutOfRangeException(nameof(address), $"Input Register address must be >= {INPUT_BASE}");
        return off;
    }

    private ushort ReadU16(int address)
    {
        return (ushort)client.ReadInputRegisters(ToInputOffset(address), 1)[0];
    }

    private short ReadS16(int address)
    {
        // 레지스터는 0~65535(int)로 들어오므로, signed로 해석하려면 ushort->short 변환이 안전합니다.
        return unchecked((short)(ushort)client.ReadInputRegisters(ToInputOffset(address), 1)[0]);
    }

    private int ReadS32(int address)
    {
        // int32 → 레지스터 2개 사용 (High, Low)
        int[] regs = client.ReadInputRegisters(ToInputOffset(address), 2);
        int high = regs[0];
        int low = regs[1];
        int value = (high << 16) | (low & 0xFFFF);
        return value;
    }

    private uint ReadU32(int address)
    {
        int[] regs = client.ReadInputRegisters(ToInputOffset(address), 2);
        uint high = (uint)regs[0];
        uint low = (uint)regs[1];
        uint value = (high << 16) | (low & 0xFFFF);
        return value;
    }

    // ✅ Modbus 연결 종료
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            if (client != null && client.Connected)
            {
                client.Disconnect();
            }
        }
        catch
        {
            // 종료 과정 예외는 무시
        }
    }
}
