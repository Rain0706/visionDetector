using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using AVFoundation;
using CoreMedia;
using CoreVideo;
using Foundation;

namespace yoloTest.Platforms.iOS
{
    //inherit AVCaptureVideoDateOutputSampleBufferDelegate to intercept camera frames
    public class CameraStreamManager : AVCaptureVideoDataOutputSampleBufferDelegate
    {
        private AVCaptureSession _captureSession;
        private YoloVisionService _visionService;

        public CameraStreamManager(YoloVisionService visionService)
        {
            _visionService = visionService;
        }

        public void StartStream()
        {
            _captureSession = new AVCaptureSession();
            //set the capture session preset to 640*480 for better performance, deduction the memory usage and CPU load
            _captureSession.SessionPreset = AVCaptureSession.Preset640x480;

            //get the back camera
            var camera = AVCaptureDevice.GetDefaultDevice(AVMediaTypes.Video);
            if (camera == null) return;

            var input = AVCaptureDeviceInput.FromDevice(camera, out var error);
            if(input != null && _captureSession.CanAddInput(input))
            {
                _captureSession.AddInput(input);
            }

            //set up the video data output
            var output = new AVCaptureVideoDataOutput
            {
                //if the inference process is too slow, throw away the old frames, ensure the system won't OOM(out of memory)
                AlwaysDiscardsLateVideoFrames = true
            };

            //set the format of the video frames to 32BGRA that CoreML prefers
            output.WeakVideoSettings = new NSDictionary(CVPixelBuffer.PixelFormatTypeKey,
                (int)CVPixelFormatType.CV32BGRA);

            //open a exclusive queue for processing the video frames
            var queue = new CoreFoundation.DispatchQueue("CameraStreamQueue");
            output.SetSampleBufferDelegate(this, queue);
            if (_captureSession.CanAddOutput(output))
            {
                _captureSession.AddOutput(output);
            }
            // open the camera
            _captureSession.StartRunning();
        }

        //when a new camera frame is captured, trigger this method(each second will trigger about 30 times)
        public override void DidOutputSampleBuffer(AVCaptureOutput captureOutput, CMSampleBuffer sampleBuffer, AVCaptureConnection connection)
        {
            // covert the image to a raw pixel buffer
            using (var pixelBuffer = sampleBuffer.GetImageBuffer() as CVPixelBuffer)
            {
                if (pixelBuffer != null)
                {
                    // sned the pixel buffer to the vision service for inference
                    _visionService.PredictFrame(pixelBuffer);
                }
            }

            // release the sample buffer to free up the memory
            sampleBuffer.Dispose();
        }


    }
}
