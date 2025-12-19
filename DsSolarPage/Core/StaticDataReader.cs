using System;
using System.Text;
using EasyModbus;

public class StaticDataReader
{
    private readonly ModbusClient client;

    // ✅ Input Register 기준 주소 (30001 -> offset 0)
    private const int INPUT_BASE = 30001;

    public StaticDataReader(ModbusClient sharedClient)
    {
        client = sharedClient ?? throw new ArgumentNullException(nameof(sharedClient));
    }

    public StaticInfo Read()
    {
        var info = new StaticInfo();

        // F006 (Uint16 + char) : 레지스터(16bit) 안의 2바이트가 뒤집혀 저장된 케이스 대응
        info.ModelName = ReadStringF006_Swapped(30001, 8);
        info.SerialNumber = ReadStringF006_Swapped(30009, 8);

        // Uint16
        info.SwVersion = ReadU16(30017);
        info.InverterCapacity = ReadU16(30018);
        info.StringNum = ReadU16(30019);
        info.Password = ReadU16(30020);
        info.InverterId = ReadU16(30021);
        info.InstallYear = ReadU16(30022);
        info.InstallMonthDay = ReadU16(30023);

        // 네트워크 (Uint16)
        info.Mac1 = ReadU16(30024);
        info.Mac2 = ReadU16(30025);
        info.Mac3 = ReadU16(30026);
        info.LocalIp1 = ReadU16(30027);
        info.LocalIp2 = ReadU16(30028);
        info.Gateway1 = ReadU16(30029);
        info.Gateway2 = ReadU16(30030);
        info.SubnetMask1 = ReadU16(30031);
        info.SubnetMask2 = ReadU16(30032);
        info.RemoteIp1 = ReadU16(30033);
        info.RemoteIp2 = ReadU16(30034);
        info.LocalPort = ReadU16(30035);
        info.RemotePort = ReadU16(30036);

        // COM2
        info.Com2Baudrate = ReadU32(30037);
        info.Com2DataBits = ReadU16(30039);
        info.Com2Parity = ReadU16(30040);
        info.Com2StopBit = ReadU16(30041);
        info.Com2FlowControl = ReadU16(30042);

        // COM3
        info.Com3Baudrate = ReadU32(30043);
        info.Com3DataBits = ReadU16(30045);
        info.Com3Parity = ReadU16(30046);
        info.Com3StopBit = ReadU16(30047);
        info.Com3FlowControl = ReadU16(30048);

        // Flash Address
        info.FlashAddress = ReadS16(30089);

        info.LocalIpText = MakeIpText(info.LocalIp1, info.LocalIp2);
        info.MacText = MakeMacText(info.Mac1, info.Mac2, info.Mac3);
        info.GatewayText = MakeIpText(info.Gateway1, info.Gateway2);
        info.SubnetMaskText = MakeIpText(info.SubnetMask1, info.SubnetMask2);
        info.RemoteIpText = MakeIpText(info.RemoteIp1, info.RemoteIp2);

        return info;
    }

    // ✅ Input Register 오프셋 변환
    private int ToInputOffset(int address) => address - INPUT_BASE;

    // ✅ Holding → Input
    private ushort ReadU16(int address)
        => (ushort)client.ReadInputRegisters(ToInputOffset(address), 1)[0];

    // ✅ Holding → Input
    private uint ReadU32(int address)
    {
        var regs = client.ReadInputRegisters(ToInputOffset(address), 2);

        uint low = (uint)regs[0];
        uint high = (uint)regs[1];

        return (high << 16) | (low & 0xFFFF);
    }

    // ✅ Holding → Input (signed)
    private short ReadS16(int address)
        => unchecked((short)(ushort)client.ReadInputRegisters(ToInputOffset(address), 1)[0]);

    // ✅ Holding → Input
    private string ReadStringF006_Swapped(int address, int length)
    {
        var arr = client.ReadInputRegisters(ToInputOffset(address), length);
        byte[] buf = new byte[length * 2];

        for (int i = 0; i < length; i++)
        {
            buf[i * 2] = (byte)(arr[i] & 0xFF);
            buf[i * 2 + 1] = (byte)((arr[i] >> 8) & 0xFF);
        }

        var raw = Encoding.ASCII.GetString(buf);
        return CleanAsciiString(raw);
    }

    private string CleanAsciiString(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";

        s = s.Trim('\0', ' ');

        int cut = s.Length;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            bool printable = (c >= 0x20 && c <= 0x7E);
            if (!printable || c == '?')
            {
                cut = i;
                break;
            }
        }

        return s.Substring(0, cut).Trim();
    }

    // ✅ 변경: ip2의 high/low 바이트 swap
    private string MakeIpText(ushort ip1, ushort ip2)
    {
        byte a = (byte)((ip1 >> 8) & 0xFF);
        byte b = (byte)(ip1 & 0xFF);

        byte c = (byte)(ip2 & 0xFF);
        byte d = (byte)((ip2 >> 8) & 0xFF);

        return $"{a}.{b}.{c}.{d}";
    }

    // ✅ 변경: mac2, mac3의 high/low 바이트 swap
    private string MakeMacText(ushort mac1, ushort mac2, ushort mac3)
    {
        byte a = (byte)((mac1 >> 8) & 0xFF);
        byte b = (byte)(mac1 & 0xFF);

        byte c = (byte)(mac2 & 0xFF);
        byte d = (byte)((mac2 >> 8) & 0xFF);

        byte e = (byte)(mac3 & 0xFF);
        byte f = (byte)((mac3 >> 8) & 0xFF);

        return $"{a:X2}:{b:X2}:{c:X2}:{d:X2}:{e:X2}:{f:X2}";
    }
}
