using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenCV.Net;
using FlyCapture2Managed;
using System.Threading;
using System.Reactive.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;

namespace Bonsai.PointGrey
{
    public class FlyCapture : Source<FlyCaptureDataFrame>
    {
        IObservable<FlyCaptureDataFrame> source;
        readonly object captureLock = new object();

        public FlyCapture()
        {
            ColorProcessing = ColorProcessingAlgorithm.Default;
            source = Observable.Create<FlyCaptureDataFrame>((observer, cancellationToken) =>
            {
                return Task.Factory.StartNew(() =>
                {
                    lock (captureLock)
                    {
                        ManagedCamera camera;
                        using (var manager = new ManagedBusManager())
                        {
                            var guid = manager.GetCameraFromIndex((uint)Index);
                            camera = new ManagedCamera();
                            camera.Connect(guid);
                        }

                        try
                        {
                            var colorProcessing = ColorProcessing;
                            using (var image = new ManagedImage())
                            {
                                camera.StartCapture();
                                while (!cancellationToken.IsCancellationRequested)
                                {
                                    IplImage output;
                                    BayerTileFormat bayerTileFormat;
                                    camera.RetrieveBuffer(image);
                                    if (image.pixelFormat == PixelFormat.PixelFormatMono8 ||
                                        image.pixelFormat == PixelFormat.PixelFormatMono16 ||
                                        (image.pixelFormat == PixelFormat.PixelFormatRaw8 &&
                                           (image.bayerTileFormat == BayerTileFormat.None ||
                                            colorProcessing == ColorProcessingAlgorithm.NoColorProcessing)))
                                    {
                                        unsafe
                                        {
                                            bayerTileFormat = image.bayerTileFormat;
                                            var depth = image.pixelFormat == PixelFormat.PixelFormatMono16 ? IplDepth.U16 : IplDepth.U8;
                                            var bitmapHeader = new IplImage(new Size((int)image.cols, (int)image.rows), depth, 1, new IntPtr(image.data));
                                            output = new IplImage(bitmapHeader.Size, bitmapHeader.Depth, bitmapHeader.Channels);
                                            CV.Copy(bitmapHeader, output);
                                        }
                                    }
                                    else
                                    {
                                        unsafe
                                        {
                                            bayerTileFormat = BayerTileFormat.None;
                                            output = new IplImage(new Size((int)image.cols, (int)image.rows), IplDepth.U8, 3);
                                            using (var convertedImage = new ManagedImage(
                                                (uint)output.Height,
                                                (uint)output.Width,
                                                (uint)output.WidthStep,
                                                (byte*)output.ImageData.ToPointer(),
                                                (uint)(output.WidthStep * output.Height),
                                                PixelFormat.PixelFormatBgr))
                                            {
                                                convertedImage.colorProcessingAlgorithm = colorProcessing;
                                                image.Convert(PixelFormat.PixelFormatBgr, convertedImage);
                                            }
                                        }
                                    }

                                    observer.OnNext(new FlyCaptureDataFrame(output, image.imageMetadata, bayerTileFormat));
                                }
                            }
                        }
                        finally
                        {
                            camera.StopCapture();
                            camera.Disconnect();
                            camera.Dispose();
                        }
                    }
                },
                cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
            })
            .PublishReconnectable()
            .RefCount();
        }

        public int Index { get; set; }

        public ColorProcessingAlgorithm ColorProcessing { get; set; }

        public override IObservable<FlyCaptureDataFrame> Generate()
        {
            return source;
        }
    }
}
