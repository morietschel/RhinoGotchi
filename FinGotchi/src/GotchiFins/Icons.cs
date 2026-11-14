using System;
using Eto.Drawing;

namespace GotchiFins
{
    /// <summary>
    /// Line icons for the five commands the game teaches, plus the game itself. The same
    /// drawings are used in the panel and baked into the RhinoGotchi toolbar's bitmaps, so a
    /// player sees one icon per command everywhere.
    /// </summary>
    internal static class Icons
    {
        public const string Game = "GotchiFins";

        /// <summary>Draws the icon for <paramref name="command"/> into a square <paramref name="r"/>.</summary>
        public static void Draw(Graphics g, string command, RectangleF r, Color ink)
        {
            float k = Math.Min(r.Width, r.Height) / 24f;
            float ox = r.X + (r.Width - 24 * k) / 2, oy = r.Y + (r.Height - 24 * k) / 2;
            PointF P(float x, float y) => new PointF(ox + x * k, oy + y * k);
            var pen = new Pen(ink, Math.Max(1f, 1.7f * k)) { LineJoin = PenLineJoin.Round, LineCap = PenLineCap.Round };
            Color tint = Color.FromArgb(ink.Rb, ink.Gb, ink.Bb, 60);

            switch (command)
            {
                case "Box":
                    g.FillPolygon(tint, P(4, 8), P(12, 4), P(20, 8), P(12, 12));
                    g.DrawPolygon(pen, P(4, 8), P(12, 4), P(20, 8), P(20, 16), P(12, 20), P(4, 16));
                    g.DrawLine(pen, P(4, 8), P(12, 12));
                    g.DrawLine(pen, P(12, 12), P(20, 8));
                    g.DrawLine(pen, P(12, 12), P(12, 20));
                    break;

                case "Polyline":
                {
                    PointF[] pts = { P(3.5f, 19), P(8, 6.5f), P(12.5f, 15), P(16, 5.5f), P(20.5f, 19) };
                    for (int i = 0; i + 1 < pts.Length; i++)
                        g.DrawLine(pen, pts[i], pts[i + 1]);
                    float d = 3.4f * k;
                    foreach (PointF p in pts)
                        g.FillEllipse(ink, p.X - d / 2, p.Y - d / 2, d, d);
                    break;
                }

                case "ExtrudeCrv":
                    // A zig-zag profile pushed sideways into a thin solid, and the push arrow
                    g.FillPolygon(tint, P(4, 20), P(4, 5), P(7, 3), P(7, 18));
                    g.DrawPolygon(pen, P(4, 20), P(4, 5), P(7, 3), P(7, 18));
                    g.DrawLine(pen, P(7, 3), P(9, 6));
                    g.DrawLine(pen, P(9, 6), P(7.5f, 10));
                    g.DrawLine(pen, P(7.5f, 10), P(9, 14));
                    g.DrawLine(pen, P(9, 14), P(7, 18));
                    g.DrawLine(pen, P(12, 12), P(20.5f, 12));
                    g.DrawLine(pen, P(17, 8.5f), P(20.5f, 12));
                    g.DrawLine(pen, P(17, 15.5f), P(20.5f, 12));
                    break;

                case "ArrayCrv":
                {
                    // Fins standing along a dashed curve
                    var dash = new Pen(ink, Math.Max(1f, 1.3f * k)) { DashStyle = DashStyles.Dash, LineCap = PenLineCap.Round };
                    PointF prev = P(2.5f, 20.5f);
                    for (int i = 1; i <= 10; i++)
                    {
                        float u = i / 10f;
                        PointF next = P(2.5f + 19 * u, 20.5f - 7 * u * u);
                        g.DrawLine(dash, prev, next);
                        prev = next;
                    }
                    for (int i = 0; i < 4; i++)
                    {
                        float u = (i + 0.5f) / 4f, x = 2.5f + 19 * u, y = 20.5f - 7 * u * u;
                        g.DrawLine(pen, P(x, y), P(x, y - 11));
                    }
                    break;
                }

                case "Sun":
                {
                    float c = 12, rr = 4.6f;
                    g.FillEllipse(tint, P(c - rr, c - rr).X, P(c - rr, c - rr).Y, 2 * rr * k, 2 * rr * k);
                    g.DrawEllipse(pen, P(c - rr, c - rr).X, P(c - rr, c - rr).Y, 2 * rr * k, 2 * rr * k);
                    for (int i = 0; i < 8; i++)
                    {
                        double a = i * Math.PI / 4;
                        float cx = (float)Math.Cos(a), cy = (float)Math.Sin(a);
                        g.DrawLine(pen, P(c + cx * 7.2f, c + cy * 7.2f), P(c + cx * 9.6f, c + cy * 9.6f));
                    }
                    break;
                }

                case "ApplyBoxMapping":
                {
                    // A cube with a texture grid on its faces
                    g.FillPolygon(tint, P(4, 8), P(12, 4), P(20, 8), P(12, 12));
                    g.DrawPolygon(pen, P(4, 8), P(12, 4), P(20, 8), P(20, 16), P(12, 20), P(4, 16));
                    g.DrawLine(pen, P(4, 8), P(12, 12));
                    g.DrawLine(pen, P(12, 12), P(20, 8));
                    g.DrawLine(pen, P(12, 12), P(12, 20));
                    var thin = new Pen(ink, Math.Max(0.8f, 0.9f * k));
                    g.DrawLine(thin, P(8, 10), P(8, 18));          // left face grid
                    g.DrawLine(thin, P(4, 12), P(12, 16));
                    g.DrawLine(thin, P(16, 10), P(16, 18));        // right face grid
                    g.DrawLine(thin, P(12, 16), P(20, 12));
                    break;
                }

                default:
                {
                    // The game: three wavy fins, like the facade
                    for (int f = 0; f < 3; f++)
                    {
                        float x = 6 + f * 6;
                        PointF prev = P(x, 3.5f);
                        for (int i = 1; i <= 8; i++)
                        {
                            float y = 3.5f + i * 2.1f;
                            PointF next = P(x + (float)(1.6 * Math.Sin(i * 1.2 + f)), y);
                            g.DrawLine(pen, prev, next);
                            prev = next;
                        }
                    }
                    break;
                }
            }
        }
    }
}
