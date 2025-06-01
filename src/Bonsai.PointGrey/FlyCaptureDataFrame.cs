using FlyCapture2Managed;
using OpenCV.Net;

namespace Bonsai.PointGrey
{
    public class FlyCaptureDataFrame
    {
        public FlyCaptureDataFrame(IplImage image, ImageMetadata metadata)
            : this(image, metadata, BayerTileFormat.None)
        {
        }

        public FlyCaptureDataFrame(IplImage image, ImageMetadata metadata, BayerTileFormat bayerTileFormat)
        {
            Image = image;
            Metadata = metadata;
            BayerTileFormat = bayerTileFormat;
        }

        public IplImage Image { get; private set; }

        public ImageMetadata Metadata { get; private set; }

        public BayerTileFormat BayerTileFormat { get; private set; }

        public override string ToString()
        {
            return string.Format("{{Image={0}, Bayer={1}}}", Image, BayerTileFormat);
        }
    }
}
