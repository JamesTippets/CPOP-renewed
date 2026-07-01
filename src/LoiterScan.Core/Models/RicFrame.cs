namespace LoiterScan.Core.Models;

/// <summary>Decomposes relative position into the Radial / In-track / Cross-track (RIC) frame.</summary>
public static class RicFrame
{
    public static (double R, double I, double C) EciToRic(OrbitState primary, OrbitState secondary)
    {
        double rx = primary.X, ry = primary.Y, rz = primary.Z;
        double rMag = Math.Sqrt(rx * rx + ry * ry + rz * rz);
        rx /= rMag; ry /= rMag; rz /= rMag;

        double hx = primary.Y * primary.Vz - primary.Z * primary.Vy;
        double hy = primary.Z * primary.Vx - primary.X * primary.Vz;
        double hz = primary.X * primary.Vy - primary.Y * primary.Vx;
        double hMag = Math.Sqrt(hx * hx + hy * hy + hz * hz);
        hx /= hMag; hy /= hMag; hz /= hMag;

        double ix = hy * rz - hz * ry;
        double iy = hz * rx - hx * rz;
        double iz = hx * ry - hy * rx;

        double dx = secondary.X - primary.X;
        double dy = secondary.Y - primary.Y;
        double dz = secondary.Z - primary.Z;

        return (
            R: dx * rx + dy * ry + dz * rz,
            I: dx * ix + dy * iy + dz * iz,
            C: dx * hx + dy * hy + dz * hz);
    }
}
