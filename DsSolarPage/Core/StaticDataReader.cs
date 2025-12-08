using System.Text;
using EasyModbus;


public class StaticDataReader
{
    private ModbusClient client;

    public StaticDataReader(string ip, int port)
    {
        client = new ModbusClient(ip, port);
        client.Connect();
    }

    public StaticInfo Read()
    {
        var info = new StaticInfo();

        info.ModelName = ReadString(30001, 8);
        info.SerialNumber = ReadString(30009, 8);

        info.SwVersion = ReadU16(30017).ToString();
        info.InverterCapacity = ReadU16(30018);
        info.StringNum = ReadU16(30019);
        info.Password = ReadU16(30020);
        info.InverterId = ReadU16(30021);
        info.InstallYear = ReadU16(30022);
        info.InstallMonthDay = ReadU16(30023).ToString();

        // 네트워크
        info.Mac1 = ReadU16(30024).ToString();
        info.Mac2 = ReadU16(30025).ToString();
        info.Mac3 = ReadU16(30026).ToString();
        info.LocalIp1 = ReadU16(30027).ToString();
        info.LocalIp2 = ReadU16(30028).ToString();
        info.Gateway1 = ReadU16(30029).ToString();
        info.Gateway2 = ReadU16(30030).ToString();
        info.SubnetMask1 = ReadU16(30031).ToString();
        info.SubnetMask2 = ReadU16(30032).ToString();
        info.RemoteIp1 = ReadU16(30033).ToString();
        info.RemoteIp2 = ReadU16(30034).ToString();
        info.LocalPort = ReadU16(30035);
        info.RemotePort = ReadU16(30036);

        // COM2
        info.Com2Baudrate = ReadU16(30037);
        info.Com2DataBits = ReadU16(30039);
        info.Com2Parity = ReadU16(30040);
        info.Com2StopBit = ReadU16(30041);
        info.Com2FlowControl = ReadU16(30042);

        // COM3
        info.Com3Baudrate = ReadU16(30043);
        info.Com3DataBits = ReadU16(30045);
        info.Com3Parity = ReadU16(30046);
        info.Com3StopBit = ReadU16(30047);
        info.Com3FlowControl = ReadU16(30048);

        // Flash Address
        info.FlashAddress = ReadU16(30089).ToString();

        return info;
    }

    private ushort ReadU16(int address)
    {
        return (ushort)client.ReadHoldingRegisters(address - 1, 1)[0];
    }

    private string ReadString(int address, int length)
    {
        var arr = client.ReadHoldingRegisters(address - 1, length);
        byte[] buf = new byte[length * 2];

        for (int i = 0; i < length; i++)
        {
            buf[i * 2] = (byte)(arr[i] >> 8);
            buf[i * 2 + 1] = (byte)(arr[i] & 0xFF);
        }

        return Encoding.ASCII.GetString(buf).Trim('\0', ' ');
    }
}
