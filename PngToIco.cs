using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// Converts a square PNG into a multi-size .ico (16/24/32/48/64/128/256, PNG-compressed entries).
public static class PngToIco
{
    public static int Main(string[] args)
    {
        if (args.Length < 2) { Console.WriteLine("usage: PngToIco <in.png> <out.ico>"); return 1; }
        try
        {
            Convert(args[0], args[1]);
            Console.WriteLine("ico written: " + args[1]);
            SavePreview(args[0], @"C:\Users\Falli\.openclaw\workspace\tmp\icon_256.png", 256);
            SavePreview(args[0], @"C:\Users\Falli\.openclaw\workspace\tmp\icon_16x6.png", 16, 6);
            Console.WriteLine("previews OK");
            return 0;
        }
        catch (Exception ex) { Console.WriteLine("ERROR: " + ex.Message); return 1; }
    }

    private static void SavePreview(string pngPath, string outPath, int size, int scale = 1)
    {
        using (var src = new Bitmap(pngPath))
        {
            if (scale > 1)
            {
                using (var small = new Bitmap(size, size, PixelFormat.Format32bppArgb))
                using (var g = Graphics.FromImage(small))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(src, 0, 0, size, size);
                    using (var big = new Bitmap(size * scale, size * scale, PixelFormat.Format32bppArgb))
                    using (var g2 = Graphics.FromImage(big))
                    {
                        g2.InterpolationMode = InterpolationMode.NearestNeighbor;
                        g2.PixelOffsetMode = PixelOffsetMode.Half;
                        g2.DrawImage(small, 0, 0, size * scale, size * scale);
                        big.Save(outPath, ImageFormat.Png);
                    }
                }
            }
            else
            {
                using (var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
                using (var g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(src, 0, 0, size, size);
                    bmp.Save(outPath, ImageFormat.Png);
                }
            }
        }
    }

    private static void Convert(string pngPath, string icoPath)
    {
        int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
        var dirs = new List<byte[]>();
        var datas = new List<byte[]>();
        int offset = 6 + sizes.Length * 16;
        using (var src = new Bitmap(pngPath))
        {
            foreach (int s in sizes)
            {
                using (var bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb))
                using (var g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(Color.Transparent);
                    g.DrawImage(src, 0, 0, s, s);
                    byte[] data;
                    using (var ms = new MemoryStream()) { bmp.Save(ms, ImageFormat.Png); data = ms.ToArray(); }
                    byte[] dir = new byte[16];
                    dir[0] = (byte)(s >= 256 ? 0 : s);
                    dir[1] = (byte)(s >= 256 ? 0 : s);
                    dir[2] = 0;
                    dir[3] = 0;
                    WriteU16(dir, 4, 1);
                    WriteU16(dir, 6, 32);
                    WriteU32(dir, 8, (uint)data.Length);
                    WriteU32(dir, 12, (uint)offset);
                    offset += data.Length;
                    dirs.Add(dir);
                    datas.Add(data);
                }
            }
        }
        using (var fs = new FileStream(icoPath, FileMode.Create))
        using (var bw = new BinaryWriter(fs))
        {
            bw.Write((ushort)0);
            bw.Write((ushort)1);
            bw.Write((ushort)sizes.Length);
            foreach (var d in dirs) bw.Write(d);
            foreach (var d in datas) bw.Write(d);
        }
    }

    private static void WriteU16(byte[] b, int off, ushort v)
    {
        b[off] = (byte)(v & 0xFF);
        b[off + 1] = (byte)((v >> 8) & 0xFF);
    }

    private static void WriteU32(byte[] b, int off, uint v)
    {
        b[off] = (byte)(v & 0xFF);
        b[off + 1] = (byte)((v >> 8) & 0xFF);
        b[off + 2] = (byte)((v >> 16) & 0xFF);
        b[off + 3] = (byte)((v >> 24) & 0xFF);
    }
}
