using MySql.Data.MySqlClient;
using System;

public class DynamicRepository
{
    private readonly string _connStr;

    public DynamicRepository(string connStr)
    {
        _connStr = connStr;
    }

    public void Insert(DynamicData d)
    {
        using var conn = new MySqlConnection(_connStr);

        conn.Open();

        string sql = @"
    INSERT INTO dynamic_log (
        device_id, stack_no, measured_at,
        pv_voltage, pv_current, pv_power,
        dcn_voltage,
        inv_rs_voltage, inv_st_voltage, inv_tr_voltage,
        l1_voltage, l2_voltage, l3_voltage,
        l1_current, l2_current, l3_current,
        active_power_total, reactive_power_total, power_factor_total,
        frequency, stack_temp,
        today_wh, accum_kwh,
        accum_wh,
        run_mode,
        fault_bits_0_15, fault_bits_16_31,
        fault_bits_32_47, fault_bits_48_63,
        ad0, ad1, ad2, ad3,
        restart_time,
        anti_pid_state,
        peak_power_install,
        peak_power_today,
        max_active_power
    )
    VALUES (
        @device_id, @stack_no, @measured_at,
        @pv_v, @pv_c, @pv_p,
        @dcn_v,
        @rs_v, @st_v, @tr_v,
        @l1_v, @l2_v, @l3_v,
        @l1_c, @l2_c, @l3_c,
        @act_p, @react_p, @pf,
        @freq, @temp,
        @today_wh, @accum_kwh,
        @accum_wh,
        @run_mode,
        @fb0, @fb1, @fb2, @fb3,
        @ad0, @ad1, @ad2, @ad3,
        @restart_time,
        @anti_pid_state,
        @peak_install,
        @peak_today,
        @max_active
    );
";

        using var cmd = new MySqlCommand(sql, conn);

        cmd.Parameters.AddWithValue("@device_id", d.DeviceId);
        cmd.Parameters.AddWithValue("@stack_no", d.StackNo);
        cmd.Parameters.AddWithValue("@measured_at", d.MeasuredAt);

        cmd.Parameters.AddWithValue("@pv_v", d.PvVoltage);
        cmd.Parameters.AddWithValue("@pv_c", d.PvCurrent);
        cmd.Parameters.AddWithValue("@pv_p", d.PvPower);

        cmd.Parameters.AddWithValue("@dcn_v", d.DcnVoltage);

        cmd.Parameters.AddWithValue("@rs_v", d.InvRsVoltage);
        cmd.Parameters.AddWithValue("@st_v", d.InvStVoltage);
        cmd.Parameters.AddWithValue("@tr_v", d.InvTrVoltage);

        cmd.Parameters.AddWithValue("@l1_v", d.L1Voltage);
        cmd.Parameters.AddWithValue("@l2_v", d.L2Voltage);
        cmd.Parameters.AddWithValue("@l3_v", d.L3Voltage);

        cmd.Parameters.AddWithValue("@l1_c", d.L1Current);
        cmd.Parameters.AddWithValue("@l2_c", d.L2Current);
        cmd.Parameters.AddWithValue("@l3_c", d.L3Current);

        cmd.Parameters.AddWithValue("@act_p", d.ActivePowerTotal);
        cmd.Parameters.AddWithValue("@react_p", d.ReactivePowerTotal);
        cmd.Parameters.AddWithValue("@pf", d.PowerFactorTotal);

        cmd.Parameters.AddWithValue("@freq", d.Frequency);
        cmd.Parameters.AddWithValue("@temp", d.StackTemp);

        cmd.Parameters.AddWithValue("@today_wh", d.TodayWh);
      
             cmd.Parameters.AddWithValue("@accum_wh", d.AccumWh);
        cmd.Parameters.AddWithValue("@accum_kwh", d.AccumKwh);

        cmd.Parameters.AddWithValue("@run_mode", d.RunMode);

        cmd.Parameters.AddWithValue("@fb0", d.FaultBits0_15);
        cmd.Parameters.AddWithValue("@fb1", d.FaultBits16_31);
        cmd.Parameters.AddWithValue("@fb2", d.FaultBits32_47);
        cmd.Parameters.AddWithValue("@fb3", d.FaultBits48_63);

        cmd.Parameters.AddWithValue("@ad0", d.Ad0);
        cmd.Parameters.AddWithValue("@ad1", d.Ad1);
        cmd.Parameters.AddWithValue("@ad2", d.Ad2);
        cmd.Parameters.AddWithValue("@ad3", d.Ad3);

        cmd.Parameters.AddWithValue("@restart_time", d.RestartTime);
        cmd.Parameters.AddWithValue("@anti_pid_state", d.AntiPidState);

        cmd.Parameters.AddWithValue("@peak_install", d.PeakPowerInstall);
        cmd.Parameters.AddWithValue("@peak_today", d.PeakPowerToday);
        cmd.Parameters.AddWithValue("@max_active", d.MaxActivePower);

        cmd.ExecuteNonQuery();
        conn.Close();
    }


}
