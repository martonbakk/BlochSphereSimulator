using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.Windows.Media.Media3D;
using Complex = System.Numerics.Complex;

namespace BlochSphereSimulator.Models
{
    /// <summary>
    /// Single-qubit quantum mathematics. Conventions follow the course slides:
    /// |psi> = cos(theta/2)|0> + e^(j*phi) sin(theta/2)|1>, the imaginary unit is written as j,
    /// and the phase gate is P(alpha) = diag(1, e^(j*alpha)).
    /// </summary>
    public static class QuantumMath
    {
        private static readonly double InvSqrt2 = 1.0 / Math.Sqrt(2.0);
        private static readonly Complex J = Complex.ImaginaryOne;

        public static double DegToRad(double deg) => deg * Math.PI / 180.0;
        public static double RadToDeg(double rad) => rad * 180.0 / Math.PI;

        private static Matrix<Complex> M(Complex a, Complex b, Complex c, Complex d) =>
            Matrix<Complex>.Build.DenseOfArray(new Complex[,] { { a, b }, { c, d } });

        // ---------- Basic gates ----------

        /// <summary>Identity gate.</summary>
        public static Matrix<Complex> I() => M(1, 0, 0, 1);

        /// <summary>Pauli-X (bit-flip): [[0,1],[1,0]].</summary>
        public static Matrix<Complex> X() => M(0, 1, 1, 0);

        /// <summary>Pauli-Y: [[0,-j],[j,0]].</summary>
        public static Matrix<Complex> Y() => M(0, -J, J, 0);

        /// <summary>Pauli-Z (phase-flip): [[1,0],[0,-1]].</summary>
        public static Matrix<Complex> Z() => M(1, 0, 0, -1);

        /// <summary>Hadamard: 1/sqrt(2) [[1,1],[1,-1]].</summary>
        public static Matrix<Complex> H() => M(InvSqrt2, InvSqrt2, InvSqrt2, -InvSqrt2);

        // ---------- Phase gate family ----------

        /// <summary>Phase gate P(alpha) = [[1,0],[0,e^(j*alpha)]]: rotation about the z axis by alpha.</summary>
        public static Matrix<Complex> Phase(double alphaDeg) =>
            M(1, 0, 0, Complex.Exp(J * DegToRad(alphaDeg)));

        /// <summary>S = P(90°).</summary>
        public static Matrix<Complex> S() => Phase(90);

        /// <summary>S† = P(-90°).</summary>
        public static Matrix<Complex> Sdg() => Phase(-90);

        /// <summary>T = P(45°).</summary>
        public static Matrix<Complex> T() => Phase(45);

        /// <summary>T† = P(-45°).</summary>
        public static Matrix<Complex> Tdg() => Phase(-45);

        /// <summary>Square root of X: 1/2 [[1+j, 1-j],[1-j, 1+j]]. Applying it twice gives X.</summary>
        public static Matrix<Complex> SqrtX()
        {
            var p = (1 + J) / 2.0;
            var m = (1 - J) / 2.0;
            return M(p, m, m, p);
        }

        // ---------- Rotation gates ----------

        public static Matrix<Complex> Rx(double angleDeg)
        {
            double t = DegToRad(angleDeg);
            return M(Math.Cos(t / 2), -J * Math.Sin(t / 2), -J * Math.Sin(t / 2), Math.Cos(t / 2));
        }

        public static Matrix<Complex> Ry(double angleDeg)
        {
            double t = DegToRad(angleDeg);
            return M(Math.Cos(t / 2), -Math.Sin(t / 2), Math.Sin(t / 2), Math.Cos(t / 2));
        }

        public static Matrix<Complex> Rz(double angleDeg)
        {
            double t = DegToRad(angleDeg);
            return M(Complex.Exp(-J * t / 2), 0, 0, Complex.Exp(J * t / 2));
        }

        // ---------- Gate catalogue ----------

