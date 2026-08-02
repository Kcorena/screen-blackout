using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// Generates a multi-size ScreenBlackout.ico (16/24/32/48/64/128/256),
// drawn with GDI+: an indigo disc with an amber crescent moon.
public static class IconGen
{
    public static int Main()
    {
        try
        {
            Generate(@"D:\ScreenBlackout\ScreenBlackout.ico");
            Console.WriteLine("icon generated OK");
            SavePreview(@"C:\Users\Falli\.openclaw\workspace\tmp\icon_256.png", 256);
            SavePreview(@"C:\Users\Falli\.openclaw\workspace\tmp\icon_16x6.png", 16, 6);
            Console.WriteLine("previews OK");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR: " + ex.Message);
            return 1;
        }
    }

    private static void SavePreview(string path, int size, int scale = 1)
    {
        using (var bmp = Draw(size))
        {
            if (scale > 1)
            {
                using (var big = new Bitmap(size * scale, size * scale, PixelFormat.Format32bppArgb))
                using (var g = Graphics.FromImage(big))
                {
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    g.DrawImage(bmp, 0, 0, size * scale, size * scale);
                    big.Save(path, ImageFormat.Png);
                }
            }
            else
            {
                bmp.Save(path, ImageFormat.Png);
            }
        }
    }

    private static void Generate(string outPath)
    {
        int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
        var dirs = new List<byte[]>();
        var datas = new List<byte[]>();
        int offset = 6 + sizes.Length * 16;
        foreach (int s in sizes)
        {
            using (var bmp = Draw(s))
            {
                byte[] data;
                if (s >= 256)
                {
                    using (var ms = new MemoryStream()) { bmp.Save(ms, ImageFormat.Png); data = ms.ToArray(); }
                }
                else
                {
                    data = ToBmpData(bmp);
                }
                byte[] dir = new byte[16];
                dir[0] = (byte)(s >= 256 ? 0 : s);   // 0 means 256
                dir[1] = (byte)(s >= 256 ? 0 : s);
                dir[2] = 0;                          // color count
                dir[3] = 0;                          // reserved
                WriteU16(dir, 4, 1);                 // planes
                WriteU16(dir, 6, 32);                // bpp
                WriteU32(dir, 8, (uint)data.Length);
                WriteU32(dir, 12, (uint)offset);
                offset += data.Length;
                dirs.Add(dir);
                datas.Add(data);
            }
        }
        using (var fs = new FileStream(outPath, FileMode.Create))
        using (var bw = new BinaryWriter(fs))
        {
            bw.Write((ushort)0);
            bw.Write((ushort)1);
            bw.Write((ushort)sizes.Length);
            foreach (var d in dirs) bw.Write(d);
            foreach (var d in datas) bw.Write(d);
        }
    }

    private static Bitmap Draw(int s)
    {
        var bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            // 1. black rounded-square background (full bleed)
            using (var bg = RoundedRect(new RectangleF(0, 0, s, s), s * 0.23f))
            using (var b = new SolidBrush(Color.FromArgb(255, 10, 10, 14)))
                g.FillPath(b, bg);

            // 2. power symbol: open ring + vertical bar, blue->purple gradient, thick stroke
            float cx = s / 2f, cy = s / 2f;
            float Ro = s * 0.30f;                       // outer ring radius
            float t = Math.Max(1.25f, s * 0.085f);      // stroke thickness (thick)
            float Ri = Ro - t;                          // inner ring radius

            using (var gradient = new LinearGradientBrush(
                new PointF(0, 0), new PointF(s, s),
                Color.FromArgb(255, 59, 130, 246),      // blue #3B82F6
                Color.FromArgb(255, 147, 51, 234)))     // purple #9333EA
            {
                // ring: C-shape open at the top (gap ~70 deg centred at 12 o'clock)
                using (var ring = new GraphicsPath())
                {
                    ring.AddArc(cx - Ro, cy - Ro, Ro * 2, Ro * 2, 305f, 290f);   // outer arc
                    ring.AddArc(cx - Ri, cy - Ri, Ri * 2, Ri * 2, 235f, -290f);  // inner arc (reverse)
                    ring.CloseFigure();
                    g.FillPath(gradient, ring);
                }

                // vertical bar with rounded caps, merging into the ring's top
                float barTop = cy - Ro - t * 1.3f;
                float barBottom = cy - Ro + t * 0.9f;
                using (var pen = new Pen(gradient, t)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                })
                    g.DrawLine(pen, cx, barTop, cx, barBottom);
            }
        }
        return bmp;
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    private static byte[] ToBmpData(Bitmap bmp)
    {
        int s = bmp.Width;
        using (var ms = new MemoryStream())
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write(40);                 // biSize
            bw.Write(s);                  // biWidth
            bw.Write(s * 2);              // biHeight (XOR + AND)
            bw.Write((short)1);           // biPlanes
            bw.Write((short)32);          // biBitCount
            bw.Write(0);                  // biCompression BI_RGB
            bw.Write(s * s * 4);          // biSizeImage
            bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);
            for (int y = s - 1; y >= 0; y--)
                for (int x = 0; x < s; x++)
                {
                    var c = bmp.GetPixel(x, y);
                    bw.Write(c.B); bw.Write(c.G); bw.Write(c.R); bw.Write(c.A);
                }
            int stride = ((s + 31) / 32) * 4;
            byte[] andRow = new byte[stride];
            for (int y = 0; y < s; y++) bw.Write(andRow);
            return ms.ToArray();
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
