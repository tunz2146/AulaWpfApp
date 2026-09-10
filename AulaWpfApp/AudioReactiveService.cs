using System;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace AulaWpfApp
{
    public class AudioReactiveService : IDisposable
    {
        private const float GainFactor = 6.0f; // Hệ số khuếch đại - tăng nếu vẫn thấy nhỏ, giảm nếu bị "kịch kim" liên tục

        private WasapiLoopbackCapture? _capture;

        /// <summary>
        /// Sự kiện bắn ra mỗi khi có 1 mẫu âm thanh mới được xử lý.
        /// Giá trị level nằm trong khoảng 0.0 (im lặng) đến 1.0 (rất to).
        /// </summary>
        public event Action<float>? LevelChanged;

        public bool IsRunning { get; private set; }

        public void Start()
        {
            if (IsRunning)
            {
                return;
            }

            _capture = new WasapiLoopbackCapture();
            _capture.DataAvailable += OnDataAvailable;
            _capture.RecordingStopped += (s, e) => IsRunning = false;

            _capture.StartRecording();
            IsRunning = true;
        }

        public void Stop()
        {
            _capture?.StopRecording();
            IsRunning = false;
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (_capture == null)
            {
                return;
            }

            float level = CalculateRms(e.Buffer, e.BytesRecorded, _capture.WaveFormat);
            LevelChanged?.Invoke(level);
        }

        /// <summary>
        /// Tính RMS (Root Mean Square) của buffer âm thanh, có khuếch đại và làm nhạy đường cong.
        /// Hỗ trợ định dạng IEEE Float (32-bit) - định dạng phổ biến của WASAPI loopback.
        /// </summary>
        private float CalculateRms(byte[] buffer, int bytesRecorded, WaveFormat waveFormat)
        {
            if (waveFormat.BitsPerSample != 32 || waveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
            {
                return 0f;
            }

            int sampleCount = bytesRecorded / 4;
            if (sampleCount == 0)
            {
                return 0f;
            }

            double sumSquares = 0;

            for (int i = 0; i < sampleCount; i++)
            {
                float sample = BitConverter.ToSingle(buffer, i * 4);
                sumSquares += sample * sample;
            }

            double rms = Math.Sqrt(sumSquares / sampleCount);

            double amplified = rms * GainFactor;
            double curved = Math.Sqrt(amplified);

            return (float)Math.Clamp(curved, 0.0, 1.0);
        }

        public void Dispose()
        {
            Stop();
            _capture?.Dispose();
        }
    }
}