using System;
using MySql.Data.MySqlClient;

public class ErrorRepository
{
	private readonly string _connStr;

	public ErrorRepository(string connStr)
	{
		_connStr = connStr;
	}

	public void Insert(string level, string deviceId, string summary, Exception ex)
	{
		using var conn = new MySqlConnection(_connStr);
		conn.Open();

		const string sql = @"
INSERT INTO error_log (occurred_at, level, device_id, summary, message)
VALUES (NOW(), @level, @device_id, @summary, @message);
";

		using var cmd = new MySqlCommand(sql, conn);
		cmd.Parameters.AddWithValue("@level", level);
		cmd.Parameters.AddWithValue("@device_id", deviceId);
		cmd.Parameters.AddWithValue("@summary", summary);
		cmd.Parameters.AddWithValue("@message", ex.ToString());
		cmd.ExecuteNonQuery();
	}
}
