using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// Generates a multi-size ScreenBlackout.ico (16/24/32/48/64/128/256),
// drawn with GDI+: a monitor outline with a black screen.
public static class IconGen
{
    public static int Main()
    {
        try
        {
            Generate(@"D:\ScreenBlackout\ScreenBlackout.ico");
            Console.WriteLine("icon generated OK");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR: " + ex.Message);
            return 1;
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
            float t = Math.Max(1.5f, s * 0.11f);          // border thickness
            float m = Math.Max(1f, s * 0.05f);            // margin
            float w = s - 2 * m;
            float h = w * 0.72f;                          // screen aspect
            var screenRect = new RectangleF(m, m, w, h);
            var border = Color.FromArgb(255, 241, 245, 249);
            using (var pen = new Pen(border, t) { LineJoin = LineJoin.Round })
            {
                using (var path = RoundedRect(screenRect, s * 0.09f))
                    g.DrawPath(pen, path);
            }
            var inner = RectangleF.Inflate(screenRect, -t, -t);
            using (var brush = new SolidBrush(Color.FromArgb(255, 15, 23, 42)))
                g.FillRectangle(brush, inner);
            float sw = s * 0.22f;
            float st = Math.Max(1f, s * 0.07f);
            using (var pen = new Pen(border, st))
            {
                float baseY = m + h;
                g.DrawLine(pen, s / 2f - sw / 2, baseY, s / 2f + sw / 2, baseY);
                g.DrawLine(pen, s / 2f, baseY, s / 2f, baseY + s * 0.08f);
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
