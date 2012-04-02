using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenCV.Net;
using FlyCapture2Managed;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;

namespace Bonsai.PointGrey
{
    public class FlyCapture : Source<IplImage>
    {
        ManagedCamera camera;
        ManagedImage image;
        ManagedImage convertedImage;
        Thread captureThread;
        volatile bool running;
        ManualResetEventSlim stop;

        public int Index { get; set; }

        void CaptureNewFrame()
        {
            camera.StartCapture();

            while (running)
            {
                camera.RetrieveBuffer(image);
                if (image.pixelFormat == FlyCapture2Managed.PixelFormat.PixelFormatMono8)
                {
                    unsafe
                    {
                        var bitmapHeader = new IplImage(new CvSize((int)image.cols, (int)image.rows), 8, 1, new IntPtr(image.data));
                        var output = new IplImage(bitmapHeader.Size, bitmapHeader.Depth, bitmapHeader.NumChannels);
                        Core.cvCopy(bitmapHeader, output);
                        Subject.OnNext(output);
                    }
                }
                else
                {
                    image.Convert(FlyCapture2Managed.PixelFormat.PixelFormatBgr, convertedImage);

                    var bitmap = convertedImage.bitmap;
                    var bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                    try
                    {
                        var bitmapHeader = new IplImage(new CvSize(bitmap.Width, bitmap.Height), 8, 3, bitmapData.Scan0);
                        var output = new IplImage(bitmapHeader.Size, bitmapHeader.Depth, bitmapHeader.NumChannels);
                        Core.cvCopy(bitmapHeader, output);
                        Subject.OnNext(output);
                    }
                    finally { bitmap.UnlockBits(bitmapData); }
                }
            }

            camera.StopCapture();
            stop.Set();
        }

        protected override void Start()
        {
            running = true;
            captureThread = new Thread(CaptureNewFrame);
            captureThread.Start();
        }

        protected override void Stop()
        {
            running = false;
            stop.Wait();
        }

        public override IDisposable Load()
        {
            using (var manager = new ManagedBusManager())
            {
                var guid = manager.GetCameraFromIndex((uint)Index);
                camera = new ManagedCamera();
                camera.Connect(guid);
            }

            image = new ManagedImage();
            convertedImage = new ManagedImage();
            stop = new ManualResetEventSlim();
            return base.Load();
        }

        protected override void Unload()
        {
            stop.Dispose();
            convertedImage.Dispose();
            image.Dispose();
            camera.Disconnect();
            base.Unload();
        }
    }
}
