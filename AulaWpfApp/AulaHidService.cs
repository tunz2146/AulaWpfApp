using System;
using System.Collections.Generic;
using HidSharp;

namespace AulaWpfApp
{
    public class AulaHidService
    {
        private const int AulaVendorId = 0x2E3C;
        private const int AulaProductId = 0xC365;
        private const uint RgbCollectionUsage = 0xFF1B0091; // Usage của collection vendor-defined nhận lệnh RGB

        private HidDevice? _rgbDevice;
        private HidStream? _rgbStream;

        public bool IsConnected => _rgbDevice != null && _rgbStream != null;

        public string? ConnectedDevicePath => _rgbDevice?.DevicePath;

        /// <summary>
        /// Bảng ánh xạ tên phím -> key_index nội bộ của bàn phím (giải mã từ Wireshark).
        /// </summary>
        public static readonly Dictionary<string, int> KeyIndexMap = new()
        {
            {"Esc",23},{"1",24},{"2",25},{"3",26},{"4",27},{"5",28},{"6",29},{"7",30},{"8",31},{"9",32},{"0",33},{"-",34},{"=",35},{"Backspace",38},
            {"Tab",46},{"Q",47},{"W",48},{"E",49},{"R",50},{"T",51},{"Y",52},{"U",53},{"I",54},{"O",55},{"P",58},{"[",59},{"]",60},{"\\",62},
            {"Caps",70},{"A",72},{"S",73},{"D",74},{"F",75},{"G",77},{"H",78},{"J",79},{"K",80},{"L",81},{";",82},{"'",83},{"Enter",85},
            {"L-Shift",93},{"Z",96},{"X",97},{"C",98},{"V",99},{"B",100},{"N",101},{"M",102},{",",103},{".",104},{"/",105},{"R-Shift",106},
            {"L-Ctrl",118},{"L-Win",119},{"L-Alt",120},{"Space",124},{"R-Alt",127},{"Menu",128},{"R-Ctrl",129},{"Fn",130}
        };

        private const int PerKeyPayloadLength = 406; // 7 packets x 58 bytes
        private const byte PerKeySubCmd = 0x36;

        /// <summary>
        /// Quét và kết nối tới collection HID vendor-defined dùng cho RGB/Rapid Trigger/SOCD.
        /// Trả về true nếu tìm và kết nối thành công.
        /// </summary>
        public bool Connect()
        {
            Disconnect(); // đảm bảo đóng kết nối cũ trước khi mở mới

            var devices = DeviceList.Local.GetHidDevices(AulaVendorId, AulaProductId);

            foreach (var device in devices)
            {
                if (TryMatchRgbUsage(device))
                {
                    try
                    {
                        _rgbStream = device.Open();
                        _rgbDevice = device;
                        return true;
                    }
                    catch
                    {
                        // Không mở được (có thể do thiết bị đang bị chiếm bởi app khác), thử device tiếp theo
                    }
                }
            }

            return false;
        }

