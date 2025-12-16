public class StaticInfo
{
    public string DeviceId { get; set; }
    public string ModelName { get; set; }
    public string SerialNumber { get; set; }
    public ushort InverterCapacity { get; set; }
    public ushort StringNum { get; set; }
    public ushort InverterId { get; set; }
    public ushort Password { get; set; }
    public ushort SwVersion { get; set; }
    public ushort InstallYear { get; set; }
    public ushort InstallMonthDay { get; set; }

    public ushort Mac1 { get; set; }
    public ushort Mac2 { get; set; }
    public ushort Mac3 { get; set; }
    public ushort LocalIp1 { get; set; }
    public ushort LocalIp2 { get; set; }
    public ushort Gateway1 { get; set; }
    public ushort Gateway2 { get; set; }
    public ushort SubnetMask1 { get; set; }
    public ushort SubnetMask2 { get; set; }
    public ushort RemoteIp1 { get; set; }
    public ushort RemoteIp2 { get; set; }
    public ushort LocalPort { get; set; }
    public ushort RemotePort { get; set; }

    public uint Com2Baudrate { get; set; }
    public ushort Com2DataBits { get; set; }
    public ushort Com2Parity { get; set; }
    public ushort Com2StopBit { get; set; }
    public ushort Com2FlowControl { get; set; }

    public uint Com3Baudrate { get; set; }
    public ushort Com3DataBits { get; set; }
    public ushort Com3Parity { get; set; }
    public ushort Com3StopBit { get; set; }
    public ushort Com3FlowControl { get; set; }


    public short FlashAddress { get; set; }

    public string LocalIpText { get; set; }
    public string MacText { get; set; }
    public string GatewayText { get; set; }
    public string SubnetMaskText { get; set; }
    public string RemoteIpText { get; set; }

}



