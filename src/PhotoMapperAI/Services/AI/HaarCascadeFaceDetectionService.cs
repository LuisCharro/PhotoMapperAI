using OpenCvSharp;
using PhotoMapperAI.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace PhotoMapperAI.Services.AI;

/// <summary>
/// Face detection service using OpenCV Haar Cascade classifiers.
/// Detects both face and eyes for precise portrait centering.
/// </summary>
public class HaarCascadeFaceDetectionService : IFaceDetectionService
{
    private CascadeClassifier? _faceCascade;
    private CascadeClassifier? _eyeCascade;
    private bool _initialized = false;
    private readonly double _faceScaleFactor;
    private readonly int _faceMinNeighbors;
    private readonly double _eyeScaleFactor;
    private readonly int _eyeMinNeighbors;

    /// <summary>
    /// Gets the model name for display/logging.
    /// </summary>
    public string ModelName => "haar-cascade";

    /// <summary>
    /// Creates a new Haar Cascade face detection service.
    /// </summary>
    /// <param name="faceScaleFactor">Scale factor for face detection (default: 1.2)</param>
    /// <param name="faceMinNeighbors">Minimum neighbors for face detection (default: 8)</param>
    /// <param name="eyeScaleFactor">Scale factor for eye detection (default: 1.2)</param>
    /// <param name="eyeMinNeighbors">Minimum neighbors for eye detection (default: 8)</param>
    public HaarCascadeFaceDetectionService(
        double faceScaleFactor = 1.2,
        int faceMinNeighbors = 8,
        double eyeScaleFactor = 1.2,
        int eyeMinNeighbors = 8)
    {
        _faceScaleFactor = faceScaleFactor;
        _faceMinNeighbors = faceMinNeighbors;
        _eyeScaleFactor = eyeScaleFactor;
        _eyeMinNeighbors = eyeMinNeighbors;
    }

    /// <summary>
    /// Initializes the Haar Cascade classifiers.
    /// </summary>
    public async Task<bool> InitializeAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var faceCascadePath = GetResourcePath("haarcascade_frontalface_default.xml");
                var eyeCascadePath = GetResourcePath("haarcascade_eye.xml");

                if (!File.Exists(faceCascadePath))
                {
                    Console.WriteLine($"[HaarCascade] Face cascade file not found: {faceCascadePath}");
                    return false;
                }

                if (!File.Exists(eyeCascadePath))
                {
                    Console.WriteLine($"[HaarCascade] Eye cascade file not found: {eyeCascadePath}");
                    return false;
                }

                _faceCascade = new CascadeClassifier(faceCascadePath);
                _eyeCascade = new CascadeClassifier(eyeCascadePath);

