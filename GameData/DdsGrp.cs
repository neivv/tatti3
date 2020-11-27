using System;
using System.IO;
using System.Collections.Generic;
using System.Windows.Media;

namespace Tatti3.GameData
{
    class DdsGrp : IDisposable
    {
        // Input stream will be kept alive to load icon graphics when asked. User probably
        // wants to use an caching layer on top of this.
        public DdsGrp(Stream input)
        {
            this.reader = new BinaryReader(input);
            var fileSize = reader.ReadUInt32();
            var count = (int)reader.ReadUInt16();
            var scale = reader.ReadByte();
            var version = reader.ReadByte();
            if (version != 16)
            {
                throw new InvalidDataException($"Invalid ddsgrp version {version:x}");
            }
            this.frames = new List<FrameDecl>(count);
            for (int i = 0; i < count; i++)
            {
                var unk = reader.ReadUInt32();
                var width = reader.ReadUInt16();
                var height = reader.ReadUInt16();
                var size = reader.ReadUInt32();
                var offset = reader.BaseStream.Position;
                reader.BaseStream.Seek((long)size, SeekOrigin.Current);
                this.frames.Add(new FrameDecl(offset, size, width, height));
            }
        }

        public Frame GetFrame(int index)
        {
            var decl = frames[index];
            reader.BaseStream.Seek(decl.Offset, SeekOrigin.Begin);
            byte[] ddsBytes = new byte[(int)decl.Size];
            var read = reader.Read(ddsBytes, 0, (int)decl.Size);
            if (read != (int)decl.Size)
            {
                throw new Exception($"Excepted to read {decl.Size} bytes, read only {read}");
            }
            var dds = Pfim.Dds.Create(new MemoryStream(ddsBytes), new Pfim.PfimConfig());
            var format = dds.Format switch
            {
                Pfim.ImageFormat.Rgb24 => PixelFormats.Bgr24,
                Pfim.ImageFormat.Rgba32 => PixelFormats.Bgra32,
                _ => throw new Exception($"Can't handle {dds.Format}"),
            };
            return new Frame(dds.Data, decl.Width, decl.Height, dds.Stride, format);
        }

        public int Count { get => frames.Count; }
        List<FrameDecl> frames;
        BinaryReader reader;

        readonly struct FrameDecl
        {
            public FrameDecl(long offset, uint size, ushort width, ushort height)
            {
                this.Offset = offset;
                this.Size = size;
                this.Width = width;
                this.Height = height;
            }

            public long Offset { get; }
            public uint Size { get; }
            public ushort Width { get; }
            public ushort Height { get; }
        }

        public readonly struct Frame
        {
            public Frame(byte[] data, ushort width, ushort height, int stride, PixelFormat format)
            {
                this.Data = data;
                this.Width = width;
                this.Height = height;
                this.Format = format;
                this.Stride = stride;
            }

            public byte[] Data { get; }
            public ushort Width { get; }
            public ushort Height { get; }
            public PixelFormat Format { get; }
            public int Stride { get; }
        }

        void IDisposable.Dispose()
        {
            this.reader.Dispose();
        }
    }
}
