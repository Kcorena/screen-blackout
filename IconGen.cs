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

            float m = Math.Max(1f, s * 0.06f);
            float R1 = s / 2f - m;                       // big disc radius
            var C1 = new PointF(s / 2f, s / 2f);         // big disc center
            float Rm = s * 0.34f;                        // amber moon radius
            float R2 = s * 0.36f;                        // small "behind" circle radius
            var C2 = new PointF(s * 0.66f, s * 0.32f);   // small circle center, up-right

            var indigo = Color.FromArgb(255, 67, 56, 202);   // #4338CA
            var amber = Color.FromArgb(255, 245, 158, 11);   // #F59E0B

            // 1. big disc
            using (var b = new SolidBrush(indigo))
                g.FillEllipse(b, new RectangleF(C1.X - R1, C1.Y - R1, R1 * 2, R1 * 2));

            // 2. amber moon (full circle, centered on the disc)
            using (var b = new SolidBrush(amber))
                g.FillEllipse(b, new RectangleF(C1.X - Rm, C1.Y - Rm, Rm * 2, Rm * 2));

            // 3. small indigo circle behind (up-right) -> cuts the amber into a crescent
            using (var b = new SolidBrush(indigo))
                g.FillEllipse(b, new RectangleF(C2.X - R2, C2.Y - R2, R2 * 2, R2 * 2));

            // 4. transparent seam along the small circle's arc that lies inside the big disc,
            //    so the two circles read as separate overlapping shapes
            float dx = C2.X - C1.X, dy = C2.Y - C1.Y;
            float d = (float)Math.Sqrt(dx * dx + dy * dy);
            float a = (R1 * R1 - R2 * R2 + d * d) / (2 * d);
            float hsq = R1 * R1 - a * a;
            if (hsq > 0)
            {
                float h = (float)Math.Sqrt(hsq);
                float ux = dx / d, uy = dy / d;
                float px = C1.X + ux * a, py = C1.Y + uy * a;
                float t1x = px - uy * h, t1y = py + ux * h;
                float t2x = px + uy * h, t2y = py - ux * h;
                float th1 = Deg(Atan2(t1y - C2.Y, t1x - C2.X));
                float th2 = Deg(Atan2(t2y - C2.Y, t2x - C2.X));
                float thA = Deg(Atan2(C1.Y - C2.Y, C1.X - C2.X));
                float delta = (th2 - th1 + 360f) % 360f;   // arc th1 -> th2 (clockwise)
                float rel = (thA - th1 + 360f) % 360f;     // direction toward C1
                float start, sweep;
                if (rel <= delta) { start = th1; sweep = delta; }
                else { start = th2; sweep = 360f - delta; }

                float seamW = Math.Max(1f, s * 0.035f);
                using (var pen = new Pen(Color.FromArgb(0, 0, 0, 0), seamW))
                {
                    g.CompositingMode = CompositingMode.SourceCopy;
                    g.DrawArc(pen, new RectangleF(C2.X - R2, C2.Y - R2, R2 * 2, R2 * 2), start, sweep);
                    g.CompositingMode = CompositingMode.SourceOver;
                }
            }
        }
        return bmp;
    }

    private static float Atan2(float y, float x)
    {
        return (float)Math.Atan2(y, x);
    }

    private static float Deg(float rad)
    {
        float d = rad * 180f / (float)Math.PI;
        return (d % 360f + 360f) % 360f;
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
