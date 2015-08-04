using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenCV.Net;
using FlyCapture2Managed;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Reactive.Linq;
using System.Reactive.Disposables;

namespace Bonsai.PointGrey
{
    public class FlyCapture : Source<FlyCaptureDataFrame>
    {
        IObservable<FlyCaptureDataFrame> source;

        public FlyCapture()
        {
            source = Observable.Create<FlyCaptureDataFrame>(observer =>
            {
                ManagedCamera camera;
                ManagedImage image;
                ManagedImage convertedImage;
                using (var manager = new ManagedBusManager())
                {
                    var guid = manager.GetCameraFromIndex((uint)Index);
                    camera = new ManagedCamera();
                    camera.Connect(guid);
                }

                var running = true;
                image = new ManagedImage();
                convertedImage = new ManagedImage();
                var thread = new Thread(() =>
                {
                    camera.StartCapture();
                    while (running)
                    {
                        IplImage output;
                        camera.RetrieveBuffer(image);
                        if (image.pixelFormat == FlyCapture2Managed.PixelFormat.PixelFormatMono8 ||
                            image.pixelFormat == FlyCapture2Managed.PixelFormat.PixelFormatMono16 ||
                            (image.pixelFormat == FlyCapture2Managed.PixelFormat.PixelFormatRaw8 &&
                             image.bayerTileFormat == BayerTileFormat.None))
                        {
                            unsafe
                            {
                                var depth = image.pixelFormat == FlyCapture2Managed.PixelFormat.PixelFormatMono16 ? IplDepth.U16 : IplDepth.U8;
                                var bitmapHeader = new IplImage(new OpenCV.Net.Size((int)image.cols, (int)image.rows), depth, 1, new IntPtr(image.data));
                                output = new IplImage(bitmapHeader.Size, bitmapHeader.Depth, bitmapHeader.Channels);
                                CV.Copy(bitmapHeader, output);
                            }
                        }
                        else
                        {
                            image.Convert(FlyCapture2Managed.PixelFormat.PixelFormatBgr, convertedImage);

                            var bitmap = convertedImage.bitmap;
                            var bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                            try
                            {
                                var bitmapHeader = new IplImage(new OpenCV.Net.Size(bitmap.Width, bitmap.Height), IplDepth.U8, 3, bitmapData.Scan0);
                                output = new IplImage(bitmapHeader.Size, bitmapHeader.Depth, bitmapHeader.Channels);
                                CV.Copy(bitmapHeader, output);
                            }
                            finally { bitmap.UnlockBits(bitmapData); }
                        }

                        observer.OnNext(new FlyCaptureDataFrame(output, image.imageMetadata));
                    }
                });

                thread.Start();
                return () =>
                {
                    running = false;
                    if (thread != Thread.CurrentThread) thread.Join();
                    camera.StopCapture();
                    convertedImage.Dispose();
                    image.Dispose();
                    camera.Disconnect();
                };
            })
            .PublishReconnectable()
            .RefCount();
        }

        public int Index { get; set; }

        public override IObservable<FlyCaptureDataFrame> Generate()
        {
            return source;
        }
    }
}
