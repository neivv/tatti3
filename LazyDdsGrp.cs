using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Tatti3
{
    class LazyDdsGrp
    {
        public LazyDdsGrp(GameData.DdsGrp? inner, (float, float, float) color)
        {
            this.inner = inner;
            this.color = color;
            this.cache = new Dictionary<int, BitmapSource?>();
        }

        public BitmapSource? Image(int id)
        {
            if (cache.TryGetValue(id, out BitmapSource? result))
            {
                return result;
            }
            if (inner != null)
            {
                if (inner.Count > id)
                {
                    var frame = inner.GetFrame(id);
                    byte[] data = frame.Data;
                    if (frame.Format == PixelFormats.Bgr24)
                    {
                        for (int i = 0; i < (int)frame.Width * (int)frame.Height * 3; i += 3)
                        {
                            data[i] = (byte)((float)data[i] * this.color.Item3);
                            data[i + 1] = (byte)((float)data[i + 1] * this.color.Item2);
                            data[i + 2] = (byte)((float)data[i + 2] * this.color.Item1);
                        }
                    }
                    else if (frame.Format == PixelFormats.Bgra32)
                    {
                        for (int i = 0; i < (int)frame.Width * (int)frame.Height * 4; i += 4)
                        {
                            data[i] = (byte)((float)data[i] * this.color.Item3);
                            data[i + 1] = (byte)((float)data[i + 1] * this.color.Item2);
                            data[i + 2] = (byte)((float)data[i + 2] * this.color.Item1);
                        }
                    }
                    result = BitmapSource.Create(
                        (int)frame.Width,
                        (int)frame.Height,
                        96.0,
                        96.0,
                        frame.Format,
                        null,
                        data,
                        frame.Stride
                    );
                }
            }
            cache.Add(id, result);
            return result;
        }

        GameData.DdsGrp? inner;
        (float, float, float) color;
        Dictionary<int, BitmapSource?> cache;
    }
}