        /// <summary>
        /// Returns the gate matrix plus the Bloch-sphere rotation (axis, angle) used for animation.
        /// Parametric gates use the given angle so that e.g. Rz(270°) animates a 270° turn.
        /// </summary>
        public static GateDefinition GetGate(string name, double angleDeg)
        {
            var xAxis = new Vector3D(1, 0, 0);
            var yAxis = new Vector3D(0, 1, 0);
            var zAxis = new Vector3D(0, 0, 1);
            var hAxis = new Vector3D(InvSqrt2, 0, InvSqrt2);

            return name switch
            {
                "X" => new GateDefinition("X", X(), xAxis, 180),
                "Y" => new GateDefinition("Y", Y(), yAxis, 180),
                "Z" => new GateDefinition("Z", Z(), zAxis, 180),
                "H" => new GateDefinition("H", H(), hAxis, 180),
                "S" => new GateDefinition("S", S(), zAxis, 90),
                "Sdg" => new GateDefinition("S†", Sdg(), zAxis, -90),
                "T" => new GateDefinition("T", T(), zAxis, 45),
                "Tdg" => new GateDefinition("T†", Tdg(), zAxis, -45),
                "SqrtX" => new GateDefinition("√X", SqrtX(), xAxis, 90),
                "P" => new GateDefinition($"P({angleDeg:0.#}°)", Phase(angleDeg), zAxis, angleDeg),
                "Rx" => new GateDefinition($"Rx({angleDeg:0.#}°)", Rx(angleDeg), xAxis, angleDeg),
                "Ry" => new GateDefinition($"Ry({angleDeg:0.#}°)", Ry(angleDeg), yAxis, angleDeg),
                "Rz" => new GateDefinition($"Rz({angleDeg:0.#}°)", Rz(angleDeg), zAxis, angleDeg),
                _ => throw new ArgumentException($"Unknown gate: {name}", nameof(name))
            };
        }

        // ---------- States ----------

        public static Vector<Complex> GetStateVector(double thetaDeg, double phiDeg)
        {
            double theta = DegToRad(thetaDeg);
            double phi = DegToRad(phiDeg);
            var alpha = new Complex(Math.Cos(theta / 2.0), 0);
            var beta = Complex.Exp(J * phi) * Math.Sin(theta / 2.0);
            return Vector<Complex>.Build.Dense(new[] { alpha, beta });
        }

        /// <summary>Named basis states used by the preset buttons.</summary>
        public static IReadOnlyDictionary<string, (double Theta, double Phi)> Presets { get; } =
            new Dictionary<string, (double, double)>
            {
                ["|0⟩"] = (0, 0),
                ["|1⟩"] = (180, 0),
                ["|+⟩"] = (90, 0),
                ["|−⟩"] = (90, 180),
                ["|+j⟩"] = (90, 90),
                ["|−j⟩"] = (90, 270),
            };

        public static Point3D GetBlochCoordinates(Vector<Complex> state)
        {
            var alpha = state[0];
            var beta = state[1];
            var cross = Complex.Conjugate(alpha) * beta;
            double x = 2 * cross.Real;
            double y = 2 * cross.Imaginary;
            double z = alpha.Magnitude * alpha.Magnitude - beta.Magnitude * beta.Magnitude;
            return new Point3D(x, y, z);
        }

        /// <summary>Bloch angles (theta in [0,180], phi in [0,360)) of a state, in degrees.</summary>
        public static (double ThetaDeg, double PhiDeg) GetBlochAngles(Vector<Complex> state)
        {
            var p = GetBlochCoordinates(state);
            double z = Math.Clamp(p.Z, -1, 1);
            double theta = RadToDeg(Math.Acos(z));
            double phi = 0;
            if (Math.Sqrt(p.X * p.X + p.Y * p.Y) > 1e-9)
            {
                phi = RadToDeg(Math.Atan2(p.Y, p.X));
                if (phi < 0) phi += 360;
            }
            return (theta, phi);
        }

        /// <summary>
        /// Removes the global phase so that the |0> amplitude is real and non-negative
        /// (the form used on the slides). Physically the state is unchanged.
        /// </summary>
        public static Vector<Complex> NormalizeGlobalPhase(Vector<Complex> state)
        {
            var a = state[0];
            Complex factor = a.Magnitude > 1e-12
                ? Complex.Conjugate(a) / a.Magnitude
                : Complex.Conjugate(state[1]) / state[1].Magnitude; // alpha ~ 0: make beta real instead
            var result = state * factor;
            return result / result.L2Norm();
        }

        // ---------- Measurement (3rd postulate) ----------

        /// <summary>P(0) = |alpha|^2 and P(1) = |beta|^2 for a measurement in the computational (Z) basis.</summary>
        public static (double P0, double P1) ProbabilitiesZ(Vector<Complex> state)
        {
            double p0 = state[0].Magnitude * state[0].Magnitude;
            double p1 = state[1].Magnitude * state[1].Magnitude;
            double sum = p0 + p1;
            return (p0 / sum, p1 / sum);
        }

