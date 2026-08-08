using OpenCvSharp;

namespace LaptopGuard;

/// <summary>
/// Grabs a single still frame from the default webcam. Opens the device,
/// takes one frame, closes it immediately — the camera is not held open
/// continuously, so there's no always-on recording and no visible "camera
/// in use" indicator lingering between captures.
/// </summary>
public sealed class WebcamCapture
{
    private readonly EventLogger _logger;
    private readonly object _camLock = new();

    public WebcamCapture(EventLogger logger)
    {
        _logger = logger;
    }

    public string? CaptureStill(string reason)
    {
        lock (_camLock)
        {
            try
            {
                using var capture = new VideoCapture(0); // default camera
                if (!capture.IsOpened())
                {
                    _logger.Log("webcam_error", new { reason, error = "no camera or already in use" });
                    return null;
                }

                using var frame = new Mat();

                // Discard the first couple of frames — most webcams need a
                // few frames to auto-adjust exposure/white balance, so frame
                // zero is often dark/washed out.
                for (int i = 0; i < 3; i++)
                {
                    capture.Read(frame);
                }

                if (frame.Empty())
                {
                    _logger.Log("webcam_error", new { reason, error = "empty frame" });
                    return null;
                }

                string filename = $"{DateTime.Now:yyyyMMdd-HHmmss-fff}_{reason}.jpg";
                string path = Path.Combine(EventLogger.PhotosDir, filename);
                frame.SaveImage(path);

                _logger.Log("photo_captured", new { reason, file = filename });
                return path;
            }
            catch (Exception ex)
            {
                _logger.Log("webcam_error", new { reason, error = ex.Message });
                return null;
            }
        }
    }
}
