using System;
using System.Collections.Generic;
using System.Threading;
using EasyModbus;
using EasyModbus.Exceptions;
using MySql.Data.MySqlClient;

internal class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== Solar Collector 시작 ===");

        // 설정
        string inverterIp = "192.168.21.10";
        int port = 502;
        string connStr =
            "server=158.247.214.46;port=3306;Database=dsSolar;Uid=_hoo;Pwd=Dsplm7433mysql##;";

        // 상태/의존성 묶음
        var ctx = new CollectorContext(inverterIp, port, connStr);

        // 서비스(클래스)들
        var logger = new ErrorGateLogger();
        var conn = new ModbusConnectionManager();
        var stat = new StaticCollector();
        var dyn = new DynamicCollector();

        Console.WriteLine();
        Console.WriteLine("=== 데이터 수집 시작 (60초 간격, Modbus 연결 1개 공유) ===");

        while (true)
        {
            try
            {
                if (!conn.EnsureConnected(ctx, logger))
                {
                    Console.WriteLine("[동적] 연결 실패 → 이번 회차 스킵 (다음 회차 재시도)");
                }
                else
                {
                    if (ctx.StaticNeedsRefresh)
                    {
                        Console.WriteLine("[정적] 저장 조건 충족 → 정적 저장 시도");
                        if (!stat.TryCollectAndSave(ctx, logger, conn))
                        {
                            Thread.Sleep(60000);
                            continue;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(ctx.DeviceId))
                    {
                        Console.WriteLine("[동적] deviceId가 비어있어 동적 수집 스킵");
                    }
                    else
                    {
                        dyn.CollectAndSaveAllStacks(ctx, logger, conn);
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 동적 저장 회차 종료 (deviceId={ctx.DeviceId})");
                    }
                }
            }
            catch (Exception exOuter)
            {
                string summary = $"[회차/외부 오류] {exOuter.Message}";
                logger.SafeInsertError(ctx, "UNKNOWN_CYCLE", summary, exOuter);
                Console.WriteLine(summary);
                conn.MarkDisconnected(ctx);
            }

            Thread.Sleep(60000);
        }
    }
}

// =====================
// 아래부터는 “같은 파일” 안에 클래스만 추가
// =====================

public class CollectorContext
{
    public string InverterIp { get; }
    public int Port { get; }

    public ErrorRepository ErrorRepo { get; }
    public StaticRepository StaticRepo { get; }
    public DynamicRepository DynRepo { get; }

    public string DeviceId { get; set; } = "";
    public bool StaticNeedsRefresh { get; set; } = true;
    public bool LastConnOk { get; set; } = false;

    public Dictionary<string, ErrGate> ErrState { get; } = new();

    public ModbusClient SharedClient { get; set; }
    public StaticDataReader StaticReader { get; set; }
    public DynamicDataReader DynReader { get; set; }

    public CollectorContext(string inverterIp, int port, string connStr)
    {
        InverterIp = inverterIp;
        Port = port;
        ErrorRepo = new ErrorRepository(connStr);
        StaticRepo = new StaticRepository(connStr);
        DynRepo = new DynamicRepository(connStr);
    }
}

public struct ErrGate
{
    public string LastMessage;
    public DateTime LastLogUtc;
    public int RepeatCount;
}

public class ErrorGateLogger
{
    public bool ShouldLog(CollectorContext ctx, string key, string message)
    {
        var now = DateTime.UtcNow;

        if (!ctx.ErrState.TryGetValue(key, out var gate))
        {
            gate = new ErrGate { LastMessage = message, LastLogUtc = DateTime.MinValue, RepeatCount = 0 };
        }

        if (!string.Equals(gate.LastMessage, message, StringComparison.Ordinal))
        {
            gate.LastMessage = message;
            gate.LastLogUtc = now;
            gate.RepeatCount = 0;
            ctx.ErrState[key] = gate;
            return true;
        }

        gate.RepeatCount++;

        if ((now - gate.LastLogUtc).TotalSeconds >= 300)
        {
            gate.LastLogUtc = now;
            ctx.ErrState[key] = gate;
            return true;
        }

        if (gate.RepeatCount % 5 == 0)
        {
            gate.LastLogUtc = now;
            ctx.ErrState[key] = gate;
            return true;
        }

        ctx.ErrState[key] = gate;
        return false;
    }

    public void SafeInsertError(CollectorContext ctx, string level, string summary, Exception ex)
    {
        try
        {
            if (!ShouldLog(ctx, level, summary)) return;
            ctx.ErrorRepo.Insert(level, ctx.DeviceId, summary, ex);
        }
        catch (Exception logEx)
        {
            Console.WriteLine($"[에러로그 DB 저장 실패] {logEx.Message}");
        }
    }
}

