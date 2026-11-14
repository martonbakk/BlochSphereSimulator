using System;
using BlochSphereSimulator.Models;
using MathNet.Numerics.LinearAlgebra;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows.Media.Media3D;
using Complex = System.Numerics.Complex;

namespace BlochSphereSimulator.Tests
{
    [TestClass]
    public class QuantumMathTests
    {
        private const double Eps = 1e-9;
        private static readonly string[] AllGates = { "X", "Y", "Z", "H", "S", "Sdg", "T", "Tdg", "SqrtX", "P", "Rx", "Ry", "Rz" };

        [TestMethod]
        public void GetStateVector_Theta0Phi0_IsKet0()
        {
            var psi = QuantumMath.GetStateVector(0, 0);
            AssertComplexAreEqual(Complex.One, psi[0]);
            AssertComplexAreEqual(Complex.Zero, psi[1]);
        }

        [TestMethod]
        public void GetStateVector_MatchesLectureFormula()
        {
            // |psi> = cos(theta/2)|0> + e^(j phi) sin(theta/2)|1>
            var psi = QuantumMath.GetStateVector(60, 120);
            AssertComplexAreEqual(new Complex(Math.Cos(Math.PI / 6), 0), psi[0]);
            AssertComplexAreEqual(Complex.FromPolarCoordinates(Math.Sin(Math.PI / 6), 2 * Math.PI / 3), psi[1]);
        }

        [TestMethod]
        public void Hadamard_OnKet0_GivesPlusState_BlochXPlusOne()
        {
            var psi = QuantumMath.H() * QuantumMath.GetStateVector(0, 0);
            AssertPoint(new Point3D(1, 0, 0), QuantumMath.GetBlochCoordinates(psi));
        }

        [TestMethod]
        public void PauliX_OnKet0_GivesKet1_BlochZMinusOne()
        {
            var psi = QuantumMath.X() * QuantumMath.GetStateVector(0, 0);
            AssertPoint(new Point3D(0, 0, -1), QuantumMath.GetBlochCoordinates(psi));
        }

        [TestMethod]
        public void Phase90_OnPlus_GivesPlusJ_BlochYPlusOne()
        {
            var plus = QuantumMath.H() * QuantumMath.GetStateVector(0, 0);
            var psi = QuantumMath.Phase(90) * plus;
            AssertPoint(new Point3D(0, 1, 0), QuantumMath.GetBlochCoordinates(psi));
        }

        [TestMethod]
        public void LectureMatrices_ActOnGenericState_AsOnSlides()
        {
            // Slides: Z(a,b) = (a,-b), Y(a,b) = (-jb, ja), P(alpha)(a,b) = (a, e^(j alpha) b)
            var a = new Complex(0.6, 0);
            var b = new Complex(0, 0.8);
            var v = Vector<Complex>.Build.Dense(new[] { a, b });
            var j = Complex.ImaginaryOne;

            var z = QuantumMath.Z() * v;
            AssertComplexAreEqual(a, z[0]);
            AssertComplexAreEqual(-b, z[1]);

            var y = QuantumMath.Y() * v;
            AssertComplexAreEqual(-j * b, y[0]);
            AssertComplexAreEqual(j * a, y[1]);

            var p = QuantumMath.Phase(30) * v;
            AssertComplexAreEqual(a, p[0]);
            AssertComplexAreEqual(Complex.Exp(j * Math.PI / 6) * b, p[1]);
        }

        [TestMethod]
        public void Gates_AreUnitary()
        {
            foreach (var g in AllGates)
                Assert.IsTrue(QuantumMath.IsUnitary(QuantumMath.GetGate(g, 77).Matrix), g);
        }

        [TestMethod]
        public void Hadamard_IsSelfInverse()
        {
            AssertMatrix(QuantumMath.I(), QuantumMath.H() * QuantumMath.H());
        }

        [TestMethod]
        public void SAndT_ArePhaseGateSpecialCases()
        {
            AssertMatrix(QuantumMath.Phase(90), QuantumMath.S());
            AssertMatrix(QuantumMath.Phase(45), QuantumMath.T());
            AssertMatrix(QuantumMath.S(), QuantumMath.T() * QuantumMath.T());
            AssertMatrix(QuantumMath.Z(), QuantumMath.S() * QuantumMath.S());
            AssertMatrix(QuantumMath.I(), QuantumMath.S() * QuantumMath.Sdg());
            AssertMatrix(QuantumMath.I(), QuantumMath.T() * QuantumMath.Tdg());
        }

        [TestMethod]
        public void SqrtX_Squared_IsX()
        {
            AssertMatrix(QuantumMath.X(), QuantumMath.SqrtX() * QuantumMath.SqrtX());
        }

