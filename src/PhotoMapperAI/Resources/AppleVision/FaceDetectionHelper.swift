import Foundation
import Vision
import CoreGraphics
import ImageIO

struct OutputPoint: Codable {
    let x: Int
    let y: Int
}

struct OutputRect: Codable {
    let x: Int
    let y: Int
    let width: Int
    let height: Int
}

struct OutputPayload: Codable {
    let faceDetected: Bool
    let bothEyesDetected: Bool
    let faceConfidence: Double
    let imageWidth: Int
    let imageHeight: Int
    let faceRect: OutputRect?
    let leftEye: OutputPoint?
    let rightEye: OutputPoint?
    let faceCenter: OutputPoint?
    let error: String?
}

enum HelperError: Error, LocalizedError {
    case invalidArguments
    case imageLoadFailed
    case detectionFailed(String)

    var errorDescription: String? {
        switch self {
        case .invalidArguments:
            return "Expected a single image path argument."
        case .imageLoadFailed:
            return "Failed to load image."
        case .detectionFailed(let message):
            return message
        }
    }
}

private func clamp(_ value: Int, min minimum: Int, max maximum: Int) -> Int {
    return Swift.max(minimum, Swift.min(maximum, value))
}

private func toPixelPoint(_ point: CGPoint, width: Int, height: Int) -> OutputPoint {
    let x = clamp(Int(round(point.x * Double(width))), min: 0, max: max(width - 1, 0))
    let y = clamp(Int(round((1.0 - point.y) * Double(height))), min: 0, max: max(height - 1, 0))
    return OutputPoint(x: x, y: y)
}

private func averagePoint(_ region: VNFaceLandmarkRegion2D, boundingBox: CGRect) -> CGPoint? {
    let points = region.normalizedPoints
    guard !points.isEmpty else {
        return nil
    }

    var totalX: Double = 0
    var totalY: Double = 0

    for point in points {
        totalX += Double(boundingBox.origin.x + boundingBox.size.width * CGFloat(point.x))
        totalY += Double(boundingBox.origin.y + boundingBox.size.height * CGFloat(point.y))
    }

    let count = Double(points.count)
    return CGPoint(x: totalX / count, y: totalY / count)
}

private func selectBestFace(_ observations: [VNFaceObservation]) -> VNFaceObservation? {
    return observations.max { lhs, rhs in
        let lhsArea = lhs.boundingBox.width * lhs.boundingBox.height
        let rhsArea = rhs.boundingBox.width * rhs.boundingBox.height
        let lhsScore = lhsArea * CGFloat(max(lhs.confidence, 0.1))
        let rhsScore = rhsArea * CGFloat(max(rhs.confidence, 0.1))
        return lhsScore < rhsScore
    }
}

private func detect(imagePath: String) throws -> OutputPayload {
    let url = URL(fileURLWithPath: imagePath)
    guard let source = CGImageSourceCreateWithURL(url as CFURL, nil),
          let cgImage = CGImageSourceCreateImageAtIndex(source, 0, nil) else {
        throw HelperError.imageLoadFailed
    }

    let width = cgImage.width
    let height = cgImage.height

    let request = VNDetectFaceLandmarksRequest()
    let handler = VNImageRequestHandler(cgImage: cgImage, options: [:])

    do {
        try handler.perform([request])
    } catch {
        throw HelperError.detectionFailed("Vision request failed: \(error.localizedDescription)")
    }

    guard let observations = request.results as? [VNFaceObservation],
          let bestFace = selectBestFace(observations) else {
        return OutputPayload(
            faceDetected: false,
            bothEyesDetected: false,
            faceConfidence: 0,
            imageWidth: width,
            imageHeight: height,
            faceRect: nil,
            leftEye: nil,
            rightEye: nil,
            faceCenter: nil,
            error: nil
        )
    }

    let box = bestFace.boundingBox
    let rect = OutputRect(
        x: clamp(Int(round(box.origin.x * Double(width))), min: 0, max: max(width - 1, 0)),
        y: clamp(Int(round((1.0 - box.origin.y - box.height) * Double(height))), min: 0, max: max(height - 1, 0)),
        width: max(1, Int(round(box.width * Double(width)))),
        height: max(1, Int(round(box.height * Double(height))))
    )

    let center = OutputPoint(
        x: clamp(rect.x + rect.width / 2, min: 0, max: max(width - 1, 0)),
        y: clamp(rect.y + rect.height / 2, min: 0, max: max(height - 1, 0))
    )

    var leftEyePoint: OutputPoint?
    var rightEyePoint: OutputPoint?
    var bothEyesDetected = false

    if let landmarks = bestFace.landmarks,
       let leftEye = landmarks.leftEye,
       let rightEye = landmarks.rightEye,
       let leftCenter = averagePoint(leftEye, boundingBox: box),
       let rightCenter = averagePoint(rightEye, boundingBox: box) {
        leftEyePoint = toPixelPoint(leftCenter, width: width, height: height)
        rightEyePoint = toPixelPoint(rightCenter, width: width, height: height)
        bothEyesDetected = true
    }

    return OutputPayload(
        faceDetected: true,
        bothEyesDetected: bothEyesDetected,
        faceConfidence: Double(bestFace.confidence),
        imageWidth: width,
        imageHeight: height,
        faceRect: rect,
        leftEye: leftEyePoint,
        rightEye: rightEyePoint,
        faceCenter: center,
        error: nil
    )
}

let payload: OutputPayload

do {
    guard CommandLine.arguments.count == 2 else {
        throw HelperError.invalidArguments
    }

    payload = try detect(imagePath: CommandLine.arguments[1])
} catch {
    let message = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
    payload = OutputPayload(
        faceDetected: false,
        bothEyesDetected: false,
        faceConfidence: 0,
        imageWidth: 0,
        imageHeight: 0,
        faceRect: nil,
        leftEye: nil,
        rightEye: nil,
        faceCenter: nil,
        error: message
    )
}

let encoder = JSONEncoder()
encoder.outputFormatting = [.sortedKeys]
let data = try encoder.encode(payload)
FileHandle.standardOutput.write(data)
