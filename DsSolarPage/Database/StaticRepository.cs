using MySql.Data.MySqlClient;
public class StaticRepository
{
    private readonly MySqlConnection conn;

    public StaticRepository(string connStr)
    {
        conn = new MySqlConnection(connStr);
    }

    public void Save(StaticInfo info)
    {
        conn.Open();

        string sql = @"
INSERT INTO static_info
(
    device_id,
    model_name,
    serial_number,
    inverter_capacity,
    string_num,
    inverter_id,
    password,
    sw_version,
    install_year,
    install_month_day,
    mac_addr_1,
    mac_addr_2,
    mac_addr_3,
    local_ip_1,
    local_ip_2,
    gateway_1,
    gateway_2,
    subnet_mask_1,
    subnet_mask_2,
    remote_ip_1,
    remote_ip_2,
    local_port,
    remote_port,
    com2_baudrate,
    com2_data_bits,
    com2_parity,
    com2_stop_bit,
    com2_flow_control,
    com3_baudrate,
    com3_data_bits,
    com3_parity,
    com3_stop_bit,
    com3_flow_control,
local_ip_text,
mac_text,
gateway_text,
subnet_mask_text,
remote_ip_text,

    flash_address
)
VALUES
(
    @device_id,
    @model_name,
    @serial_number,
    @inverter_capacity,
    @string_num,
    @inverter_id,
    @password,
    @sw_version,
    @install_year,
    @install_month_day,
    @mac_addr_1,
    @mac_addr_2,
    @mac_addr_3,
    @local_ip_1,
    @local_ip_2,
    @gateway_1,
    @gateway_2,
    @subnet_mask_1,
    @subnet_mask_2,
    @remote_ip_1,
    @remote_ip_2,
    @local_port,
    @remote_port,
    @com2_baudrate,
    @com2_data_bits,
    @com2_parity,
    @com2_stop_bit,
    @com2_flow_control,
    @com3_baudrate,
    @com3_data_bits,
    @com3_parity,
    @com3_stop_bit,
    @com3_flow_control,
@local_ip_text,
@mac_text,
@gateway_text,
@subnet_mask_text,
@remote_ip_text,
    @flash_address
);";

        using (var cmd = new MySql.Data.MySqlClient.MySqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("@device_id", info.DeviceId);
            cmd.Parameters.AddWithValue("@model_name", info.ModelName);
            cmd.Parameters.AddWithValue("@serial_number", info.SerialNumber);
            cmd.Parameters.AddWithValue("@inverter_capacity", info.InverterCapacity);
            cmd.Parameters.AddWithValue("@string_num", info.StringNum);

            // ★ 추가 파라미터
            cmd.Parameters.AddWithValue("@inverter_id", info.InverterId);
            cmd.Parameters.AddWithValue("@password", info.Password);

            cmd.Parameters.AddWithValue("@sw_version", info.SwVersion);
            cmd.Parameters.AddWithValue("@install_year", info.InstallYear);
            cmd.Parameters.AddWithValue("@install_month_day", info.InstallMonthDay);

            cmd.Parameters.AddWithValue("@mac_addr_1", info.Mac1);
            cmd.Parameters.AddWithValue("@mac_addr_2", info.Mac2);
            cmd.Parameters.AddWithValue("@mac_addr_3", info.Mac3);

            cmd.Parameters.AddWithValue("@local_ip_1", info.LocalIp1);
            cmd.Parameters.AddWithValue("@local_ip_2", info.LocalIp2);
            cmd.Parameters.AddWithValue("@gateway_1", info.Gateway1);
            cmd.Parameters.AddWithValue("@gateway_2", info.Gateway2);
            cmd.Parameters.AddWithValue("@subnet_mask_1", info.SubnetMask1);
            cmd.Parameters.AddWithValue("@subnet_mask_2", info.SubnetMask2);
            cmd.Parameters.AddWithValue("@remote_ip_1", info.RemoteIp1);
            cmd.Parameters.AddWithValue("@remote_ip_2", info.RemoteIp2);

            cmd.Parameters.AddWithValue("@local_port", info.LocalPort);
            cmd.Parameters.AddWithValue("@remote_port", info.RemotePort);

            cmd.Parameters.AddWithValue("@com2_baudrate", info.Com2Baudrate);
            cmd.Parameters.AddWithValue("@com2_data_bits", info.Com2DataBits);
            cmd.Parameters.AddWithValue("@com2_parity", info.Com2Parity);
            cmd.Parameters.AddWithValue("@com2_stop_bit", info.Com2StopBit);
            cmd.Parameters.AddWithValue("@com2_flow_control", info.Com2FlowControl);

            cmd.Parameters.AddWithValue("@com3_baudrate", info.Com3Baudrate);
            cmd.Parameters.AddWithValue("@com3_data_bits", info.Com3DataBits);
            cmd.Parameters.AddWithValue("@com3_parity", info.Com3Parity);
            cmd.Parameters.AddWithValue("@com3_stop_bit", info.Com3StopBit);
            cmd.Parameters.AddWithValue("@com3_flow_control", info.Com3FlowControl);

            cmd.Parameters.AddWithValue("@flash_address", info.FlashAddress);
            cmd.Parameters.AddWithValue("@local_ip_text", info.LocalIpText);
            cmd.Parameters.AddWithValue("@mac_text", info.MacText);

            cmd.Parameters.AddWithValue("@gateway_text", info.GatewayText);
            cmd.Parameters.AddWithValue("@subnet_mask_text", info.SubnetMaskText);
            cmd.Parameters.AddWithValue("@remote_ip_text", info.RemoteIpText);

            cmd.ExecuteNonQuery();
        }

        conn.Close();
    }
}