                _initialized = _faceCascade != null && _eyeCascade != null;
                Console.WriteLine($"[HaarCascade] Initialized: {_initialized}");
                return _initialized;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HaarCascade] Error initializing: {ex.Message}");
                _initialized = false;
                return false;
            }
        });
    }

    /// <summary>
    /// Detects face and eye landmarks in a photo.
    /// </summary>
    public async Task<FaceLandmarks> DetectFaceLandmarksAsync(string imagePath)
    {
        return await Task.Run(() =>
        {
            var startTime = DateTime.UtcNow;

            if (!_initialized)
            {
                return new FaceLandmarks
                {
                    FaceDetected = false,
                    ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
                    ModelUsed = ModelName,
                    Metadata = new Dictionary<string, string>
                    {
                        { "error", "Model not initialized" }
                    }
                };
            }

            try
            {
                // Load image
                using var image = Cv2.ImRead(imagePath);
                if (image.Empty())
                {
                    return new FaceLandmarks
                    {
                        FaceDetected = false,
                        ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
                        ModelUsed = ModelName
                    };
                }

                // Convert to grayscale
                using var grayImage = image.CvtColor(ColorConversionCodes.BGR2GRAY);

                // Detect face
                var faceRect = DetectFace(grayImage);

                if (faceRect == null)
                {
                    return new FaceLandmarks
                    {
                        FaceDetected = false,
                        ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
                        ModelUsed = ModelName
                    };
                }

                // Detect eyes within face region
                var faceRegion = grayImage[faceRect.Value];
                var eyes = DetectEyes(faceRegion);

                var landmarks = new FaceLandmarks
                {
                    FaceDetected = true,
                    FaceRect = new PhotoMapperAI.Models.Rectangle(
                        faceRect.Value.X,
                        faceRect.Value.Y,
                        faceRect.Value.Width,
                        faceRect.Value.Height
                    ),
                    FaceCenter = new PhotoMapperAI.Models.Point(
                        faceRect.Value.X + faceRect.Value.Width / 2,
                        faceRect.Value.Y + faceRect.Value.Height / 2
                    ),
                    ModelUsed = ModelName,
                    FaceConfidence = 1.0 // Haar Cascade doesn't provide confidence
                };

                // Process detected eyes
                if (eyes != null && eyes.Length >= 2)
                {
                    // Exactly 2 eyes - perfect
                    if (eyes.Length == 2)
                    {
                        landmarks.LeftEye = new PhotoMapperAI.Models.Point(
                            faceRect.Value.X + eyes[0].X + eyes[0].Width / 2,
                            faceRect.Value.Y + eyes[0].Y + eyes[0].Height / 2
                        );
                        landmarks.RightEye = new PhotoMapperAI.Models.Point(
                            faceRect.Value.X + eyes[1].X + eyes[1].Width / 2,
                            faceRect.Value.Y + eyes[1].Y + eyes[1].Height / 2
                        );
                        landmarks.BothEyesDetected = true;
                    }
                    else
                    {
                        // More than 2 eyes - merge overlapping and take best 2
                        var mergedEyes = MergeOverlappingEyes(eyes);
                        if (mergedEyes.Length >= 2)
                        {
                            // Sort by X position (left to right)
                            var sortedEyes = mergedEyes.OrderBy(e => e.X).ToArray();
                            landmarks.LeftEye = new PhotoMapperAI.Models.Point(
                                faceRect.Value.X + sortedEyes[0].X + sortedEyes[0].Width / 2,
                                faceRect.Value.Y + sortedEyes[0].Y + sortedEyes[0].Height / 2
                            );
                            landmarks.RightEye = new PhotoMapperAI.Models.Point(
                                faceRect.Value.X + sortedEyes[1].X + sortedEyes[1].Width / 2,
                                faceRect.Value.Y + sortedEyes[1].Y + sortedEyes[1].Height / 2
                            );
                            landmarks.BothEyesDetected = true;
                        }
                    }
                }
                else if (eyes != null && eyes.Length == 1)
                {
                    // Only one eye detected
                    landmarks.LeftEye = new PhotoMapperAI.Models.Point(
                        faceRect.Value.X + eyes[0].X + eyes[0].Width / 2,
                        faceRect.Value.Y + eyes[0].Y + eyes[0].Height / 2
                    );
                    landmarks.BothEyesDetected = false;
                }

                landmarks.ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

                Console.WriteLine($"[HaarCascade] Face detected: {faceRect.Value.Width}x{faceRect.Value.Height}, Eyes: {(eyes?.Length ?? 0)}");

                return landmarks;
            }
            catch (Exception ex)
            {
                return new FaceLandmarks
                {
                    FaceDetected = false,
                    ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
                    ModelUsed = ModelName,
                    Metadata = new Dictionary<string, string>
                    {
                        { "error", ex.Message }
                    }
                };
            }
        });
    }

    /// <summary>
    /// Batch detects faces in multiple photos.
    /// </summary>
    public async Task<List<FaceLandmarks>> DetectFaceLandmarksBatchAsync(List<string> imagePaths)
    {
        var results = new List<FaceLandmarks>();
        foreach (var imagePath in imagePaths)
        {
            var result = await DetectFaceLandmarksAsync(imagePath);
            results.Add(result);
        }
        return results;
    }

    /// <summary>
    /// Checks if the service is available.
    /// </summary>
    public async Task<bool> IsAvailableAsync()
    {
        return await Task.Run(() =>
        {
            var faceCascadePath = GetResourcePath("haarcascade_frontalface_default.xml");
            var eyeCascadePath = GetResourcePath("haarcascade_eye.xml");
            return File.Exists(faceCascadePath) && File.Exists(eyeCascadePath);
        });
    }

    #region Private Methods

    /// <summary>
    /// Gets the path to an embedded resource file.
    /// </summary>
    private static string GetResourcePath(string fileName)
    {
        // Try multiple locations
        var locations = new[]
        {
            // Location 1: Resources folder in the executable directory
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", fileName),
            // Location 2: Resources folder relative to the assembly
            Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "Resources", fileName),
            // Location 3: Project root Resources folder (for development)
            Path.Combine(Directory.GetCurrentDirectory(), "src", "PhotoMapperAI", "Resources", fileName),
            // Location 4: Direct Resources folder (for development)
            Path.Combine(Directory.GetCurrentDirectory(), "Resources", fileName),
        };

        foreach (var location in locations)
        {
            if (File.Exists(location))
            {
                return location;
            }
        }

        // Return the first location even if it doesn't exist (for error messages)
        return locations[0];
    }

    /// <summary>
    /// Detects a face in the grayscale image.
    /// </summary>
    private Rect? DetectFace(Mat grayImage)
    {
        if (_faceCascade == null) return null;

        var faces = _faceCascade.DetectMultiScale(
            grayImage,
            scaleFactor: _faceScaleFactor,
            minNeighbors: _faceMinNeighbors,
            flags: HaarDetectionTypes.DoRoughSearch | HaarDetectionTypes.FindBiggestObject | HaarDetectionTypes.ScaleImage,
            minSize: new OpenCvSharp.Size(50, 50)
        );

        if (faces.Length == 0) return null;

        // If multiple faces, return the largest
        if (faces.Length > 1)
        {
            return faces.OrderByDescending(f => f.Width * f.Height).First();
        }

        return faces[0];
    }

    /// <summary>
    /// Detects eyes within a face region.
    /// </summary>
    private Rect[] DetectEyes(Mat faceRegion)
    {
        if (_eyeCascade == null) return Array.Empty<Rect>();

        return _eyeCascade.DetectMultiScale(
            faceRegion,
            scaleFactor: _eyeScaleFactor,
            minNeighbors: _eyeMinNeighbors,
            flags: HaarDetectionTypes.ScaleImage,
            minSize: new OpenCvSharp.Size(20, 20)
        );
    }

    /// <summary>
    /// Merges overlapping eye regions to handle false positives.
    /// </summary>
    private static Rect[] MergeOverlappingEyes(Rect[] eyes)
    {
        var mergedRegions = new List<Rect>();
        
        foreach (var eye in eyes)
        {
            var currentEye = eye;
            var overlapping = mergedRegions.FirstOrDefault(r => r.IntersectsWith(currentEye));
            
            if (overlapping.Width > 0)
            {
                // Merge with existing region
                var merged = Rect.Union(overlapping, currentEye);
                mergedRegions.Remove(overlapping);
                mergedRegions.Add(merged);
            }
            else
            {
                mergedRegions.Add(currentEye);
            }
        }

        // Sort by X position (left to right)
        return mergedRegions.OrderBy(r => r.X).ToArray();
    }

    #endregion
}
