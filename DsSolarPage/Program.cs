using System;
using System.Collections.Generic;
using System.Threading;
using EasyModbus.Exceptions;              // ✅ (3) Modbus 예외 분류용
using MySql.Data.MySqlClient;             // ✅ (3) DB 예외 분류용

internal class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== Solar Collector 시작 ===");

        // 인버터 정보
        string inverterIp = "192.168.21.10";   // 실제 인버터 주소
        int port = 502;

        //string inverterIp = "COM3";  // ★ 실제 PC에 잡힌 COM 포트
        //int port = 0;

        // DB 연결 정보
        string connStr =
            "server=158.247.214.46;port=3306;Database=dsSolar;Uid=_hoo;Pwd=Dsplm7433mysql##;";

        var errorRepo = new ErrorRepository(connStr);

        // 정적 DeviceId (시리얼 or TEST_IP_…)
        string deviceId = "";

        // ✅ (1) 에러 폭주 방지용 상태(메모리)
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

            // 메시지가 바뀌면 바로 기록
            if (!string.Equals(gate.LastMessage, message, StringComparison.Ordinal))
            {
                gate.LastMessage = message;
                gate.LastLogUtc = now;
                gate.RepeatCount = 0;
                errState[key] = gate;
                return true;
            }

            // 같은 메시지 반복이면 카운트 증가
            gate.RepeatCount++;

            // 5분에 1번은 무조건 기록(장시간 문제 추적용)
            if ((now - gate.LastLogUtc).TotalSeconds >= 300)
            {
                gate.LastLogUtc = now;
                errState[key] = gate;
                return true;
            }

            // 같은 에러가 너무 자주면 5번마다 한 번만 기록(폭주 방지)
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
                // ✅ (1) 폭주 방지: level+summary로 게이트
                string gateKey = $"{level}";
                if (!ShouldLog(gateKey, summary))
                    return;

                errorRepo.Insert(level, devId, summary, ex);
            }
            catch (Exception logEx)
            {
                // ✅ (2) errorRepo.Insert 실패도 콘솔에 남김(조용히 삼키지 않음)
                Console.WriteLine($"[에러로그 DB 저장 실패] {logEx.Message}");
            }
        }

        string ClassifyLevel(string baseStage, Exception ex)
        {
            // ✅ (3) 에러 분류
            // Modbus 연결 계열
            if (ex is ConnectionException)
                return $"MODBUS_CONNECT_{baseStage}";

            // DB 계열
            if (ex is MySqlException)
                return $"DB_{baseStage}";

            // 나머지
            return $"UNKNOWN_{baseStage}";
        }

        // -------------------------------------------------------
        // 1) 정적 데이터 수집 루프
        // -------------------------------------------------------
        Console.WriteLine("=== 정적 데이터 수집 시작 ===");

        while (true)
        {
            try
            {
                Console.WriteLine("[정적] 인버터 연결 및 데이터 읽기 시도...");

                var staticReader = new StaticDataReader(inverterIp, port);
                StaticInfo s = staticReader.Read();   // 여기서 연결/읽기 실패 시 예외 발생

                if (string.IsNullOrWhiteSpace(s.SerialNumber))
                {
                    // 실제 인버터 없거나 시리얼 못 읽은 테스트 상황용
                    deviceId = "TEST_" + inverterIp.Replace(".", "_");
                    Console.WriteLine($"[정적] 시리얼 없음 → 테스트용 deviceId = {deviceId}");
                }
                else
                {
                    deviceId = s.SerialNumber.Trim();
                    Console.WriteLine($"[정적] 시리얼 읽기 성공 → deviceId = {deviceId}");
                }

                s.DeviceId = deviceId;

                var staticRepo = new StaticRepository(connStr);
                staticRepo.Save(s);

                Console.WriteLine("[정적] DB 저장 완료");
                Console.WriteLine("=== 정적 데이터 수집 완료, 동적 루프로 이동 ===");
                break;  // ★ 성공했으니 정적 루프 탈출
            }
            catch (Exception ex)
            {
                string summary = $"[정적/오류] {ex.Message}";
                string level = ClassifyLevel("STATIC", ex);

                SafeInsertError(level, deviceId, summary, ex);

                Console.WriteLine($"[정적/연결 오류] {ex.Message}");
                Console.WriteLine("[정적] 60초 후 다시 시도합니다...");
                Thread.Sleep(60000);  // 1분 후 재시도
            }
        }

        // -------------------------------------------------------
        // 2) 동적 데이터 수집 루프 (60초 간격)
        // -------------------------------------------------------
        Console.WriteLine();
        Console.WriteLine("=== 동적 데이터 수집 시작 (60초 간격) ===");

        var dynRepo = new DynamicRepository(connStr);

        while (true)
        {
            try
            {
                // ★ 매 회차마다 연결 새로 시도
                var dynReader = new DynamicDataReader(inverterIp, port);

                for (int stackNo = 0; stackNo <= 4; stackNo++) // Main + Stack1~4
                {
                    DynamicData d;

                    // ✅ (3) Read(통신/레지스터) 단계 오류 분리
                    try
                    {
                        d = dynReader.ReadForStack(deviceId, stackNo);
                    }
                    catch (Exception exRead)
                    {
                        string summary = $"[동적/READ 오류] stack={stackNo} {exRead.Message}";
                        string level = (exRead is ConnectionException)
                            ? "MODBUS_READ_DYNAMIC"
                            : "UNKNOWN_READ_DYNAMIC";

                        SafeInsertError(level, deviceId, summary, exRead);
                        continue; // 다음 stack으로
                    }

                    // ✅ (3) DB Insert 단계 오류 분리
                    try
                    {
                        dynRepo.Insert(d);
                    }
                    catch (Exception exDb)
                    {
                        string summary = $"[동적/DB 오류] stack={stackNo} {exDb.Message}";
                        string level = (exDb is MySqlException)
                            ? "DB_INSERT_DYNAMIC"
                            : "UNKNOWN_DB_DYNAMIC";

                        SafeInsertError(level, deviceId, summary, exDb);
                        continue; // 다음 stack으로
                    }
                }

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 동적 데이터 저장 완료");
            }
            catch (Exception ex)
            {
                // dynReader 생성/연결 단계 등 “바깥” 오류
                string summary = $"[동적/연결 오류] {ex.Message}";
                string level = ClassifyLevel("DYNAMIC", ex);

                SafeInsertError(level, deviceId, summary, ex);

                Console.WriteLine($"[동적/연결 오류] {ex.Message}");
                Console.WriteLine("[동적] 다음 회차에서 다시 시도합니다.");
            }

            Thread.Sleep(60000); // ★ 성공/실패 상관없이 60초마다 반복
        }
    }

    // ✅ (1) 폭주 방지 상태 보관용
    private struct ErrGate
    {
        public string LastMessage;
        public DateTime LastLogUtc;
        public int RepeatCount;
    }
}
