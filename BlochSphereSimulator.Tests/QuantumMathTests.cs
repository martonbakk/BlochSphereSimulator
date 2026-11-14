using System;
using BlochSphereSimulator.Models;
using MathNet.Numerics.LinearAlgebra;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Complex = System.Numerics.Complex;

namespace BlochSphereSimulator.Tests
{
    [TestClass]
    public class QuantumMathTests
    {
        private const double Eps = 1e-9;

        [TestMethod]
        public void GetStateVector_Theta0Phi0_IsKet0()
        {
            var psi = QuantumMath.GetStateVector(0, 0);
            AssertComplexAreEqual(Complex.One, psi[0]);
            AssertComplexAreEqual(Complex.Zero, psi[1]);
        }

        [TestMethod]
        public void Hadamard_OnKet0_GivesPlusState_BlochXPlusOne()
        {
            var ket0 = QuantumMath.GetStateVector(0, 0);
            var psi = QuantumMath.H() * ket0;
            var bloch = QuantumMath.GetBlochCoordinates(psi);

            Assert.AreEqual(1.0, bloch.X, 1e-8);
            Assert.AreEqual(0.0, bloch.Y, 1e-8);
            Assert.AreEqual(0.0, bloch.Z, 1e-8);
        }

        [TestMethod]
        public void PauliX_OnKet0_GivesKet1_BlochZMinusOne()
        {
            var ket0 = QuantumMath.GetStateVector(0, 0);
            var psi = QuantumMath.X() * ket0;
            var bloch = QuantumMath.GetBlochCoordinates(psi);

            Assert.AreEqual(0.0, bloch.X, 1e-8);
            Assert.AreEqual(0.0, bloch.Y, 1e-8);
            Assert.AreEqual(-1.0, bloch.Z, 1e-8);
        }

        [TestMethod]
        public void Phase90_OnPlus_GivesPlusI_BlochYPlusOne()
        {
            var plus = QuantumMath.H() * QuantumMath.GetStateVector(0, 0);
            var psi = QuantumMath.Phase(90) * plus;
            var bloch = QuantumMath.GetBlochCoordinates(psi);

            Assert.AreEqual(0.0, bloch.X, 1e-8);
            Assert.AreEqual(1.0, bloch.Y, 1e-8);
            Assert.AreEqual(0.0, bloch.Z, 1e-8);
        }

        [TestMethod]
        public void Gates_AreUnitary()
        {
            AssertUnitary(QuantumMath.X());
            AssertUnitary(QuantumMath.Y());
            AssertUnitary(QuantumMath.Z());
            AssertUnitary(QuantumMath.H());
            AssertUnitary(QuantumMath.Rx(37));
            AssertUnitary(QuantumMath.Ry(121));
            AssertUnitary(QuantumMath.Rz(251));
            AssertUnitary(QuantumMath.Phase(77));
        }

        private static void AssertUnitary(Matrix<Complex> u)
        {
            var identity = Matrix<Complex>.Build.DenseIdentity(2);
            var left = u * u.ConjugateTranspose();
            var right = u.ConjugateTranspose() * u;

            for (var r = 0; r < 2; r++)
            {
                for (var c = 0; c < 2; c++)
                {
                    AssertComplexAreEqual(identity[r, c], left[r, c]);
                    AssertComplexAreEqual(identity[r, c], right[r, c]);
                }
            }
        }

        private static void AssertComplexAreEqual(Complex expected, Complex actual)
        {
            Assert.AreEqual(expected.Real, actual.Real, Eps);
            Assert.AreEqual(expected.Imaginary, actual.Imaginary, Eps);
        }
    }
}