        /// <summary>P(+) = |&lt;+|psi&gt;|^2 and P(-) = |&lt;-|psi&gt;|^2 for a measurement in the X (Hadamard) basis.</summary>
        public static (double PPlus, double PMinus) ProbabilitiesX(Vector<Complex> state)
        {
            var plus = (state[0] + state[1]) * InvSqrt2;
            var minus = (state[0] - state[1]) * InvSqrt2;
            double pp = plus.Magnitude * plus.Magnitude;
            double pm = minus.Magnitude * minus.Magnitude;
            double sum = pp + pm;
            return (pp / sum, pm / sum);
        }

        /// <summary>
        /// Projective measurement. Returns the outcome (0 or 1; in the X basis 0 means |+>, 1 means |->)
        /// and the post-measurement state M_m|psi> / sqrt(P(m)), which is the basis state itself.
        /// </summary>
        public static (int Outcome, Vector<Complex> PostState) Measure(Vector<Complex> state, MeasurementBasis basis, Random rng)
        {
            double p0 = basis == MeasurementBasis.Z ? ProbabilitiesZ(state).P0 : ProbabilitiesX(state).PPlus;
            int outcome = rng.NextDouble() < p0 ? 0 : 1;

            Vector<Complex> post = basis switch
            {
                MeasurementBasis.Z => outcome == 0 ? GetStateVector(0, 0) : GetStateVector(180, 0),
                _ => outcome == 0 ? GetStateVector(90, 0) : GetStateVector(90, 180),
            };
            return (outcome, post);
        }

        /// <summary>Simulates N independent preparations + measurements (like shots on a real device).</summary>
        public static (int Count0, int Count1) SampleShots(Vector<Complex> state, MeasurementBasis basis, int shots, Random rng)
        {
            double p0 = basis == MeasurementBasis.Z ? ProbabilitiesZ(state).P0 : ProbabilitiesX(state).PPlus;
            int c0 = 0;
            for (int i = 0; i < shots; i++)
                if (rng.NextDouble() < p0) c0++;
            return (c0, shots - c0);
        }

        // ---------- Geometry helpers for animation ----------

        /// <summary>Rotates a point about a unit axis through the origin (Rodrigues' formula).</summary>
        public static Point3D RotateAboutAxis(Point3D p, Vector3D axis, double angleDeg)
        {
            var k = axis;
            k.Normalize();
            var v = new Vector3D(p.X, p.Y, p.Z);
            double a = DegToRad(angleDeg);
            var r = v * Math.Cos(a) + Vector3D.CrossProduct(k, v) * Math.Sin(a) + k * Vector3D.DotProduct(k, v) * (1 - Math.Cos(a));
            return new Point3D(r.X, r.Y, r.Z);
        }

        /// <summary>Points of the arc traced by the Bloch vector while the gate rotates it.</summary>
        public static List<Point3D> RotationPath(Point3D start, Vector3D axis, double angleDeg, int segments = 48)
        {
            var points = new List<Point3D>(segments + 1);
            for (int i = 0; i <= segments; i++)
                points.Add(RotateAboutAxis(start, axis, angleDeg * i / segments));
            return points;
        }

        /// <summary>Straight path (used for the measurement "jump").</summary>
        public static List<Point3D> StraightPath(Point3D start, Point3D end, int segments = 24)
        {
            var points = new List<Point3D>(segments + 1);
            for (int i = 0; i <= segments; i++)
            {
                double t = (double)i / segments;
                points.Add(new Point3D(start.X + (end.X - start.X) * t, start.Y + (end.Y - start.Y) * t, start.Z + (end.Z - start.Z) * t));
            }
            return points;
        }

        public static bool IsUnitary(Matrix<Complex> u, double eps = 1e-9)
        {
            var prod = u.ConjugateTranspose() * u;
            for (int r = 0; r < 2; r++)
                for (int c = 0; c < 2; c++)
                    if ((prod[r, c] - (r == c ? Complex.One : Complex.Zero)).Magnitude > eps) return false;
            return true;
        }

        /// <summary>Formats a complex number with the course's j notation, e.g. "0.707 + 0.500j".</summary>
        public static string FormatComplex(Complex c, int digits = 3)
        {
            string fmt = "F" + digits;
            double re = Math.Abs(c.Real) < 5e-7 ? 0 : c.Real;
            double im = Math.Abs(c.Imaginary) < 5e-7 ? 0 : c.Imaginary;
            if (im == 0) return re.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture);
            string sign = im < 0 ? "−" : "+";
            return $"{re.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture)} {sign} {Math.Abs(im).ToString(fmt, System.Globalization.CultureInfo.InvariantCulture)}j";
        }
    }

    public enum MeasurementBasis { Z, X }

    public sealed record GateDefinition(string Label, Matrix<Complex> Matrix, Vector3D Axis, double AngleDeg);
}
