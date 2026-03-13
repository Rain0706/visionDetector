using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

#if IOS
using CoreML;
using Vision;
using Foundation;
#endif

namespace yoloTest
{
    public class YoloVisionService
    {
#if IOS
        private VNCoreMLModel? _visionModel;
        private VNCoreMLRequest? _visionRequest;

        //load and compile the model
        public bool LoadModel()
        {
            try
            {
                // find the model in the app bundle(mlpackage)
                var modelUrl = NSBundle.MainBundle.GetUrlForResource("best", "mlpackage");
                if (modelUrl == null)
                {
                    Debug.WriteLine("couldn't find the best.mlpackage, please confirm the location of  file");
                    return false;
                }
                // compile the model
                var compiledModelUrl = MLModel.CompileModel(modelUrl, out var compileError);
                if (compileError != null)
                {
                    Debug.WriteLine($"model Compilation Failed: {compileError.LocalizedDescription}");
                    return false;
                }
                // load the compiled model
                var mlModel = MLModel.Create(compiledModelUrl, out var createError);
                if (createError != null)
                {
                    Debug.WriteLine($"model loading Failed: {createError.LocalizedDescription}");
                    return false;
                }
                //covert the CoreML model to Vision model
                _visionModel = VNCoreMLModel.FromMLModel(mlModel, out var visionError);
                if (visionError != null)
                {
                    Debug.WriteLine($"Vision model Conversion Failed: {visionError.LocalizedDescription}");
                    return false;
                }
                // initialize image inference request
                _visionRequest = new VNCoreMLRequest(_visionModel, (request, error) =>
                {
                    // when the request is completed, this callback will be called with the results
                    ProcessObservationResults(request.GetResults<VNRecognizedObjectObservation>());
                });

                // set the image crop and scale option to fit the model's input size
                _visionRequest.ImageCropAndScaleOption = VNImageCropAndScaleOption.ScaleFill;

                Debug.WriteLine("YOLOv8 CoreML model have been loaded and initialized successfully!");
                return true;

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception Error: {ex.Message}");
                return false;
            }
        }

        private void ProcessObservationResults(VNRecognizedObjectObservation[]? observations)
        {
            if (observations == null || observations.Length == 0) return;

            foreach (var observation in observations)
            {
                //get the highest confidence label(ex: vehicle, pedestrian)
                var bestLabel = observation.Labels[0];
                Debug.WriteLine($"{bestLabel.Identifier} detected (confidence :{bestLabel.Confidence:F2})");

                // observation.BoundingBox can get the coordinates of the detected object in the image(0.0 ~1.0)
            }
        }

#endif
    }
}
