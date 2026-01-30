using System;
using System.Collections.Generic;
using System.Text;
using OpenCvSharp;
using PoseIntegrator.Vision.Models;

namespace PoseIntegrator.Vision.PoseDetection;

public interface IPoseDetector: IDisposable
{
    public PoseDetectionResult Detect(Mat frame, long timestampMs);
}

