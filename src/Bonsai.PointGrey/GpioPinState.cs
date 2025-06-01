using OpenCV.Net;
using System;
using System.Linq;
using System.Reactive.Linq;

namespace Bonsai.PointGrey
{
    public class GpioPinState : Transform<FlyCaptureDataFrame, Scalar>
    {
        public override IObservable<Scalar> Process(IObservable<FlyCaptureDataFrame> source)
        {
            return source.Select(input =>
            {
                var gpio = input.Metadata.embeddedGPIOPinState;
                var gpio0 = ((gpio >> 31) & 0x1);
                var gpio1 = ((gpio >> 30) & 0x1);
                var gpio2 = ((gpio >> 29) & 0x1);
                var gpio3 = ((gpio >> 28) & 0x1);
                return new Scalar(gpio0, gpio1, gpio2, gpio3);
            });
        }
    }
}
