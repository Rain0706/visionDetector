using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.Graphics;

namespace yoloTest
{
    public class DetectionDrawable : IDrawable
    {
        // store all the frames to be drawn on the current screen
        public List<BoundingBoxInfo> Boxes { get; set; } = new List<BoundingBoxInfo>();

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            if (Boxes == null || Boxes.Count == 0) return;

            // iPhone back camera Preset 640*480 when it Portrait
            float videoAspet = 480f / 640f;
            // to calculate the actual video width and height
            float actualVideoWidth = dirtyRect.Width;
            float actualVideoHeight = dirtyRect.Width / videoAspet;
            // calculate the offset of the top and bottom borders
            float yOffset = (dirtyRect.Height - actualVideoHeight) / 2;

            foreach (var box in Boxes)
            {
                //convert the 0.0 to 1.0 ratio to the actual pixel count of the mobile phone
                float x = box.X * dirtyRect.Width;
                float width = box.Width * dirtyRect.Width;
                float height = box.Height * dirtyRect.Height;

                //// map the coordinates from 0 to 1 to the actual video display area
                //float x = box.X * actualVideoWidth;
                //float width = box.Width * actualVideoWidth;
                //float height = box.Height * actualVideoHeight;

                // we must subtract(Y + Height) from 1 to reverse the Y-axis
                float y = (1 - box.Y - box.Height) * dirtyRect.Height;

                //// reverse the Y-axis and add the offset
                //float y = (1 - box.Y - box.Height) * actualVideoHeight + yOffset;

                // draw the bounding box 
                canvas.StrokeColor = Colors.Red;
                canvas.StrokeSize = 4;
                canvas.DrawRectangle(x, y, width, height);

                //draw a semi-transparent red rectangle behind the text to improve readability
                canvas.FillColor = Colors.Red.WithAlpha(0.8f);
                canvas.FillRectangle(x, y - 22, width, 22);

                // draw the label and confidence score above the bounding box
                canvas.FontColor = Colors.White;
                canvas.FontSize = 14;
                canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
                canvas.DrawString($"{box.ClassName} ({box.Confidence:F2})", x + 5, y - 18, HorizontalAlignment.Left);
            }
        }
    }
}