        private bool TryMatchRgbUsage(HidDevice device)
        {
            try
            {
                var reportDescriptor = device.GetReportDescriptor();
                foreach (var deviceItem in reportDescriptor.DeviceItems)
                {
                    foreach (var usage in deviceItem.Usages.GetAllValues())
                    {
                        if (usage == RgbCollectionUsage)
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // Bỏ qua device đọc report descriptor lỗi
            }

            return false;
        }

        public void Disconnect()
        {
            _rgbStream?.Dispose();
            _rgbStream = null;
            _rgbDevice = null;
        }

        /// <summary>
        /// Gửi 1 report 64-byte tới collection RGB. Trả về true nếu gửi thành công.
        /// </summary>
        public bool SendRgbReport(byte[] report)
        {
            if (_rgbStream == null)
            {
                return false;
            }

            try
            {
                _rgbStream.Write(report);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ===================== MÀU TOÀN BÀN PHÍM (1 màu chung) =====================

        /// <summary>
        /// Helper tạo report đổi màu theo giao thức đã giải mã từ Wireshark:
        /// 01 07 00 00 00 0e [colorIndex] 04 00 ff ...
        /// </summary>
        public static byte[] BuildColorReport(byte colorIndex)
        {
            byte[] report = new byte[64];
            report[0] = 0x01;
            report[1] = 0x07;
            report[5] = 0x0e;
            report[6] = colorIndex;
            report[7] = 0x04;
            report[9] = 0xff;
            return report;
        }

        /// <summary>
        /// Report để bật chế độ "custom color" (toàn bàn phím 1 màu) — chỉ cần gửi 1 lần trước khi bắt đầu stream màu.
        /// </summary>
        public static byte[] BuildEnableCustomColorReport()
        {
            byte[] report = new byte[64];
            report[0] = 0x01;
            report[1] = 0x17;
            report[5] = 0x02;
            return report;
        }

        /// <summary>
        /// Report set màu RGB tùy ý cho toàn bàn phím (0-255 mỗi kênh). Dùng sau khi đã gọi BuildEnableCustomColorReport() một lần.
        /// </summary>
        public static byte[] BuildCustomColorReport(byte r, byte g, byte b)
        {
            byte[] report = new byte[64];
            report[0] = 0x01;
            report[1] = 0x07;
            report[5] = 0x0e;
            report[6] = 0x01;
            report[7] = 0x04;
            report[9] = r;
            report[10] = g;
            report[11] = b;
            return report;
        }

        // ===================== MÀU RIÊNG TỪNG PHÍM (PER-KEY) =====================

        /// <summary>
        /// Xây dựng 7 report cần gửi để set màu riêng cho nhiều phím cùng lúc.
        /// keyColors: map key_index -> (R,G,B). Phím không có trong map sẽ tắt (0,0,0).
        /// </summary>
public static List<byte[]> BuildPerKeyColorReports(Dictionary<int, (byte R, byte G, byte B)> keyColors)
{
    byte[] payload = new byte[PerKeyPayloadLength]; // 406 byte = 7 x 58

    foreach (var kv in keyColors)
    {
        int offset = kv.Key * 3;
        if (offset + 2 < payload.Length)
        {
            payload[offset] = kv.Value.R;
            payload[offset + 1] = kv.Value.G;
            payload[offset + 2] = kv.Value.B;
        }
    }

    var reports = new List<byte[]>();

    // 7 gói dữ liệu: seq 0-6, sub-cmd 0x36
    for (byte seq = 0; seq <= 6; seq++)
    {
        byte[] report = new byte[64];
        report[0] = 0x01;
        report[1] = 0x09;
        report[4] = seq;
        report[5] = PerKeySubCmd;

        int srcOffset = seq * 58;
        Array.Copy(payload, srcOffset, report, 6, 58);

        reports.Add(report);
    }

    // Gói terminator riêng: seq = 7, sub-cmd 0x12, toàn 0
    byte[] terminator = new byte[64];
    terminator[0] = 0x01;
    terminator[1] = 0x09;
    terminator[4] = 0x07;
    terminator[5] = 0x12;
    reports.Add(terminator);

    return reports;
}

        /// <summary>
        /// Report "apply" để kích hoạt chế độ custom per-key sau khi đã gửi đủ 7 report dữ liệu ở trên.
        /// </summary>
        public static byte[] BuildApplyPerKeyReport()
{
    byte[] report = new byte[64];
    report[0] = 0x01;
    report[1] = 0x07;
    report[5] = 0x0e;
    report[6] = 0x0a; // mode = custom per-key
    report[7] = 0x04;
    report[8] = 0x03;
    report[9] = 0xff;
    report[16] = 0x01;
    return report;
}

        /// <summary>
        /// Gửi toàn bộ chuỗi 8 report (7 report dữ liệu + 1 report apply) để set màu riêng cho các phím chỉ định.
        /// Trả về true nếu gửi thành công tất cả.
        /// </summary>
        public bool SendPerKeyColors(Dictionary<int, (byte R, byte G, byte B)> keyColors)
        {
            var reports = BuildPerKeyColorReports(keyColors);
            foreach (var report in reports)
            {
                if (!SendRgbReport(report))
                {
                    return false;
                }
            }
            return SendRgbReport(BuildApplyPerKeyReport());
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}