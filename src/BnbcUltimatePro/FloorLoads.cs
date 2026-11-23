using System;
using System.Collections.Generic;

namespace BnbcUltimatePro
{
    internal class WallMaterial { public string Name; public double Gamma; public string Ref; public override string ToString() { return Name; } }

    internal static class WallCalc
    {
        // Unit weights from BNBC 2020 Table 6.2.1 (PDF p.532-533). "Custom" is entered by the user.
        public static readonly List<WallMaterial> Materials = new List<WallMaterial>
        {
            new WallMaterial { Name = "Brick", Gamma = 18.9, Ref = "Table 6.2.1 (p.532)" },
            new WallMaterial { Name = "Concrete, stone aggregate (unreinforced)", Gamma = 22.8, Ref = "Table 6.2.1 (p.533)" },
            new WallMaterial { Name = "Concrete, brick aggregate (unreinforced)", Gamma = 20.4, Ref = "Table 6.2.1 (p.533)" },
            new WallMaterial { Name = "Custom (enter unit weight)", Gamma = double.NaN, Ref = "user supplied" }
        };

        /// <summary>w = (gamma*t + faces*finish) * h * (1 - opening), in kN/m.</summary>
        public static double LineLoad(double gamma, double t, double h, double finishPerFace, int faces, double openingFraction)
        {
            return (gamma * t + faces * finishPerFace) * h * (1.0 - openingFraction);
        }
    }
}
