namespace OLED_Sleeper.Features.Sky
{
    /// <summary>
    /// Small, self-contained astronomy: sidereal time, equatorial-to-horizon conversion, planet positions
    /// (JPL "Keplerian elements for approximate positions of the major planets", valid 1800-2050) and a
    /// low-precision Sun and Moon. Accurate to well under a degree for the planets and about a degree for the
    /// Moon, which is far below what anyone can see on a screensaver.
    /// </summary>
    public static class Astronomy
    {
        public const double Deg = Math.PI / 180;
        private const double Obliquity = 23.43928 * Deg;

        public enum Body { Mercury, Venus, Mars, Jupiter, Saturn }

        /// <summary>Days since J2000.0 (2000-01-01 12:00 UTC).</summary>
        public static double DaysSinceJ2000(DateTime utc) =>
            (utc.ToUniversalTime() - new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc)).TotalDays;

        /// <summary>Local sidereal time in degrees for an observer at <paramref name="longitudeEast"/>.</summary>
        public static double LocalSiderealTime(double d, double longitudeEast) =>
            Normalize(280.46061837 + 360.98564736629 * d + longitudeEast);

        /// <summary>
        /// Converts right ascension / declination (degrees) to an East-North-Up unit vector for an observer at
        /// <paramref name="latitude"/> with local sidereal time <paramref name="lst"/> (degrees).
        /// </summary>
        public static (double E, double N, double U) ToHorizon(double ra, double dec, double lst, double latitude)
        {
            double h = (lst - ra) * Deg, d = dec * Deg, phi = latitude * Deg;
            double cosD = Math.Cos(d), sinD = Math.Sin(d), cosH = Math.Cos(h), sinH = Math.Sin(h);
            double sinPhi = Math.Sin(phi), cosPhi = Math.Cos(phi);
            return (-cosD * sinH,
                    sinD * cosPhi - cosD * cosH * sinPhi,
                    sinD * sinPhi + cosD * cosH * cosPhi);
        }

        /// <summary>Geocentric right ascension / declination (degrees) of a planet.</summary>
        public static (double Ra, double Dec) Planet(Body body, double d)
        {
            var (px, py, pz) = Heliocentric(Elements(body), d);
            var (ex, ey, ez) = Heliocentric(EarthElements, d);
            return EclipticVectorToEquatorial(px - ex, py - ey, pz - ez);
        }

        /// <summary>Apparent geocentric RA/Dec (degrees) and ecliptic longitude of the Sun.</summary>
        public static (double Ra, double Dec, double Longitude) Sun(double d)
        {
            double g = Normalize(357.528 + 0.9856003 * d) * Deg;
            double lambda = Normalize(280.460 + 0.9856474 * d + 1.915 * Math.Sin(g) + 0.020 * Math.Sin(2 * g));
            var (ra, dec) = EclipticToEquatorial(lambda, 0);
            return (ra, dec, lambda);
        }

        /// <summary>Geocentric RA/Dec (degrees) of the Moon, its illuminated fraction (0-1) and whether it is waxing.</summary>
        public static (double Ra, double Dec, double Illuminated, bool Waxing) Moon(double d)
        {
            double l = 218.316 + 13.176396 * d;
            double m = (134.963 + 13.064993 * d) * Deg;
            double f = (93.272 + 13.229350 * d) * Deg;
            double ms = (357.529 + 0.98560028 * d) * Deg;
            double dd = (297.850 + 12.190749 * d) * Deg;   // mean elongation

            double lambda = Normalize(l + 6.289 * Math.Sin(m) + 1.274 * Math.Sin(2 * dd - m) + 0.658 * Math.Sin(2 * dd)
                                      - 0.186 * Math.Sin(ms) - 0.114 * Math.Sin(2 * f));
            double beta = 5.128 * Math.Sin(f);

            var (ra, dec) = EclipticToEquatorial(lambda, beta);
            var sun = Sun(d);
            double diff = Normalize(lambda - sun.Longitude);
            double elongation = Math.Acos(Math.Cos(diff * Deg) * Math.Cos(beta * Deg));
            return (ra, dec, (1 - Math.Cos(elongation)) / 2, diff < 180);
        }