public class ModbusConnectionManager
{
    public void ResetSharedConnection(CollectorContext ctx)
    {
        try
        {
            if (ctx.SharedClient != null && ctx.SharedClient.Connected)
                ctx.SharedClient.Disconnect();
        }
        catch { }

        ctx.SharedClient = null;
        ctx.StaticReader = null;
        ctx.DynReader = null;
    }

    public void MarkDisconnected(CollectorContext ctx)
    {
        ctx.StaticNeedsRefresh = true;
        ctx.LastConnOk = false;
        ResetSharedConnection(ctx);
    }

    public bool EnsureConnected(CollectorContext ctx, ErrorGateLogger logger)
    {
        try
        {
            if (ctx.SharedClient == null)
                ctx.SharedClient = new ModbusClient(ctx.InverterIp, ctx.Port);

            if (!ctx.SharedClient.Connected)
                ctx.SharedClient.Connect();

            if (!ctx.LastConnOk)
                ctx.StaticNeedsRefresh = true;

            ctx.LastConnOk = true;

            if (ctx.StaticReader == null) ctx.StaticReader = new StaticDataReader(ctx.SharedClient);
            if (ctx.DynReader == null) ctx.DynReader = new DynamicDataReader(ctx.SharedClient);

            return true;
        }
        catch (Exception ex)
        {
            string summary = $"[CONNECT 오류] {ex.Message}";
            string level = (ex is ConnectionException) ? "MODBUS_CONNECT" : "UNKNOWN_CONNECT";

            logger.SafeInsertError(ctx, level, summary, ex);
            Console.WriteLine(summary);

            MarkDisconnected(ctx);
            return false;
        }
    }
}

public class StaticCollector
{
    public bool TryCollectAndSave(CollectorContext ctx, ErrorGateLogger logger, ModbusConnectionManager conn)
    {
        try
        {
            Console.WriteLine("[정적] Read 시도...");

            var s = ctx.StaticReader.Read();

            if (string.IsNullOrWhiteSpace(s.SerialNumber))
            {
                ctx.DeviceId = "TEST_" + ctx.InverterIp.Replace(".", "_");
                Console.WriteLine($"[정적] 시리얼 없음 → TEST deviceId = {ctx.DeviceId}");
            }
            else
            {
                ctx.DeviceId = s.SerialNumber.Trim();
                Console.WriteLine($"[정적] 시리얼 OK → deviceId = {ctx.DeviceId}");
            }

            s.DeviceId = ctx.DeviceId;
            ctx.StaticRepo.Save(s);

            Console.WriteLine("[정적] DB 저장 완료");
            ctx.StaticNeedsRefresh = false;
            return true;
        }
        catch (Exception ex)
        {
            string summary = $"[정적/오류] {ex.Message}";
            string level = (ex is ConnectionException) ? "MODBUS_READ_STATIC"
                        : (ex is MySqlException) ? "DB_STATIC"
                        : "UNKNOWN_STATIC";

            logger.SafeInsertError(ctx, level, summary, ex);
            Console.WriteLine(summary);

            conn.MarkDisconnected(ctx);
            return false;
        }
    }
}

public class DynamicCollector
{
    public void CollectAndSaveAllStacks(CollectorContext ctx, ErrorGateLogger logger, ModbusConnectionManager conn)
    {
        for (int stackNo = 0; stackNo <= 4; stackNo++)
        {
            DynamicData d;

            try
            {
                d = ctx.DynReader.ReadForStack(ctx.DeviceId, stackNo);
            }
            catch (Exception exRead)
            {
                string summary = $"[동적/READ 오류] stack={stackNo} {exRead.Message}";
                string level = (exRead is ConnectionException) ? "MODBUS_READ_DYNAMIC" : "UNKNOWN_READ_DYNAMIC";

                logger.SafeInsertError(ctx, level, summary, exRead);
                Console.WriteLine(summary);

                conn.MarkDisconnected(ctx);
                break;
            }

            try
            {
                ctx.DynRepo.Insert(d);
            }
            catch (Exception exDb)
            {
                string summary = $"[동적/DB 오류] stack={stackNo} {exDb.Message}";
                string level = (exDb is MySqlException) ? "DB_INSERT_DYNAMIC" : "UNKNOWN_DB_DYNAMIC";

                logger.SafeInsertError(ctx, level, summary, exDb);
                Console.WriteLine(summary);
                continue;
            }
        }
    }
}
