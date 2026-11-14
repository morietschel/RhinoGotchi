using System;
using System.Globalization;
using System.Threading;

namespace RhinoGotchi.Tutorials
{
    /// <summary>
    /// Runs code with the invariant culture, so numbers come out as 0.024 whatever the Windows
    /// language. Tutorial text and feedback quote values learners type into Rhino's command line,
    /// which always wants a decimal point.
    /// </summary>
    internal static class InvariantText
    {
        public static T Run<T>(Func<T> build)
        {
            Thread thread = Thread.CurrentThread;
            CultureInfo previous = thread.CurrentCulture;
            thread.CurrentCulture = CultureInfo.InvariantCulture;
            try
            {
                return build();
            }
            finally
            {
                thread.CurrentCulture = previous;
            }
        }
    }
}