        /// <summary>
        /// The animation rotates the Bloch vector about the gate's axis by its angle. The end point of
        /// that path must be the Bloch point of U|psi> for every gate and for many input states.
        /// </summary>
        [TestMethod]
        public void RotationPathEnd_MatchesMatrixResult_ForAllGates()
        {
            var rng = new Random(1234);
            for (int trial = 0; trial < 50; trial++)
            {
                var psi = QuantumMath.GetStateVector(rng.NextDouble() * 180, rng.NextDouble() * 360);
                double angle = rng.NextDouble() * 720 - 360;
                foreach (var name in AllGates)
                {
                    var gate = QuantumMath.GetGate(name, angle);
                    var start = QuantumMath.GetBlochCoordinates(psi);
                    var path = QuantumMath.RotationPath(start, gate.Axis, gate.AngleDeg);
                    var expected = QuantumMath.GetBlochCoordinates(gate.Matrix * psi);
                    AssertPoint(expected, path[^1], 1e-9, $"{name} angle={angle}");
                    // every point of the path stays on the sphere surface
                    foreach (var p in path)
                        Assert.AreEqual(1.0, Math.Sqrt(p.X * p.X + p.Y * p.Y + p.Z * p.Z), 1e-9);
                }
            }
        }

        [TestMethod]
        public void PreparationCircuit_FromLecture_ReachesTargetBlochPoint()
        {
            // |0> -> H -> P(alpha) -> H -> P(90 + beta)  ==  (theta = alpha, phi = beta) up to global phase
            var rng = new Random(7);
            for (int i = 0; i < 40; i++)
            {
                double alpha = rng.NextDouble() * 180, beta = rng.NextDouble() * 360;
                var psi = QuantumMath.Phase(90 + beta) * QuantumMath.H() * QuantumMath.Phase(alpha) * QuantumMath.H() * QuantumMath.GetStateVector(0, 0);
                AssertPoint(QuantumMath.GetBlochCoordinates(QuantumMath.GetStateVector(alpha, beta)), QuantumMath.GetBlochCoordinates(psi), 1e-9);

                // Explicit global phase e^(j alpha/2) from the lecture note
                var expected = QuantumMath.GetStateVector(alpha, beta) * Complex.Exp(Complex.ImaginaryOne * QuantumMath.DegToRad(alpha) / 2);
                AssertComplexAreEqual(expected[0], psi[0]);
                AssertComplexAreEqual(expected[1], psi[1]);
            }
        }

        [TestMethod]
        public void Interferometer_FromLecture_GivesCosSquaredStatistics()
        {
            // H -> P(delta) -> H on |0>: P(0) = cos^2(delta/2) (interferometer lecture, shots=10000)
            foreach (var delta in new[] { 0.0, 60.0, 90.0, 180.0, 270.0 })
            {
                var psi = QuantumMath.H() * QuantumMath.Phase(delta) * QuantumMath.H() * QuantumMath.GetStateVector(0, 0);
                double expected = Math.Pow(Math.Cos(QuantumMath.DegToRad(delta) / 2), 2);
                Assert.AreEqual(expected, QuantumMath.ProbabilitiesZ(psi).P0, 1e-12, $"delta={delta}");
            }
            var p60 = QuantumMath.H() * QuantumMath.Phase(60) * QuantumMath.H() * QuantumMath.GetStateVector(0, 0);
            Assert.AreEqual(0.75, QuantumMath.ProbabilitiesZ(p60).P0, 1e-12);
        }

        [TestMethod]
        public void Hadamard_OnBasisStates_AsOnSlides()
        {
            // H|0> = (|0>+|1>)/sqrt2, H|1> = (|0>-|1>)/sqrt2
            var h0 = QuantumMath.H() * QuantumMath.GetStateVector(0, 0);
            var h1 = QuantumMath.H() * QuantumMath.GetStateVector(180, 0);
            double s = 1 / Math.Sqrt(2);
            AssertComplexAreEqual(s, h0[0]); AssertComplexAreEqual(s, h0[1]);
            AssertComplexAreEqual(s, h1[0]); AssertComplexAreEqual(-s, h1[1]);
        }

        [TestMethod]
        public void BlochAngles_RoundTrip()
        {
            var (t, p) = QuantumMath.GetBlochAngles(QuantumMath.GetStateVector(123.4, 291.7));
            Assert.AreEqual(123.4, t, 1e-9);
            Assert.AreEqual(291.7, p, 1e-9);
        }

        [TestMethod]
        public void NormalizeGlobalPhase_MakesAlphaRealAndKeepsBloch()
        {
            var psi = QuantumMath.GetStateVector(70, 40) * Complex.Exp(new Complex(0, 1.234));
            var n = QuantumMath.NormalizeGlobalPhase(psi);
            Assert.AreEqual(0.0, n[0].Imaginary, Eps);
            Assert.IsTrue(n[0].Real >= 0);
            AssertPoint(QuantumMath.GetBlochCoordinates(psi), QuantumMath.GetBlochCoordinates(n));

            var ket1 = QuantumMath.GetStateVector(180, 0) * Complex.ImaginaryOne;
            var n1 = QuantumMath.NormalizeGlobalPhase(ket1);
            AssertComplexAreEqual(Complex.One, n1[1]);
        }

