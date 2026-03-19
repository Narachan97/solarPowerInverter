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

        // 설정 (현장 환경에 맞게 유지)
        string inverterIp = "192.168.21.10";
        int port = 502;
        string connStr = "server=158.247.214.46;port=3306;Database=dsSolar;Uid=_hoo;Pwd=Dsplm7433mysql##;";

        // 상태/의존성 묶음
        var ctx = new CollectorContext(inverterIp, port, connStr);

        // 서비스(클래스)들
        var logger = new ErrorGateLogger();
        var conn = new ModbusConnectionManager();
        var stat = new StaticCollector();
        var dyn = new DynamicCollector();

        Console.WriteLine();
        Console.WriteLine("=== 데이터 수집 시작 (60초 간격, Modbus 연결 1개 공유) ===");

        // 수집 주기 (밀리초)
        const int delayMs = 60000;

        while (true)
        {
            try
            {
                // 1. Modbus 연결 확인 및 연결
                if (!conn.EnsureConnected(ctx, logger))
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [통신] 연결 실패 → 이번 회차 스킵 (다음 회차 재시도)");
                }
                else
                {
                    bool canProceedDynamic = true;

                    // 2. 정적 데이터(시리얼 등) 수집 필요 시 실행
                    if (ctx.StaticNeedsRefresh)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [정적] 저장 조건 충족 → 정적 데이터 갱신 시도");
                        if (!stat.TryCollectAndSave(ctx, logger, conn))
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [정적] 갱신 실패 → 동적 수집 스킵");
                            canProceedDynamic = false;
                        }
                    }

                    // 3. 동적 데이터(발전량 등) 수집
                    if (canProceedDynamic)
                    {
                        if (string.IsNullOrWhiteSpace(ctx.DeviceId))
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [동적] deviceId가 비어있어 동적 수집 스킵");
                        }
                        else
                        {
                            dyn.CollectAndSaveAllStacks(ctx, logger, conn);
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 동적 저장 회차 종료 (deviceId={ctx.DeviceId})");
                        }
                    }
                }
            }
            catch (Exception exOuter)
            {
                // 예기치 못한 최상위 예외 처리
                string summary = $"[회차/외부 오류] {exOuter.Message}";
                logger.SafeInsertError(ctx, "UNKNOWN_CYCLE", summary, exOuter);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {summary}");

                // 심각한 오류이므로 안전을 위해 Modbus 소켓 초기화
                conn.MarkDisconnected(ctx);
            }

            // 4. 회차 종료 후 대기 (루프 끝에서 단 한 번만 호출되도록 보장)
            Thread.Sleep(delayMs);
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

    // 기존에 작성하신 Repository 인스턴스들
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

// =====================
// 에러 로깅 처리부
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

// =====================
// Modbus 통신 관리부 (핵심 수정 영역)
// =====================
public class ModbusConnectionManager
{
    public void ResetSharedConnection(CollectorContext ctx)
    {
        try
        {
            // [중요] Connected 상태와 무관하게 객체가 있으면 강제로 Disconnect를 시도합니다.
            // 인버터 통신보드의 TCP 소켓 누수를 막기 위한 가장 핵심적인 조치입니다.
            if (ctx.SharedClient != null)
            {
                ctx.SharedClient.Disconnect();
            }
        }
        catch
        {
            // Disconnect 중 발생하는 에러는 이미 소켓이 끊어졌거나 파괴된 상태이므로 무시합니다.
        }
        finally
        {
            // 메모리 참조 해제
            ctx.SharedClient = null;
            ctx.StaticReader = null;
            ctx.DynReader = null;
        }
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
            {
                ctx.SharedClient = new ModbusClient(ctx.InverterIp, ctx.Port);
                // [중요] 응답 없는 무한 대기 방지를 위해 타임아웃을 설정합니다. (단위: 밀리초)
                ctx.SharedClient.ConnectionTimeout = 5000;
            }

            if (!ctx.SharedClient.Connected)
            {
                ctx.SharedClient.Connect();
            }

            if (!ctx.LastConnOk)
            {
                ctx.StaticNeedsRefresh = true;
            }

            ctx.LastConnOk = true;

            // Reader 객체 초기화
            if (ctx.StaticReader == null) ctx.StaticReader = new StaticDataReader(ctx.SharedClient);
            if (ctx.DynReader == null) ctx.DynReader = new DynamicDataReader(ctx.SharedClient);

            return true;
        }
        catch (Exception ex)
        {
            string summary = $"[CONNECT 오류] {ex.Message}";
            string level = (ex is ConnectionException || ex is System.Net.Sockets.SocketException) ? "MODBUS_CONNECT" : "UNKNOWN_CONNECT";

            logger.SafeInsertError(ctx, level, summary, ex);
            Console.WriteLine(summary);

            // 통신 연결 실패 시 확실하게 소켓 및 객체 초기화
            MarkDisconnected(ctx);
            return false;
        }
    }
}

// =====================
// 정적 데이터 수집부
// =====================
public class StaticCollector
{
    public bool TryCollectAndSave(CollectorContext ctx, ErrorGateLogger logger, ModbusConnectionManager conn)
    {
        try
        {
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
            string level = (ex is ConnectionException || ex is System.IO.IOException) ? "MODBUS_READ_STATIC"
                        : (ex is MySqlException) ? "DB_STATIC"
                        : "UNKNOWN_STATIC";

            logger.SafeInsertError(ctx, level, summary, ex);
            Console.WriteLine(summary);

            // [중요] DB 저장 실패(MySqlException)는 인버터 통신 단절 사유가 아니므로 Modbus 연결을 유지합니다.
            // 인버터 통신 관련 에러일 때만 소켓을 초기화합니다.
            if (!(ex is MySqlException))
            {
                conn.MarkDisconnected(ctx);
            }

            return false;
        }
    }
}

// =====================
// 동적 데이터 수집부
// =====================
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
                string level = (exRead is ConnectionException || exRead is System.IO.IOException) ? "MODBUS_READ_DYNAMIC" : "UNKNOWN_READ_DYNAMIC";

                logger.SafeInsertError(ctx, level, summary, exRead);
                Console.WriteLine(summary);

                // 통신 에러 발생 시 즉시 소켓 초기화 후 이번 회차의 나머지 스택 읽기를 중단(break)
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
                // DB 에러 시에는 Modbus를 끊지 않고 다음 스택(continue) 데이터 읽기를 계속 시도합니다.
                continue;
            }
        }
    }
}
