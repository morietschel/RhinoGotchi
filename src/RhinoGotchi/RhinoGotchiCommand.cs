using System.Runtime.InteropServices;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace RhinoGotchi
{
    /// <summary>
    /// The sample command from McNeel's "Your First Plugin" guide:
    /// pick two points and add a line between them.
    /// </summary>
    [Guid("bfcfbeee-5952-4fa0-9479-c1ea59b220c7")]
    public class RhinoGotchiCommand : Command
    {
        public RhinoGotchiCommand()
        {
            Instance = this;
        }

        /// <summary>The only instance of this command.</summary>
        public static RhinoGotchiCommand? Instance { get; private set; }

        /// <summary>The command name as it appears on the Rhino command line.</summary>
        public override string EnglishName => "RhinoGotchi";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            RhinoApp.WriteLine("The {0} command will add a line right now.", EnglishName);

            Result rc = RhinoGet.GetPoint("Start of line", false, out Point3d pt0);
            if (rc != Result.Success)
                return rc;

            var gp = new GetPoint();
            gp.SetCommandPrompt("End of line");
            gp.SetBasePoint(pt0, true);
            gp.DrawLineFromPoint(pt0, true);
            gp.Get();
            if (gp.CommandResult() != Result.Success)
                return gp.CommandResult();

            Point3d pt1 = gp.Point();
            double length = pt0.DistanceTo(pt1);
            if (length < RhinoMath.ZeroTolerance)
                return Result.Nothing;

            doc.Objects.AddLine(pt0, pt1);
            doc.Views.Redraw();
            RhinoApp.WriteLine("The {0} command added one line to the document.", EnglishName);
            RhinoApp.WriteLine("The distance between the two points is {0} {1}.", length, doc.ModelUnitSystem.ToString().ToLowerInvariant());

            return Result.Success;
        }
    }
}
