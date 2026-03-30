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
        Console.WriteLine("=== Solar Collector 시작 (Stateless 모드) ===");

        // 설정
        string inverterIp = "192.168.21.10";
        int port = 502;
        string connStr = "server=158.247.214.46;port=3306;Database=dsSolar;Uid=_hoo;Pwd=Dsplm7433mysql##;";

        var ctx = new CollectorContext(inverterIp, port, connStr);
        var logger = new ErrorGateLogger();
        var stat = new StaticCollector();
        var dyn = new DynamicCollector();

        Console.WriteLine();
        Console.WriteLine("=== 데이터 수집 시작 (60초 간격, 매회 연결/해제 반복) ===");

        const int delayMs = 60000;

        while (true)
        {
            DateTime cycleStart = DateTime.Now;
            ModbusClient cycleClient = null;

            try
            {
                // 1. 매 사이클마다 새로운 소켓 생성 및 타임아웃 5초 설정
                cycleClient = new ModbusClient(ctx.InverterIp, ctx.Port);
                cycleClient.ConnectionTimeout = 5000;

                // 연결 시도
                cycleClient.Connect();

                // Reader 객체 초기화
                ctx.StaticReader = new StaticDataReader(cycleClient);
                ctx.DynReader = new DynamicDataReader(cycleClient);

                bool canProceedDynamic = true;

                // 2. 정적 데이터 수집 (최초 1회 또는 연결 갱신 시)
                if (ctx.StaticNeedsRefresh)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [정적] 데이터 갱신 시도...");
                    if (!stat.TryCollectAndSave(ctx, logger))
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [정적] 갱신 실패 → 동적 수집 스킵");
                        canProceedDynamic = false;
                    }
                }

                // 3. 동적 데이터 수집
                if (canProceedDynamic)
                {
                    if (string.IsNullOrWhiteSpace(ctx.DeviceId))
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [동적] deviceId가 없어 수집 스킵");
                    }
                    else
                    {
                        dyn.CollectAndSaveAllStacks(ctx, logger);
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 동적 저장 완료 (deviceId={ctx.DeviceId})");
                    }
                }
            }
            catch (Exception exOuter)
            {
                string summary = $"[통신/사이클 오류] {exOuter.Message}";
                string level = (exOuter is ConnectionException || exOuter is System.Net.Sockets.SocketException) ? "MODBUS_CONNECT" : "UNKNOWN_CYCLE";

                logger.SafeInsertError(ctx, level, summary, exOuter);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {summary}");

                // 통신 실패 시 다음 사이클에 정적 데이터부터 다시 읽도록 플래그 설정
                ctx.StaticNeedsRefresh = true;
            }
            finally
            {
                // 4. [가장 중요] 사이클 종료 시 무조건 소켓 연결 해제 (메모리 누수 원천 차단)
                if (cycleClient != null)
                {
                    try
                    {
                        cycleClient.Disconnect();
                    }
                    catch { /* Disconnect 에러 무시 */ }

                    cycleClient = null;
                }

                // 참조 해제하여 가비지 컬렉터가 확실히 정리하도록 유도
                ctx.StaticReader = null;
                ctx.DynReader = null;
            }

            // 5. 남은 시간 계산 후 대기 (수행 시간을 빼서 정확히 60초 주기 맞춤)
            int elapsedMs = (int)(DateTime.Now - cycleStart).TotalMilliseconds;
            int sleepTime = Math.Max(0, delayMs - elapsedMs);
            Thread.Sleep(sleepTime);
        }
    }
}

// =====================
// 데이터 상태 및 컨텍스트
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

    public Dictionary<string, ErrGate> ErrState { get; } = new();

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

// =====================
// 에러 로깅 처리부 (기존과 동일)
// =====================
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
            gate.LastMessage = message; gate.LastLogUtc = now; gate.RepeatCount = 0;
            ctx.ErrState[key] = gate; return true;
        }
        gate.RepeatCount++;
        if ((now - gate.LastLogUtc).TotalSeconds >= 300)
        {
            gate.LastLogUtc = now; ctx.ErrState[key] = gate; return true;
        }
        if (gate.RepeatCount % 5 == 0)
        {
            gate.LastLogUtc = now; ctx.ErrState[key] = gate; return true;
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
        catch (Exception logEx) { Console.WriteLine($"[DB로깅 실패] {logEx.Message}"); }
    }
}

// =====================
// 정적 데이터 수집부
// =====================
public class StaticCollector
{
    public bool TryCollectAndSave(CollectorContext ctx, ErrorGateLogger logger)
    {
        try
        {
            var s = ctx.StaticReader.Read();

            if (string.IsNullOrWhiteSpace(s.SerialNumber))
                ctx.DeviceId = "TEST_" + ctx.InverterIp.Replace(".", "_");
            else
                ctx.DeviceId = s.SerialNumber.Trim();

            s.DeviceId = ctx.DeviceId;
            ctx.StaticRepo.Save(s);

            ctx.StaticNeedsRefresh = false;
            return true;
        }
        catch (Exception ex)
        {
            string summary = $"[정적/오류] {ex.Message}";
            string level = (ex is ConnectionException || ex is System.IO.IOException) ? "MODBUS_READ_STATIC" : "DB_STATIC";

            logger.SafeInsertError(ctx, level, summary, ex);
            Console.WriteLine(summary);

            if (!(ex is MySqlException)) ctx.StaticNeedsRefresh = true;
            return false;
        }
    }
}

// =====================
// 동적 데이터 수집부
// =====================
public class DynamicCollector
{
    public void CollectAndSaveAllStacks(CollectorContext ctx, ErrorGateLogger logger)
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
                logger.SafeInsertError(ctx, "MODBUS_READ_DYNAMIC", summary, exRead);
                Console.WriteLine(summary);
                // 즉시 읽기 중단 (finally 블록에서 통신은 끊어짐)
                break;
            }

            try
            {
                ctx.DynRepo.Insert(d);
            }
            catch (Exception exDb)
            {
                string summary = $"[동적/DB 오류] stack={stackNo} {exDb.Message}";
                logger.SafeInsertError(ctx, "DB_INSERT_DYNAMIC", summary, exDb);
                Console.WriteLine(summary);
            }
        }
    }
}