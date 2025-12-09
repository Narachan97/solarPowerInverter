using System;
using System.Threading;

internal class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== Solar Collector 시작 ===");

        // 인버터 정보
        string inverterIp = "192.168.0.109";   // 실제 인버터 주소
        int port = 502;

        // DB 연결 정보
        string connStr =
            "server=158.247.214.46;port=3306;Database=dsSolar;Uid=_hoo;Pwd=Dsplm7433mysql##;";

        // 정적 DeviceId (시리얼 or TEST_IP_…)
        string deviceId = "";

        // -------------------------------------------------------
        // 1) 정적 데이터 수집 루프
        //    → 인버터/통신/DB 중 하나라도 실패하면
        //      60초 후 다시 시도, 성공할 때까지 반복
        // -------------------------------------------------------
        Console.WriteLine("=== 정적 데이터 수집 시작 ===");

        while (true)
        {
            try
            {
                Console.WriteLine("[정적] 인버터 연결 및 데이터 읽기 시도...");

                var staticReader = new StaticDataReader(inverterIp, port);
                StaticInfo s = staticReader.Read();   // 여기서 연결 실패 시 예외 발생

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
                Console.WriteLine($"[정적/연결 오류] {ex.Message}");
                Console.WriteLine("[정적] 60초 후 다시 시도합니다...");
                Thread.Sleep(60000);  // 1분 후 재시도
            }
        }

        // -------------------------------------------------------
        // 2) 동적 데이터 수집 루프 (60초 간격)
        //    → 연결 실패/통신 실패/DB 오류 모두 잡고
        //      60초 후 계속 재시도
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
                    DynamicData d = dynReader.ReadForStack(deviceId, stackNo);
                    dynRepo.Insert(d);
                }

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 동적 데이터 저장 완료");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[동적/연결 오류] {ex.Message}");
                Console.WriteLine("[동적] 다음 회차에서 다시 시도합니다.");
            }

            Thread.Sleep(60000); // ★ 성공/실패 상관없이 60초마다 반복
        }
    }
}
