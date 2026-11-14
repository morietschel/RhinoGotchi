using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rhino;
using Rhino.DocObjects;
using Rhino.Render;
using EtoColor = Eto.Drawing.Color;
using SD = System.Drawing;

namespace GotchiFins
{
    /// <summary>The three finishes the fins can have in level 6.</summary>
    public enum Finish
    {
        Birch,
        Aluminium,
        Blue,
    }

    /// <summary>
    /// The level-6 materials: birch wood (with a grain texture the game draws itself), light
    /// grey matte aluminium, and painted blue. They are added to the document as render
    /// materials named "GotchiFins …", so they also show up in Rhino's Properties → Material
    /// list. The texture is scaled with a box mapping (ApplyBoxMapping) of <see cref="MappingSize"/>.
    /// </summary>
    internal static class Finishes
    {
        /// <summary>The texture box the level asks for: the grain repeats every 2 units.</summary>
        public const double MappingSize = 2;

        public static readonly Finish[] All = { Finish.Birch, Finish.Aluminium, Finish.Blue };

        public static string Label(Finish f) => f switch
        {
            Finish.Birch => "Birch wood",
            Finish.Aluminium => "Matte aluminium",
            _ => "Painted blue",
        };

        public static string MaterialName(Finish f) => "GotchiFins " + Label(f);

        public static SD.Color Color(Finish f) => f switch
        {
            Finish.Birch => SD.Color.FromArgb(0xE3, 0xCF, 0xA8),
            Finish.Aluminium => SD.Color.FromArgb(0xC6, 0xC9, 0xCD),
            _ => SD.Color.FromArgb(0x2A, 0x5C, 0xB8),
        };

        public static EtoColor EtoColor(Finish f)
        {
            SD.Color c = Color(f);
            return Eto.Drawing.Color.FromArgb(c.R, c.G, c.B);
        }

        /// <summary>Which finish a material name is, if any.</summary>
        public static Finish? FromName(string? name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            foreach (Finish f in All)
                if (string.Equals(name, MaterialName(f), StringComparison.OrdinalIgnoreCase))
                    return f;
            return null;
        }

        /// <summary>Adds the three materials to the document if they aren't there yet.</summary>
        public static void Ensure(RhinoDoc doc)
        {
            foreach (Finish f in All)
                Find(doc, f);
        }

        /// <summary>The finish's render material in the document, created on first use.</summary>
        public static RenderMaterial? Find(RhinoDoc doc, Finish f)
        {
            string name = MaterialName(f);
            RenderMaterial? existing = doc.RenderMaterials.FirstOrDefault(m => m.Name == name);
            if (existing != null)
                return existing;
            try
            {
                var material = new Material { Name = name, DiffuseColor = Color(f) };
                switch (f)
                {
                    case Finish.Birch:
                        material.Shine = Material.MaxShine * 0.15;
                        string? texture = BirchTexture();
                        if (texture != null)
                            material.SetBitmapTexture(texture);
                        break;
                    case Finish.Aluminium:
                        material.Shine = Material.MaxShine * 0.25;
                        material.Reflectivity = 0.12;
                        break;
                    default:
                        material.Shine = Material.MaxShine * 0.55;
                        material.Reflectivity = 0.05;
                        break;
                }
                RenderMaterial render = RenderMaterial.CreateBasicMaterial(material, doc);
                render.Name = name;
                doc.RenderMaterials.Add(render);
                return doc.RenderMaterials.FirstOrDefault(m => m.Name == name) ?? render;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Gives every fin the finish (the panel's swatches do this; Rhino's way is Properties → Material).</summary>
        public static void Apply(RhinoDoc doc, Finish f, IEnumerable<Guid> fins)
        {
            RenderMaterial? material = Find(doc, f);
            if (material == null)
                return;
            foreach (Guid id in fins)
            {
                RhinoObject? obj = doc.Objects.FindId(id);
                if (obj == null)
                    continue;
                // Commit through the document so Rendered mode picks it up: material from the object, this material
                ObjectAttributes attributes = obj.Attributes.Duplicate();
                attributes.MaterialSource = ObjectMaterialSource.MaterialFromObject;
                attributes.RenderMaterial = material;
                doc.Objects.ModifyAttributes(obj, attributes, true);
            }
            doc.Views.Redraw();
        }

        /// <summary>
        /// Draws a birch plywood grain (pale wood, fine wavy darker lines, a few flecks) and saves
        /// it in the temp folder, once. Returns its path, or null if it can't be written.
        /// </summary>
        private static string? BirchTexture()
        {
            try
            {
                string folder = Path.Combine(Path.GetTempPath(), "GotchiFins");
                string path = Path.Combine(folder, "birch.png");
                if (File.Exists(path))
                    return path;
                Directory.CreateDirectory(folder);

                const int size = 512;
                using var bmp = new Eto.Drawing.Bitmap(size, size, Eto.Drawing.PixelFormat.Format32bppRgba);
                using (var g = new Eto.Drawing.Graphics(bmp))
                {
                    g.AntiAlias = true;
                    g.FillRectangle(Eto.Drawing.Color.FromArgb(0xE6, 0xD3, 0xAE), 0, 0, size, size);
                    var random = new Random(7);
                    for (int i = 0; i < 70; i++)
                    {
                        float y0 = (float)(random.NextDouble() * size);
                        float amp = (float)(2 + random.NextDouble() * 6), wave = (float)(80 + random.NextDouble() * 160);
                        int dark = 0xB8 + random.Next(0x20);
                        var pen = new Eto.Drawing.Pen(Eto.Drawing.Color.FromArgb(dark, dark - 0x1E, dark - 0x48, 90 + random.Next(80)), 0.6f + (float)random.NextDouble() * 1.4f);
                        var prev = new Eto.Drawing.PointF(0, y0);
                        for (int x = 8; x <= size; x += 8)
                        {
                            var next = new Eto.Drawing.PointF(x, y0 + amp * (float)Math.Sin(x / wave * Math.PI * 2 + i));
                            g.DrawLine(pen, prev, next);
                            prev = next;
                        }
                    }
                    for (int i = 0; i < 14; i++)
                    {
                        float x = (float)(random.NextDouble() * size), y = (float)(random.NextDouble() * size);
                        g.FillEllipse(Eto.Drawing.Color.FromArgb(0x9C, 0x7A, 0x4E, 120), x, y, 3 + random.Next(4), 1.5f + random.Next(2));
                    }
                }
                bmp.Save(path, Eto.Drawing.ImageFormat.Png);
                return path;
            }
            catch
            {
                return null;
            }
        }
    }
}
