using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Tatti3
{
    class LazyDdsGrp
    {
        public LazyDdsGrp(GameData.DdsGrp? inner)
        {
            this.inner = inner;
            this.cache = new Dictionary<int, BitmapSource?>();
        }

        public BitmapSource? Image(int id)
        {
            BitmapSource? result = null;
            if (cache.TryGetValue(id, out result))
            {
                return result;
            }
            if (inner != null)
            {
                if (inner.Count > id)
                {
                    var frame = inner.GetFrame(id);
                    result = BitmapSource.Create(
                        (int)frame.Width,
                        (int)frame.Height,
                        96.0,
                        96.0,
                        frame.Format,
                        null,
                        frame.Data,
                        frame.Stride
                    );
                }
            }
            cache.Add(id, result);
            return result;
        }

        GameData.DdsGrp? inner;
        Dictionary<int, BitmapSource?> cache;
    }
}
