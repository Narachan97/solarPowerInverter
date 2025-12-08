using System;
using System.IO;
using System.Threading;

internal class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== Solar Collector 시작 ===");

        // 인버터 정보
        string inverterIp = "192.168.21.10";   // 실제 인버터 주소
        int port = 502;

        // DB 연결 정보
        string connStr =
            "server=158.247.214.46;port=3306;Database=dsSolar;Uid=_hoo;Pwd=Dsplm7433mysql##;";

        // --------------------------------------------
        // 1) 정적 데이터 읽기 (프로그램 시작 시 1번만)
        // --------------------------------------------

        string deviceId = "";  // 그대로 둠

        try
        {
            Console.WriteLine("[정적 데이터] 인버터 연결 중...");

            var staticReader = new StaticDataReader(inverterIp, port);
            StaticInfo s = staticReader.Read();

            if (string.IsNullOrWhiteSpace(s.SerialNumber))
            {
                // 실제 인버터 없어서 시리얼 못 읽는 테스트 상황용
                deviceId = "TEST_" + inverterIp.Replace(".", "_");
                Console.WriteLine($"[경고] 시리얼 번호를 읽을 수 없습니다. 테스트용 deviceId 사용 → {deviceId}");
            }
            else
            {
                deviceId = s.SerialNumber.Trim();
                Console.WriteLine($"[정적 데이터] 읽기 성공 → deviceId = {deviceId}");
            }

            s.DeviceId = deviceId;
            var staticRepo = new StaticRepository(connStr);
            staticRepo.Save(s);

            Console.WriteLine("[정적 데이터] DB 저장 완료");
        }
        catch (Exception ex)
        {
            Console.WriteLine("정적 데이터 수집 오류:");
            Console.WriteLine(ex.Message);
            // 여기서는 굳이 종료 안 해도 됨 → 원하면 return 유지
            // return;
        }

        // --------------------------------------------
        // 2) 동적 데이터 무한 루프
        // --------------------------------------------

        Console.WriteLine();
        Console.WriteLine("=== 동적 데이터 수집 시작 (60초 간격) ===");

        var dynReader = new DynamicDataReader(inverterIp, port);
        var dynRepo = new DynamicRepository(connStr);

        while (true)
        {
            try
            {
                for (int stackNo = 0; stackNo <= 4; stackNo++) // Main + Stack1~4
                {
                    DynamicData d = dynReader.ReadForStack(deviceId, stackNo);
                    dynRepo.Insert(d);
                }

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 동적 데이터 저장 완료");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[동적 에러] " + ex.Message);
            }

            Thread.Sleep(60000); // 60초 간격
        }
    }
}
