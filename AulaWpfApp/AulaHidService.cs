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
/// Report để bật chế độ "custom color" — chỉ cần gửi 1 lần trước khi bắt đầu stream màu.
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
/// Report set màu RGB tùy ý (0-255 mỗi kênh). Dùng sau khi đã gọi BuildEnableCustomColorReport() một lần.
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
    }
}