        public static double Normalize(double degrees)
        {
            degrees %= 360;
            return degrees < 0 ? degrees + 360 : degrees;
        }

        // --- internals ---

        private readonly record struct OrbitalElements(
            double A, double ARate, double E, double ERate, double I, double IRate,
            double L, double LRate, double Peri, double PeriRate, double Node, double NodeRate);

        private static readonly OrbitalElements EarthElements =
            new(1.00000261, 0.00000562, 0.01671123, -0.00004392, -0.00001531, -0.01294668,
                100.46457166, 35999.37244981, 102.93768193, 0.32327364, 0, 0);

        private static OrbitalElements Elements(Body body) => body switch
        {
            Body.Mercury => new(0.38709927, 0.00000037, 0.20563593, 0.00001906, 7.00497902, -0.00594749,
                                252.25032350, 149472.67411175, 77.45779628, 0.16047689, 48.33076593, -0.12534081),
            Body.Venus => new(0.72333566, 0.00000390, 0.00677672, -0.00004107, 3.39467605, -0.00078890,
                                181.97909950, 58517.81538729, 131.60246718, 0.00268329, 76.67984255, -0.27769418),
            Body.Mars => new(1.52371034, 0.00001847, 0.09339410, 0.00007882, 1.84969142, -0.00813131,
                                -4.55343205, 19140.30268499, -23.94362959, 0.44441088, 49.55953891, -0.29257343),
            Body.Jupiter => new(5.20288700, -0.00011607, 0.04838624, -0.00013253, 1.30439695, -0.00183714,
                                34.39644051, 3034.74612775, 14.72847983, 0.21252668, 100.47390909, 0.20469106),
            _ => new(9.53667594, -0.00125060, 0.05386179, -0.00050991, 2.48599187, 0.00193609,
                                49.95424423, 1222.49362201, 92.59887831, -0.41897216, 113.66242448, -0.28867794),
        };

        private static (double X, double Y, double Z) Heliocentric(OrbitalElements el, double d)
        {
            double t = d / 36525.0;
            double a = el.A + el.ARate * t;
            double e = el.E + el.ERate * t;
            double i = (el.I + el.IRate * t) * Deg;
            double l = el.L + el.LRate * t;
            double peri = el.Peri + el.PeriRate * t;
            double node = el.Node + el.NodeRate * t;

            double omega = (peri - node) * Deg;
            double m = Normalize(l - peri);
            if (m > 180) m -= 360;
            m *= Deg;
            node *= Deg;

            double ecc = m + e * Math.Sin(m);
            for (int k = 0; k < 6; k++)
                ecc -= (ecc - e * Math.Sin(ecc) - m) / (1 - e * Math.Cos(ecc));

            double xp = a * (Math.Cos(ecc) - e);
            double yp = a * Math.Sqrt(1 - e * e) * Math.Sin(ecc);

            double cw = Math.Cos(omega), sw = Math.Sin(omega), cn = Math.Cos(node), sn = Math.Sin(node), ci = Math.Cos(i), si = Math.Sin(i);
            return ((cw * cn - sw * sn * ci) * xp + (-sw * cn - cw * sn * ci) * yp,
                    (cw * sn + sw * cn * ci) * xp + (-sw * sn + cw * cn * ci) * yp,
                    sw * si * xp + cw * si * yp);
        }

        private static (double Ra, double Dec) EclipticToEquatorial(double lambda, double beta)
        {
            double l = lambda * Deg, b = beta * Deg;
            return EclipticVectorToEquatorial(Math.Cos(b) * Math.Cos(l), Math.Cos(b) * Math.Sin(l), Math.Sin(b));
        }

        private static (double Ra, double Dec) EclipticVectorToEquatorial(double x, double y, double z)
        {
            double ye = y * Math.Cos(Obliquity) - z * Math.Sin(Obliquity);
            double ze = y * Math.Sin(Obliquity) + z * Math.Cos(Obliquity);
            return (Normalize(Math.Atan2(ye, x) / Deg), Math.Atan2(ze, Math.Sqrt(x * x + ye * ye)) / Deg);
        }
    }
}
