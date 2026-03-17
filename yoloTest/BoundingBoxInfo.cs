using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace yoloTest
{
    public class BoundingBoxInfo
    {
        public string ClassName { get; set; } = string.Empty;
        public float Confidence { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }

    }
}
