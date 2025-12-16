using EasyModbus;
using System;

public class DynamicDataReader : IDisposable
{
    private readonly ModbusClient client;
    private bool _disposed;

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
        data.PvPower = ReadS32(30063 + offset);   // int32

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
        data.PowerFactorTotal = ReadU16(30079 + offset);  // / 1000.0f 예: 스케일 0.001 가정
        data.Frequency = ReadU16(30080 + offset);   // / 100.0f 예: 0.01 Hz 스케일 가정
        data.StackTemp = ReadS16(30081 + offset);    // / 10.0f 예: 0.1 ℃ 스케일 가정

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
        data.AccumWh = ReadU16(30090 + offset);   // 30090 : 누적 전력량(Wh)   Uint16(F002)
        data.AccumKwh = ReadU32(30091 + offset);   // 30091~30092 : 누적(kWh)    Uint32(F004)
        data.TodayWh = ReadU32(30093 + offset);   // 30093~30094 : 오늘 누적(Wh) Uint32(F004)

        // Peak / Max (PDF: 30095~30099)
        data.PeakPowerInstall = ReadU32(30095 + offset); // 30095~30096 : Peak Power after Installation  Uint32(F004)
        data.PeakPowerToday = ReadU32(30097 + offset); // 30097~30098 : Peak Power Today               Uint32(F004)
        data.MaxActivePower = ReadU16(30099 + offset); // 30099 : Maximum Active_power  




        return data;
    }

    private ushort ReadU16(int address)
    {
        return (ushort)client.ReadHoldingRegisters(address - 1, 1)[0];
    }

    private short ReadS16(int address)
    {
        return (short)client.ReadHoldingRegisters(address - 1, 1)[0];
    }

    private int ReadS32(int address)
    {
        // int32 → 레지스터 2개 사용 (High, Low)
        int[] regs = client.ReadHoldingRegisters(address - 1, 2);
        int high = regs[0];
        int low = regs[1];
        int value = (high << 16) | (low & 0xFFFF);
        return value;
    }

    private uint ReadU32(int address)
    {
        int[] regs = client.ReadHoldingRegisters(address - 1, 2);
        uint high = (uint)regs[0];
        uint low = (uint)regs[1];
        uint value = (high << 16) | (low & 0xFFFF);
        return value;
    }

    // ✅ 추가: Modbus 연결 종료(정석)
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
            // 종료 과정 예외는 무시 (정리하다가 프로그램이 죽는 것 방지)
        }
    }
}
