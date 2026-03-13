from ultralytics import YOLO

#load the slight pre-traning model
model = YOLO('yolov8n.pt')

#export to CoreML
model.export(format ='coreml', nms=True)

