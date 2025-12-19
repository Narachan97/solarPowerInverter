// ================================
// Program.cs (최종 정리본)
// - Modbus TCP 연결 1개(sharedClient)만 사용
// - 정적은 "시작 1회 + 끊김 후 복구 1회" 저장
// - 정적 저장 성공(또는 TEST로라도 저장) 전에는 동적 저장 안 함
// - 끊김 감지는 Connect/Read 예외로만 판단
// ================================

using System;
using System.Collections.Generic;
using System.Threading;
using EasyModbus;
using EasyModbus.Exceptions;
using MySql.Data.MySqlClient;

internal class Program
{
    private struct ErrGate
    {
        public string LastMessage;
        public DateTime LastLogUtc;
        public int RepeatCount;
    }

    static void Main(string[] args)
    {
        Console.WriteLine("=== Solar Collector 시작 ===");

        // 인버터 정보
        string inverterIp = "192.168.21.10";
        int port = 502;

        // DB 연결 정보
        string connStr =
            "server=158.247.214.46;port=3306;Database=dsSolar;Uid=_hoo;Pwd=Dsplm7433mysql##;";

        var errorRepo = new ErrorRepository(connStr);
        var staticRepo = new StaticRepository(connStr);
        var dynRepo = new DynamicRepository(connStr);

        string deviceId = "";

        // 에러 폭주 방지
        var errState = new Dictionary<string, ErrGate>();

        bool ShouldLog(string key, string message)
        {
            var now = DateTime.UtcNow;

            if (!errState.TryGetValue(key, out var gate))
            {
                gate = new ErrGate
                {
                    LastMessage = message,
                    LastLogUtc = DateTime.MinValue,
                    RepeatCount = 0
                };
            }

            if (!string.Equals(gate.LastMessage, message, StringComparison.Ordinal))
            {
                gate.LastMessage = message;
                gate.LastLogUtc = now;
                gate.RepeatCount = 0;
                errState[key] = gate;
                return true;
            }

            gate.RepeatCount++;

            if ((now - gate.LastLogUtc).TotalSeconds >= 300)
            {
                gate.LastLogUtc = now;
                errState[key] = gate;
                return true;
            }

            if (gate.RepeatCount % 5 == 0)
            {
                gate.LastLogUtc = now;
                errState[key] = gate;
                return true;
            }

            errState[key] = gate;
            return false;
        }

        void SafeInsertError(string level, string devId, string summary, Exception ex)
        {
            try
            {
                if (!ShouldLog(level, summary)) return;
                errorRepo.Insert(level, devId, summary, ex);
            }
            catch (Exception logEx)
            {
                Console.WriteLine($"[에러로그 DB 저장 실패] {logEx.Message}");
            }
        }

        // ----------------------------
        // 상태 플래그
        // ----------------------------
        bool staticNeedsRefresh = true;   // 시작 시 정적 저장 필요
        bool lastConnOk = false;          // 직전 회차 연결 성공 여부(복구 감지)

        // ----------------------------
        // Modbus 공용 연결 1개
        // ----------------------------
        ModbusClient sharedClient = null;
        StaticDataReader staticReader = null;
        DynamicDataReader dynReader = null;

        void ResetSharedConnection()
        {
            try
            {
                if (sharedClient != null && sharedClient.Connected)
                    sharedClient.Disconnect();
            }
            catch { }

            sharedClient = null;
            staticReader = null;
            dynReader = null;
        }

        void MarkDisconnected()
        {
            // 끊김 발생/의심 시점: 정적 재저장 예약 + 다음 연결 성공 시 “복구”로 인식
            staticNeedsRefresh = true;
            lastConnOk = false;
            ResetSharedConnection();
        }

        bool EnsureConnected()
        {
            try
            {
                if (sharedClient == null)
                    sharedClient = new ModbusClient(inverterIp, port);

                if (!sharedClient.Connected)
                    sharedClient.Connect();

                // 복구 감지(직전 실패 → 이번 성공)
                if (!lastConnOk)
                    staticNeedsRefresh = true;

                lastConnOk = true;

                if (staticReader == null) staticReader = new StaticDataReader(sharedClient);
                if (dynReader == null) dynReader = new DynamicDataReader(sharedClient);

                return true;
            }
            catch (Exception ex)
            {
                string summary = $"[CONNECT 오류] {ex.Message}";
                string level = (ex is ConnectionException) ? "MODBUS_CONNECT" : "UNKNOWN_CONNECT";

                SafeInsertError(level, deviceId, summary, ex);
                Console.WriteLine(summary);

                MarkDisconnected();
                return false;
            }
        }

        bool TryCollectAndSaveStatic()
        {
            try
            {
                Console.WriteLine("[정적] Read 시도...");

                StaticInfo s = staticReader.Read();

                // 시리얼이 비어도 정적은 반드시 저장(정책)
                if (string.IsNullOrWhiteSpace(s.SerialNumber))
                {
                    deviceId = "TEST_" + inverterIp.Replace(".", "_");
                    Console.WriteLine($"[정적] 시리얼 없음 → TEST deviceId = {deviceId}");
                }
                else
                {
                    deviceId = s.SerialNumber.Trim();
                    Console.WriteLine($"[정적] 시리얼 OK → deviceId = {deviceId}");
                }

                s.DeviceId = deviceId;
                staticRepo.Save(s);

                Console.WriteLine("[정적] DB 저장 완료");
                staticNeedsRefresh = false;
                return true;
            }
            catch (Exception ex)
            {
                string summary = $"[정적/오류] {ex.Message}";
                string level = (ex is ConnectionException) ? "MODBUS_READ_STATIC"
                            : (ex is MySqlException) ? "DB_STATIC"
                            : "UNKNOWN_STATIC";

                SafeInsertError(level, deviceId, summary, ex);
                Console.WriteLine(summary);

                // 정적 실패면 이번 회차 동적도 하지 않음(정적 먼저 정책)
                MarkDisconnected();
                return false;
            }
        }

        Console.WriteLine();
        Console.WriteLine("=== 데이터 수집 시작 (60초 간격, Modbus 연결 1개 공유) ===");

        while (true)
        {
            try
            {
                bool connOk = EnsureConnected();

                if (!connOk)
                {
                    Console.WriteLine("[동적] 연결 실패 → 이번 회차 스킵 (다음 회차 재시도)");
                }
                else
                {
                    // 정적 저장이 필요하면 무조건 정적부터
                    if (staticNeedsRefresh)
                    {
                        Console.WriteLine("[정적] 저장 조건 충족 → 정적 저장 시도");
                        if (!TryCollectAndSaveStatic())
                        {
                            Thread.Sleep(60000);
                            continue;
                        }
                    }

                    // 안전장치
                    if (string.IsNullOrWhiteSpace(deviceId))
                    {
                        Console.WriteLine("[동적] deviceId가 비어있어 동적 수집 스킵");
                    }
                    else
                    {
                        for (int stackNo = 0; stackNo <= 4; stackNo++)
                        {
                            DynamicData d;

                            try
                            {
                                d = dynReader.ReadForStack(deviceId, stackNo);
                            }
                            catch (Exception exRead)
                            {
                                string summary = $"[동적/READ 오류] stack={stackNo} {exRead.Message}";
                                string level = (exRead is ConnectionException) ? "MODBUS_READ_DYNAMIC" : "UNKNOWN_READ_DYNAMIC";

                                SafeInsertError(level, deviceId, summary, exRead);
                                Console.WriteLine(summary);

                                MarkDisconnected();
                                break;
                            }

                            try
                            {
                                dynRepo.Insert(d);
                            }
                            catch (Exception exDb)
                            {
                                string summary = $"[동적/DB 오류] stack={stackNo} {exDb.Message}";
                                string level = (exDb is MySqlException) ? "DB_INSERT_DYNAMIC" : "UNKNOWN_DB_DYNAMIC";

                                SafeInsertError(level, deviceId, summary, exDb);
                                Console.WriteLine(summary);
                                continue;
                            }
                        }

                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 동적 저장 회차 종료 (deviceId={deviceId})");
                    }
                }
            }
            catch (Exception exOuter)
            {
                string summary = $"[회차/외부 오류] {exOuter.Message}";
                SafeInsertError("UNKNOWN_CYCLE", deviceId, summary, exOuter);
                Console.WriteLine(summary);

                MarkDisconnected();
            }

            Thread.Sleep(60000);
        }
    }
}
