using System;
using OpenCV.Net;
using FlyCapture2Managed;
using System.Threading;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Bonsai.PointGrey
{
    public class FlyCapture : Source<FlyCaptureDataFrame>
    {
        IObservable<FlyCaptureDataFrame> source;
        readonly object captureLock = new object();

        public FlyCapture()
        {
            NumBuffers = 10;
            GrabMode = GrabMode.BufferFrames;
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

                        var capture = 0;
                        var numBuffers = NumBuffers;
                        var config = camera.GetConfiguration();
                        config.grabMode = GrabMode;
                        config.numBuffers = (uint)NumBuffers;
                        config.highPerformanceRetrieveBuffer = true;
                        camera.SetConfiguration(config);

                        try
                        {
                            var colorProcessing = ColorProcessing;
                            using (var image = new ManagedImage())
                            using (var notification = cancellationToken.Register(() =>
                            {
                                Interlocked.Exchange(ref capture, 0);
                                camera.StopCapture();
                            }))
                            {
                                camera.StartCapture();
                                Interlocked.Exchange(ref capture, 1);
                                while (!cancellationToken.IsCancellationRequested)
                                {
                                    IplImage output;
                                    BayerTileFormat bayerTileFormat;
                                    try { camera.RetrieveBuffer(image); }
                                    catch (FC2Exception)
                                    {
                                        if (capture == 0) break;
                                        else throw;
                                    }

                                    var raw16 = image.pixelFormat == PixelFormat.PixelFormatRaw16;
                                    if (image.pixelFormat == PixelFormat.PixelFormatMono8 ||
                                        image.pixelFormat == PixelFormat.PixelFormatMono16 ||
                                        ((image.pixelFormat == PixelFormat.PixelFormatRaw8 || raw16) &&
                                         (image.bayerTileFormat == BayerTileFormat.None ||
                                          colorProcessing == ColorProcessingAlgorithm.NoColorProcessing)))
                                    {
                                        unsafe
                                        {
                                            bayerTileFormat = image.bayerTileFormat;
                                            var depth = image.pixelFormat == PixelFormat.PixelFormatMono16 || raw16 ? IplDepth.U16 : IplDepth.U8;
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
                                            var depth = raw16 ? IplDepth.U16 : IplDepth.U8;
                                            var format = raw16 ? PixelFormat.PixelFormatBgr16 : PixelFormat.PixelFormatBgr;
                                            output = new IplImage(new Size((int)image.cols, (int)image.rows), depth, 3);
                                            using (var convertedImage = new ManagedImage(
                                                (uint)output.Height,
                                                (uint)output.Width,
                                                (uint)output.WidthStep,
                                                (byte*)output.ImageData.ToPointer(),
                                                (uint)(output.WidthStep * output.Height),
                                                format))
                                            {
                                                convertedImage.colorProcessingAlgorithm = colorProcessing;
                                                image.Convert(format, convertedImage);
                                            }
                                        }
                                    }

                                    observer.OnNext(new FlyCaptureDataFrame(output, image.imageMetadata, bayerTileFormat));
                                }
                            }
                        }
                        finally
                        {
                            if (capture != 0) camera.StopCapture();
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

        public int NumBuffers { get; set; }

        public GrabMode GrabMode { get; set; }

        public override IObservable<FlyCaptureDataFrame> Generate()
        {
            return source;
        }
    }
}