        [TestMethod]
        public void Presets_HaveExpectedBlochPoints()
        {
            var expected = new (string, Point3D)[]
            {
                ("|0⟩", new Point3D(0, 0, 1)), ("|1⟩", new Point3D(0, 0, -1)),
                ("|+⟩", new Point3D(1, 0, 0)), ("|−⟩", new Point3D(-1, 0, 0)),
                ("|+j⟩", new Point3D(0, 1, 0)), ("|−j⟩", new Point3D(0, -1, 0)),
            };
            foreach (var (name, point) in expected)
            {
                var (t, p) = QuantumMath.Presets[name];
                AssertPoint(point, QuantumMath.GetBlochCoordinates(QuantumMath.GetStateVector(t, p)), 1e-9, name);
            }
        }

        [TestMethod]
        public void Probabilities_ZAndXBasis()
        {
            var psi = QuantumMath.GetStateVector(60, 0); // cos30|0> + sin30|1>
            var (p0, p1) = QuantumMath.ProbabilitiesZ(psi);
            Assert.AreEqual(0.75, p0, Eps);
            Assert.AreEqual(0.25, p1, Eps);

            var (pp, pm) = QuantumMath.ProbabilitiesX(QuantumMath.GetStateVector(90, 0)); // |+>
            Assert.AreEqual(1.0, pp, Eps);
            Assert.AreEqual(0.0, pm, Eps);

            var (qp, qm) = QuantumMath.ProbabilitiesX(QuantumMath.GetStateVector(0, 0)); // |0>
            Assert.AreEqual(0.5, qp, Eps);
            Assert.AreEqual(0.5, qm, Eps);
        }

        [TestMethod]
        public void Measure_CollapsesToBasisState_AndRepeatsSameResult()
        {
            var rng = new Random(42);
            var psi = QuantumMath.GetStateVector(100, 33);
            foreach (var basis in new[] { MeasurementBasis.Z, MeasurementBasis.X })
            {
                var (outcome, post) = QuantumMath.Measure(psi, basis, rng);
                // Projective measurement: measuring again in the same basis always gives the same result (M0 M0 = M0)
                for (int i = 0; i < 20; i++)
                    Assert.AreEqual(outcome, QuantumMath.Measure(post, basis, rng).Outcome);
                var b = QuantumMath.GetBlochCoordinates(post);
                if (basis == MeasurementBasis.Z) Assert.AreEqual(1.0, Math.Abs(b.Z), Eps);
                else Assert.AreEqual(1.0, Math.Abs(b.X), Eps);
            }
        }

        [TestMethod]
        public void SampleShots_ConvergesToBornRule()
        {
            var rng = new Random(99);
            var psi = QuantumMath.GetStateVector(60, 0); // P(0) = 0.75
            var (c0, c1) = QuantumMath.SampleShots(psi, MeasurementBasis.Z, 100000, rng);
            Assert.AreEqual(100000, c0 + c1);
            Assert.AreEqual(0.75, c0 / 100000.0, 0.01);
        }

        [TestMethod]
        public void FormatComplex_UsesJNotation()
        {
            Assert.AreEqual("0.500 + 0.500j", QuantumMath.FormatComplex(new Complex(0.5, 0.5)));
            Assert.AreEqual("0.000 − 1.000j", QuantumMath.FormatComplex(new Complex(0, -1)));
            Assert.AreEqual("0.707", QuantumMath.FormatComplex(new Complex(0.70710678, 1e-12)));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void GetGate_UnknownName_Throws()
        {
            QuantumMath.GetGate("CNOT", 0);
        }

        // ---------- helpers ----------

        private static void AssertPoint(Point3D expected, Point3D actual, double eps = 1e-8, string msg = "")
        {
            Assert.AreEqual(expected.X, actual.X, eps, "x " + msg);
            Assert.AreEqual(expected.Y, actual.Y, eps, "y " + msg);
            Assert.AreEqual(expected.Z, actual.Z, eps, "z " + msg);
        }

        private static void AssertMatrix(Matrix<Complex> expected, Matrix<Complex> actual)
        {
            for (var r = 0; r < 2; r++)
                for (var c = 0; c < 2; c++)
                    AssertComplexAreEqual(expected[r, c], actual[r, c]);
        }

        private static void AssertComplexAreEqual(Complex expected, Complex actual)
        {
            Assert.AreEqual(expected.Real, actual.Real, Eps);
            Assert.AreEqual(expected.Imaginary, actual.Imaginary, Eps);
        }
    }
}
