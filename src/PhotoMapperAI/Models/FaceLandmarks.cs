namespace PhotoMapperAI.Models;

/// <summary>
/// Represents detected face and eye landmarks for portrait cropping.
/// </summary>
public class FaceLandmarks
{
    /// <summary>
    /// Was a face detected?
    /// </summary>
    public bool FaceDetected { get; set; }

    /// <summary>
    /// Were both eyes detected?
    /// </summary>
    public bool BothEyesDetected { get; set; }

    /// <summary>
    /// Face rectangle (x, y, width, height)
    /// </summary>
    public Rectangle? FaceRect { get; set; }

    /// <summary>
    /// Left eye position (x, y)
    /// </summary>
    public Point? LeftEye { get; set; }

    /// <summary>
    /// Right eye position (x, y)
    /// </summary>
    public Point? RightEye { get; set; }

    /// <summary>
    /// Face center point (x, y)
    /// </summary>
    public Point? FaceCenter { get; set; }

    /// <summary>
    /// Model/algorithm used for detection
    /// </summary>
    public string ModelUsed { get; set; } = string.Empty;

    /// <summary>
    /// Confidence score of face detection (0.0 to 1.0)
    /// </summary>
    public double FaceConfidence { get; set; }

    /// <summary>
    /// Time taken for detection (milliseconds)
    /// </summary>
    public long ProcessingTimeMs { get; set; }

    /// <summary>
    /// Calculate eye midpoint (used for portrait centering)
    /// Null if both eyes not detected
    /// </summary>
    public Point? EyeMidpoint
    {
        get
        {
            if (LeftEye.HasValue && RightEye.HasValue)
            {
                return new Point(
                    (LeftEye.Value.X + RightEye.Value.X) / 2,
                    (LeftEye.Value.Y + RightEye.Value.Y) / 2
                );
            }
            return null;
        }
    }
}

/// <summary>
/// Represents a 2D point (x, y coordinates).
/// </summary>
public record Point(int X, int Y);

/// <summary>
/// Represents a rectangle (x, y, width, height).
/// </summary>
public record Rectangle(int X, int Y, int Width, int Height);
