using FlyCapture2Managed;
using OpenCV.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bonsai.PointGrey
{
    public class FlyCaptureDataFrame
    {
        public FlyCaptureDataFrame(IplImage image, ImageMetadata metadata)
        {
            Image = image;
            Metadata = metadata;
        }

        public IplImage Image { get; private set; }

        public ImageMetadata Metadata { get; private set; }
    }
}
